namespace Trupe.Abstractions.Pipelines.Metadatas;

/// <summary>
/// Pipeline metadata that carries the <see cref="IActorContext"/> of the current actor.
/// </summary>
/// <param name="Context">The actor context associated with the pipeline execution.</param>
public record ActorContextMetadata(IActorContext Context);
