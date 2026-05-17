using System;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Factory for creating <see cref="IReceivePipeline"/> instances configured for a specific actor and message type.
/// </summary>
public interface IReceivePipelineFactory
{
    /// <summary>
    /// Creates a receive pipeline composed of middleware matching the given actor and message types.
    /// </summary>
    /// <param name="actorType">The type of the actor that will receive messages.</param>
    /// <param name="messageType">The type of the message to be received.</param>
    /// <returns>A configured <see cref="IReceivePipeline"/> instance.</returns>
    IReceivePipeline Create(Type actorType, Type messageType);
}
