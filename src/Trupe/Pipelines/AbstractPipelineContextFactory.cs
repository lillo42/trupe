using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Base class for pipeline context factories that collects middleware metadata for a given actor and message type.
/// </summary>
/// <param name="lookup">The pipeline lookup used to resolve registered middlewares.</param>
public abstract class AbstractPipelineContextFactory(IPipelineLookup lookup)
{
    /// <summary>
    /// Collects and returns ordered metadata from middleware attributes and registrations for the specified actor, message type, and scope.
    /// </summary>
    /// <param name="actorType">The type of the actor processing the message.</param>
    /// <param name="messageType">The type of the message being processed.</param>
    /// <param name="scope">The middleware scope to filter by (send or receive).</param>
    /// <returns>A list of metadata objects from matching middlewares, ordered by priority.</returns>
    protected List<object?> GetMetadata(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
        Type messageType,
        MiddlewareScope scope
    )
    {
        var middlewareAttributes = actorType
            .GetCustomAttributes<MiddlewareAttribute>(true)
            .Cast<IMiddlewareConfiguration>()
            .ToList();

        middlewareAttributes.AddRange(lookup.GetMiddlewares(actorType, messageType));

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            middlewareAttributes.AddRange(GetHandleMessageTyped(actorType, messageType));
        }
        else
        {
            middlewareAttributes.AddRange(GetHandleMessage(actorType));
        }

        return middlewareAttributes
            .Where(x => x.Scope.HasFlag(scope))
            .OrderBy(x => x.Order)
            .Select(x => x.Metadata)
            .ToList();
    }

    private static IEnumerable<MiddlewareAttribute> GetHandleMessage(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType
    )
    {
        var method = actorType.GetMethod(
            nameof(IActor.HandleAsync),
            [typeof(object), typeof(CancellationToken)]
        );

        if (method != null)
        {
            return method.GetCustomAttributes<MiddlewareAttribute>(true);
        }

        return [];
    }

    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "The unfriendly method is not reachable with AOT"
    )]
    private static IEnumerable<MiddlewareAttribute> GetHandleMessageTyped(
        Type actorType,
        Type messageType
    )
    {
        var handleInterface = typeof(IHandleActorMessage<>).MakeGenericType(messageType);

        if (!handleInterface.IsAssignableFrom(actorType))
        {
            return GetHandleMessage(actorType);
        }

        var method = actorType.GetMethod(
            nameof(IActor.HandleAsync),
            [messageType, typeof(CancellationToken)]
        );

        if (method != null)
        {
            return method.GetCustomAttributes<MiddlewareAttribute>(true);
        }

        return GetHandleMessage(actorType);
    }
}
