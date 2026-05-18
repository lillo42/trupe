using System.Threading.Tasks;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines.Middlewares;

/// <summary>
/// Receive middleware that handles ask-pattern messages by capturing the actor's response or exception and completing the caller's awaitable task.
/// </summary>
public class AskMiddleware : IReceiveMiddleware
{
    /// <summary>
    /// Processes the message and, if it is an ask message, sets the result or exception on the ask handle.
    /// </summary>
    /// <param name="context">The receive pipeline context containing the message and actor context.</param>
    /// <param name="next">The delegate to invoke the next middleware in the pipeline.</param>
    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        if (context.Message is IAskMessage askMessage)
        {
            try
            {
                await next(context);
                askMessage.SetResult(context.ActorContext.Response);
            }
            catch (AskException ex) // If the exception is an AskException, we can set it directly on the askMessage.
            {
                askMessage.SetException(ex);
            }
        }
        else
        {
            await next(context);
        }
    }
}
