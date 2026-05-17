using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Pipelines.Metadatas;

/// <summary>
/// Pipeline metadata that carries the <see cref="IMessage"/> being processed.
/// </summary>
/// <param name="Message">The message being processed in the pipeline.</param>
public record ActorMessageMetadata(IMessage Message);
