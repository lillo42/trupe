using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Trupe.ActorReferences;
using Trupe.Events;
using Trupe.Exceptions;
using Trupe.Factories;
using Trupe.Mailboxes;
using Trupe.Messages;
using Trupe.Supervisors.Commands;
using Trupe.SystemMessages;

namespace Trupe.Supervisors;

/// <summary>
/// An abstract base class for partition-based supervisors that manage a fixed pool of child actors.
/// Messages are routed to actors based on a partition key, ensuring consistent actor assignment for the same key.
/// </summary>
/// <typeparam name="TActor">The type of actor to supervise.</typeparam>
/// <param name="actorFactory">The factory used to create child actors.</param>
/// <param name="logger">The logger for supervisor operations.</param>
/// <param name="workers">The number of worker actors to create in the partition.</param>
public abstract partial class PartitionSupervisor<TActor>(
    IActorFactory actorFactory,
    ILogger logger,
    int workers
) : Actor, ISupervisor, IHandleActorMessage<ActorFailed>, IAsyncDisposable
{
    /// <summary>
    /// Gets the factory used to create child actors.
    /// </summary>
    protected virtual IActorFactory ActorFactory { get; } = actorFactory;

    /// <summary>
    /// Gets the logger for supervisor operations.
    /// </summary>
    protected virtual ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the number of worker actors in the partition.
    /// </summary>
    protected virtual int Workers { get; } = workers;

    /// <summary>
    /// Gets the supervision strategy used when a child actor fails.
    /// Default is <see cref="Strategy.OneForOne"/>.
    /// </summary>
    protected virtual Strategy Strategy => Strategy.OneForOne;

    /// <summary>
    /// Gets the maximum number of restarts allowed within the <see cref="RestartWindow"/>.
    /// Default is 3.
    /// </summary>
    protected virtual int MaxRestarts => 3;

    /// <summary>
    /// Gets the time window for counting restart attempts.
    /// If this window elapses without a restart, the restart count is reset.
    /// Default is 5 seconds.
    /// </summary>
    protected virtual TimeSpan RestartWindow => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the list of actor metadata for all child actors managed by this supervisor.
    /// </summary>
    protected ImmutableList<ActorMetadata> Actors { get; private set; } = [];

    /// <summary>
    /// Gets the collection of actor references for all child actors.
    /// </summary>
    public IEnumerable<IActorReference> Children => Actors.Select(x => x.Reference);

    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartitionSupervisor{TActor}"/> class
    /// with the default number of workers equal to the processor count.
    /// </summary>
    /// <param name="actorFactory">The factory used to create child actors.</param>
    /// <param name="logger">The logger for supervisor operations.</param>
    public PartitionSupervisor(IActorFactory actorFactory, ILogger logger)
        : this(actorFactory, logger, Environment.ProcessorCount) { }

    /// <summary>
    /// Initializes the supervisor by creating and starting all worker actors.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public override async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < Workers; i++)
        {
            CreateActor(CreateMailbox());
        }

        await OnInitializeAsync(cancellationToken);
        _initialized = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Stops and disposes all child actors before the supervisor restarts.
    /// </remarks>
    public override async ValueTask BeforeRestartAsync(
        CancellationToken cancellationToken = default
    )
    {
        foreach (var metadata in Actors)
        {
            await StopActorAsync(metadata);

            await DisposeObjectAsync(metadata.Actor);

            metadata.Actor = null!;
            metadata.Process = null!;
            metadata.Metadata.Clear();
        }

        Actors = [];
    }

    /// <summary>
    /// Handles a failed actor message by applying the appropriate failure action based on the supervision strategy.
    /// </summary>
    /// <param name="message">The actor failed message containing failure details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the handling operation.</returns>
    public async ValueTask HandleAsync(
        ActorFailed message,
        CancellationToken cancellationToken = default
    )
    {
        using var _actorType = Logger.BeginScope("{ActorType}", message.Actor.GetType());
        LoggerMessages.ProcessingFailedActor(Logger, message.Exception);

        var metadata = Actors.FirstOrDefault(x => x.Actor == message.Actor);
        if (metadata == null)
        {
            LoggerMessages.ActorNotFound(Logger);
            return;
        }

        ResetCounter(metadata);

        var action = GetFailureAction(metadata, message.Exception);

        LoggerMessages.GoingToApplyFailureAction(Logger, action, Strategy);
        if (action == FailureAction.Restart)
        {
            await ApplyRestartAsync(metadata);
        }
        else if (action == FailureAction.Stop)
        {
            await ApplyStopAsync(metadata);
        }
        else if (action == FailureAction.Escalate)
        {
            await ApplyEscalateAsync(metadata, message.Message, message.Exception);
        }
        else
        {
            await ApplyResumeAsync(metadata);
        }

        if (message.Message is IAskMessage askMessage)
        {
            LoggerMessages.CancelAskMessage(Logger);
            askMessage.SetCanceled();
        }

        if (
            message.Exception is EscalateFailureException
            {
                ActorMessage: IAskMessage escalateAskMessage
            }
        )
        {
            LoggerMessages.CancelAskMessage(Logger);
            escalateAskMessage.SetCanceled();
        }
    }

    /// <summary>
    /// Disposes the supervisor and all child actors asynchronously.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        foreach (var metadata in Actors)
        {
            await DisposeObjectAsync(metadata.Actor);

            metadata.Actor = null!;
            metadata.Process = null!;
            metadata.Metadata.Clear();
        }

        Actors = [];
    }

    /// <summary>
    /// Gets the actor reference for a given partition key.
    /// The same key will always route to the same actor.
    /// </summary>
    /// <param name="key">The partition key used to determine which actor to route to.</param>
    /// <returns>The actor reference for the partition.</returns>
    protected virtual IActorReference GetActorReference(object key)
    {
        var hash = Math.Abs(GetHashcode(key));

        return Actors[hash % Actors.Count].Reference;
    }

    /// <summary>
    /// Gets the hash code for a partition key.
    /// Override this method to customize partition key hashing.
    /// </summary>
    /// <param name="key">The partition key.</param>
    /// <returns>The hash code for the key.</returns>
    protected virtual int GetHashcode(object key)
    {
        return key.GetHashCode();
    }

    /// <summary>
    /// Called after all worker actors have been created during initialization.
    /// Override this method to perform additional initialization logic.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the initialization operation.</returns>
    protected virtual ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates a mailbox for a new worker actor.
    /// Override this method to use a custom mailbox implementation.
    /// </summary>
    /// <returns>A new mailbox instance.</returns>
    protected virtual IMailbox CreateMailbox()
    {
        return new ChannelMailbox();
    }

    /// <summary>
    /// Creates and starts a new child actor.
    /// </summary>
    /// <param name="mailbox">The mailbox for the actor.</param>
    /// <returns>The metadata for the created actor.</returns>
    protected virtual ActorMetadata CreateActor(IMailbox mailbox)
    {
        if (_initialized)
        {
            throw new SupervisorAlreadyInitializedException("");
        }

        if (Actors.Count >= Workers)
        {
            // TODO: Improve exception message to include more details about the supervisor and the attempted creation.
            throw new System.InvalidOperationException(
                $"Cannot create more than {Workers} actors in a partition supervisor."
            );
        }

        var reference = new LocalActorReference(mailbox);
        var actor = ActorFactory.CreateActor(typeof(TActor));
        actor.Context = new ActorContext(reference);

        var process = new ActorProcess(actor, mailbox);
        process.Failure += HandleFailure;

        var metadata = new ActorMetadata(actor, mailbox, process, reference);
        Actors = Actors.Add(metadata);

        process.Start(new LocalTellMessage(new InitializeActor()));

        return metadata;
    }

    /// <summary>
    /// Handles failure events from child actor processes.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The failure event arguments.</param>
    protected virtual void HandleFailure(object? sender, ActorFailureEventArgs args)
    {
        using var _ = Logger.BeginScope("{ActorType}", args.Actor.GetType());
        LoggerMessages.HandlingFailedActor(Logger, args.Exception);
        Context.Self.Tell(new ActorFailed(args.Actor, args.Message, args.Exception));
    }

    /// <summary>
    /// Disposes an object, supporting both sync and async disposal patterns.
    /// </summary>
    /// <param name="obj">The object to dispose.</param>
    /// <returns>A task representing the disposal operation.</returns>
    protected virtual ValueTask DisposeObjectAsync(object obj)
    {
        if (obj is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }
        else if (obj is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Resets the restart counter if the restart window has elapsed.
    /// </summary>
    /// <param name="metadata">The actor metadata to check.</param>
    protected virtual void ResetCounter(ActorMetadata metadata)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - metadata.LastRestartTime) > RestartWindow)
        {
            LoggerMessages.ResetingActorCounter(Logger, metadata.ActorType);
            metadata.RestartCount = 0;
        }
    }

    /// <summary>
    /// Determines the appropriate failure action for a failed actor.
    /// </summary>
    /// <param name="metadata">The metadata of the failed actor.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>The action to take in response to the failure.</returns>
    protected virtual FailureAction GetFailureAction(ActorMetadata metadata, Exception exception)
    {
        if (metadata.RestartCount >= MaxRestarts)
        {
            return FailureAction.Escalate;
        }

        return FailureAction.Restart;
    }

    /// <summary>
    /// Applies the stop action to the failed actor(s) based on the supervision strategy.
    /// </summary>
    /// <param name="metadata">The metadata of the failed actor.</param>
    /// <returns>A task representing the stop operation.</returns>
    protected virtual async Task ApplyStopAsync(ActorMetadata metadata)
    {
        if (Strategy == Strategy.OneForOne)
        {
            await StopActorAsync(metadata);
        }
        else if (Strategy == Strategy.AllForOne)
        {
            await Task.WhenAll(Actors.Select(StopActorAsync));
        }
    }

    /// <summary>
    /// Stops a single actor.
    /// </summary>
    /// <param name="metadata">The metadata of the actor to stop.</param>
    /// <returns>A task representing the stop operation.</returns>
    protected virtual async Task StopActorAsync(ActorMetadata metadata)
    {
        LoggerMessages.StoppingActor(Logger);

        metadata.Process.Failure -= HandleFailure;
        await metadata.Process.StopAsync();

        LoggerMessages.ActorStopped(Logger);
    }

    /// <summary>
    /// Applies the resume action, allowing the actor to continue processing messages.
    /// </summary>
    /// <param name="metadata">The metadata of the actor to resume.</param>
    /// <returns>A task representing the resume operation.</returns>
    protected virtual async Task ApplyResumeAsync(ActorMetadata metadata)
    {
        LoggerMessages.ResumingActor(Logger);

        await metadata.Process.StopAsync();
        metadata.Process.Start();

        LoggerMessages.ActorResumed(Logger);
    }

    /// <summary>
    /// Escalates the failure to the parent supervisor by throwing an exception.
    /// </summary>
    /// <param name="metadata">The metadata of the failed actor.</param>
    /// <param name="message">The message that caused the failure.</param>
    /// <param name="exception">The original exception.</param>
    /// <returns>A task representing the escalation operation.</returns>
    /// <exception cref="EscalateFailureException">Always thrown to escalate to parent.</exception>
    protected virtual async Task ApplyEscalateAsync(
        ActorMetadata metadata,
        IMessage message,
        Exception exception
    )
    {
        LoggerMessages.ScalatingError(Logger);

        await metadata.Process.StopAsync();
        throw new EscalateFailureException(
            "Unable to handle actor failure",
            metadata.Reference,
            message,
            exception
        );
    }

    /// <summary>
    /// Applies the restart action to the failed actor(s) based on the supervision strategy.
    /// </summary>
    /// <param name="metadata">The metadata of the failed actor.</param>
    /// <returns>A task representing the restart operation.</returns>
    protected virtual async Task ApplyRestartAsync(ActorMetadata metadata)
    {
        metadata.RestartCount++;
        metadata.LastRestartTime = DateTimeOffset.UtcNow;

        if (Strategy == Strategy.OneForOne)
        {
            await ResetActorAsync(metadata);
        }
        else if (Strategy == Strategy.AllForOne)
        {
            await Task.WhenAll(Actors.Select(ResetActorAsync));
        }
    }

    /// <summary>
    /// Resets an actor by stopping, disposing, and recreating it.
    /// </summary>
    /// <param name="metadata">The metadata of the actor to reset.</param>
    /// <returns>A task representing the reset operation.</returns>
    protected virtual async Task ResetActorAsync(ActorMetadata metadata)
    {
        LoggerMessages.ResetingActor(Logger);

        await StopActorAsync(metadata);
        await BeforeRestartActorAsync(metadata);

        await DisposeObjectAsync(metadata.Actor);

        await ResetMailboxAsync(metadata);

        LoggerMessages.CreatingNewActorInstance(Logger);
        metadata.Actor = ActorFactory.CreateActor(metadata.ActorType);
        metadata.Actor.Context = new ActorContext(metadata.Reference);
        LoggerMessages.ActoCreateWithSuccess(Logger);

        LoggerMessages.CreateNewProcess(Logger);
        metadata.Process = new ActorProcess(metadata.Actor, metadata.Mailbox);
        metadata.Process.Failure += HandleFailure;
        metadata.Process.Start(
            new LocalTellMessage(new InitializeActor()),
            new LocalTellMessage(new AfterRestartActor())
        );
        LoggerMessages.ActorProcessStarted(Logger);
    }

    /// <summary>
    /// Resets the mailbox for a supervisor actor during restart.
    /// </summary>
    /// <param name="metadata">The metadata of the actor.</param>
    /// <returns>A task representing the reset operation.</returns>
    protected virtual async ValueTask ResetMailboxAsync(ActorMetadata metadata)
    {
        if (metadata.IsSupervisor)
        {
            await metadata.Mailbox.CleanAsync();
        }
    }

    /// <summary>
    /// Calls the actor's <see cref="IActor.BeforeRestartAsync"/> method before restarting.
    /// </summary>
    /// <param name="metadata">The metadata of the actor being restarted.</param>
    /// <returns>A task representing the operation.</returns>
    protected virtual async ValueTask BeforeRestartActorAsync(ActorMetadata metadata)
    {
        try
        {
            LoggerMessages.CallBeforeRestartActor(Logger);
            await metadata.Actor.BeforeRestartAsync();
            LoggerMessages.SuccessBeforeRestartActor(Logger);
        }
        catch (Exception ex)
        {
            LoggerMessages.ErrorDuringCallBeforeRestartActor(Logger, ex);
        }
    }

    private static partial class LoggerMessages
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Received failure notification from child actor"
        )]
        public static partial void HandlingFailedActor(ILogger logger, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Processing actor failure for supervised child"
        )]
        public static partial void ProcessingFailedActor(ILogger logger, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Failed actor not found in supervised children list"
        )]
        public static partial void ActorNotFound(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Canceling pending ask message due to actor failure"
        )]
        public static partial void CancelAskMessage(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Applying failure action '{FailureAction}' using '{Strategy}' strategy"
        )]
        public static partial void GoingToApplyFailureAction(
            ILogger logger,
            FailureAction failureAction,
            Strategy strategy
        );

        [LoggerMessage(Level = LogLevel.Trace, Message = "Resuming actor after failure")]
        public static partial void ResumingActor(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = "Actor resumed and ready to process messages"
        )]
        public static partial void ActorResumed(ILogger logger);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Resetting actor state for restart")]
        public static partial void ResetingActor(ILogger logger);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Actor restarted successfully")]
        public static partial void ActorRestarted(ILogger logger);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Stopping actor process")]
        public static partial void StoppingActor(ILogger logger);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Actor process stopped successfully")]
        public static partial void ActorStopped(ILogger logger);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Invoking BeforeRestartAsync on actor")]
        public static partial void CallBeforeRestartActor(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = "BeforeRestartAsync completed successfully"
        )]
        public static partial void SuccessBeforeRestartActor(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "BeforeRestartAsync threw an exception, ignoring and continuing with restart"
        )]
        public static partial void ErrorDuringCallBeforeRestartActor(
            ILogger logger,
            Exception exception
        );

        [LoggerMessage(Level = LogLevel.Trace, Message = "Creating new actor instance for restart")]
        public static partial void CreatingNewActorInstance(ILogger logger);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Actor instance created successfully")]
        public static partial void ActoCreateWithSuccess(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = "Creating and starting actor process with InitializeActor and AfterRestartActor messages"
        )]
        public static partial void CreateNewProcess(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = "Actor process started and processing messages"
        )]
        public static partial void ActorProcessStarted(ILogger logger);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Escalating failure to parent supervisor")]
        public static partial void ScalatingError(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Resetting restart counter for actor type {ActorType} after restart window elapsed"
        )]
        public static partial void ResetingActorCounter(ILogger logger, Type actorType);
    }
}
