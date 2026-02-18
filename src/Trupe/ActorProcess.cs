using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Events;
using Trupe.Exceptions;
using Trupe.Mailboxes;
using Trupe.Messages;
using Trupe.SystemMessages;

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
public class ActorProcess(IActor actor, IMailbox mailbox) : IAsyncDisposable
{
    /// <summary>
    /// Cache for typed message handler delegates, keyed by message payload type.
    /// This avoids reflection overhead on every message by caching the delegate once per type.
    /// </summary>
    private static readonly ConcurrentDictionary<
        Type,
        Func<IActor, IMessage, CancellationToken, ValueTask>
    > _typedCallHandle = new();

    /// <summary>
    /// Cancellation token source used to signal the actor to stop processing messages.
    /// </summary>
    private CancellationTokenSource? _cts;

    /// <summary>
    /// The task representing the actor's message processing loop.
    /// </summary>
    private Task? _executing;

    /// <summary>
    /// Event raised when an unhandled exception occurs during message processing.
    /// </summary>
    /// <remarks>
    /// Subscribers can use this event to implement supervision strategies such as
    /// restarting the actor, escalating the failure, or logging the error.
    /// </remarks>
    public event EventHandler<ActorFailureEventArgs>? Failure;

    /// <summary>
    /// Event raised when the actor receives a <see cref="SystemMessages.Terminate"/> message and stops processing.
    /// </summary>
    /// <remarks>
    /// Subscribers can use this event to detect voluntary actor termination, as opposed to
    /// failure-based termination signaled through the <see cref="Failure"/> event.
    /// </remarks>
    public event EventHandler<ActorTerminateEventArgs>? Terminate;

    /// <summary>
    /// Starts the actor's message processing loop.
    /// </summary>
    /// <param name="messages">
    /// Optional initial messages to process before consuming from the mailbox.
    /// These are typically system messages like <see cref="InitializeActor"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method is idempotent - calling it on an already running actor has no effect.
    /// </para>
    /// <para>
    /// The actor will first process any provided initial messages, then begin
    /// consuming and processing messages from its mailbox.
    /// </para>
    /// </remarks>
    public void Start(params IMessage[] messages)
    {
        if (_executing != null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _executing = Task.Run(() => RunAsync(new Queue<IMessage>(messages), _cts.Token));
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
        try
        {
            await _executing;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation exceptions during shutdown
        }

        _cts.Dispose();

        _cts = null;
        _executing = null;
    }

    /// <summary>
    /// Executes the actor's main message processing loop.
    /// </summary>
    /// <param name="messages">Initial messages to process before consuming from the mailbox.</param>
    /// <param name="cancellationToken">Token to signal the loop to stop.</param>
    /// <returns>A task representing the message processing loop.</returns>
    private async Task RunAsync(Queue<IMessage> messages, CancellationToken cancellationToken)
    {
        while (messages.TryDequeue(out var message))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                message.CancellationToken
            );

            if (!await ProcessAsync(actor, message, cts.Token))
            {
                return;
            }
        }

        await foreach (var message in mailbox.WithCancellation(cancellationToken))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                message.CancellationToken
            );

            if (!await ProcessAsync(actor, message, cts.Token))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Processes a single message by dispatching it to the appropriate handler.
    /// </summary>
    /// <param name="actor">The actor instance to process the message.</param>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">Token to cancel the processing.</param>
    /// <returns>
    /// <c>true</c> if processing completed successfully and the loop should continue;
    /// <c>false</c> if an unhandled exception occurred and the loop should stop.
    /// </returns>
    /// <remarks>
    /// This method handles system messages (<see cref="InitializeActor"/>, <see cref="AfterRestartActor"/>),
    /// typed message dispatch, and response handling for ask-pattern messages.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "The unfriendly method is not reachable with AOT"
    )]
    private async ValueTask<bool> ProcessAsync(
        IActor actor,
        IMessage message,
        CancellationToken cancellationToken
    )
    {
        actor.Context.Response = null;

        try
        {
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
                Terminate?.Invoke(this, new ActorTerminateEventArgs(actor, terminate.Reason));
                return false;
            }
            else if (RuntimeFeature.IsDynamicCodeSupported)
            {
                var callHandle = _typedCallHandle.GetOrAdd(
                    message.Payload.GetType(),
                    CreateCallHandleDelegate
                );

                await callHandle(actor, message, cancellationToken);
            }
            else
            {
                await actor.HandleAsync(message.Payload, cancellationToken);
            }

            if (message is IAskMessage askMessage)
            {
                askMessage.SetResult(actor.Context.Response);
            }

            actor.Context.Response = null;
        }
        catch (AskException ex)
        {
            if (message is IAskMessage askMessage)
            {
                askMessage.SetException(ex);
            }
        }
        catch (Exception ex)
        {
            Failure?.Invoke(this, new ActorFailureEventArgs(actor, message, ex));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Invokes the typed message handler for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message payload.</typeparam>
    /// <param name="actor">The actor to handle the message.</param>
    /// <param name="message">The message containing the payload.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the handler execution.</returns>
    private static async ValueTask CallHandle<TMessage>(
        IActor actor,
        IMessage message,
        CancellationToken cancellationToken
    )
    {
        if (actor is IHandleActorMessage<TMessage> handle)
        {
            await handle.HandleAsync((TMessage)message.Payload, cancellationToken);
        }
        else
        {
            await actor.HandleAsync(message.Payload, cancellationToken);
        }
    }

    /// <summary>
    /// Cached <see cref="MethodInfo"/> for the <see cref="CallHandle{TMessage}"/> method,
    /// used to create typed delegates at runtime.
    /// </summary>
    private static readonly MethodInfo s_callHandleMethodInfo = typeof(ActorProcess).GetMethod(
        nameof(CallHandle),
        BindingFlags.Static | BindingFlags.NonPublic
    )!;

    /// <summary>
    /// Creates a delegate for invoking the typed message handler for a specific message type.
    /// </summary>
    /// <param name="messageType">The type of the message payload.</param>
    /// <returns>A delegate that invokes the typed handler for the specified message type.</returns>
    /// <remarks>
    /// This method uses reflection to create a generic delegate, which is then cached
    /// in <see cref="_typedCallHandle"/> for subsequent calls with the same message type.
    /// </remarks>
    [RequiresDynamicCode(
        "The native code for this instantiation might not be available at runtime."
    )]
    [UnconditionalSuppressMessage(
        "Aot",
        "IL2060",
        Justification = "The unfriendly method is not reachable with AOT"
    )]
    private static Func<IActor, IMessage, CancellationToken, ValueTask> CreateCallHandleDelegate(
        Type messageType
    )
    {
        var typed = s_callHandleMethodInfo.MakeGenericMethod(messageType);
        return typed.CreateDelegate<Func<IActor, IMessage, CancellationToken, ValueTask>>();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Stops the actor process and removes all event handler subscriptions.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await StopAsync();

        Delegate.RemoveAll(Failure, Failure);
        Delegate.RemoveAll(Terminate, Terminate);
    }
}
