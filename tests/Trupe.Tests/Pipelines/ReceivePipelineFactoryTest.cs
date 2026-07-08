using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Pipelines;

namespace Trupe.Tests.Pipelines;

public abstract class ReceivePipelineFactoryTest
{
    [Test]
    public async Task Create_Should_ReturnPipelineWithMiddleware()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(GlobalMiddleware)).Returns(new GlobalMiddleware());
        serviceProvider.GetService(typeof(MiddlewareA)).Returns(new MiddlewareA());
        serviceProvider.GetService(typeof(MiddlewareB)).Returns(new MiddlewareB());
        serviceProvider.GetService(typeof(MiddlewareC)).Returns(new MiddlewareC());
        serviceProvider.GetService(typeof(MiddlewareD)).Returns(new MiddlewareD());
        serviceProvider.GetService(typeof(MiddlewareE)).Returns(new MiddlewareE());

        var lookup = Substitute.For<IPipelineLookup>();

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

        var factory = new ReceivePipelineFactory(serviceProvider, lookup);

        await Assert
            .That(() => factory.Create(typeof(SomeActor), typeof(MessageA)))
            .ThrowsNothing();

        serviceProvider.Received(1).GetService(typeof(GlobalMiddleware));
        serviceProvider.Received(1).GetService(typeof(MiddlewareA));
        serviceProvider.DidNotReceive().GetService(typeof(MiddlewareB));
        serviceProvider.Received(1).GetService(typeof(MiddlewareC));
        serviceProvider.Received(1).GetService(typeof(MiddlewareD));
        serviceProvider.DidNotReceive().GetService(typeof(MiddlewareE));
    }

    public record MessageA;

    public abstract record MessageB;

    [GlobalMiddleware]
    public class SomeActor : Actor, IHandleActorMessage<MessageA>, IHandleActorMessage<MessageB>
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

    private class GlobalMiddleware : IReceiveMiddleware
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

    private class MiddlewareA : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
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

    private class MiddlewareB : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record MiddlewareCMetadata;

    private class MiddlewareC : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    private record MiddlewareDMetadata;

    private class MiddlewareD : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareEMetadata;

    private class MiddlewareE : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record SomeMetadata;

    public record OtherMetadata;
}
