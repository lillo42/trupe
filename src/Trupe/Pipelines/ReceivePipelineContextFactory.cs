using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Factory that creates <see cref="ReceivePipelineContext"/> instances by collecting metadata from middleware registrations and attributes.
/// </summary>
/// <param name="serviceProvider">The service provider to include in the created context.</param>
/// <param name="lookup">The pipeline lookup used to resolve registered middlewares.</param>
public class ReceivePipelineContextFactory(IServiceProvider serviceProvider, IPipelineLookup lookup)
    : AbstractPipelineContextFactory(lookup),
        IReceivePipelineContextFactory
{
    /// <summary>
    /// Creates a new receive pipeline context for the specified actor, message, and metadata.
    /// </summary>
    /// <param name="actor">The actor instance that will process the message.</param>
    /// <param name="actorContext">The actor's execution context.</param>
    /// <param name="message">The inbound message to process.</param>
    /// <param name="metadata">Additional metadata objects to include in the pipeline context.</param>
    /// <param name="cancellationToken">The cancellation token for this pipeline execution.</param>
    /// <returns>A new <see cref="IReceivePipelineContext"/> instance.</returns>
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
