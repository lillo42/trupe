using System.Collections.Immutable;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions.Pipelines;
using Trupe.Pipelines;

namespace Trupe.Tests.Pipelines;

public class ReceivePipelineTest
{
    [Test]
    public async Task ExecuteAsync_Should_CallAllMiddlewares()
    {
        var middlewares = ImmutableList<IReceiveMiddleware>
            .Empty.Add(new SomeMiddleware())
            .Add(new SomeMiddleware())
            .Add(new SomeMiddleware());

        var context = Substitute.For<IReceivePipelineContext>();

        var pipeline = new ReceivePipeline(middlewares);

        await Assert.That(async () => await pipeline.ExecuteAsync(context)).ThrowsNothing();

        await Assert
            .That(middlewares)
            .All(x => x is SomeMiddleware someMiddleware && someMiddleware.Invoked);
    }

    private class SomeMiddleware : IReceiveMiddleware
    {
        public bool Invoked { get; private set; }

        public async ValueTask InvokeAsync(
            IReceivePipelineContext context,
            NextReceiveDelegate next
        )
        {
            Invoked = true;
            await next(context);
        }
    }
}
