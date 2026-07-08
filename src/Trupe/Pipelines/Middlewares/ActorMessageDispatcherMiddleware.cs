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
    /// <summary>
    /// Metadata key used to force the generic <c>HandleAsync(object, CancellationToken)</c> overload
    /// instead of the typed <c>IHandleActorMessage&lt;T&gt;</c> overload.
    /// Set this key to <see langword="true"/> in the pipeline context items to opt out of typed dispatch.
    /// </summary>
    public const string ForceUseGenericHandle = "Trupe:ForceUseGenericHandle";

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
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "The dynamic code path is only used when RuntimeFeature.IsDynamicCodeSupported is true."
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2111:Method with parameters or return value with DynamicallyAccessedMembersAttribute is accessed via reflection",
        Justification = "The dynamic code path is only used when RuntimeFeature.IsDynamicCodeSupported is true."
    )]
    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "The dynamic code path is only used when RuntimeFeature.IsDynamicCodeSupported is true."
    )]
    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        var cancellationToken = context.CancellationToken;
        var message = context.Message;
        var actor = context.Actor;

        var useGeneric =
            context.Items.TryGetValue(ForceUseGenericHandle, out var obj)
            && obj is bool useGen
            && useGen;

        if (message.Payload is InitializeActor)
        {
            await actor.InitializeAsync(cancellationToken);
        }
        else if (message.Payload is AfterRestartActor)
        {
            await actor.AfterRestartAsync(cancellationToken);
        }
        else if (RuntimeFeature.IsDynamicCodeSupported && !useGeneric)
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
    [RequiresUnreferencedCode(
        "If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met."
    )]
    private static Func<IActor, object, CancellationToken, ValueTask> CreateCallHandleDelegate(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.PublicMethods
        )]
            Type messageType
    )
    {
        var typed = s_callHandleMethodInfo.MakeGenericMethod(messageType);
        return typed.CreateDelegate<Func<IActor, object, CancellationToken, ValueTask>>();
    }
}
