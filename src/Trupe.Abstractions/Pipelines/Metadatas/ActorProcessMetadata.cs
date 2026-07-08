namespace Trupe.Abstractions.Pipelines.Metadatas;

/// <summary>
/// Pipeline metadata that provides access to the actor process handling the current message.
/// </summary>
/// <param name="Process">The actor process associated with the current pipeline execution.</param>
public record ActorProcessMetadata(IActorProcess Process);
