using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Pipelines;
using Trupe.Messages;
using Trupe.Pipelines.Middlewares;

namespace Trupe.Tests.Pipelines.Middlewares;

public class AskMiddlewareTest
{
    [Test]
    public async Task InvokeAsync_Should_NotCathException_When_MessageIsTell()
    {
        var middleware = new AskMiddleware();

        var message = new TellMessage(new object(), [], CancellationToken.None);
        var context = Substitute.For<IReceivePipelineContext>();
        context.Message.Returns(message);

        await Assert
            .That(async () =>
                await middleware.InvokeAsync(context, _ => throw new InvalidOperationException())
            )
            .Throws<InvalidOperationException>();

        await Assert
            .That(async () =>
                await middleware.InvokeAsync(context, _ => throw new SomeAskException())
            )
            .Throws<SomeAskException>();
    }

    [Test]
    public async Task InvokeAsync_Should_NotCathException_When_MessageIsAskAndNonAskException()
    {
        var middleware = new AskMiddleware();

        var message = new AskMessage(new object(), [], CancellationToken.None);
        var context = Substitute.For<IReceivePipelineContext>();
        context.Message.Returns(message);

        await Assert
            .That(async () =>
                await middleware.InvokeAsync(context, _ => throw new InvalidOperationException())
            )
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task InvokeAsync_Should_CathException_When_MessageIsAskAndAskException()
    {
        var middleware = new AskMiddleware();

        var message = new AskMessage(new object(), [], CancellationToken.None);
        var context = Substitute.For<IReceivePipelineContext>();
        context.Message.Returns(message);

        await Assert
            .That(async () =>
                await middleware.InvokeAsync(context, _ => throw new SomeAskException())
            )
            .ThrowsNothing();

        await Assert.That(message.AsTask).Throws<SomeAskException>();
    }

    [Test]
    public async Task InvokeAsync_Should_SetResult()
    {
        var middleware = new AskMiddleware();

        var message = new AskMessage(new object(), [], CancellationToken.None);
        var context = Substitute.For<IReceivePipelineContext>();
        context.Message.Returns(message);

        var response = new object();
        var actorContext = Substitute.For<IActorContext>();
        actorContext.Response.Returns(response);
        context.ActorContext.Returns(actorContext);

        await Assert
            .That(async () => await middleware.InvokeAsync(context, _ => new ValueTask()))
            .ThrowsNothing();

        await Assert.That(message.AsTask).ThrowsNothing().And.IsEqualTo(response);
    }

    public class SomeAskException : AskException { }
}
