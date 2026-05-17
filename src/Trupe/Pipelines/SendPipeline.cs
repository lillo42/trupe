using System.Collections.Immutable;
using System.Threading.Tasks;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class SendPipeline(ImmutableList<ISendMiddleware> middlewares) : ISendPipeline
{
    public ValueTask ExecuteAsync(ISendPipelineContext context)
    {
        return InvokeAsync(context, 0);
    }

    private ValueTask InvokeAsync(ISendPipelineContext context, int next)
    {
        if (middlewares.Count == next)
        {
            return new ValueTask();
        }

        var middleware = middlewares[next];
        return middleware.InvokeAsync(context, c => InvokeAsync(c, next + 1));
    }
}
