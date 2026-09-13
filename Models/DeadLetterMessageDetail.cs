using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AzureDeadLetterMonitor.Models;

public class DeadLetterMessageDetail
{
    public required string MessageId { get; init; }
    public long SequenceNumber { get; init; }
    public DateTimeOffset EnqueuedTime { get; init; }
    public int DeliveryCount { get; init; }
    public string? DeadLetterReason { get; init; }
    public string? DeadLetterErrorDescription { get; init; }
    public string? ContentType { get; init; }
    public string? CorrelationId { get; init; }
    public string? Subject { get; init; }
    public IReadOnlyDictionary<string, object>? ApplicationProperties { get; init; }
    public string BodyRaw { get; init; } = string.Empty;

    public string FormattedEnqueuedTime => EnqueuedTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string FormattedBody
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BodyRaw))
                return "(Empty message body)";

            try
            {
                using var jsonDoc = JsonDocument.Parse(BodyRaw);
                return JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                // Not JSON, return raw text
                return BodyRaw;
            }
        }
    }
}
