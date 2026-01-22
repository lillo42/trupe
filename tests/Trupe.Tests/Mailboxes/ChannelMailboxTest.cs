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

        var message = new LocalTellMessage(new object());

        await mailbox.EnqueueAsync(message, cancellationToken);

        await foreach (var dequeuedMessage in mailbox.WithCancellation(cancellationToken))
        {
            await Assert.That(dequeuedMessage).EqualTo(message);
            break;
        }
    }

    [Test]
    [Timeout(5000)]
    public async Task DequeueEnqueue_Should_Successed(CancellationToken cancellationToken)
    {
        var mailbox = new ChannelMailbox();
        var message = new LocalTellMessage(new object());

        var dequeueTask = Task.Run(
            async () =>
            {
                await foreach (var dequeuedMessage in mailbox.WithCancellation(cancellationToken))
                {
                    await Assert.That(dequeuedMessage).EqualTo(message);
                    break;
                }
            },
            cancellationToken
        );

        await mailbox.EnqueueAsync(message, cancellationToken);

        await dequeueTask;
    }
}
