using System;
using System.Threading;
using System.Threading.Tasks;
using Trupe.ActorReferences;
using Trupe.Mailboxes;
using Trupe.Messages;

namespace Trupe.Tests.ActorReferences;

public class LocalActorReferenceTest
{
    [Test]
    [Timeout(5000)]
    public async Task Tell_Should_ReturnImmediately(CancellationToken cancellationToken)
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox();
        var actorRef = new LocalActorReference(new Uri("/", UriKind.RelativeOrAbsolute), mailbox);

        // Act
        actorRef.Tell(message);

        // Assert
        await foreach (var receivedMessage in mailbox.WithCancellation(cancellationToken))
        {
            await Assert
                .That(receivedMessage)
                .IsNotNull()
                .And.Member(x => x.Payload, x => x.IsSameReferenceAs(message));
            break; // We only expect one message
        }
    }

    [Test]
    public async Task Tell_Should_Throw_When_MailboxTookTooLongToBeInsert()
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox(1);
        var actorRef = new LocalActorReference(new Uri("/", UriKind.RelativeOrAbsolute), mailbox);

        // Act
        await mailbox.EnqueueAsync(new LocalTellMessage(new object()), CancellationToken.None);
        await Assert
            .That(() => actorRef.Tell(message, TimeSpan.FromSeconds(1)))
            .Throws<TimeoutException>();
    }

    [Test]
    [Timeout(5000)]
    public async Task TellAsync_Should_ReturnImmediately(CancellationToken cancellationToken)
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox();
        var actorRef = new LocalActorReference(new Uri("/", UriKind.RelativeOrAbsolute), mailbox);

        // Act
        await actorRef.TellAsync(message, cancellationToken);

        // Assert
        await foreach (var receivedMessage in mailbox.WithCancellation(cancellationToken))
        {
            await Assert
                .That(receivedMessage)
                .IsNotNull()
                .And.Member(x => x.Payload, x => x.IsSameReferenceAs(message));
            break; // We only expect one message
        }
    }

    [Test]
    public async Task TellAsync_Should_Throw_When_MailboxTookTooLongToBeInsert()
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox(1);
        var actorRef = new LocalActorReference(new Uri("/", UriKind.RelativeOrAbsolute), mailbox);

        // Act
        await mailbox.EnqueueAsync(new LocalTellMessage(new object()), CancellationToken.None);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert
            .That(() => actorRef.TellAsync(message, cts.Token).AsTask())
            .Throws<OperationCanceledException>();
    }

    [Test]
    [Timeout(5000)]
    public async Task Ask_Should_ReturnSuccessed(CancellationToken cancellationToken)
    {
        // Arrange
        var message = new object();
        var responseValue = new object();

        var mailbox = new ChannelMailbox();
        var actorRef = new LocalActorReference(new Uri("/", UriKind.RelativeOrAbsolute), mailbox);

        // Act
        var task = Task.Run(
            async () =>
            {
                await foreach (var receivedMessage in mailbox.WithCancellation(cancellationToken))
                {
                    await Assert
                        .That(receivedMessage)
                        .IsNotNull()
                        .And.Member(x => x.Payload, x => x.IsSameReferenceAs(message))
                        .And.IsTypeOf<IAskMessage>();
                    var askMessage = (IAskMessage)receivedMessage;
                    askMessage.SetResult(responseValue);
                    break; // We only expect one message
                }
            },
            cancellationToken
        );

        var response = actorRef.Ask<object, object>(message);

        await Assert.That(response).IsSameReferenceAs(responseValue);
        await task;
    }

    [Test]
    public async Task Ask_Should_Throw_When_MailboxTookTooLongToBeInsert()
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox(1);
        var actorRef = new LocalActorReference(new Uri("/", UriKind.RelativeOrAbsolute), mailbox);

        // Act
        await mailbox.EnqueueAsync(new LocalTellMessage(new object()), CancellationToken.None);
        await Assert
            .That(() => actorRef.Ask<object, object>(message, TimeSpan.FromSeconds(1)))
            .Throws<TimeoutException>();
    }

    [Test]
    [Timeout(5000)]
    public async Task AskAsync_Should_ReturnSuccessed(CancellationToken cancellationToken)
    {
        // Arrange
        var message = new object();
        var responseValue = new object();

        var mailbox = new ChannelMailbox();
        var actorRef = new LocalActorReference(new Uri("/", UriKind.RelativeOrAbsolute), mailbox);

        // Act
        var task = Task.Run(
            async () =>
            {
                await foreach (var receivedMessage in mailbox.WithCancellation(cancellationToken))
                {
                    await Assert
                        .That(receivedMessage)
                        .IsNotNull()
                        .And.Member(x => x.Payload, x => x.IsSameReferenceAs(message))
                        .And.IsTypeOf<IAskMessage>();
                    var askMessage = (IAskMessage)receivedMessage;
                    askMessage.SetResult(responseValue);
                    break; // We only expect one message
                }
            },
            cancellationToken
        );

        var response = await actorRef.AskAsync<object, object>(message, cancellationToken);

        await Assert.That(response).IsSameReferenceAs(responseValue);
        await task;
    }

    [Test]
    public async Task AskAsync_Should_Throw_When_MailboxTookTooLongToBeInsert()
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox(1);
        var actorRef = new LocalActorReference(new Uri("/", UriKind.RelativeOrAbsolute), mailbox);

        // Act
        await mailbox.EnqueueAsync(new LocalTellMessage(new object()), CancellationToken.None);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert
            .That(async () => await actorRef.AskAsync<object, object>(message, cts.Token).AsTask())
            .Throws<OperationCanceledException>();
    }
}
