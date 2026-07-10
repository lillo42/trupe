using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions.Messages;
using Trupe.Mailboxes;
using TUnit.Core;

namespace Trupe.Tests.Diagnostics;

[NotInParallel("diagnostics")]
public class ChannelMailboxDiagnosticsTests
{
    [Test]
    public async Task EnqueueAsync_Should_IncrementEnqueueCounter()
    {
        using var collector = new MetricsCollector();
        var (mailbox, mailboxId) = CreateMailbox();

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        var measurement = collector.Measurements
            .FirstOrDefault(m => m.Name == "mailbox.enqueue" &&
                                 m.Tags.Any(t => t.Key == "mailbox" && (string?)t.Value == mailboxId));
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task EnqueueAsync_Should_UpdateMailboxLengthGauge_ToOne()
    {
        using var collector = new MetricsCollector();
        var (mailbox, mailboxId) = CreateMailbox();

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        var measurement = collector.Measurements
            .LastOrDefault(m => m.Name == "mailbox.length" &&
                                m.Tags.Any(t => t.Key == "mailbox" && (string?)t.Value == mailboxId));
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task EnqueueAsync_Should_RecordEnqueueDuration()
    {
        using var collector = new MetricsCollector();
        var (mailbox, mailboxId) = CreateMailbox();

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        var measurement = collector.Measurements
            .FirstOrDefault(m => m.Name == "mailbox.enqueue.duration" &&
                                 m.Tags.Any(t => t.Key == "mailbox" && (string?)t.Value == mailboxId));
        await Assert.That(measurement).IsNotNull();
    }

    [Test]
    public async Task DequeueAsync_Should_IncrementDequeueCounter()
    {
        var (mailbox, mailboxId) = CreateMailbox();
        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        using var collector = new MetricsCollector();

        await mailbox.DequeueAsync();

        var measurement = collector.Measurements
            .FirstOrDefault(m => m.Name == "mailbox.dequeue" &&
                                 m.Tags.Any(t => t.Key == "mailbox" && (string?)t.Value == mailboxId));
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task DequeueAsync_Should_UpdateMailboxLengthGauge_ToZero()
    {
        using var collector = new MetricsCollector();
        var (mailbox, mailboxId) = CreateMailbox();

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());
        await mailbox.DequeueAsync();

        var measurement = collector.Measurements
            .LastOrDefault(m => m.Name == "mailbox.length" &&
                                m.Tags.Any(t => t.Key == "mailbox" && (string?)t.Value == mailboxId));
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(0);
    }

    [Test]
    public async Task DequeueAsync_Should_RecordDequeueDuration()
    {
        var (mailbox, mailboxId) = CreateMailbox();
        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        using var collector = new MetricsCollector();

        await mailbox.DequeueAsync();

        var measurement = collector.Measurements
            .FirstOrDefault(m => m.Name == "mailbox.dequeue.duration" &&
                                 m.Tags.Any(t => t.Key == "mailbox" && (string?)t.Value == mailboxId));
        await Assert.That(measurement).IsNotNull();
    }

    private static (ChannelMailbox Mailbox, string MailboxId) CreateMailbox()
    {
        var mailbox = new ChannelMailbox();
        var mailboxId = Guid.NewGuid().ToString();
        mailbox.Metadata = new[] { new KeyValuePair<string, object?>("mailbox", mailboxId) };
        return (mailbox, mailboxId);
    }
}
