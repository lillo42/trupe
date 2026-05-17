using System.Collections.Immutable;
using System.Threading.Tasks;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Executes a chain of receive middlewares in order for inbound message processing.
/// </summary>
/// <param name="middlewares">The ordered list of receive middlewares to execute.</param>
public class ReceivePipeline(ImmutableList<IReceiveMiddleware> middlewares) : IReceivePipeline
{
    /// <summary>
    /// Executes the receive pipeline by invoking all middlewares sequentially.
    /// </summary>
    /// <param name="context">The receive pipeline context for the current message.</param>
    public ValueTask ExecuteAsync(IReceivePipelineContext context)
    {
        return InvokeAsync(context, 0);
    }

    private ValueTask InvokeAsync(IReceivePipelineContext context, int next)
    {
        if (middlewares.Count == next)
        {
            return new ValueTask();
        }

        var middleware = middlewares[next];
        return middleware.InvokeAsync(context, c => InvokeAsync(c, next + 1));
    }
}
