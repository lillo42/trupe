using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Messages;
using Trupe.Pipelines;

namespace Trupe.Tests.Pipelines;

public class ReceivePipelineContextFactoryTest
{
    [Test]
    public async Task Create()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        var lookup = Substitute.For<IPipelineLookup>();

        var contextFactory = new ReceivePipelineContextFactory(serviceProvider, lookup);

        var actor = new SomeActor();
        var actorContext = Substitute.For<IActorContext>();
        var message = new TellMessage(new MessageA(), []);

        lookup
            .GetMiddlewares(typeof(SomeActor), typeof(MessageA))
            .Returns([
                new MiddlewareConfiguration(0, null, typeof(MiddlewareC))
                {
                    Metadata = new MiddlewareCMetadata(),
                    Scope = MiddlewareScope.Receive,
                },
                new MiddlewareConfiguration(0, null, typeof(MiddlewareD))
                {
                    Metadata = new MiddlewareDMetadata(),
                    Scope = MiddlewareScope.Receive,
                },
            ]);

        var pipelineContext = contextFactory.Create(
            actor,
            actorContext,
            message,
            [new SomeMetadata()],
            CancellationToken.None
        );

        using (Assert.Multiple())
        {
            await Assert.That(pipelineContext.Actor).IsEqualTo(actor);
            await Assert.That(pipelineContext.ActorContext).IsEqualTo(actorContext);
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
                    Scope = MiddlewareScope.Receive,
                },
                new MiddlewareConfiguration(0, null, typeof(MiddlewareE))
                {
                    Metadata = new MiddlewareEMetadata(),
                    Scope = MiddlewareScope.Receive,
                },
            ]);

        pipelineContext = contextFactory.Create(
            actor,
            actorContext,
            message,
            [new OtherMetadata()],
            CancellationToken.None
        );

        using (Assert.Multiple())
        {
            await Assert.That(pipelineContext.Actor).IsEqualTo(actor);
            await Assert.That(pipelineContext.ActorContext).IsEqualTo(actorContext);
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

    public record MessageA();

    public record MessageB();

    [GlobalMiddlawre]
    public class SomeActor : Actor, IHandleActorMessage<MessageA>, IHandleActorMessage<MessageB>
    {
        [MiddlawreA]
        public ValueTask HandleAsync(
            MessageA message,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }

        [MiddlawreB]
        public ValueTask HandleAsync(
            MessageB message,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }
    }

    public record GlobalMiddlewareMetadata();

    public class GlobalMiddlawreAttribute() : MiddlewareAttribute(0)
    {
        public override object? Metadata => new GlobalMiddlewareMetadata();
        public override Type MiddlewareType => typeof(GlobalMiddleware);
    }

    public class GlobalMiddleware : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareAMetadata();

    public class MiddlawreAAttribute() : MiddlewareAttribute(1)
    {
        public override object? Metadata => new MiddlewareAMetadata();
        public override Type MiddlewareType => typeof(MiddlewareA);
    }

    public class MiddlewareA : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareBMetadata();

    public class MiddlawreBAttribute() : MiddlewareAttribute(1)
    {
        public override object? Metadata => new MiddlewareBMetadata();
        public override Type MiddlewareType => typeof(MiddlewareB);
    }

    public class MiddlewareB : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareCMetadata();

    public class MiddlewareC : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareDMetadata();

    public class MiddlewareD : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareEMetadata();

    public class MiddlewareE : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record SomeMetadata();

    public record OtherMetadata();
}
