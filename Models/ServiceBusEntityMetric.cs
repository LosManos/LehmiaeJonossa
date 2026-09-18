using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzureDeadLetterMonitor.Models;

public enum EntityKind
{
    Queue,
    TopicSubscription
}

public class ServiceBusEntityMetric : ObservableObject
{
    private int _hotlistIndex;
    public int HotlistIndex
    {
        get => _hotlistIndex;
        set
        {
            if (SetProperty(ref _hotlistIndex, value))
            {
                OnPropertyChanged(nameof(FormattedHotlistIndex));
                OnPropertyChanged(nameof(FormattedRowNumber));
                OnPropertyChanged(nameof(HotlistShortcutTip));
                OnPropertyChanged(nameof(HotlistInspectButtonToolTip));
            }
        }
    }

    public string FormattedHotlistIndex => HotlistIndex > 0 ? HotlistIndex.ToString() : string.Empty;
    public string FormattedRowNumber => HotlistIndex > 0 ? $"#{HotlistIndex}" : string.Empty;

    public string HotlistShortcutTip => HotlistIndex is >= 1 and <= 9
        ? (OperatingSystem.IsMacOS()
            ? $"Shortcut: ⌘{HotlistIndex} to select (Enter to inspect)"
            : $"Shortcut: Ctrl+{HotlistIndex} to select (Enter to inspect)")
        : (HotlistIndex > 0 ? $"Item #{HotlistIndex}" : string.Empty);

    public string HotlistInspectButtonToolTip => HotlistIndex is >= 1 and <= 9
        ? (OperatingSystem.IsMacOS()
            ? $"Open Dead Letter Queue inspector (⌘{HotlistIndex} to select, Enter to open)"
            : $"Open Dead Letter Queue inspector (Ctrl+{HotlistIndex} to select, Enter to open)")
        : "Open Dead Letter Queue inspector";
    public required string Name { get; init; }
    public EntityKind Kind { get; init; }
    public string? TopicName { get; init; }
    public string? SubscriptionName { get; init; }

    public long ActiveMessageCount { get; init; }
    public long DeadLetterMessageCount { get; init; }
    public long ScheduledMessageCount { get; init; }
    public long TotalMessageCount { get; init; }
    public long SizeInBytes { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
    public DateTimeOffset? AccessedAt { get; init; }

    public bool HasDeadLetters => DeadLetterMessageCount > 0;
    public bool HasActiveMessages => ActiveMessageCount > 0;

    public string DisplayName => Kind == EntityKind.TopicSubscription 
        ? $"{TopicName} / {SubscriptionName}" 
        : Name;

    public string KindDisplay => Kind == EntityKind.TopicSubscription 
        ? "Topic Subscription" 
        : "Queue";

    public string FormattedDeadLetterCount => DeadLetterMessageCount.ToString("N0");
    public string FormattedActiveCount => ActiveMessageCount.ToString("N0");
    public string FormattedTotalCount => TotalMessageCount.ToString("N0");

    public string FormattedSize
    {
        get
        {
            if (SizeInBytes < 1024) return $"{SizeInBytes} B";
            if (SizeInBytes < 1024 * 1024) return $"{(SizeInBytes / 1024.0):F1} KB";
            if (SizeInBytes < 1024 * 1024 * 1024) return $"{(SizeInBytes / (1024.0 * 1024.0)):F1} MB";
            return $"{(SizeInBytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
        }
    }

    public string FormattedLastActivity
    {
        get
        {
            var date = AccessedAt ?? UpdatedAt;
            if (!date.HasValue) return "N/A";
            
            var diff = DateTimeOffset.UtcNow - date.Value;
            if (diff.TotalSeconds < 60) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return date.Value.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
