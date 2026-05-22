using System.Threading.Tasks;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;

namespace Trupe.Pipelines.Middlewares;

/// <summary>
/// Send middleware that enqueues the outgoing message into the target actor's mailbox.
/// </summary>
public class ActorProcessDispatcherMiddleware : ISendMiddleware
{
    /// <summary>
    /// Enqueues the message into the mailbox obtained from the pipeline metadata.
    /// </summary>
    /// <param name="context">The send pipeline context containing the message and mailbox metadata.</param>
    /// <param name="next">The delegate to invoke the next middleware in the pipeline (not called; this is a terminal middleware).</param>
    public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
    {
        var process = context.Metadata.GetRequiredMetadata<ActorProcessMetadata>().Process;
        await process.Mailbox.EnqueueAsync(context.Message, context.CancellationToken);
    }
}
