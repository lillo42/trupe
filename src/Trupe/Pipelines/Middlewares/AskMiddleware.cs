using System.Threading.Tasks;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines.Middlewares;

public class AskMiddleware : IReceiveMiddleware
{
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
