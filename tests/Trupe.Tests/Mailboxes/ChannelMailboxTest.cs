using System;
using System.Threading;
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
        var mailbox = new ChannelMailbox(1);

        await mailbox.EnqueueAsync(Substitute.For<IMessage>());

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));
        var message = Substitute.For<IMessage>();

        await Assert
            .That(async () => await mailbox.EnqueueAsync(message, cts.Token))
            .Throws<OperationCanceledException>();
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
