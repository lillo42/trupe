using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;
using Trupe.Abstractions.SystemMessages;
using Trupe.Pipelines.Metadatas;

namespace Trupe.Pipelines.Middlewares;

public class ActorMessageDispatcherMiddleware : IMiddleware
{
    private readonly ConcurrentDictionary<
        Type,
        Func<IActor, object, CancellationToken, ValueTask>
    > _typedCallHandle = new();

    public async ValueTask InvokeAsync(IPipelineContext context, NextDelegate next)
    {
        var cancellationToken = context.CancellationToken;
        var message = context.Metadata.GetRequiredMetadata<ActorMessageMetadata>().Message;
        var actor = context.Metadata.GetRequiredMetadata<ActorMetadata>().Actor;

        if (message.Payload is InitializeActor)
        {
            await actor.InitializeAsync(cancellationToken);
        }
        else if (message.Payload is AfterRestartActor)
        {
            await actor.AfterRestartAsync(cancellationToken);
        }
        else if (message.Payload is Terminate terminate)
        {
            var process = context.Metadata.GetRequiredMetadata<ActorProcessMetadata>().Process;
            process.Terminate(terminate.Reason);
        }
        else if (RuntimeFeature.IsDynamicCodeSupported)
        {
            var callHandle = _typedCallHandle.GetOrAdd(
                message.Payload.GetType(),
                CreateCallHandleDelegate
            );

            await callHandle(actor, message.Payload, cancellationToken);
        }
        else
        {
            await actor.HandleAsync(message.Payload, cancellationToken);
        }

        await next(context);
    }

    private static readonly MethodInfo s_callHandleMethodInfo =
        typeof(ActorMessageDispatcherMiddleware).GetMethod(
            nameof(CallHandle),
            BindingFlags.Static | BindingFlags.NonPublic
        )!;

    private static async ValueTask CallHandle<TMessage>(
        IActor actor,
        object message,
        CancellationToken cancellationToken
    )
    {
        if (actor is IHandleActorMessage<TMessage> handle)
        {
            await handle.HandleAsync((TMessage)message, cancellationToken);
        }
        else
        {
            await actor.HandleAsync(message, cancellationToken);
        }
    }

    [RequiresDynamicCode(
        "The native code for this instantiation might not be available at runtime."
    )]
    [UnconditionalSuppressMessage(
        "Aot",
        "IL2060",
        Justification = "The unfriendly method is not reachable with AOT"
    )]
    private static Func<IActor, object, CancellationToken, ValueTask> CreateCallHandleDelegate(
        Type messageType
    )
    {
        var typed = s_callHandleMethodInfo.MakeGenericMethod(messageType);
        return typed.CreateDelegate<Func<IActor, object, CancellationToken, ValueTask>>();
    }
}
