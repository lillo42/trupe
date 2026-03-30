using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class PipelineFactory(IServiceProvider provider, IPipelineLookup lookup) : IPipelineFactory
{
    private static readonly ConcurrentDictionary<(Type, Type), ImmutableList<Type>> _cache = [];

    public IPipeline Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
        Type messageType
    )
    {
        var types = _cache.GetOrAdd(
            (actorType, messageType),
            val => GetMiddlewareTypes(val.Item1, val.Item2)
        );

        return new Pipeline(
            types.Select(type => (IMiddleware)provider.GetRequiredService(type)).ToImmutableList()
        );
    }

    private ImmutableList<Type> GetMiddlewareTypes(Type actorType, Type messageType)
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
            .OrderBy(x => x.Order)
            .Select(x => x.MiddlewareType)
            .ToImmutableList();
    }

    private static IEnumerable<MiddlewareAttribute> GetHandleMessage(Type actorType)
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
