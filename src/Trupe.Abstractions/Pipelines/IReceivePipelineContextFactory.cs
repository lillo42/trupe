using System.Threading;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Factory for creating <see cref="IReceivePipelineContext"/> instances.
/// </summary>
public interface IReceivePipelineContextFactory
{
    /// <summary>
    /// Creates a new receive pipeline context for the given actor, message, and metadata.
    /// </summary>
    /// <param name="actor">The actor that will process the message.</param>
    /// <param name="actorContext">The context of the receiving actor.</param>
    /// <param name="message">The message being delivered to the actor.</param>
    /// <param name="metadata">Additional metadata objects to attach to the pipeline context.</param>
    /// <param name="cancellationToken">A token to signal cancellation of the pipeline execution.</param>
    /// <returns>A new <see cref="IReceivePipelineContext"/> instance.</returns>
    IReceivePipelineContext Create(
        IActor actor,
        IActorContext actorContext,
        IMessage message,
        object?[] metadata,
        CancellationToken cancellationToken
    );
}
