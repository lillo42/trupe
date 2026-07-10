using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
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
public class ActorProcess : IActorProcess, IAsyncDisposable
{
    private static readonly Counter<int> StopCounter = TrupeDiagnostics.Meter.CreateCounter<int>("actor-process.stop",
        unit: "{operations}",
        description: "Number of actor processes stopped gracefully via a Stop message.");

    private static readonly Counter<int> KillCounter = TrupeDiagnostics.Meter.CreateCounter<int>("actor-process.kill",
        unit: "{operations}",
        description: "Number of actor processes killed via cancellation.");

    private static readonly Counter<int> SuccessCounter = TrupeDiagnostics.Meter.CreateCounter<int>(
        "actor-process.message-processing.success",
        unit: "{operations}",
        description: "Number of messages processed successfully.");

    private static readonly Counter<int> ErrorCounter = TrupeDiagnostics.Meter.CreateCounter<int>(
        "actor-process.message-processing.failed",
        unit: "{operations}",
        description: "Number of messages that failed to process due to an unhandled exception.");

    private static readonly Counter<int> TimeoutCounter = TrupeDiagnostics.Meter.CreateCounter<int>(
        "actor-process.message-processing.timeout",
        unit: "{operations}",
        description: "Number of messages whose processing was cancelled or timed out.");

    private static readonly Counter<int> SkippedCounter = TrupeDiagnostics.Meter.CreateCounter<int>(
        "actor-process.message-processing.skipped",
        unit: "{operations}",
        description: "Number of messages skipped due to a null message or a cancelled message token.");

    private static readonly Histogram<long> MessageProcessingDuration = TrupeDiagnostics.Meter.CreateHistogram<long>(
        "actor-process.message-processing.duration",
        unit: "ms",
        description: "Duration of actor message processing in milliseconds.");

    private readonly ActorProcessListenerCollection _collection = [];

    private bool _isDisposed;
    private CancellationTokenSource? _cts;
    private Task? _executing;
    private readonly IActor _actor;

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
    public ActorProcess(IActor actor, IMailbox mailbox)
    {
        _actor = actor;
        Mailbox = mailbox;
        Actor = actor;
    }

    /// <inheritdoc />
    public IMailbox Mailbox { get; set; }

    /// <inheritdoc />
    public IActor Actor { get; set; }

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

        KillCounter.Add(1,
            new KeyValuePair<string, object?>("actor", Actor.Context.Name),
            new KeyValuePair<string, object?>("actor.type", Actor.GetType()));


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
                SkippedCounter.Add(1,
                    new KeyValuePair<string, object?>("actor", _actor.Context.Name),
                    new KeyValuePair<string, object?>("actor.type", _actor.GetType()),
                    new KeyValuePair<string, object?>("message.type", message.GetType()),
                    new KeyValuePair<string, object?>("message.payload.type", message.Payload.GetType()));

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
                SkippedCounter.Add(1,
                    new KeyValuePair<string, object?>("actor", _actor.Context.Name),
                    new KeyValuePair<string, object?>("actor.type", _actor.GetType()),
                    new KeyValuePair<string, object?>("message.type", message?.GetType()),
                    new KeyValuePair<string, object?>("message.payload.type", message?.Payload.GetType()));
                continue;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                message.CancellationToken
            );

            if (message.Payload is Stop)
            {
                StopCounter.Add(1,
                    new KeyValuePair<string, object?>("actor", _actor.Context.Name),
                    new KeyValuePair<string, object?>("actor.type", _actor.GetType()));

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
        var activity = TrupeDiagnostics.ActivitySource
            .StartActivity("actor-process.message-processing", ActivityKind.Internal, null)?
            .SetTag("actor", actor.Context.Name)
            .SetTag("actor.type", actor.GetType())
            .SetTag("message.type", message.GetType())
            .SetTag("message.payload.type", message.Payload.GetType());

        var stopwatch = Stopwatch.StartNew();

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

            stopwatch.Stop();

            SuccessCounter.Add(1,
                new KeyValuePair<string, object?>("actor", actor.Context.Name),
                new KeyValuePair<string, object?>("actor.type", actor.GetType()),
                new KeyValuePair<string, object?>("message.type", message.GetType()),
                new KeyValuePair<string, object?>("message.payload.type", message.Payload.GetType()));

            activity?.SetStatus(ActivityStatusCode.Ok, "Message processed successfully.");
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            TimeoutCounter.Add(1,
                new KeyValuePair<string, object?>("actor", actor.Context.Name),
                new KeyValuePair<string, object?>("actor.type", actor.GetType()),
                new KeyValuePair<string, object?>("message.type", message.GetType()),
                new KeyValuePair<string, object?>("message.payload.type", message.Payload.GetType()));

            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, "Message processing was cancelled or timed out.");

            // It was requested to stop the process
            if (message is IAskMessage askMessage)
            {
                askMessage.SetCanceled();
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            ErrorCounter.Add(1,
                new KeyValuePair<string, object?>("actor", actor.Context.Name),
                new KeyValuePair<string, object?>("actor.type", actor.GetType()),
                new KeyValuePair<string, object?>("message.type", message.GetType()),
                new KeyValuePair<string, object?>("message.payload.type", message.Payload.GetType()));

            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, "Failed to process message.");

            _collection.InvokeOnFailed(this, message, ex);
            throw;
        }
        finally
        {
            MessageProcessingDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("actor", actor.Context.Name),
                new KeyValuePair<string, object?>("actor.type", actor.GetType()),
                new KeyValuePair<string, object?>("message.type", message.GetType()),
                new KeyValuePair<string, object?>("message.payload.type", message.Payload.GetType()));

            await DisposeContextIfNecessary(message, scope);

            actor.Context.Metadata = previousMetadata;
            actor.Context.ServiceProvider = previousServiceProvider;

            if (accessor != null)
            {
                accessor.ReceiveContext = null;
            }

            activity?.Dispose();
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