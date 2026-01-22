using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Trupe.ActorReferences;
using Trupe.Mailboxes;
using Trupe.Messages;

namespace Trupe.Tests;

public class ActorProcessTest
{
    public record SimpleResponse(string Content);

    public record SimpleMessage(string Content)
    {
        public bool Processed { get; set; } = false;
    }

    public class SimpleTypedActor : Actor, IHandleActorMessage<SimpleMessage>
    {
        public ValueTask HandleAsync(
            SimpleMessage message,
            CancellationToken cancellationToken = default
        )
        {
            message.Processed = true;
            Context.Response = new SimpleResponse(message.Content);

            return ValueTask.CompletedTask;
        }
    }

    public class SimpleUntypedActor : Actor
    {
        public override ValueTask HandleAsync(
            object? message,
            CancellationToken cancellationToken = default
        )
        {
            if (message is SimpleMessage simpleMessage)
            {
                simpleMessage.Processed = true;
                Context.Response = new SimpleResponse(simpleMessage.Content);
                return ValueTask.CompletedTask;
            }

            return base.HandleAsync(message, cancellationToken);
        }
    }

    [Test]
    [SkipOnNativeAot]
    public async Task ProcessTellMessageWithTypedActor_Should_MarkMessageAsProcessed()
    {
        // Arrange
        var mailbox = new ChannelMailbox();
        var actor = new SimpleTypedActor
        {
            Context = new ActorContext(
                new LocalActorReference(new Uri("trupe://system/actor"), mailbox)
            ),
        };

        var actorProcess = new ActorProcess(actor, mailbox);
        actorProcess.Start();

        var message = new SimpleMessage("Hello, Actor!");

        // Act
        await mailbox.EnqueueAsync(new LocalTellMessage(message), CancellationToken.None);

        // Allow some time for the message to be processed
        await Task.Delay(100);

        // Assert
        await actorProcess.StopAsync();
        await Assert.That(message).Member(x => x.Processed, x => x.IsTrue());
    }

    [Test]
    [Timeout(5000)]
    [SkipOnNativeAot]
    public async Task ProcessAskMessageWithTypedActor_Should_MarkMessageAsProcessed(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();
        var actor = new SimpleTypedActor
        {
            Context = new ActorContext(
                new LocalActorReference(new Uri("trupe://system/actor"), mailbox)
            ),
        };

        var actorProcess = new ActorProcess(actor, mailbox);
        actorProcess.Start();

        var message = new SimpleMessage("Hello, Actor!");

        // Act
        var askMessage = new LocalAskMessage(message, cancellationToken);
        await mailbox.EnqueueAsync(askMessage, cancellationToken);

        // Allow some time for the message to be processed
        var response = await askMessage.AsTask();

        // Assert
        await actorProcess.StopAsync();
        await Assert.That(message).Member(x => x.Processed, x => x.IsTrue());
        await Assert
            .That(response)
            .IsTypeOf<SimpleResponse>()
            .And.Member(x => x.Content, x => x.IsEqualTo(message.Content));
    }

    [Test]
    public async Task ProcessTellMessageWithUntypedActor_Should_MarkMessageAsProcessed()
    {
        // Arrange
        var mailbox = new ChannelMailbox();
        var actor = new SimpleUntypedActor
        {
            Context = new ActorContext(
                new LocalActorReference(new Uri("trupe://system/actor"), mailbox)
            ),
        };

        var actorProcess = new ActorProcess(actor, mailbox);
        actorProcess.Start();

        var message = new SimpleMessage("Hello, Actor!");

        // Act
        await mailbox.EnqueueAsync(new LocalTellMessage(message), CancellationToken.None);

        // Allow some time for the message to be processed
        await Task.Delay(100);

        // Assert
        await actorProcess.StopAsync();
        await Assert.That(message).Member(x => x.Processed, x => x.IsTrue());
    }

    [Test]
    [Timeout(5000)]
    public async Task ProcessAskMessageWithUntypedActor_Should_MarkMessageAsProcessed(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();
        var actor = new SimpleUntypedActor
        {
            Context = new ActorContext(
                new LocalActorReference(new Uri("trupe://system/actor"), mailbox)
            ),
        };

        var actorProcess = new ActorProcess(actor, mailbox);
        actorProcess.Start();

        var message = new SimpleMessage("Hello, Actor!");

        // Act
        var askMessage = new LocalAskMessage(message, cancellationToken);
        await mailbox.EnqueueAsync(askMessage, cancellationToken);

        // Allow some time for the message to be processed
        var response = await askMessage.AsTask();

        // Assert
        await actorProcess.StopAsync();
        await Assert.That(message).Member(x => x.Processed, x => x.IsTrue());
        await Assert
            .That(response)
            .IsTypeOf<SimpleResponse>()
            .And.Member(x => x.Content, x => x.IsEqualTo(message.Content));
    }
}
