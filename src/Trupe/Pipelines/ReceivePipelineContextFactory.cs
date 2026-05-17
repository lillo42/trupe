using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class ReceivePipelineContextFactory(IServiceProvider serviceProvider, IPipelineLookup lookup)
    : AbstractPipelineContextFactory(serviceProvider, lookup),
        IReceivePipelineContextFactory
{
    private static readonly ConcurrentDictionary<(Type, Type), ImmutableList<object?>> _cache = [];

    public IReceivePipelineContext Create(
        IActor actor,
        IActorContext actorContext,
        IMessage message,
        object?[] metadata,
        CancellationToken cancellationToken
    )
    {
        var finalMetadata = _cache
            .GetOrAdd(
                (actor.GetType(), message.Payload.GetType()),
                val => GetMetadata(val.Item1, val.Item2, MiddlewareScope.Receive)
            )
            .ToList();

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
