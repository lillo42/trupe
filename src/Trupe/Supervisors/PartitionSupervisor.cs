using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Supervisors;
using Trupe.Abstractions.SystemMessages;
using Trupe.Mailboxes;
using Trupe.Messages;
using Trupe.Supervisors.Commands;

namespace Trupe.Supervisors;

/// <summary>
/// An abstract base class for partition-based supervisors that manage a fixed pool of child actors.
/// Messages are routed to actors based on a partition key, ensuring consistent actor assignment for the same key.
/// </summary>
/// <typeparam name="TActor">The type of actor to supervise.</typeparam>
/// <param name="actorFactory">The factory used to create child actors.</param>
/// <param name="logger">The logger for supervisor operations.</param>
/// <param name="workers">The number of worker actors to create in the partition.</param>
public abstract partial class PartitionSupervisor<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActor
>(ILogger logger, int workers)
    : Actor,
        ISupervisor,
        IHandleActorMessage<ActorFailed>,
        IHandleActorMessage<ActorTerminated>,
        IAsyncDisposable
{
    /// <summary>
    /// Gets the factory used to create child actors.
    /// </summary>
    protected virtual IActorFactory ActorFactory =>
        Context.ServiceProvider.GetRequiredService<IActorFactory>();

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
    /// Gets the default restart policy for child actors.
    /// Default is <see cref="RestartPolicy.Permanent"/>.
    /// </summary>
    protected virtual RestartPolicy DefaultRestartPolicy => RestartPolicy.Permanent;

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
    protected ImmutableList<Child> Children { get; private set; } = [];

    /// <summary>
    /// Gets the collection of actor references for all child actors.
    /// </summary>
    IEnumerable<IActorReference> ISupervisor.Children => Children.Select(x => x.Reference);

    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartitionSupervisor{TActor}"/> class
    /// with the default number of workers equal to the processor count.
    /// </summary>
    /// <param name="logger">The logger for supervisor operations.</param>
    public PartitionSupervisor(ILogger logger)
        : this(logger, Environment.ProcessorCount) { }

    /// <summary>
    /// Initializes the supervisor by creating and starting all worker actors.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public override async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < Workers; i++)
        {
            await CreateActorAsync(
                new ChildSpecification(typeof(TActor))
                {
                    RestartPolicy = DefaultRestartPolicy,
                    Mailbox = CreateMailbox(),
                }
            );
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
        foreach (var metadata in Children)
        {
            await StopActorAsync(metadata);
            await DisposeObjectAsync(metadata.Actor);
            await DisposeObjectAsync(metadata.Actor.Context);

            metadata.Actor = null!;
            metadata.Process = null!;
            metadata.Metadata.Clear();
        }

        Children = [];
    }

    /// <inheritdoc />
    /// <remarks>
    /// Routes <see cref="ActorFailed"/> and <see cref="ActorTerminated"/> messages
    /// to their respective typed handlers before falling back to the base implementation.
    /// </remarks>
    public override ValueTask HandleAsync(
        object? message,
        CancellationToken cancellationToken = default
    )
    {
        if (message is ActorFailed failed)
        {
            return HandleAsync(failed, cancellationToken);
        }
        else if (message is ActorTerminated terminated)
        {
            return HandleAsync(terminated, cancellationToken);
        }

        return base.HandleAsync(message, cancellationToken);
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
        PartitionLog.ProcessingFailedActor(Logger, message.Exception);

        var child = Children.FirstOrDefault(x => x.Actor == message.Actor);
        if (child == null)
        {
            PartitionLog.ActorNotFound(Logger);
            return;
        }

        await OnActorFailed(child, message.Message, message.Exception, cancellationToken);
        PartitionLog.FailedActorProcessed(Logger);
    }

    /// <summary>
    /// Handles a terminated actor message by applying the appropriate action based on the actor's restart policy.
    /// </summary>
    /// <param name="message">The actor terminated message containing termination details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the handling operation.</returns>
    public async ValueTask HandleAsync(
        ActorTerminated message,
        CancellationToken cancellationToken = default
    )
    {
        PartitionLog.ProcessingTerminatedActor(Logger, message.Reason);

        var child = Children.FirstOrDefault(x => x.Actor == message.Actor);
        if (child == null)
        {
            PartitionLog.ActorNotFound(Logger);
            return;
        }

        await OnActorTerminated(child, message.Reason);

        PartitionLog.FinishedProcessingTerminatedActor(Logger);
    }

    /// <summary>
    /// Disposes the supervisor and all child actors asynchronously.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        foreach (var metadata in Children)
        {
            await StopActorAsync(metadata);
            await DisposeObjectAsync(metadata.Actor);
            await DisposeObjectAsync(metadata.Actor.Context);

            await metadata.Process.DisposeAsync();

            metadata.Actor = null!;
            metadata.Process = null!;
            metadata.Metadata.Clear();
        }

        Children = [];
    }

    /// <summary>
    /// Gets the actor reference for a given partition key by computing a hash-based index into the children list.
    /// </summary>
    /// <typeparam name="TKey">The type of the partition key.</typeparam>
    /// <param name="key">The partition key used to determine the target actor.</param>
    /// <returns>The actor reference assigned to the partition for the given key.</returns>
    protected virtual IActorReference GetActorReference<TKey>(TKey key)
        where TKey : notnull
    {
        var hash = Math.Abs(GetHashcode(key));

        return Children[hash % Children.Count].Reference;
    }

    /// <summary>
    /// Computes a hash code for the given partition key.
    /// Override this method to customize the partitioning strategy.
    /// </summary>
    /// <typeparam name="TKey">The type of the partition key.</typeparam>
    /// <param name="key">The partition key to hash.</param>
    /// <returns>A hash code used to determine the partition index.</returns>
    protected virtual int GetHashcode<TKey>(TKey key)
        where TKey : notnull
    {
        return HashCode.Combine(key);
    }

    /// <summary>
    /// Called after all worker actors have been created during initialization.
    /// Override this method to perform additional initialization logic.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the initialization operation.</returns>
    protected virtual ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask();
    }

    /// <summary>
    /// Called when a child actor fails. Resets the restart counter, determines the failure action,
    /// and cancels any pending ask messages associated with the failure.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <param name="message">The message that was being processed when the failure occurred.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the failure handling operation.</returns>
    protected virtual async Task OnActorFailed(
        Child child,
        IMessage message,
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        ResetCounter(child);

        var action = GetFailureAction(child, exception);

        PartitionLog.GoingToApplyFailureAction(Logger, action, Strategy);
        if (action == FailureAction.Restart)
        {
            await ApplyRestartAsync(child);
        }
        else if (action == FailureAction.Stop)
        {
            await ApplyStopAsync(child);
        }
        else if (action == FailureAction.Escalate)
        {
            await ApplyEscalateAsync(child, message, exception);
        }
        else
        {
            await ApplyResumeAsync(child);
        }

        if (message is IAskMessage askMessage)
        {
            PartitionLog.CancelAskMessage(Logger);
            askMessage.SetCanceled();
        }

        if (exception is EscalateFailureException { ActorMessage: IAskMessage escalateAskMessage })
        {
            PartitionLog.CancelAskMessage(Logger);
            escalateAskMessage.SetCanceled();
        }
    }

    /// <summary>
    /// Called when a child actor terminates. Permanent actors are restarted;
    /// non-permanent actors have their references terminated.
    /// </summary>
    /// <param name="child">The metadata of the terminated child actor.</param>
    /// <param name="reason">The reason for termination, or <see langword="null"/> if not specified.</param>
    /// <returns>A task representing the termination handling operation.</returns>
    protected virtual ValueTask OnActorTerminated(Child child, string? reason)
    {
        if (child.RestartPolicy == RestartPolicy.Permanent)
        {
            return new ValueTask(ResetActorAsync(child));
        }
        else
        {
            child.Reference.Terminate(reason);
            return new ValueTask();
        }
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
    /// Creates and starts a new child actor from a <see cref="ChildSpecification"/>.
    /// </summary>
    /// <param name="specification">The specification defining the actor to create.</param>
    /// <returns>The metadata for the created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">Thrown if the supervisor has already been initialized.</exception>
    /// <exception cref="TooManyWorkerException">Thrown if the maximum number of workers has been reached.</exception>
    protected virtual async Task<Child> CreateActorAsync(ChildSpecification specification)
    {
        if (_initialized)
        {
            throw new SupervisorAlreadyInitializedException(
                "Supervisor has already been initialized. Cannot create new actors after initialization."
            );
        }

        if (Children.Count >= Workers)
        {
            throw new TooManyWorkerException(
                $"Cannot create more than {Workers} actors in a partition supervisor."
            );
        }

        var reference = new ActorReference(
            specification.ActorType,
            Context.ServiceProvider,
            specification.Mailbox
        );
        var actor = ActorFactory.CreateActor(typeof(TActor));
        actor.Context = new ActorContext(reference, Context.ServiceProvider.CreateAsyncScope());

        var process = new ActorProcess(actor, specification.Mailbox);
        process.Failed += HandleFailure;
        process.Terminated += HandleTermination;

        var child = new Child(
            actor,
            specification.Mailbox,
            process,
            reference,
            specification.RestartPolicy,
            typeof(TActor)
        );
        Children = Children.Add(child);

        await process.StartAsync(new TellMessage(new InitializeActor(), []));

        return child;
    }

    /// <summary>
    /// Handles failure events from child actor processes.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The failure event arguments.</param>
    protected virtual void HandleFailure(object? sender, ActorFailureEventArgs args)
    {
        using var _ = Logger.BeginScope("{ActorType}", args.Actor.GetType());
        PartitionLog.HandlingFailedActor(Logger, args.Exception);
        Context.Self.Tell(new ActorFailed(args.Actor, args.Message, args.Exception));
    }

    /// <summary>
    /// Handles termination events from child actor processes.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The termination event arguments.</param>
    protected virtual void HandleTermination(object? sender, ActorTerminateEventArgs args)
    {
        using var _ = Logger.BeginScope("{ActorType}", args.Actor.GetType());
        PartitionLog.HandlingTerminatedActor(Logger, args.Reason);
        Context.Self.Tell(new ActorTerminated(args.Actor, args.Reason));
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

        return new ValueTask();
    }

    /// <summary>
    /// Resets the restart counter if the restart window has elapsed.
    /// </summary>
    /// <param name="metadata">The actor metadata to check.</param>
    protected virtual void ResetCounter(Child metadata)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - metadata.LastRestartTime) > RestartWindow)
        {
            PartitionLog.ResettingActorCounter(Logger, metadata.ActorType);
            metadata.RestartCount = 0;
        }
    }

    /// <summary>
    /// Determines the appropriate failure action for a failed actor.
    /// </summary>
    /// <param name="metadata">The metadata of the failed actor.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>The action to take in response to the failure.</returns>
    protected virtual FailureAction GetFailureAction(Child metadata, Exception exception)
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
    protected virtual async Task ApplyStopAsync(Child metadata)
    {
        if (Strategy == Strategy.OneForOne)
        {
            await StopActorAsync(metadata);
        }
        else if (Strategy == Strategy.AllForOne)
        {
            await Task.WhenAll(Children.Select(StopActorAsync));
        }
    }

    /// <summary>
    /// Stops a single actor.
    /// </summary>
    /// <param name="metadata">The metadata of the actor to stop.</param>
    /// <returns>A task representing the stop operation.</returns>
    protected virtual async Task StopActorAsync(Child metadata)
    {
        PartitionLog.StoppingActor(Logger);

        metadata.Process.Failed -= HandleFailure;
        metadata.Process.Terminated -= HandleTermination;
        await metadata.Process.StopAsync();

        PartitionLog.ActorStopped(Logger);
    }

    /// <summary>
    /// Applies the resume action, allowing the actor to continue processing messages.
    /// </summary>
    /// <param name="metadata">The metadata of the actor to resume.</param>
    /// <returns>A task representing the resume operation.</returns>
    protected virtual async Task ApplyResumeAsync(Child metadata)
    {
        PartitionLog.ResumingActor(Logger);

        await metadata.Process.StopAsync();
        await metadata.Process.StartAsync();

        PartitionLog.ActorResumed(Logger);
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
        Child metadata,
        IMessage message,
        Exception exception
    )
    {
        PartitionLog.EscalatingError(Logger);

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
    protected virtual async Task ApplyRestartAsync(Child metadata)
    {
        metadata.RestartCount++;
        metadata.LastRestartTime = DateTimeOffset.UtcNow;

        if (Strategy == Strategy.OneForOne)
        {
            await ResetActorAsync(metadata);
        }
        else if (Strategy == Strategy.AllForOne)
        {
            await Task.WhenAll(Children.Select(ResetActorAsync));
        }
    }

    /// <summary>
    /// Resets an actor by stopping, disposing, and recreating it.
    /// </summary>
    /// <param name="child">The metadata of the actor to reset.</param>
    /// <returns>A task representing the reset operation.</returns>
    protected virtual async Task ResetActorAsync(Child child)
    {
        PartitionLog.ResettingActor(Logger);

        await StopActorAsync(child);
        await BeforeRestartActorAsync(child);

        await DisposeObjectAsync(child.Actor);
        await DisposeObjectAsync(child.Actor.Context);

        await ResetMailboxAsync(child);

        PartitionLog.CreatingNewActorInstance(Logger);
        child.Actor = ActorFactory.CreateActor(child.ActorType);
        child.Actor.Context = new ActorContext(
            child.Reference,
            Context.ServiceProvider.CreateAsyncScope()
        );
        PartitionLog.ActorCreatedWithSuccess(Logger);

        await child.Process.DisposeAsync();
        PartitionLog.CreateNewProcess(Logger);
        child.Process = new ActorProcess(child.Actor, child.Mailbox);
        child.Process.Failed += HandleFailure;
        child.Process.Terminated += HandleTermination;

        await child.Process.StartAsync(
            new TellMessage(new InitializeActor(), []),
            new TellMessage(new AfterRestartActor(), [])
        );

        PartitionLog.ActorProcessStarted(Logger);
    }

    /// <summary>
    /// Resets the mailbox for a supervisor actor during restart.
    /// </summary>
    /// <param name="metadata">The metadata of the actor.</param>
    /// <returns>A task representing the reset operation.</returns>
    protected virtual async ValueTask ResetMailboxAsync(Child metadata)
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
    protected virtual async ValueTask BeforeRestartActorAsync(Child metadata)
    {
        try
        {
            PartitionLog.CallBeforeRestartActor(Logger);
            await metadata.Actor.BeforeRestartAsync();
            PartitionLog.SuccessBeforeRestartActor(Logger);
        }
        catch (Exception ex)
        {
            PartitionLog.ErrorDuringCallBeforeRestartActor(Logger, ex);
        }
    }
}

internal static partial class PartitionLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Received failure notification from child actor"
    )]
    public static partial void HandlingFailedActor(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Received termination notification from child actor with reason: {Reason}"
    )]
    public static partial void HandlingTerminatedActor(ILogger logger, string? reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing actor failure for supervised child"
    )]
    public static partial void ProcessingFailedActor(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Failed Actor processed")]
    public static partial void FailedActorProcessed(ILogger logger);

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

    [LoggerMessage(Level = LogLevel.Trace, Message = "Actor resumed and ready to process messages")]
    public static partial void ActorResumed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Resetting actor state for restart")]
    public static partial void ResettingActor(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Actor restarted successfully")]
    public static partial void ActorRestarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Stopping actor process")]
    public static partial void StoppingActor(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Actor process stopped successfully")]
    public static partial void ActorStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Invoking BeforeRestartAsync on actor")]
    public static partial void CallBeforeRestartActor(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "BeforeRestartAsync completed successfully")]
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
    public static partial void ActorCreatedWithSuccess(ILogger logger);

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
    public static partial void EscalatingError(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Resetting restart counter for actor type {ActorType} after restart window elapsed"
    )]
    public static partial void ResettingActorCounter(ILogger logger, Type actorType);

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Processing termination ({Reason}) of child actor"
    )]
    public static partial void ProcessingTerminatedActor(ILogger logger, string? reason);

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Finished processing termination of child actor"
    )]
    public static partial void FinishedProcessingTerminatedActor(ILogger logger);
}
