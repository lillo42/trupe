using System;
using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<(Type, Type), ImmutableList<object?>> _cache = [];

    public ISendPipelineContext Create(
        IActorReference reference,
        Type actorType,
        IMessage message,
        object?[] metadata,
        CancellationToken cancellationToken
    )
    {
        var finalMetadata = _cache
            .GetOrAdd(
                (actorType, message.Payload.GetType()),
                val => GetMetadata(val.Item1, val.Item2, MiddlewareScope.Send)
            )
            .ToList();

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
