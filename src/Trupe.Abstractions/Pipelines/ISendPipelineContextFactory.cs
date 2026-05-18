using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Factory for creating <see cref="ISendPipelineContext"/> instances.
/// </summary>
public interface ISendPipelineContextFactory
{
    /// <summary>
    /// Creates a new send pipeline context for the given target, message, and metadata.
    /// </summary>
    /// <param name="reference">The reference to the target actor.</param>
    /// <param name="actorType">The type of the target actor.</param>
    /// <param name="message">The message being sent.</param>
    /// <param name="metadata">Additional metadata objects to attach to the pipeline context.</param>
    /// <param name="cancellationToken">A token to signal cancellation of the pipeline execution.</param>
    /// <returns>A new <see cref="ISendPipelineContext"/> instance.</returns>
    ISendPipelineContext Create(
        IActorReference reference,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
        IMessage message,
        object?[] metadata,
        CancellationToken cancellationToken
    );
}
