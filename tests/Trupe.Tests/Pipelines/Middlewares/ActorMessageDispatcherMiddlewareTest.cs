using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.SystemMessages;
using Trupe.Messages;
using Trupe.Pipelines.Middlewares;

namespace Trupe.Tests.Pipelines.Middlewares;

public class ActorMessageDispatcherMiddlewareTest
{
    [Test]
    public async Task InvokeAsync_Should_CallInitializeAsync_When_ReceiveInitializeActor()
    {
        var context = Substitute.For<IReceivePipelineContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        context.Message.Returns(new TellMessage(new InitializeActor(), []));
        context.Items.Returns([]);

        var actor = Substitute.For<IActor>();
        context.Actor.Returns(actor);

        var middleware = new ActorMessageDispatcherMiddleware();

        await Assert
            .That(async () => await middleware.InvokeAsync(context, _ => new ValueTask()))
            .ThrowsNothing();

        await actor.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InvokeAsync_Should_CallAfterRestartAsync_When_ReceiveAfterRestartActor()
    {
        var context = Substitute.For<IReceivePipelineContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        context.Message.Returns(new TellMessage(new AfterRestartActor(), []));
        context.Items.Returns([]);

        var actor = Substitute.For<IActor>();
        context.Actor.Returns(actor);

        var middleware = new ActorMessageDispatcherMiddleware();

        await Assert
            .That(async () => await middleware.InvokeAsync(context, _ => new ValueTask()))
            .ThrowsNothing();

        await actor.Received(1).AfterRestartAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InvokeAsync_Should_CallTypedHandle()
    {
        var context = Substitute.For<IReceivePipelineContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        context.Message.Returns(new TellMessage(new SomeRequest(), []));
        context.Items.Returns([]);

        var actor = new SomeActor();
        context.Actor.Returns(actor);

        var middleware = new ActorMessageDispatcherMiddleware();

        await Assert
            .That(async () => await middleware.InvokeAsync(context, _ => new ValueTask()))
            .ThrowsNothing();

        await Assert.That(actor.Invoked).IsTrue();
    }

    [Test]
    public async Task InvokeAsync_Should_CallGenericHandle()
    {
        var context = Substitute.For<IReceivePipelineContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        context.Message.Returns(new TellMessage(new OtherRequest(), []));
        context.Items.Returns([]);

        var actor = new SomeActor();
        context.Actor.Returns(actor);

        var middleware = new ActorMessageDispatcherMiddleware();

        await Assert
            .That(async () => await middleware.InvokeAsync(context, _ => new ValueTask()))
            .ThrowsNothing();

        await Assert.That(actor.InvokedViaGeneric).IsTrue();
    }

    [Test]
    public async Task InvokeAsync_Should_CallGenericHandle_When_Forced()
    {
        var context = Substitute.For<IReceivePipelineContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        context.Message.Returns(new TellMessage(new OtherRequest(), []));
        context.Items.Returns(
            new Dictionary<string, object?>
            {
                [ActorMessageDispatcherMiddleware.ForceUseGenericHandle] = true,
            }
        );

        var actor = new SomeActor();
        context.Actor.Returns(actor);

        var middleware = new ActorMessageDispatcherMiddleware();

        await Assert
            .That(async () => await middleware.InvokeAsync(context, _ => new ValueTask()))
            .ThrowsNothing();

        await Assert.That(actor.InvokedViaGeneric).IsTrue();
    }

    public record SomeRequest;

    public record OtherRequest;

    public class SomeActor : Actor, IHandleActorMessage<SomeRequest>
    {
        public bool Invoked { get; private set; }
        public bool InvokedViaGeneric { get; private set; }

        public ValueTask HandleAsync(
            SomeRequest message,
            CancellationToken cancellationToken = default
        )
        {
            Invoked = true;
            return new ValueTask();
        }

        public override async ValueTask HandleAsync(
            object? message,
            CancellationToken cancellationToken = default
        )
        {
            if (message is OtherRequest)
            {
                InvokedViaGeneric = true;
                return;
            }
            else if (message is SomeRequest)
            {
                InvokedViaGeneric = true;
                return;
            }

            await base.HandleAsync(message, cancellationToken);
        }
    }
}
