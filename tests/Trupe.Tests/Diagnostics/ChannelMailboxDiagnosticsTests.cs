using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions.Messages;
using Trupe.Mailboxes;

namespace Trupe.Tests.Diagnostics;

public class ChannelMailboxDiagnosticsTests
{
    [Test]
    public async Task EnqueueAsync_Should_IncrementEnqueueCounter()
    {
        using var collector = new MetricsCollector();
        var mailbox = new ChannelMailbox();

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "mailbox.enqueue");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task EnqueueAsync_Should_UpdateMailboxLengthGauge_ToOne()
    {
        using var collector = new MetricsCollector();
        var mailbox = new ChannelMailbox();

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        var measurement = collector.Measurements.LastOrDefault(m => m.Name == "mailbox.length");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task EnqueueAsync_Should_RecordEnqueueDuration()
    {
        using var collector = new MetricsCollector();
        var mailbox = new ChannelMailbox();

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "mailbox.enqueue_duration");
        await Assert.That(measurement).IsNotNull();
    }

    [Test]
    public async Task DequeueAsync_Should_IncrementDequeueCounter()
    {
        var mailbox = new ChannelMailbox();
        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        using var collector = new MetricsCollector();

        await mailbox.DequeueAsync();

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "mailbox.dequeue");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task DequeueAsync_Should_UpdateMailboxLengthGauge_ToZero()
    {
        using var collector = new MetricsCollector();
        var mailbox = new ChannelMailbox();

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());
        await mailbox.DequeueAsync();

        var measurement = collector.Measurements.LastOrDefault(m => m.Name == "mailbox.length");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(0);
    }

    [Test]
    public async Task DequeueAsync_Should_RecordDequeueDuration()
    {
        var mailbox = new ChannelMailbox();
        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        using var collector = new MetricsCollector();

        await mailbox.DequeueAsync();

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "mailbox.dequeue_duration");
        await Assert.That(measurement).IsNotNull();
    }
}
