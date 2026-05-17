using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Messages;
using Trupe.Extensions;
using Trupe.Mailboxes;
using Trupe.Messages;

namespace Trupe.Tests.ActorReferences;

public class ActorReferenceTest
{
    private static ActorReference CreateActorReference(ChannelMailbox mailbox)
    {
        var provider = new ServiceCollection()
            .AddTrupe(_ => { })
            .BuildServiceProvider();

        return new ActorReference(typeof(object), provider, mailbox);
    }

    [Test]
    [Timeout(5000)]
    public async Task Tell_Should_ReturnImmediately(CancellationToken cancellationToken)
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox();
        var actorRef = CreateActorReference(mailbox);

        // Act
        actorRef.Tell(message);

        // Assert
        var receivedMessage = await mailbox.DequeueAsync(cancellationToken);
        await Assert
            .That(receivedMessage)
            .IsNotNull()
            .And.Member(x => x!.Payload, x => x.IsSameReferenceAs(message));
    }

    [Test]
    public async Task Tell_Should_Throw_When_MailboxTookTooLongToBeInsert()
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox(1);
        var actorRef = CreateActorReference(mailbox);

        // Act
        await mailbox.EnqueueAsync(new TellMessage(new object(), []), CancellationToken.None);
        await Assert
            .That(() => actorRef.Tell(message, TimeSpan.FromSeconds(1)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [Timeout(5000)]
    public async Task TellAsync_Should_ReturnImmediately(CancellationToken cancellationToken)
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox();
        var actorRef = CreateActorReference(mailbox);

        // Act
        await actorRef.TellAsync(message, cancellationToken);

        // Assert
        var receivedMessage = await mailbox.DequeueAsync(cancellationToken);
        await Assert
            .That(receivedMessage)
            .IsNotNull()
            .And.Member(x => x!.Payload, x => x.IsSameReferenceAs(message));
    }

    [Test]
    public async Task TellAsync_Should_Throw_When_MailboxTookTooLongToBeInsert()
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox(1);
        var actorRef = CreateActorReference(mailbox);

        // Act
        await mailbox.EnqueueAsync(new TellMessage(new object(), []), CancellationToken.None);

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
        var actorRef = CreateActorReference(mailbox);

        // Act
        var task = Task.Run(
            async () =>
            {
                var receivedMessage = await mailbox.DequeueAsync(cancellationToken);
                await Assert
                    .That(receivedMessage)
                    .IsNotNull()
                    .And.Member(x => x!.Payload, x => x.IsSameReferenceAs(message))
                    .And.IsTypeOf<IAskMessage>();
                var askMessage = (IAskMessage)receivedMessage!;
                askMessage.SetResult(responseValue);
            },
            cancellationToken
        );

        var response = actorRef.Ask<object>(message);

        await Assert.That(response).IsSameReferenceAs(responseValue);
        await task;
    }

    [Test]
    public async Task Ask_Should_Throw_When_MailboxTookTooLongToBeInsert()
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox(1);
        var actorRef = CreateActorReference(mailbox);

        // Act
        await mailbox.EnqueueAsync(new TellMessage(new object(), []), CancellationToken.None);
        await Assert
            .That(() => actorRef.Ask<object>(message, TimeSpan.FromSeconds(1)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [Timeout(5000)]
    public async Task AskAsync_Should_ReturnSuccessed(CancellationToken cancellationToken)
    {
        // Arrange
        var message = new object();
        var responseValue = new object();

        var mailbox = new ChannelMailbox();
        var actorRef = CreateActorReference(mailbox);

        // Act
        var task = Task.Run(
            async () =>
            {
                var receivedMessage = await mailbox.DequeueAsync(cancellationToken);
                await Assert
                    .That(receivedMessage)
                    .IsNotNull()
                    .And.Member(x => x!.Payload, x => x.IsSameReferenceAs(message))
                    .And.IsTypeOf<IAskMessage>();
                var askMessage = (IAskMessage)receivedMessage!;
                askMessage.SetResult(responseValue);
            },
            cancellationToken
        );

        var response = await actorRef.AskAsync<object>(message, cancellationToken);

        await Assert.That(response).IsSameReferenceAs(responseValue);
        await task;
    }

    [Test]
    public async Task AskAsync_Should_Throw_When_MailboxTookTooLongToBeInsert()
    {
        // Arrange
        var message = new object();

        var mailbox = new ChannelMailbox(1);
        var actorRef = CreateActorReference(mailbox);

        // Act
        await mailbox.EnqueueAsync(new TellMessage(new object(), []), CancellationToken.None);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert
            .That(async () => await actorRef.AskAsync<object>(message, cts.Token))
            .Throws<OperationCanceledException>();
    }
}
