using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class SendPipelineContextFactory(IServiceProvider serviceProvider, IPipelineLookup lookup)
    : AbstractPipelineContextFactory(serviceProvider, lookup),
        ISendPipelineContextFactory
{
    public ISendPipelineContext Create(
        IActorReference reference,
        Type actorType,
        IMessage message,
        object?[] metadata,
        CancellationToken cancellationToken
    )
    {
        var finalMetadata = GetMetadata(actorType, message.Payload.GetType(), MiddlewareScope.Send);
        finalMetadata.AddRange(metadata);

        return new SendPipelineContext(
            reference,
            actorType,
            message,
            serviceProvider,
            new PipelineMetadataCollection(finalMetadata.Where(x => x != null).ToImmutableList()!),
            cancellationToken
        );
    }
}
