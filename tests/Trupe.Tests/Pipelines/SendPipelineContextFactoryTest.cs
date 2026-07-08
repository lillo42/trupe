using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Messages;
using Trupe.Pipelines;

namespace Trupe.Tests.Pipelines;

public class SendPipelineContextFactoryTest
{
    [Test]
    public async Task Create_Should_CreateContextWithCorrectMetadata()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        var lookup = Substitute.For<IPipelineLookup>();

        var contextFactory = new SendPipelineContextFactory(serviceProvider, lookup);

        var @ref = Substitute.For<IActorReference>();
        var actorType = typeof(SomeActor);
        var message = new TellMessage(new MessageA(), []);

        lookup
            .GetMiddlewares(typeof(SomeActor), typeof(MessageA))
            .Returns([
                new MiddlewareConfiguration(0, null, typeof(MiddlewareC))
                {
                    Metadata = new MiddlewareCMetadata(),
                    Scope = MiddlewareScope.Send,
                },
                new MiddlewareConfiguration(0, null, typeof(MiddlewareD))
                {
                    Metadata = new MiddlewareDMetadata(),
                    Scope = MiddlewareScope.Send,
                },
            ]);

        var pipelineContext = contextFactory.Create(
            @ref,
            actorType,
            message,
            [new SomeMetadata()],
            CancellationToken.None
        );

        using (Assert.Multiple())
        {
            await Assert.That(pipelineContext.Target).IsEqualTo(@ref);
            await Assert.That(pipelineContext.ActorType).IsEqualTo(actorType);
            await Assert.That(pipelineContext.Message).IsEqualTo(message);
            await Assert.That(pipelineContext.ServiceProvider).IsEqualTo(serviceProvider);
            await Assert
                .That(pipelineContext.Metadata)
                .Count()
                .IsEqualTo(5)
                .And.Contains(x => x is GlobalMiddlewareMetadata)
                .And.Contains(x => x is MiddlewareAMetadata)
                .And.Contains(x => x is MiddlewareCMetadata)
                .And.Contains(x => x is MiddlewareDMetadata)
                .And.Contains(x => x is SomeMetadata);
        }

        message = new TellMessage(new MessageB(), []);

        lookup
            .GetMiddlewares(typeof(SomeActor), typeof(MessageB))
            .Returns([
                new MiddlewareConfiguration(0, null, typeof(MiddlewareC))
                {
                    Metadata = new MiddlewareCMetadata(),
                    Scope = MiddlewareScope.Send,
                },
                new MiddlewareConfiguration(0, null, typeof(MiddlewareE))
                {
                    Metadata = new MiddlewareEMetadata(),
                    Scope = MiddlewareScope.Send,
                },
            ]);

        pipelineContext = contextFactory.Create(
            @ref,
            actorType,
            message,
            [new OtherMetadata()],
            CancellationToken.None
        );

        using (Assert.Multiple())
        {
            await Assert.That(pipelineContext.Target).IsEqualTo(@ref);
            await Assert.That(pipelineContext.ActorType).IsEqualTo(actorType);
            await Assert.That(pipelineContext.Message).IsEqualTo(message);
            await Assert.That(pipelineContext.ServiceProvider).IsEqualTo(serviceProvider);
            await Assert
                .That(pipelineContext.Metadata)
                .Count()
                .IsEqualTo(5)
                .And.Contains(x => x is GlobalMiddlewareMetadata)
                .And.Contains(x => x is MiddlewareBMetadata)
                .And.Contains(x => x is MiddlewareCMetadata)
                .And.Contains(x => x is MiddlewareEMetadata)
                .And.Contains(x => x is OtherMetadata);
        }
    }

    private record MessageA;

    private record MessageB;

    [GlobalMiddleware]
    [GlobalReceiveMiddleware]
    private class SomeActor : Actor, IHandleActorMessage<MessageA>, IHandleActorMessage<MessageB>
    {
        [MiddlewareA]
        public ValueTask HandleAsync(
            MessageA message,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }

        [MiddlewareB]
        public ValueTask HandleAsync(
            MessageB message,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }
    }

    private record GlobalMiddlewareMetadata;

    private class GlobalMiddlewareAttribute() : MiddlewareAttribute(0)
    {
        public override object Metadata => new GlobalMiddlewareMetadata();
        public override Type MiddlewareType => typeof(GlobalMiddleware);
    }

    private class GlobalMiddleware : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record GlobalReceiveMiddlewareMetadata;

    private class GlobalReceiveMiddlewareAttribute() : MiddlewareAttribute(0)
    {
        public override object Metadata => new GlobalReceiveMiddlewareMetadata();
        public override Type MiddlewareType => typeof(GlobalReceiveMiddleware);
    }

    private class GlobalReceiveMiddleware : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record MiddlewareAMetadata;

    private class MiddlewareAAttribute() : MiddlewareAttribute(1)
    {
        public override object Metadata => new MiddlewareAMetadata();
        public override Type MiddlewareType => typeof(MiddlewareA);
    }

    private class MiddlewareA : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record MiddlewareBMetadata;

    private class MiddlewareBAttribute() : MiddlewareAttribute(1)
    {
        public override object Metadata => new MiddlewareBMetadata();
        public override Type MiddlewareType => typeof(MiddlewareB);
    }

    private class MiddlewareB : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record MiddlewareCMetadata;

    private class MiddlewareC : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record MiddlewareDMetadata;

    private class MiddlewareD : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record MiddlewareEMetadata;

    private class MiddlewareE : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record SomeMetadata;

    private record OtherMetadata;
}
