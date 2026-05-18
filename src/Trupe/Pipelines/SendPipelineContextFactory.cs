using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Factory that creates <see cref="SendPipelineContext"/> instances by collecting metadata from middleware registrations and attributes.
/// </summary>
/// <param name="serviceProvider">The service provider to include in the created context.</param>
/// <param name="lookup">The pipeline lookup used to resolve registered middlewares.</param>
public class SendPipelineContextFactory(IServiceProvider serviceProvider, IPipelineLookup lookup)
    : AbstractPipelineContextFactory(lookup),
        ISendPipelineContextFactory
{
    /// <summary>
    /// Creates a new send pipeline context for the specified target reference, actor type, message, and metadata.
    /// </summary>
    /// <param name="reference">The actor reference the message is being sent to.</param>
    /// <param name="actorType">The type of the target actor.</param>
    /// <param name="message">The outbound message to send.</param>
    /// <param name="metadata">Additional metadata objects to include in the pipeline context.</param>
    /// <param name="cancellationToken">The cancellation token for this pipeline execution.</param>
    /// <returns>A new <see cref="ISendPipelineContext"/> instance.</returns>
    public ISendPipelineContext Create(
        IActorReference reference,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
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
