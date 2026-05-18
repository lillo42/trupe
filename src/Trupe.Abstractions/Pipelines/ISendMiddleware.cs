using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Defines a middleware component that intercepts outgoing messages in the send pipeline.
/// </summary>
public interface ISendMiddleware : IMiddleware
{
    /// <summary>
    /// Processes the send pipeline context and optionally delegates to the next middleware.
    /// </summary>
    /// <param name="context">The send pipeline context containing the message and target information.</param>
    /// <param name="next">The delegate to invoke the next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next);
}
