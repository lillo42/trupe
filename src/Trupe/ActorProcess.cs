using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Mailboxes;
using Trupe.Messages;

namespace Trupe;

/// <summary>
/// Manages the execution lifecycle and message processing for an actor instance.
/// </summary>
/// <remarks>
/// This class orchestrates the core actor message loop, coordinating between
/// the actor's behavior (<see cref="IActor"/>), message queue (<see cref="IMailbox"/>),
/// and the runtime environment. It provides:
/// - Lifecycle management (start/stop)
/// - Efficient typed message dispatch using cached delegates
/// - Integration with AOT (Ahead-Of-Time) compilation constraints
/// - Graceful cancellation and shutdown
/// </remarks>
public class ActorProcess(IActor actor, IMailbox mailbox)
{
    private static readonly ConcurrentDictionary<
        Type,
        Func<IActor, IMessage, ValueTask>
    > _typedCallHandle = new();

    private CancellationTokenSource? _cts;

    private Task? _executing;

    /// <summary>
    /// Starts the actor message processing loop if not already running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is idempotent - calling it multiple times when the actor is already running
    /// will have no effect. The message processing runs on a background thread to avoid blocking
    /// the caller.
    /// </para>
    /// <para>
    /// Once started, the actor will continuously process messages from its mailbox until
    /// explicitly stopped or the cancellation token is triggered.
    /// </para>
    /// </remarks>
    public void Start()
    {
        if (_executing != null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _executing = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>
    /// Gracefully stops the actor process and waits for completion.
    /// </summary>
    /// <returns>A task that completes when the actor has stopped processing messages.</returns>
    /// <remarks>
    /// <para>
    /// This method:
    /// 1. Signals the cancellation token to stop processing new messages
    /// 2. Waits for the current message processing loop to complete
    /// 3. Ensures all resources are properly cleaned up
    /// </para>
    /// <para>
    /// The actor will finish processing the current message (if any) before stopping,
    /// but will not process any new messages that arrive after the cancellation is requested.
    /// </para>
    /// <para>
    /// This method is safe to call multiple times and will do nothing if the actor is not running.
    /// </para>
    /// </remarks>
    public async Task StopAsync()
    {
        if (_cts == null || _executing == null)
        {
            return;
        }

        await _cts.CancelAsync();
        await _executing;
    }

    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "The unfriendly method is not reachable with AOT"
    )]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in mailbox.WithCancellation(cancellationToken))
        {
            actor.Context.Response = null;

            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                var callHandle = _typedCallHandle.GetOrAdd(
                    message.Payload.GetType(),
                    CreateCallHandleDelegate
                );

                await callHandle(actor, message);
            }
            else
            {
                await actor.HandleAsync(message.Payload, message.CancellationToken);
            }

            if (message is IAskMessage askMessage)
            {
                askMessage.SetResult(actor.Context.Response);
            }

            actor.Context.Response = null;
        }
    }

    private static async ValueTask CallHandle<TMessage>(IActor actor, IMessage message)
    {
        if (actor is IHandleActorMessage<TMessage> handle)
        {
            await handle.HandleAsync((TMessage)message.Payload, message.CancellationToken);
        }
        else
        {
            await actor.HandleAsync(message.Payload, message.CancellationToken);
        }
    }

    private static readonly MethodInfo s_callHandleMethodInfo = typeof(ActorProcess).GetMethod(
        nameof(CallHandle),
        BindingFlags.Static | BindingFlags.NonPublic
    )!;

    [RequiresDynamicCode(
        "The native code for this instantiation might not be available at runtime."
    )]
    [UnconditionalSuppressMessage(
        "Aot",
        "IL2060",
        Justification = "The unfriendly method is not reachable with AOT"
    )]
    private static Func<IActor, IMessage, ValueTask> CreateCallHandleDelegate(Type messageType)
    {
        var typed = s_callHandleMethodInfo.MakeGenericMethod(messageType);
        return typed.CreateDelegate<Func<IActor, IMessage, ValueTask>>();
    }
}
