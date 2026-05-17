namespace Trupe.Abstractions.Pipelines.Metadatas;

/// <summary>
/// Pipeline metadata that carries the <see cref="IActor"/> instance involved in the pipeline execution.
/// </summary>
/// <param name="Actor">The actor instance associated with the pipeline execution.</param>
public record ActorMetadata(IActor Actor);
