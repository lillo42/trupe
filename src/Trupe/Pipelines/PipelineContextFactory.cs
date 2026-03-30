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
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class PipelineContextFactory(IServiceProvider serviceProvider, IPipelineLookup lookup)
    : IPipelineContextFactory
{
    private static readonly ConcurrentDictionary<(Type, Type), ImmutableList<object?>> _cache =
        new();

    public IPipelineContext Create(
        IMessage message,
        Type actorType,
        object?[] metadata,
        CancellationToken cancellationToken
    )
    {
        var finalMetadata = _cache
            .GetOrAdd(
                (actorType, message.Payload.GetType()),
                val => GetMetadata(val.Item1, val.Item2)
            )
            .ToList();

        finalMetadata.AddRange(metadata);

        return new PipelineContext(
            message,
            serviceProvider,
            new PipelineMetadataCollection(finalMetadata.Where(x => x != null).ToImmutableList()!),
            cancellationToken
        );
    }

    private ImmutableList<object?> GetMetadata(Type actorType, Type messageType)
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

        return middlewareAttributes.OrderBy(x => x.Order).Select(x => x.Metadata).ToImmutableList();
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
