using System;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Provides context for the send pipeline, including the target actor reference and type.
/// </summary>
public interface ISendPipelineContext : IPipelineContext
{
    /// <summary>
    /// Gets the type of the actor that the message is being sent to.
    /// </summary>
    Type ActorType { get; }

    /// <summary>
    /// Gets the reference to the target actor that will receive the message.
    /// </summary>
    IActorReference Target { get; }
}
