using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using LehmiaeJonossa.Models;

namespace LehmiaeJonossa.Services;

public class AzureServiceBusMonitorService : IServiceBusMonitorService, IAsyncDisposable
{
    private ServiceBusAdministrationClient? _adminClient;
    private ServiceBusClient? _busClient;
    private TokenCredential? _credential;

    public bool IsConnected => _adminClient != null && !IsDemoMode;
    public string? CurrentNamespace { get; private set; }
    public string AuthStatusMessage { get; private set; } = "Not connected";
    public string? CurrentAccountIdentity { get; private set; }
    public bool IsDemoMode { get; private set; } = false;

    public void SetDemoMode(bool enabled)
    {
        IsDemoMode = enabled;
        if (enabled)
        {
            CurrentNamespace = "sb-demo-environment.servicebus.windows.net";
            AuthStatusMessage = "Demo Mode (Mock Service Bus)";
            CurrentAccountIdentity = "Mock Environment";
        }
        else
        {
            AuthStatusMessage = _adminClient != null ? $"Connected: {CurrentAccountIdentity ?? "Azure"}" : "Not connected";
        }
    }

    public async Task<IReadOnlyList<string>> DiscoverNamespacesAsync(CancellationToken ct = default)
    {
        return await AzureCliHelper.ListServiceBusNamespacesAsync(ct);
    }

    public async Task ConnectAsync(string namespaceOrEndpoint, AuthMode authMode, string? clientId = null, CancellationToken ct = default)
    {
        var fqdn = NormalizeEndpoint(namespaceOrEndpoint);

        // Build token credential
        _credential = authMode switch
        {
            AuthMode.SystemAssignedManagedIdentity => new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
            AuthMode.UserAssignedManagedIdentity when !string.IsNullOrWhiteSpace(clientId) => new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId)),
            _ => CreateDefaultCredential()
        };

        try
        {
            _adminClient = new ServiceBusAdministrationClient(fqdn, _credential);
            _busClient = new ServiceBusClient(fqdn, _credential, new ServiceBusClientOptions
            {
                RetryOptions = new ServiceBusRetryOptions
                {
                    TryTimeout = TimeSpan.FromSeconds(30),
                    MaxRetries = 2
                }
            });
            CurrentNamespace = fqdn;
            IsDemoMode = false;

            if (authMode == AuthMode.SystemAssignedManagedIdentity)
            {
                CurrentAccountIdentity = "System Managed Identity";
            }
            else if (authMode == AuthMode.UserAssignedManagedIdentity)
            {
                CurrentAccountIdentity = $"User Managed Identity ({clientId})";
            }
            else
            {
                var cliAccount = await AzureCliHelper.GetCurrentAccountAsync(ct);
                if (cliAccount != null && !string.IsNullOrWhiteSpace(cliAccount.User))
                {
                    CurrentAccountIdentity = $"{cliAccount.User} (Azure CLI)";
                }
                else
                {
                    CurrentAccountIdentity = "Default Azure Credential";
                }
            }

            AuthStatusMessage = $"Connected: {CurrentAccountIdentity}";
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            AuthStatusMessage = $"Connection failed: {ex.Message}";
            throw;
        }
    }

    private static TokenCredential CreateDefaultCredential()
    {
        // On developer workstations (macOS / Windows), DefaultAzureCredential's IMDS probe (169.254.169.254)
        // fails with socket errors after 6 retries (~30s delay) before reaching AzureCliCredential.
        // We prioritize local developer tools (Azure CLI, Azure Developer CLI, PowerShell, Visual Studio)
        // and fall back safely to DefaultAzureCredential with ExcludeManagedIdentityCredential set when not on Azure.
        var isAzureEnv = IsRunningInAzureEnvironment();
        return new ChainedTokenCredential(
            new AzureCliCredential(),
            new AzureDeveloperCliCredential(),
            new AzurePowerShellCredential(),
            new VisualStudioCredential(),
            new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeManagedIdentityCredential = !isAzureEnv,
                Diagnostics = { IsLoggingEnabled = true }
            })
        );
    }

    private static bool IsRunningInAzureEnvironment()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSI_ENDPOINT")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
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
        long? fromSequenceNumber = null,
        int maxMessages = 20, 
        CancellationToken ct = default)
    {
        var resolvedEntityName = entityName;
        var resolvedSubscriptionName = subscriptionName;

        // If the entity name is formatted as "topicName/subscriptionName", extract the components
        if (resolvedEntityName.Contains('/'))
        {
            var parts = resolvedEntityName.Split('/', 2);
            resolvedEntityName = parts[0];
            if (string.IsNullOrWhiteSpace(resolvedSubscriptionName))
            {
                resolvedSubscriptionName = parts[1];
            }
        }

        if (IsDemoMode || _busClient == null)
        {
            return GetDemoDeadLetterMessages(resolvedEntityName, resolvedSubscriptionName, fromSequenceNumber, maxMessages);
        }

        ServiceBusReceiver receiver;
        var receiverOptions = new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };

        if (string.IsNullOrWhiteSpace(resolvedSubscriptionName))
        {
            receiver = _busClient.CreateReceiver(resolvedEntityName, receiverOptions);
        }
        else
        {
            receiver = _busClient.CreateReceiver(resolvedEntityName, resolvedSubscriptionName, receiverOptions);
        }

        await using (receiver)
        {
            var received = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber: fromSequenceNumber, cancellationToken: ct);
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

    private static IReadOnlyList<DeadLetterMessageDetail> GetDemoDeadLetterMessages(
        string entityName, 
        string? subName,
        long? fromSequenceNumber,
        int maxMessages)
    {
        var now = DateTimeOffset.UtcNow;
        var reasons = new[]
        {
            ("MaxDeliveryCountExceeded", "The message could not be processed after 10 delivery attempts: Connection timed out."),
            ("SchemaValidationFailed", "Business validation rejected: invalid currency or required field missing."),
            ("HeaderValidationFailed", "Missing required header 'X-Correlation-Source'."),
            ("PaymentGatewayDeclined", "Payment processor rejected card token during capture.")
        };

        var allMessages = new List<DeadLetterMessageDetail>();

        // Generate 28 messages to match orders-processing metric count
        for (int i = 0; i < 28; i++)
        {
            var seq = 500 + i;
            var reasonInfo = reasons[i % reasons.Length];
            var orderId = $"ORD-98{200 + i}-{(char)('A' + (i % 26))}";
            allMessages.Add(new DeadLetterMessageDetail
            {
                MessageId = $"msg-ord-{94820000 + i}",
                SequenceNumber = seq,
                EnqueuedTime = now.AddMinutes(-30 + i),
                DeliveryCount = (i % 3 == 0) ? 10 : (i % 3 + 1),
                DeadLetterReason = reasonInfo.Item1,
                DeadLetterErrorDescription = $"{reasonInfo.Item2} Details: Entity={entityName}, Sub={subName ?? "(none)"}, Record #{i + 1}.",
                ContentType = "application/json",
                CorrelationId = $"corr-88210-{100 + i}",
                Subject = (i % 2 == 0) ? "OrderPlacedEvent" : "OrderValidationEvent",
                ApplicationProperties = new Dictionary<string, object>
                {
                    { "OriginatingService", (i % 2 == 0) ? "CheckoutService" : "PaymentService" },
                    { "TenantId", "tenant-eu-central" },
                    { "Priority", (i % 5 == 0) ? "High" : "Normal" },
                    { "Attempt", (i % 3 == 0) ? 10 : (i % 3 + 1) }
                },
                BodyRaw = $$"""
                {
                  "orderId": "{{orderId}}",
                  "customerId": "CUST-{{4800 + i}}",
                  "amount": {{25.50 + i * 4.25}},
                  "currency": "EUR",
                  "itemCount": {{(i % 4) + 1}},
                  "notes": "Dead letter message #{{i + 1}} in {{entityName}}"
                }
                """
            });
        }

        var query = allMessages.AsEnumerable();
        if (fromSequenceNumber.HasValue)
        {
            query = query.Where(m => m.SequenceNumber >= fromSequenceNumber.Value);
        }

        return query.Take(maxMessages).ToList();
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
