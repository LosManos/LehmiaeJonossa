using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using AzureDeadLetterMonitor.Models;
using AzureDeadLetterMonitor.Services;
using AzureDeadLetterMonitor.ViewModels;

namespace AzureDeadLetterMonitor.Tests;

public class MonitorTests
{
    [Fact]
    public void EntityMetric_DeadLetterFlag_IsTrueWhenCountGreaterThanZero()
    {
        var entityWithDlq = new ServiceBusEntityMetric
        {
            Name = "orders-queue",
            Kind = EntityKind.Queue,
            ActiveMessageCount = 10,
            DeadLetterMessageCount = 5,
            TotalMessageCount = 15,
            SizeInBytes = 2048
        };

        var entityHealthy = new ServiceBusEntityMetric
        {
            Name = "payments-queue",
            Kind = EntityKind.Queue,
            ActiveMessageCount = 10,
            DeadLetterMessageCount = 0,
            TotalMessageCount = 10,
            SizeInBytes = 1024
        };

        var entityZeroActive = new ServiceBusEntityMetric
        {
            Name = "empty-queue",
            Kind = EntityKind.Queue,
            ActiveMessageCount = 0,
            DeadLetterMessageCount = 0,
            TotalMessageCount = 0,
            SizeInBytes = 0
        };

        Assert.True(entityWithDlq.HasDeadLetters);
        Assert.True(entityWithDlq.HasActiveMessages);
        Assert.Equal("5", entityWithDlq.FormattedDeadLetterCount);
        Assert.Equal("orders-queue", entityWithDlq.DisplayName);
        Assert.Equal("Queue", entityWithDlq.KindDisplay);

        Assert.False(entityHealthy.HasDeadLetters);
        Assert.True(entityHealthy.HasActiveMessages);
        Assert.Equal("0", entityHealthy.FormattedDeadLetterCount);

        Assert.False(entityZeroActive.HasActiveMessages);
        Assert.Equal("0", entityZeroActive.FormattedActiveCount);
    }

    [Fact]
    public void EntityMetric_TopicSubscriptionDisplayName_FormatsNicely()
    {
        var sub = new ServiceBusEntityMetric
        {
            Name = "events/sub-audit",
            Kind = EntityKind.TopicSubscription,
            TopicName = "events",
            SubscriptionName = "sub-audit",
            ActiveMessageCount = 2,
            DeadLetterMessageCount = 3,
            TotalMessageCount = 5,
            SizeInBytes = 4096
        };

        Assert.Equal("events / sub-audit", sub.DisplayName);
        Assert.Equal("Topic Subscription", sub.KindDisplay);
        Assert.True(sub.HasDeadLetters);
    }

    [Fact]
    public void DeadLetterMessageDetail_JsonFormatting_IndentsValidJson()
    {
        var detail = new DeadLetterMessageDetail
        {
            MessageId = "msg-1",
            SequenceNumber = 1,
            EnqueuedTime = DateTimeOffset.UtcNow,
            DeliveryCount = 1,
            DeadLetterReason = "MaxDeliveryCountExceeded",
            BodyRaw = "{\"foo\":\"bar\",\"count\":42}"
        };

        Assert.Contains("\n", detail.FormattedBody);
        Assert.Contains("\"foo\": \"bar\"", detail.FormattedBody);
        Assert.Contains("\"count\": 42", detail.FormattedBody);
    }

    [Fact]
    public void DeadLetterMessageDetail_RawString_ReturnedWhenNotJson()
    {
        var detail = new DeadLetterMessageDetail
        {
            MessageId = "msg-2",
            SequenceNumber = 2,
            EnqueuedTime = DateTimeOffset.UtcNow,
            DeliveryCount = 1,
            DeadLetterReason = "BadEncoding",
            BodyRaw = "Plain text payload that is not JSON"
        };

        Assert.Equal("Plain text payload that is not JSON", detail.FormattedBody);
    }

    [Fact]
    public async Task ServiceBusMonitorService_DemoMode_ReturnsEntitiesSortedByDeadLetters()
    {
        var service = new AzureServiceBusMonitorService();
        service.SetDemoMode(true);

        var metrics = await service.GetEntityMetricsAsync();

        Assert.NotEmpty(metrics);
        Assert.True(metrics.First().HasDeadLetters, "Entities with dead letters should appear first");
        Assert.True(metrics.First().DeadLetterMessageCount >= metrics.Last().DeadLetterMessageCount);

        var peeked = await service.PeekDeadLetterMessagesAsync("orders-processing");
        Assert.NotEmpty(peeked);
        Assert.All(peeked, p => Assert.False(string.IsNullOrEmpty(p.DeadLetterReason)));
    }

    [Fact]
    public void Themes_AllFourThemesAreAvailable()
    {
        var themes = Enum.GetValues<AppTheme>();
        Assert.Contains(AppTheme.Light, themes);
        Assert.Contains(AppTheme.Dark, themes);
        Assert.Contains(AppTheme.Lego, themes);
        Assert.Contains(AppTheme.Barbie, themes);
    }

    [Fact]
    public void AuthModes_AllThreeModesAreAvailable()
    {
        var modes = Enum.GetValues<AuthMode>();
        Assert.Contains(AuthMode.DefaultAzureCredential, modes);
        Assert.Contains(AuthMode.SystemAssignedManagedIdentity, modes);
        Assert.Contains(AuthMode.UserAssignedManagedIdentity, modes);
    }

    [Fact]
    public async Task ServiceBusMonitorService_DemoMode_SetsAccountIdentity()
    {
        var service = new AzureServiceBusMonitorService();
        service.SetDemoMode(true);
        Assert.Equal("Mock Environment", service.CurrentAccountIdentity);
        Assert.Equal("Demo Mode (Mock Service Bus)", service.AuthStatusMessage);

        var namespaces = await service.DiscoverNamespacesAsync();
        Assert.NotNull(namespaces);
    }

    [Fact]
    public void DeadLetterMessageDetail_BodyPreview_TruncatesAndFlattensNewlines()
    {
        var longBody = "Line 1\r\nLine 2 with very long content that exceeds eighty characters in length so that we can verify truncation behavior in the DataGrid row presentation.";
        var detail = new DeadLetterMessageDetail
        {
            MessageId = "msg-prev-1",
            SequenceNumber = 100,
            EnqueuedTime = DateTimeOffset.UtcNow,
            DeliveryCount = 3,
            DeadLetterReason = "Failed",
            BodyRaw = longBody
        };

        Assert.DoesNotContain("\r", detail.BodyPreview);
        Assert.DoesNotContain("\n", detail.BodyPreview);
        Assert.EndsWith("...", detail.BodyPreview);
        Assert.True(detail.BodyPreview.Length <= 85);
    }

    [Fact]
    public async Task ServiceBusMonitorService_Pagination_ReturnsPagingSlicesCorrectly()
    {
        var service = new AzureServiceBusMonitorService();
        service.SetDemoMode(true);

        // Page 1: 10 messages
        var page1 = await service.PeekDeadLetterMessagesAsync("orders-processing", maxMessages: 10);
        Assert.Equal(10, page1.Count);
        Assert.Equal(500, page1.First().SequenceNumber);

        // Page 2: starting after page 1
        var nextSeq = page1.Last().SequenceNumber + 1;
        var page2 = await service.PeekDeadLetterMessagesAsync("orders-processing", fromSequenceNumber: nextSeq, maxMessages: 10);
        Assert.Equal(10, page2.Count);
        Assert.Equal(page1.Last().SequenceNumber + 1, page2.First().SequenceNumber);
        Assert.NotEqual(page1.First().MessageId, page2.First().MessageId);
    }

    [Fact]
    public void MessageDetailViewModel_Navigation_StepsBackAndForthCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DeadLetterMessageDetail>
        {
            new() { MessageId = "id-1", SequenceNumber = 1, EnqueuedTime = now, DeliveryCount = 1, DeadLetterReason = "R1", BodyRaw = "b1" },
            new() { MessageId = "id-2", SequenceNumber = 2, EnqueuedTime = now, DeliveryCount = 2, DeadLetterReason = "R2", BodyRaw = "b2" },
            new() { MessageId = "id-3", SequenceNumber = 3, EnqueuedTime = now, DeliveryCount = 3, DeadLetterReason = "R3", BodyRaw = "b3" }
        };

        var vm = new MessageDetailViewModel(messages, initialIndex: 0);
        Assert.Equal("id-1", vm.CurrentMessage?.MessageId);
        Assert.False(vm.CanGoPrevious);
        Assert.True(vm.CanGoNext);
        Assert.Equal("Message 1 of 3", vm.PositionDisplay);

        // Advance to next
        vm.Next();
        Assert.Equal("id-2", vm.CurrentMessage?.MessageId);
        Assert.True(vm.CanGoPrevious);
        Assert.True(vm.CanGoNext);
        Assert.Equal("Message 2 of 3", vm.PositionDisplay);

        // Advance to last
        vm.Next();
        Assert.Equal("id-3", vm.CurrentMessage?.MessageId);
        Assert.True(vm.CanGoPrevious);
        Assert.False(vm.CanGoNext);

        // Cannot go past last
        vm.Next();
        Assert.Equal("id-3", vm.CurrentMessage?.MessageId);

        // Go back
        vm.Previous();
        Assert.Equal("id-2", vm.CurrentMessage?.MessageId);
    }

    [Fact]
    public async Task MessageInspectorViewModel_Paging_IncrementsAndDecrementsPages()
    {
        var service = new AzureServiceBusMonitorService();
        service.SetDemoMode(true);

        var vm = new MessageInspectorViewModel(service)
        {
            PageSize = 10
        };

        var metric = new ServiceBusEntityMetric
        {
            Name = "orders-processing",
            Kind = EntityKind.Queue,
            DeadLetterMessageCount = 28
        };

        await vm.OpenForEntityAsync(metric);

        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(10, vm.Messages.Count);
        Assert.False(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);

        // Advance to page 2
        await vm.NextPageAsync();
        Assert.Equal(2, vm.CurrentPage);
        Assert.Equal(10, vm.Messages.Count);
        Assert.True(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);

        // Advance to page 3 (remaining 8 messages)
        await vm.NextPageAsync();
        Assert.Equal(3, vm.CurrentPage);
        Assert.Equal(8, vm.Messages.Count);
        Assert.True(vm.HasPreviousPage);
        Assert.False(vm.HasNextPage); // No more full pages

        // Go back to page 2
        await vm.PreviousPageAsync();
        Assert.Equal(2, vm.CurrentPage);
        Assert.Equal(10, vm.Messages.Count);
    }

    [Fact]
    public void AppSettings_MessageDetailWindowBounds_StoresAndRetrieves()
    {
        var settings = new AppSettings
        {
            MessageDetailWindowWidth = 920,
            MessageDetailWindowHeight = 740,
            MessageDetailWindowX = 150,
            MessageDetailWindowY = 220
        };

        Assert.Equal(920, settings.MessageDetailWindowWidth);
        Assert.Equal(740, settings.MessageDetailWindowHeight);
        Assert.Equal(150, settings.MessageDetailWindowX);
        Assert.Equal(220, settings.MessageDetailWindowY);
    }

    [Fact]
    public void ServiceBusEntityMetric_HotlistIndex_FormatsAndGeneratesShortcuts()
    {
        var entity = new ServiceBusEntityMetric
        {
            Name = "orders-dlq",
            Kind = EntityKind.Queue,
            DeadLetterMessageCount = 10
        };

        Assert.Equal(0, entity.HotlistIndex);
        Assert.Equal(string.Empty, entity.FormattedHotlistIndex);
        Assert.Equal(string.Empty, entity.FormattedRowNumber);
        Assert.Equal(string.Empty, entity.HotlistShortcutTip);

        entity.HotlistIndex = 1;
        Assert.Equal("1", entity.FormattedHotlistIndex);
        Assert.Equal("#1", entity.FormattedRowNumber);
        Assert.Contains("1", entity.HotlistShortcutTip);
        Assert.Contains("1", entity.HotlistInspectButtonToolTip);

        entity.HotlistIndex = 9;
        Assert.Equal("9", entity.FormattedHotlistIndex);
        Assert.Equal("#9", entity.FormattedRowNumber);
        Assert.Contains("9", entity.HotlistShortcutTip);

        entity.HotlistIndex = 10;
        Assert.Equal("10", entity.FormattedHotlistIndex);
        Assert.Equal("#10", entity.FormattedRowNumber);
        Assert.Equal("Item #10", entity.HotlistShortcutTip);
    }

    [Fact]
    public void ServiceBusEntityMetric_HotlistIndex_NotifiesPropertyChanged()
    {
        var entity = new ServiceBusEntityMetric
        {
            Name = "orders-dlq",
            Kind = EntityKind.Queue,
            DeadLetterMessageCount = 10
        };

        var changedProps = new System.Collections.Generic.List<string>();
        entity.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProps.Add(e.PropertyName);
        };

        entity.HotlistIndex = 3;

        Assert.Contains(nameof(ServiceBusEntityMetric.HotlistIndex), changedProps);
        Assert.Contains(nameof(ServiceBusEntityMetric.FormattedHotlistIndex), changedProps);
        Assert.Contains(nameof(ServiceBusEntityMetric.HotlistShortcutTip), changedProps);
        Assert.Contains(nameof(ServiceBusEntityMetric.HotlistInspectButtonToolTip), changedProps);
    }
}

