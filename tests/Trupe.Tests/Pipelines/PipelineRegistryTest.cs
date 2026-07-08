using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Pipelines;
using Trupe.Pipelines;

namespace Trupe.Tests.Pipelines;

public class PipelineRegistryTest
{
    [Test]
    public async Task GetMiddlewares_Should_ReturnTheCorrectMiddlawresBaseOnActorTypeAndMessage()
    {
        var opt = new PipelineOptions();
        opt.Middlewares.Add(
            new PipelineMiddlewareConfiguration { MiddlewareType = typeof(MiddlewareGlobal) }
        );

        opt.Middlewares.Add(
            new PipelineMiddlewareConfiguration
            {
                MiddlewareType = typeof(MiddlewareA),
                ActorType = typeof(ActorA),
            }
        );

        opt.Middlewares.Add(
            new PipelineMiddlewareConfiguration
            {
                MiddlewareType = typeof(MiddlewareB),
                ActorType = typeof(ActorB),
            }
        );

        opt.Middlewares.Add(
            new PipelineMiddlewareConfiguration
            {
                MiddlewareType = typeof(MiddlewareSomeRequest),
                ActorType = typeof(ActorA),
                MessageType = typeof(SomeRequest),
            }
        );

        opt.Middlewares.Add(
            new PipelineMiddlewareConfiguration
            {
                MiddlewareType = typeof(MiddlewareSomeRequest),
                ActorType = typeof(ActorB),
                MessageType = typeof(SomeRequest),
            }
        );

        var options = Substitute.For<IOptions<PipelineOptions>>();
        options.Value.Returns(opt);

        var lookup = new PipelineRegistry(options);
        var middleware = lookup.GetMiddlewares(typeof(ActorA), typeof(SomeRequest));
        await Assert
            .That(middleware)
            .Count()
            .IsEqualTo(3)
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareGlobal))
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareA))
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareSomeRequest));

        middleware = lookup.GetMiddlewares(typeof(ActorA), typeof(OtherRequest));
        await Assert
            .That(middleware)
            .Count()
            .IsEqualTo(2)
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareGlobal))
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareA));

        middleware = lookup.GetMiddlewares(typeof(ActorB), typeof(SomeRequest));
        await Assert
            .That(middleware)
            .Count()
            .IsEqualTo(3)
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareGlobal))
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareB))
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareSomeRequest));

        middleware = lookup.GetMiddlewares(typeof(ActorB), typeof(OtherRequest));
        await Assert
            .That(middleware)
            .Count()
            .IsEqualTo(2)
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareGlobal))
            .And.Contains(x => x.MiddlewareType == typeof(MiddlewareB));
    }

    private record SomeRequest;

    private record OtherRequest;

    private class ActorA : Actor;

    private class ActorB : Actor;

    private class MiddlewareA : IReceiveMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new System.NotImplementedException();
        }
    }

    private class MiddlewareB : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new System.NotImplementedException();
        }
    }

    private class MiddlewareGlobal : IReceiveMiddleware, ISendMiddleware
    {
        public ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
        {
            throw new System.NotImplementedException();
        }

        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new System.NotImplementedException();
        }
    }

    private class MiddlewareSomeRequest : ISendMiddleware
    {
        public ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            throw new System.NotImplementedException();
        }
    }
}
