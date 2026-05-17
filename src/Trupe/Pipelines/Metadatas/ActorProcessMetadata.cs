namespace Trupe.Pipelines.Metadatas;

/// <summary>
/// Pipeline metadata that carries a reference to the <see cref="ActorProcess"/> managing the current actor.
/// </summary>
/// <param name="Process">The actor process instance associated with the current pipeline execution.</param>
public record ActorProcessMetadata(ActorProcess Process);
