using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using AzureDeadLetterMonitor.Models;

namespace AzureDeadLetterMonitor.Services;

public class AzureServiceBusMonitorService : IServiceBusMonitorService, IAsyncDisposable
{
    private ServiceBusAdministrationClient? _adminClient;
    private ServiceBusClient? _busClient;
    private TokenCredential? _credential;

    public bool IsConnected => _adminClient != null && !IsDemoMode;
    public string? CurrentNamespace { get; private set; }
    public string AuthStatusMessage { get; private set; } = "Not connected";
    public bool IsDemoMode { get; private set; } = false;

    public void SetDemoMode(bool enabled)
    {
        IsDemoMode = enabled;
        if (enabled)
        {
            CurrentNamespace = "sb-demo-environment.servicebus.windows.net";
            AuthStatusMessage = "Demo Mode (Mock Service Bus)";
        }
        else
        {
            AuthStatusMessage = _adminClient != null ? "Connected" : "Not connected";
        }
    }

    public async Task ConnectAsync(string namespaceOrEndpoint, AuthMode authMode, string? clientId = null, CancellationToken ct = default)
    {
        var fqdn = NormalizeEndpoint(namespaceOrEndpoint);

        // Build token credential
        _credential = authMode switch
        {
            AuthMode.SystemAssignedManagedIdentity => new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
            AuthMode.UserAssignedManagedIdentity when !string.IsNullOrWhiteSpace(clientId) => new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId)),
            _ => new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                Diagnostics = { IsLoggingEnabled = true }
            })
        };

        try
        {
            _adminClient = new ServiceBusAdministrationClient(fqdn, _credential);
            _busClient = new ServiceBusClient(fqdn, _credential);
            CurrentNamespace = fqdn;
            IsDemoMode = false;

            var authDescription = authMode switch
            {
                AuthMode.SystemAssignedManagedIdentity => "System Managed Identity",
                AuthMode.UserAssignedManagedIdentity => $"User Managed Identity ({clientId})",
                _ => "Default Azure Credential (Managed Identity / Azure CLI)"
            };

            AuthStatusMessage = $"Connected via {authDescription}";
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            AuthStatusMessage = $"Connection failed: {ex.Message}";
            throw;
        }
    }

    public async Task<IReadOnlyList<ServiceBusEntityMetric>> GetEntityMetricsAsync(CancellationToken ct = default)
    {
        if (IsDemoMode || _adminClient == null)
        {
            return GetDemoEntityMetrics();
        }

        var results = new List<ServiceBusEntityMetric>();

        try
        {
            // Fetch Queues runtime properties
            var queuesAsync = _adminClient.GetQueuesRuntimePropertiesAsync(ct);
            await foreach (var q in queuesAsync.WithCancellation(ct))
            {
                results.Add(new ServiceBusEntityMetric
                {
                    Name = q.Name,
                    Kind = EntityKind.Queue,
                    ActiveMessageCount = q.ActiveMessageCount,
                    DeadLetterMessageCount = q.DeadLetterMessageCount,
                    ScheduledMessageCount = q.ScheduledMessageCount,
                    TotalMessageCount = q.TotalMessageCount,
                    SizeInBytes = q.SizeInBytes,
                    UpdatedAt = q.UpdatedAt,
                    AccessedAt = q.AccessedAt
                });
            }

            // Fetch Topics runtime properties
            var topicsAsync = _adminClient.GetTopicsRuntimePropertiesAsync(ct);
            var topicList = new List<TopicRuntimeProperties>();
            await foreach (var t in topicsAsync.WithCancellation(ct))
            {
                topicList.Add(t);
            }

            // Fetch Subscriptions for each Topic concurrently
            var subTasks = topicList.Select(async topic =>
            {
                var topicSubs = new List<ServiceBusEntityMetric>();
                var subsAsync = _adminClient.GetSubscriptionsRuntimePropertiesAsync(topic.Name, ct);
                await foreach (var sub in subsAsync.WithCancellation(ct))
                {
                    topicSubs.Add(new ServiceBusEntityMetric
                    {
                        Name = $"{topic.Name}/{sub.SubscriptionName}",
                        Kind = EntityKind.TopicSubscription,
                        TopicName = topic.Name,
                        SubscriptionName = sub.SubscriptionName,
                        ActiveMessageCount = sub.ActiveMessageCount,
                        DeadLetterMessageCount = sub.DeadLetterMessageCount,
                        ScheduledMessageCount = 0,
                        TotalMessageCount = sub.TotalMessageCount,
                        SizeInBytes = topic.SizeInBytes,
                        UpdatedAt = sub.UpdatedAt,
                        AccessedAt = sub.AccessedAt
                    });
                }
                return topicSubs;
            });

            var subscriptionGroups = await Task.WhenAll(subTasks);
            foreach (var group in subscriptionGroups)
            {
                results.AddRange(group);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching runtime properties: {ex.Message}");
            throw;
        }

        // Return ordered: entities with dead letters first, then by dead letter count descending, then by name
        return results
            .OrderByDescending(r => r.DeadLetterMessageCount > 0)
            .ThenByDescending(r => r.DeadLetterMessageCount)
            .ThenBy(r => r.DisplayName)
            .ToList();
    }

    public async Task<IReadOnlyList<DeadLetterMessageDetail>> PeekDeadLetterMessagesAsync(
        string entityName, 
        string? subscriptionName = null, 
        int maxMessages = 20, 
        CancellationToken ct = default)
    {
        if (IsDemoMode || _busClient == null)
        {
            return GetDemoDeadLetterMessages(entityName, subscriptionName);
        }

        ServiceBusReceiver receiver;
        var receiverOptions = new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };

        if (string.IsNullOrWhiteSpace(subscriptionName))
        {
            receiver = _busClient.CreateReceiver(entityName, receiverOptions);
        }
        else
        {
            receiver = _busClient.CreateReceiver(entityName, subscriptionName, receiverOptions);
        }

        await using (receiver)
        {
            var received = await receiver.PeekMessagesAsync(maxMessages, cancellationToken: ct);
            return received.Select(m => new DeadLetterMessageDetail
            {
                MessageId = m.MessageId ?? Guid.NewGuid().ToString(),
                SequenceNumber = m.SequenceNumber,
                EnqueuedTime = m.EnqueuedTime,
                DeliveryCount = m.DeliveryCount,
                DeadLetterReason = m.DeadLetterReason ?? "UnknownReason",
                DeadLetterErrorDescription = m.DeadLetterErrorDescription ?? "No additional error description provided.",
                ContentType = m.ContentType,
                CorrelationId = m.CorrelationId,
                Subject = m.Subject,
                ApplicationProperties = m.ApplicationProperties,
                BodyRaw = m.Body.ToString()
            }).ToList();
        }
    }

    private static string NormalizeEndpoint(string input)
    {
        var cleaned = input.Trim().TrimEnd('/');
        if (cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(8);
        if (cleaned.StartsWith("sb://", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(5);

        if (!cleaned.Contains(".servicebus.windows.net", StringComparison.OrdinalIgnoreCase))
            cleaned += ".servicebus.windows.net";

        return cleaned;
    }

    private static IReadOnlyList<ServiceBusEntityMetric> GetDemoEntityMetrics()
    {
        var now = DateTimeOffset.UtcNow;
        return new List<ServiceBusEntityMetric>
        {
            new()
            {
                Name = "orders-processing",
                Kind = EntityKind.Queue,
                ActiveMessageCount = 28,
                DeadLetterMessageCount = 28,
                ScheduledMessageCount = 0,
                TotalMessageCount = 56,
                SizeInBytes = 248000,
                UpdatedAt = now.AddMinutes(-2),
                AccessedAt = now.AddMinutes(-1)
            },
            new()
            {
                Name = "payment-events/audit-sub",
                Kind = EntityKind.TopicSubscription,
                TopicName = "payment-events",
                SubscriptionName = "audit-sub",
                ActiveMessageCount = 15,
                DeadLetterMessageCount = 14,
                ScheduledMessageCount = 0,
                TotalMessageCount = 29,
                SizeInBytes = 512000,
                UpdatedAt = now.AddMinutes(-5),
                AccessedAt = now.AddMinutes(-3)
            },
            new()
            {
                Name = "shipment-notifications",
                Kind = EntityKind.Queue,
                ActiveMessageCount = 1,
                DeadLetterMessageCount = 5,
                ScheduledMessageCount = 2,
                TotalMessageCount = 8,
                SizeInBytes = 94000,
                UpdatedAt = now.AddMinutes(-12),
                AccessedAt = now.AddMinutes(-8)
            },
            new()
            {
                Name = "inventory-sync",
                Kind = EntityKind.Queue,
                ActiveMessageCount = 84,
                DeadLetterMessageCount = 0,
                ScheduledMessageCount = 10,
                TotalMessageCount = 94,
                SizeInBytes = 1450000,
                UpdatedAt = now.AddSeconds(-30),
                AccessedAt = now.AddSeconds(-15)
            },
            new()
            {
                Name = "customer-events/crm-subscriber",
                Kind = EntityKind.TopicSubscription,
                TopicName = "customer-events",
                SubscriptionName = "crm-subscriber",
                ActiveMessageCount = 312,
                DeadLetterMessageCount = 0,
                ScheduledMessageCount = 0,
                TotalMessageCount = 312,
                SizeInBytes = 2840000,
                UpdatedAt = now.AddMinutes(-1),
                AccessedAt = now.AddSeconds(-45)
            },
            new()
            {
                Name = "telemetry-ingest",
                Kind = EntityKind.Queue,
                ActiveMessageCount = 7980,
                DeadLetterMessageCount = 0,
                ScheduledMessageCount = 0,
                TotalMessageCount = 7980,
                SizeInBytes = 18450000,
                UpdatedAt = now.AddSeconds(-10),
                AccessedAt = now.AddSeconds(-5)
            }
        };
    }

    private static IReadOnlyList<DeadLetterMessageDetail> GetDemoDeadLetterMessages(string entityName, string? subName)
    {
        var now = DateTimeOffset.UtcNow;
        return new List<DeadLetterMessageDetail>
        {
            new()
            {
                MessageId = "msg-ord-94820491",
                SequenceNumber = 500,
                EnqueuedTime = now.AddMinutes(-18),
                DeliveryCount = 10,
                DeadLetterReason = "MaxDeliveryCountExceeded",
                DeadLetterErrorDescription = "The message could not be processed after 10 delivery attempts: Connection timed out connecting to inventory service [Endpoint: inv-api.internal:5001].",
                ContentType = "application/json",
                CorrelationId = "corr-88210-941",
                Subject = "OrderPlacedEvent",
                ApplicationProperties = new Dictionary<string, object>
                {
                    { "OriginatingService", "CheckoutService" },
                    { "TenantId", "tenant-eu-central" },
                    { "Priority", "High" }
                },
                BodyRaw = """
                {
                  "orderId": "ORD-98214-X",
                  "customerId": "CUST-4819",
                  "customerName": "Acme Industrial Corp",
                  "currency": "EUR",
                  "items": [
                    { "sku": "SKU-HARDWARE-99", "quantity": 2, "unitPrice": 74.99 },
                    { "sku": "SKU-CABLE-01", "quantity": 5, "unitPrice": 12.50 }
                  ],
                  "totalAmount": 212.48,
                  "paymentRef": "PAY-EUR-941029",
                  "timestamp": "2026-09-13T10:15:00Z"
                }
                """
            },
            new()
            {
                MessageId = "msg-ord-94820823",
                SequenceNumber = 531,
                EnqueuedTime = now.AddMinutes(-14),
                DeliveryCount = 10,
                DeadLetterReason = "MaxDeliveryCountExceeded",
                DeadLetterErrorDescription = "The message was dead-lettered because max delivery count was exceeded. Last error: DeserializationException: Field 'customerId' cannot be null.",
                ContentType = "application/json",
                CorrelationId = "corr-88210-994",
                Subject = "OrderPlacedEvent",
                ApplicationProperties = new Dictionary<string, object>
                {
                    { "OriginatingService", "CheckoutService" },
                    { "RetryAttempt", 10 }
                },
                BodyRaw = """
                {
                  "orderId": "ORD-98230-B",
                  "customerId": null,
                  "totalAmount": 49.00
                }
                """
            },
            new()
            {
                MessageId = "msg-ord-94820495",
                SequenceNumber = 532,
                EnqueuedTime = now.AddMinutes(-9),
                DeliveryCount = 1,
                DeadLetterReason = "SchemaValidationFailed",
                DeadLetterErrorDescription = "Business validation rejected: invalid currency code 'ZZZ' provided.",
                ContentType = "application/json",
                CorrelationId = "corr-99312-001",
                Subject = "OrderValidationEvent",
                ApplicationProperties = new Dictionary<string, object>
                {
                    { "Validator", "SchemaEngine_v2" }
                },
                BodyRaw = """
                {
                  "orderId": "ORD-INVALID-CURR",
                  "currency": "ZZZ",
                  "amount": 100.00
                }
                """
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_busClient != null)
        {
            await _busClient.DisposeAsync();
            _busClient = null;
        }
    }
}
