using System.Collections.Immutable;
using System.Threading.Tasks;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class Pipeline(ImmutableList<IMiddleware> middlewares) : IPipeline
{
    public ValueTask ExecuteAsync(IPipelineContext contex)
    {
        return InvokeAsync(contex, 0);
    }

    private ValueTask InvokeAsync(IPipelineContext contex, int next)
    {
        if (middlewares.Count == next)
        {
            return new ValueTask();
        }

        var middleware = middlewares[next];
        return middleware.InvokeAsync(contex, c => InvokeAsync(c, next + 1));
    }
}
