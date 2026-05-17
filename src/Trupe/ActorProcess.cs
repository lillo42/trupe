using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;
using Trupe.Abstractions.SystemMessages;
using Trupe.Pipelines.Metadatas;

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
    public event EventHandler<ActorFailureEventArgs>? Failed;

    /// <summary>
    /// Event raised when the actor receives a <see cref="Abstractions.SystemMessages.Terminate"/> message and stops processing.
    /// </summary>
    /// <remarks>
    /// Subscribers can use this event to detect voluntary actor termination, as opposed to
    /// failure-based termination signaled through the <see cref="Failed"/> event.
    /// </remarks>
    public event EventHandler<ActorTerminateEventArgs>? Terminated;

    /// <summary>
    /// Gets a value indicating whether the actor's message processing loop is currently running.
    /// </summary>
    public bool IsRunning => _executing != null && !_executing.IsCompleted;

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
    public async Task StartAsync(params IMessage[] messages)
    {
        await StopAsync();

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
        await StopAsync("Stop requested");
    }

    public async Task StopAsync(string? reason)
    {
        if (
            _cts == null
            || _executing == null
            || _cts.IsCancellationRequested
            || _executing.IsCompleted
        )
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

        Terminated?.Invoke(this, new ActorTerminateEventArgs(actor, reason));
    }

    public async Task RequestStopAsync(string reason)
    {
        if (
            _cts == null
            || _executing == null
            || _cts.IsCancellationRequested
            || _executing.IsCompleted
        )
        {
            return;
        }

        await _cts.CancelAsync();

        Terminated?.Invoke(this, new ActorTerminateEventArgs(actor, reason));
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
            if (message.CancellationToken.IsCancellationRequested)
            {
                continue;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                message.CancellationToken
            );

            if (!await ProcessAsync(actor, message, cts.Token))
            {
                return;
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await mailbox.DequeueAsync(cancellationToken);
            if (message == null || message.CancellationToken.IsCancellationRequested)
            {
                continue;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                message.CancellationToken
            );

            if (!await ProcessAsync(actor, message, cts.Token))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Processes a single message by dispatching it to the appropriate handler.
    /// </summary>
    /// <param name="actor">The actor instance to process the message.</param>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">Token to cancel the processing.</param>
    /// <remarks>
    /// This method handles system messages (<see cref="InitializeActor"/>, <see cref="AfterRestartActor"/>),
    /// typed message dispatch, and response handling for ask-pattern messages.
    /// </remarks>
    private async ValueTask<bool> ProcessAsync(
        IActor actor,
        IMessage message,
        CancellationToken cancellationToken
    )
    {
        SettableReceivePipelineContextAccessor? accessor = null;
        var scope = GetOrCreateServiceScope(message);
        var serviceProvider = scope.ServiceProvider;
        var previousActorContext = actor.Context;
        actor.Context = new ActorContext(actor.Context.Self, actor.Context.Metadata, scope);

        try
        {
            var pipelineFactory = serviceProvider.GetRequiredService<IReceivePipelineFactory>();
            var pipelineContextFactory =
                serviceProvider.GetRequiredService<IReceivePipelineContextFactory>();

            var pipeline = pipelineFactory.Create(actor.GetType(), message.Payload.GetType());
            var context = pipelineContextFactory.Create(
                actor,
                actor.Context,
                message,
                [
                    new ActorProcessMetadata(this),
                    new ActorMetadata(actor),
                    new ActorMessageMetadata(message),
                    new ActorContextMetadata(actor.Context),
                ],
                cancellationToken
            );

            accessor = serviceProvider.GetRequiredService<SettableReceivePipelineContextAccessor>();
            accessor.ReceiveContext = context;

            await pipeline.ExecuteAsync(context);

            return true;
        }
        catch (Exception ex)
        {
            Failed?.Invoke(this, new ActorFailureEventArgs(actor, message, ex));
            return false;
        }
        finally
        {
            await DisposeContextIfNecessary(message, scope);
            await DisposeContextIfNecessary(message, actor.Context);
            actor.Context = previousActorContext;

            if (accessor != null)
            {
                accessor.ReceiveContext = null;
            }
        }
    }

    private IServiceScope GetOrCreateServiceScope(IMessage message)
    {
        if (message.Payload is IUseSameActorScopeServiceMessage)
        {
            return new ActorServiceScope(actor.Context.ServiceProvider);
        }

        return actor.Context.ServiceProvider.CreateAsyncScope();
    }

    private async ValueTask DisposeContextIfNecessary(IMessage message, object obj)
    {
        if (message.Payload is not IUseSameActorScopeServiceMessage)
        {
            await DisposeAsync(obj);
        }
    }

    private async ValueTask DisposeAsync(object obj)
    {
        try
        {
            if (obj is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch
        {
            // Ignore exceptions during context disposal to avoid masking original processing exceptions
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Stops the actor process and removes all event handler subscriptions.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await StopAsync();

        Delegate.RemoveAll(Failed, Failed);
        Delegate.RemoveAll(Terminated, Terminated);
    }
}
