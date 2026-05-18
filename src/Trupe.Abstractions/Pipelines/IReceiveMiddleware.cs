using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Defines a middleware component that intercepts incoming messages in the receive pipeline.
/// </summary>
public interface IReceiveMiddleware : IMiddleware
{
    /// <summary>
    /// Processes the receive pipeline context and optionally delegates to the next middleware.
    /// </summary>
    /// <param name="context">The receive pipeline context containing the message and actor information.</param>
    /// <param name="next">The delegate to invoke the next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next);
}
