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

        Assert.True(entityWithDlq.HasDeadLetters);
        Assert.Equal("5", entityWithDlq.FormattedDeadLetterCount);
        Assert.Equal("orders-queue", entityWithDlq.DisplayName);
        Assert.Equal("Queue", entityWithDlq.KindDisplay);

        Assert.False(entityHealthy.HasDeadLetters);
        Assert.Equal("0", entityHealthy.FormattedDeadLetterCount);
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
}
