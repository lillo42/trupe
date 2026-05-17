using System.Collections.Immutable;
using System.Threading.Tasks;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class ReceivePipeline(ImmutableList<IReceiveMiddleware> middlewares) : IReceivePipeline
{
    public ValueTask ExecuteAsync(IReceivePipelineContext context)
    {
        return InvokeAsync(context, 0);
    }

    private ValueTask InvokeAsync(IReceivePipelineContext contex, int next)
    {
        if (middlewares.Count == next)
        {
            return new ValueTask();
        }

        var middleware = middlewares[next];
        return middleware.InvokeAsync(contex, c => InvokeAsync(c, next + 1));
    }
}
