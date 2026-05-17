using System.Threading;
using System.Threading.Tasks;
using Trupe.Mailboxes;
using Trupe.Messages;

namespace Trupe.Tests.Mailboxes;

public class ChannelMailboxTest
{
    [Test]
    [Timeout(5000)]
    public async Task EnqueueDequeue_Should_Successed(CancellationToken cancellationToken)
    {
        var mailbox = new ChannelMailbox();

        var message = new TellMessage(new object(), []);

        await mailbox.EnqueueAsync(message, cancellationToken);

        var dequeuedMessage = await mailbox.DequeueAsync(cancellationToken);
        await Assert.That(dequeuedMessage).EqualTo(message);
    }

    [Test]
    [Timeout(5000)]
    public async Task DequeueEnqueue_Should_Successed(CancellationToken cancellationToken)
    {
        var mailbox = new ChannelMailbox();
        var message = new TellMessage(new object(), []);

        var dequeueTask = Task.Run(
            async () =>
            {
                var dequeuedMessage = await mailbox.DequeueAsync(cancellationToken);
                await Assert.That(dequeuedMessage).EqualTo(message);
            },
            cancellationToken
        );

        await mailbox.EnqueueAsync(message, cancellationToken);

        await dequeueTask;
    }
}
