using System.Collections.Immutable;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions.Pipelines;
using Trupe.Pipelines;

namespace Trupe.Tests.Pipelines;

public class SendPipelineTest
{
    [Test]
    public async Task ExecuteAsync_Should_CallAllMiddlewares()
    {
        var middlewares = ImmutableList<ISendMiddleware>
            .Empty.Add(new SomeMiddleware())
            .Add(new SomeMiddleware())
            .Add(new SomeMiddleware());

        var context = Substitute.For<ISendPipelineContext>();

        var pipeline = new SendPipeline(middlewares);

        await Assert.That(async () => await pipeline.ExecuteAsync(context)).ThrowsNothing();

        await Assert
            .That(middlewares)
            .All(x => x is SomeMiddleware someMiddleware && someMiddleware.Invoked == true);
    }

    public class SomeMiddleware : ISendMiddleware
    {
        public bool Invoked { get; private set; }

        public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            Invoked = true;
            await next(context);
        }
    }
}
