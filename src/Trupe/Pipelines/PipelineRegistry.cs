using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Trupe.Abstractions.Extensions;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Registry that resolves and caches middleware configurations for a given actor and message type pair.
/// </summary>
/// <param name="options">The pipeline options containing global middleware registrations.</param>
public class PipelineRegistry(IOptions<PipelineOptions> options) : IPipelineLookup
{
    private readonly ConcurrentDictionary<
        (Type, Type),
        IEnumerable<IMiddlewareConfiguration>
    > _cache = new();

    /// <summary>
    /// Returns the middleware configurations applicable to the specified actor and message type, using a cached lookup.
    /// </summary>
    /// <param name="actorType">The type of the actor.</param>
    /// <param name="messageType">The type of the message.</param>
    /// <returns>An enumerable of matching middleware configurations.</returns>
    public IEnumerable<IMiddlewareConfiguration> GetMiddlewares(Type actorType, Type messageType)
    {
        return _cache.GetOrAdd(
            (actorType, messageType),
            val =>
                options
                    .Value.Middlewares.Where(mw =>
                        (mw.ActorType == null || mw.ActorType.IsAssignableFrom(val.Item1))
                        && (mw.MessageType == null || mw.MessageType.IsAssignableFrom(val.Item2))
                    )
                    .Select(mw =>
                    {
                        var scope = MiddlewareScope.None;
                        if (mw.MiddlewareType.IsReceiveMiddleware())
                        {
                            scope |= MiddlewareScope.Receive;
                        }

                        if (mw.MiddlewareType.IsSendMiddleware())
                        {
                            scope |= MiddlewareScope.Send;
                        }

                        return new MiddlewareConfiguration(mw.Order, mw.Metadata, mw.MiddlewareType)
                        {
                            Scope = scope,
                        };
                    })
                    .ToList()
        );
    }
}
