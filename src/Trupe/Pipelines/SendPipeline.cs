using System.Collections.Immutable;
using System.Threading.Tasks;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Executes a chain of send middlewares in order for outbound message processing.
/// </summary>
/// <param name="middlewares">The ordered list of send middlewares to execute.</param>
public class SendPipeline(ImmutableList<ISendMiddleware> middlewares) : ISendPipeline
{
    /// <summary>
    /// Executes the send pipeline by invoking all middlewares sequentially.
    /// </summary>
    /// <param name="context">The send pipeline context for the current message.</param>
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
