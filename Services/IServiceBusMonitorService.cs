using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LehmiaeJonossa.Models;

namespace LehmiaeJonossa.Services;

public interface IServiceBusMonitorService
{
    bool IsConnected { get; }
    string? CurrentNamespace { get; }
    string AuthStatusMessage { get; }
    string? CurrentAccountIdentity { get; }
    bool IsDemoMode { get; }

    Task ConnectAsync(string namespaceOrEndpoint, AuthMode authMode, string? clientId = null, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceBusEntityMetric>> GetEntityMetricsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DeadLetterMessageDetail>> PeekDeadLetterMessagesAsync(string entityName, string? subscriptionName = null, long? fromSequenceNumber = null, int maxMessages = 20, CancellationToken ct = default);
    Task<IReadOnlyList<string>> DiscoverNamespacesAsync(CancellationToken ct = default);
    void SetDemoMode(bool enabled);
}

