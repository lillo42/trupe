using System;
using System.Diagnostics.CodeAnalysis;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Factory for creating <see cref="ISendPipeline"/> instances configured for a specific actor and message type.
/// </summary>
public interface ISendPipelineFactory
{
    /// <summary>
    /// Creates a send pipeline composed of middleware matching the given actor and message types.
    /// </summary>
    /// <param name="actorType">The type of the target actor.</param>
    /// <param name="messageType">The type of the message to be sent.</param>
    /// <returns>A configured <see cref="ISendPipeline"/> instance.</returns>
    ISendPipeline Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
        Type messageType);
}
