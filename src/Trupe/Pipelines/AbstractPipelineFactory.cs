using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Base class for pipeline factories that resolves middleware types for a given actor and message type.
/// </summary>
/// <param name="provider">The service provider used to resolve middleware instances.</param>
/// <param name="lookup">The pipeline lookup used to resolve registered middlewares.</param>
public abstract class AbstractPipelineFactory(IServiceProvider provider, IPipelineLookup lookup)
{
    protected virtual IServiceProvider ServiceProvider { get; } = provider;

    /// <summary>
    /// Gets the middleware scope that this factory targets (send or receive).
    /// </summary>
    protected abstract MiddlewareScope Scope { get; }

    /// <summary>
    /// Resolves and returns an ordered list of middleware types applicable to the specified actor and message type.
    /// </summary>
    /// <param name="actorType">The type of the actor processing the message.</param>
    /// <param name="messageType">The type of the message being processed.</param>
    /// <returns>An immutable list of middleware types, ordered by priority.</returns>
    protected ImmutableList<Type> GetMiddlewareTypes(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
        Type messageType
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
            .Where(x => x.Scope.HasFlag(Scope))
            .OrderBy(x => x.Order)
            .Select(x => x.MiddlewareType)
            .ToImmutableList();
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
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
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
