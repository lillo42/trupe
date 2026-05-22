using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.SystemMessages;

namespace Trupe.Pipelines.Middlewares;

/// <summary>
/// Receive middleware that dispatches incoming messages to the appropriate actor handler method, including system messages and typed message routing.
/// </summary>
public class ActorMessageDispatcherMiddleware : IReceiveMiddleware
{
    private readonly ConcurrentDictionary<
        Type,
        Func<IActor, object, CancellationToken, ValueTask>
    > _typedCallHandle = new();

    /// <summary>
    /// Dispatches the message to the actor's handler, choosing the appropriate method based on message type and runtime capabilities.
    /// </summary>
    /// <param name="context">The receive pipeline context containing the actor, message, and metadata.</param>
    /// <param name="next">The delegate to invoke the next middleware in the pipeline.</param>
    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "CreateCallHandleDelegate is only reachable when RuntimeFeature.IsDynamicCodeSupported is true."
    )]
    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        var cancellationToken = context.CancellationToken;
        var message = context.Message;
        var actor = context.Actor;

        if (message.Payload is InitializeActor)
        {
            await actor.InitializeAsync(cancellationToken);
        }
        else if (message.Payload is AfterRestartActor)
        {
            await actor.AfterRestartAsync(cancellationToken);
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
