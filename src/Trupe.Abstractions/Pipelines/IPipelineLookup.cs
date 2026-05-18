using System;
using System.Collections.Generic;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Resolves the set of middleware configurations applicable to a given actor and message type combination.
/// </summary>
public interface IPipelineLookup
{
    /// <summary>
    /// Returns the middleware configurations that should be applied for the specified actor and message types.
    /// </summary>
    /// <param name="actorType">The type of the actor processing the message.</param>
    /// <param name="messageType">The type of the message being processed.</param>
    /// <returns>An enumerable of matching middleware configurations.</returns>
    IEnumerable<IMiddlewareConfiguration> GetMiddlewares(Type actorType, Type messageType);
}
