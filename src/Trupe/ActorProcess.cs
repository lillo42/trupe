using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;
using Trupe.Abstractions.SystemMessages;
using Trupe.Collections;
using Trupe.Guards;
using Trupe.Pipelines;

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
public class ActorProcess(IActor actor, IMailbox mailbox) : IActorProcess, IAsyncDisposable
{
    private readonly ActorProcessListenerCollection _collection = [];

    private bool _isDisposed;
    private CancellationTokenSource? _cts;
    private Task? _executing;

    /// <inheritdoc />
    public IMailbox Mailbox { get; set; } = mailbox;

    /// <inheritdoc />
    public IActor Actor { get; set; } = actor;

    /// <inheritdoc />
    public async Task StartAsync(params IMessage[] messages)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorProcess));

        await KillAsync();

        _cts = new CancellationTokenSource();
        _executing = Task.Run(async () =>
        {
            try
            {
                await RunAsync(new Queue<IMessage>(messages), _cts.Token);
            }
            catch
            {
                // Ignore any error, it's already catch by Run Async
            }
        });
    }

    /// <inheritdoc />
    public async Task KillAsync()
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorProcess));

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

            await ProcessAsync(Actor, message, cts.Token);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await Mailbox.DequeueAsync(cancellationToken);
            if (message == null || message.CancellationToken.IsCancellationRequested)
            {
                continue;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                message.CancellationToken
            );

            if (message.Payload is Stop)
            {
                _collection.InvokeOnStopped(this, TerminatedReason.Stopped);
                
                if (message is IAskMessage askMessage)
                {
                    askMessage.SetResult(null);
                }
                
                return;
            }
            
            await ProcessAsync(Actor, message, cts.Token);
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method",
        Justification = "Actor types are registered at startup and preserved by DI container registration."
    )]
    private async ValueTask ProcessAsync(
        IActor actor,
        IMessage message,
        CancellationToken cancellationToken
    )
    {
        var previousServiceProvider = actor.Context.ServiceProvider;
        var previousMetadata = new Dictionary<string, object?>(actor.Context.Metadata);

        SettableReceivePipelineContextAccessor? accessor = null;
        var scope = GetOrCreateServiceScope(message);
        var serviceProvider = scope.ServiceProvider;
        actor.Context.ServiceProvider = serviceProvider;
        
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
        }
        catch (OperationCanceledException)
        {
            // It was requested to stop the process
        }
        catch (Exception ex)
        {
            _collection.InvokeOnFailed(this, message, ex);
            if (message is IAskMessage askMessage)
            {
                askMessage.SetException(ex);
            }
            
            throw;
        }
        finally
        {
            await DisposeContextIfNecessary(message, scope);

            actor.Context.Metadata = previousMetadata;
            actor.Context.ServiceProvider = previousServiceProvider;

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
            return new ActorServiceScope(Actor.Context.ServiceProvider);
        }

        return Actor.Context.ServiceProvider.CreateAsyncScope();
    }

    private static async ValueTask DisposeContextIfNecessary(IMessage message, object obj)
    {
        if (message.Payload is not IUseSameActorScopeServiceMessage)
        {
            await DisposeAsync(obj);
        }
    }

    private static async ValueTask DisposeAsync(object obj)
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
        await DisposeAsync(true);

        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsync(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _collection.Clear();
            await KillAsync();
        }

        _isDisposed = true;
    }

    /// <inheritdoc />
    public IDisposable Register(IActorProcessListener listener)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorProcess));

        return _collection.Add(listener);
    }

    /// <inheritdoc />
    public void UnRegister(IActorProcessListener listing)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorProcess));

        _collection.Remove(listing);
    }
}
