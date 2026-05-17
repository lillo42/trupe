using System.Threading.Tasks;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;

namespace Trupe.Pipelines.Middlewares;

public class MailboxDispatcherMiddleware : ISendMiddleware
{
    public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
    {
        var mailbox = context.Metadata.GetRequiredMetadata<MailboxMetadata>();
        await mailbox.Mailbox.EnqueueAsync(context.Message, context.CancellationToken);
    }
}
