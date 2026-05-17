using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class ReceivePipelineContextFactory(IServiceProvider serviceProvider, IPipelineLookup lookup)
    : AbstractPipelineContextFactory(lookup),
        IReceivePipelineContextFactory
{
    public IReceivePipelineContext Create(
        IActor actor,
        IActorContext actorContext,
        IMessage message,
        object?[] metadata,
        CancellationToken cancellationToken
    )
    {
        var finalMetadata = GetMetadata(
            actor.GetType(),
            message.Payload.GetType(),
            MiddlewareScope.Receive
        );
        finalMetadata.AddRange(metadata);

        return new ReceivePipelineContext(
            actor,
            actorContext,
            message,
            serviceProvider,
            new PipelineMetadataCollection(finalMetadata.Where(x => x != null).ToImmutableList()!),
            cancellationToken
        );
    }
}
