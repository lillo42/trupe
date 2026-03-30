using System.Threading.Tasks;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;

namespace Trupe.Pipelines.Middlewares;

public class AskMiddleware : IMiddleware
{
    public async ValueTask InvokeAsync(IPipelineContext context, NextDelegate next)
    {
        var actorContext = context.Metadata.GetRequiredMetadata<ActorContextMetadata>();
        if (context.Message is IAskMessage askMessage)
        {
            try
            {
                await next(context);
                askMessage.SetResult(actorContext.Context.Response);
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
