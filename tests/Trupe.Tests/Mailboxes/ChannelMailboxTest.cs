using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions.Messages;
using Trupe.Mailboxes;

namespace Trupe.Tests.Mailboxes;

public class ChannelMailboxTest
{
    [Test]
    public async Task EnqueueAsync_Should_Dequeue()
    {
        var mailbox = new ChannelMailbox();
        var message = Substitute.For<IMessage>();

        await mailbox.EnqueueAsync(message);

        await Assert
            .That(async () => await mailbox.DequeueAsync())
            .ThrowsNothing()
            .And.IsEqualTo(message);
    }

    [Test]
    public async Task EnqueueAsync_Should_FollowTheConfiguration()
    {
        var mailbox = new ChannelMailbox(1, BoundedChannelFullMode.DropWrite);

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        var message = Substitute.For<IMessage>();

        await Assert
            .That(async () => await mailbox.EnqueueAsync(message))
            .ThrowsNothing();

        var dequeued = await mailbox.DequeueAsync();
        await Assert.That(dequeued).IsNotEqualTo(message);
    }

    [Test]
    public async Task DequeueAsync_Should_NotThrowAfterCancellationHasPass()
    {
        var mailbox = new ChannelMailbox(1);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));
        var message = Substitute.For<IMessage>();

        await Assert
            .That(async () => await mailbox.DequeueAsync(cts.Token))
            .ThrowsNothing()
            .And.IsNull();
    }
}
