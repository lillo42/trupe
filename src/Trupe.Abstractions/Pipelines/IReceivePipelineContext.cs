namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Provides context for the receive pipeline, including the target actor and its context.
/// </summary>
public interface IReceivePipelineContext : IPipelineContext
{
    /// <summary>
    /// Gets the actor instance that will process the received message.
    /// </summary>
    IActor Actor { get; }

    /// <summary>
    /// Gets the actor context associated with the receiving actor.
    /// </summary>
    IActorContext ActorContext { get; }
}
