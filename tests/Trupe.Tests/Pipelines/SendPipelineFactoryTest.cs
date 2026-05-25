using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Pipelines;

namespace Trupe.Tests.Pipelines;

public class SendPipelineFactoryTest
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
                    Scope = MiddlewareScope.Send,
                },
                new MiddlewareConfiguration(0, null, typeof(MiddlewareD))
                {
                    Metadata = new MiddlewareDMetadata(),
                    Scope = MiddlewareScope.Send,
                },
            ]);

        var factory = new SendPipelineFactory(serviceProvider, lookup);

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

    public record MessageA();

    public record MessageB();

    [GlobalMiddlawre]
    [GlobalReceiveMiddlawre]
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

    public class GlobalMiddleware : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record GlobalReceiveMiddlewareMetadata();

    public class GlobalReceiveMiddlawreAttribute() : MiddlewareAttribute(0)
    {
        public override object? Metadata => new GlobalReceiveMiddlewareMetadata();
        public override Type MiddlewareType => typeof(GlobalReceiveMiddleware);
    }

    public class GlobalReceiveMiddleware : IReceiveMiddleware
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

    public class MiddlewareA : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
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

    public class MiddlewareB : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareCMetadata();

    public class MiddlewareC : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareDMetadata();

    public class MiddlewareD : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record MiddlewareEMetadata();

    public class MiddlewareE : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new NotImplementedException();
        }
    }

    public record SomeMetadata();

    public record OtherMetadata();
}
