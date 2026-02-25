using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Factories;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Supervisors;
using Trupe.Abstractions.SystemMessages;
using Trupe.ActorReferences;
using Trupe.Messages;
using Trupe.Supervisors.Commands;

namespace Trupe.Supervisors;

/// <summary>
/// Abstract base class for implementing actor supervisors in the Trupe actor system.
/// </summary>
/// <remarks>
/// <para>
/// Supervisors are responsible for managing the lifecycle of child actors, including
/// creation, monitoring, and failure handling. They implement supervision strategies
/// that determine how to respond when child actors fail.
/// </para>
/// <para>
/// This class provides built-in support for:
/// <list type="bullet">
/// <item><description>One-for-one and all-for-one supervision strategies</description></item>
/// <item><description>Configurable restart limits and time windows</description></item>
/// <item><description>Automatic failure escalation when restart limits are exceeded</description></item>
/// <item><description>Child actor lifecycle management (start, stop, restart)</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="actorFactory">Factory used to create actor instances.</param>
/// <param name="logger">Logger for supervisor operations.</param>
public abstract partial class Supervisor(IActorFactory actorFactory, ILogger logger)
    : Actor,
        ISupervisor,
        IHandleActorMessage<AddActor>,
        IHandleActorMessage<ActorFailed>,
        IHandleActorMessage<ActorTerminated>,
        IAsyncDisposable
{
    /// <summary>
    /// Indicates whether the supervisor has completed initialization.
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// Gets the logger used for supervisor operations.
    /// </summary>
    protected virtual ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the factory used to create actor instances.
    /// </summary>
    protected virtual IActorFactory ActorFactory { get; } = actorFactory;

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
    /// Gets or sets the immutable list of child actors managed by this supervisor.
    /// </summary>
    protected ImmutableList<Child> Children { get; set; } = [];

    /// <summary>
    /// Gets the references to all child actors managed by this supervisor.
    /// </summary>
    IEnumerable<IActorReference> ISupervisor.Children => Children.Select(x => x.Reference);

    /// <inheritdoc />
    /// <remarks>
    /// Calls <see cref="OnInitializeAsync"/> and marks the supervisor as initialized.
    /// After initialization, <see cref="AddChild{TActor}()"/> cannot be called synchronously.
    /// </remarks>
    public sealed override async ValueTask InitializeAsync(
        CancellationToken cancellationToken = default
    )
    {
        await OnInitializeAsync(cancellationToken);

        _initialized = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Routes <see cref="AddActor"/>, <see cref="ActorFailed"/>, and <see cref="ActorTerminated"/>
    /// messages to their respective typed handlers before falling back to the base implementation.
    /// </remarks>
    public override ValueTask HandleAsync(
        object? message,
        CancellationToken cancellationToken = default
    )
    {
        if (message is AddActor addActor)
        {
            return HandleAsync(addActor, cancellationToken);
        }
        else if (message is ActorFailed actorFailed)
        {
            return HandleAsync(actorFailed, cancellationToken);
        }
        else if (message is ActorTerminated actorTerminated)
        {
            return HandleAsync(actorTerminated, cancellationToken);
        }

        return base.HandleAsync(message, cancellationToken);
    }

    /// <summary>
    /// Handles the <see cref="AddActor"/> command to create and register a new child actor.
    /// </summary>
    /// <param name="message">The command containing actor creation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public virtual ValueTask HandleAsync(AddActor message, CancellationToken cancellationToken)
    {
        CreateActor(message.Specification, (LocalActorReference)message.Reference);

        return new ValueTask();
    }

    /// <summary>
    /// Handles the <see cref="ActorFailed"/> command when a child actor encounters an error.
    /// </summary>
    /// <param name="message">The failure information including the actor, message, and exception.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the failure handling operation.</returns>
    /// <remarks>
    /// This method determines the appropriate <see cref="FailureAction"/> and applies it
    /// according to the configured <see cref="Strategy"/>.
    /// </remarks>
    public virtual async ValueTask HandleAsync(
        ActorFailed message,
        CancellationToken cancellationToken = default
    )
    {
        Log.ProcessingFailedActor(Logger, message.Exception);

        var children = Children.FirstOrDefault(x => x.Actor == message.Actor);
        if (children == null)
        {
            Log.ActorNotFound(Logger);
            return;
        }

        await OnActorFailedAsync(children, message.Message, message.Exception, cancellationToken);

        Log.FailedActorProcessed(Logger, message.Exception);
    }

    /// <summary>
    /// Handles the <see cref="ActorTerminated"/> command when a child actor is terminated.
    /// </summary>
    /// <param name="message">The termination information including the actor and reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the termination handling operation.</returns>
    public virtual async ValueTask HandleAsync(
        ActorTerminated message,
        CancellationToken cancellationToken = default
    )
    {
        Log.ProcessingTerminatedActor(Logger, message.Reason);

        var metadata = Children.FirstOrDefault(x => x.Actor == message.Actor);
        if (metadata == null)
        {
            Log.ActorNotFound(Logger);
            return;
        }

        await OnActorTerminatedAsync(metadata, message.Reason, cancellationToken);

        Log.FinishedProcessingTerminatedActor(Logger);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Disposes all child actors and clears the actor list.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        foreach (var metadata in Children)
        {
            await StopActorAsync(metadata);
            await DisposeObjectAsync(metadata.Actor);

            await metadata.Process.DisposeAsync();

            metadata.Actor = null!;
            metadata.Process = null!;
            metadata.Metadata.Clear();
        }

        Children = [];
    }

    /// <summary>
    /// Adds a child actor of the specified type with a default mailbox.
    /// </summary>
    /// <typeparam name="TActor">The type of actor to create.</typeparam>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual IActorReference AddChild<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActor
    >()
        where TActor : IActor
    {
        return AddChild(typeof(TActor));
    }

    /// <summary>
    /// Adds a child actor of the specified type with a default mailbox.
    /// </summary>
    /// <param name="actorType">The type of actor to create.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual IActorReference AddChild(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType
    )
    {
        return AddChild(new ChildSpecification(actorType));
    }

    /// <summary>
    /// Adds a child actor using the specified specification.
    /// </summary>
    /// <param name="specification">The specification defining the child actor to create.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual IActorReference AddChild(IChildSpecification specification)
    {
        if (_initialized)
        {
            throw new SupervisorAlreadyInitializedException(
                "Supervisor already initialized, it's preemptive"
            );
        }

        var actorRef = new LocalActorReference(specification.Mailbox);
        Context.Self.Tell(new AddActor(specification, actorRef));

        return actorRef;
    }

    /// <summary>
    /// Asynchronously adds a child actor of the specified type with a default mailbox.
    /// </summary>
    /// <typeparam name="TActor">The type of actor to create.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual ValueTask<IActorReference> AddChildAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActor
    >(CancellationToken cancellationToken = default)
        where TActor : IActor
    {
        return AddChildAsync(typeof(TActor), cancellationToken);
    }

    /// <summary>
    /// Asynchronously adds a child actor of the specified type with a default mailbox.
    /// </summary>
    /// <param name="actorType">The type of actor to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual ValueTask<IActorReference> AddChildAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType,
        CancellationToken cancellationToken = default
    )
    {
        return AddChildAsync(new ChildSpecification(actorType), cancellationToken);
    }

    /// <summary>
    /// Asynchronously adds a child actor using the specified specification.
    /// </summary>
    /// <param name="specification">The specification defining the child actor to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual ValueTask<IActorReference> AddChildAsync(
        IChildSpecification specification,
        CancellationToken cancellationToken = default
    )
    {
        if (_initialized)
        {
            throw new SupervisorAlreadyInitializedException(
                "Supervisor already initialized, it's preemptive"
            );
        }

        var actorRef = new LocalActorReference(specification.Mailbox);

        var val = Context.Self.TellAsync(new AddActor(specification, actorRef), cancellationToken);

        if (val.IsCompletedSuccessfully)
        {
            return new ValueTask<IActorReference>(actorRef);
        }
        else
        {
            return new ValueTask<IActorReference>(AwaitAddChildAsync(val.AsTask(), actorRef));
        }

        static async Task<IActorReference> AwaitAddChildAsync(Task val, IActorReference actorRef)
        {
            await val;
            return actorRef;
        }
    }

    /// <summary>
    /// Resets the restart counter if the restart window has elapsed.
    /// </summary>
    /// <param name="child">The child actor metadata to check.</param>
    protected virtual void ResetCounter(Child child)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - child.LastRestartTime) > RestartWindow)
        {
            Log.ResettingActorRestartCounter(Logger, child.ActorType);
            child.RestartCount = 0;
        }
    }

    /// <summary>
    /// Determines the appropriate failure action for a failed child actor.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>The <see cref="FailureAction"/> to take in response to the failure.</returns>
    protected virtual FailureAction GetFailureAction(Child child, Exception exception)
    {
        if (child.RestartCount >= MaxRestarts)
        {
            return FailureAction.Escalate;
        }

        return FailureAction.Restart;
    }

    /// <summary>
    /// Applies the stop action to the failed actor(s) based on the supervision strategy.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <returns>A task representing the stop operation.</returns>
    protected virtual async Task ApplyStopAsync(Child child)
    {
        if (Strategy == Strategy.OneForOne)
        {
            await StopActorAsync(child);
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
        Log.StoppingActor(Logger);
        metadata.Process.Failure -= HandleFailure;
        metadata.Process.Terminate -= HandleTermination;
        await metadata.Process.StopAsync();
        Log.ActorStopped(Logger);
    }

    /// <summary>
    /// Applies the resume action, allowing the actor to continue processing messages.
    /// </summary>
    /// <param name="child">The metadata of the actor to resume.</param>
    /// <returns>A task representing the resume operation.</returns>
    protected virtual async Task ApplyResumeAsync(Child child)
    {
        Log.ResumingActor(Logger);
        await child.Process.StopAsync();
        child.Process.Start();
        Log.ActorResumed(Logger);
    }

    /// <summary>
    /// Escalates the failure to the parent supervisor by throwing an <see cref="EscalateFailureException"/>.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <param name="message">The message that caused the failure.</param>
    /// <param name="exception">The original exception.</param>
    /// <returns>A task representing the escalation operation.</returns>
    /// <exception cref="EscalateFailureException">Always thrown to escalate to the parent supervisor.</exception>
    protected virtual async Task ApplyEscalateAsync(
        Child child,
        IMessage message,
        Exception exception
    )
    {
        Log.EscalatingError(Logger);
        await child.Process.StopAsync();
        throw new EscalateFailureException(
            "Unable to handle actor failure",
            child.Reference,
            message,
            exception
        );
    }

    /// <summary>
    /// Applies the restart action to the failed actor(s) based on the supervision strategy.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <returns>A task representing the restart operation.</returns>
    protected virtual async Task ApplyRestartAsync(Child child)
    {
        child.RestartCount++;
        child.LastRestartTime = DateTimeOffset.UtcNow;

        if (Strategy == Strategy.OneForOne)
        {
            await ResetActorAsync(child);
        }
        else if (Strategy == Strategy.AllForOne)
        {
            await Task.WhenAll(Children.Select(ResetActorAsync));
        }
    }

    /// <summary>
    /// Called when a child actor is terminated. Restarts permanent actors or terminates non-permanent ones.
    /// </summary>
    /// <param name="child">The metadata of the terminated child actor.</param>
    /// <param name="reason">The optional reason for termination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the termination handling operation.</returns>
    protected virtual async ValueTask OnActorTerminatedAsync(
        Child child,
        string? reason,
        CancellationToken cancellationToken = default
    )
    {
        if (child.RestartPolicy == RestartPolicy.Permanent)
        {
            await ResetActorAsync(child);
        }
        else
        {
            child.Reference.Terminate(reason);
        }
    }

    /// <summary>
    /// Called when a child actor fails. Applies the appropriate failure action based on restart policy and strategy.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <param name="message">The message that was being processed when the failure occurred.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the failure handling operation.</returns>
    protected virtual async Task OnActorFailedAsync(
        Child child,
        IMessage message,
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        if (child.RestartPolicy == RestartPolicy.Temporary)
        {
            await ApplyStopAsync(child);
            return;
        }

        ResetCounter(child);

        var action = GetFailureAction(child, exception);
        Log.GoingToApplyFailureAction(Logger, action, Strategy);

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
            Log.CancelAskMessage(Logger);
            askMessage.SetCanceled();
        }

        if (exception is EscalateFailureException { ActorMessage: IAskMessage escalateAskMessage })
        {
            Log.CancelAskMessage(Logger);
            escalateAskMessage.SetCanceled();
        }
    }

    /// <summary>
    /// Resets an actor by stopping, disposing, and recreating it with a fresh instance.
    /// </summary>
    /// <param name="child">The metadata of the actor to reset.</param>
    /// <returns>A task representing the reset operation.</returns>
    protected virtual async Task ResetActorAsync(Child child)
    {
        Log.ResettingActor(Logger);
        await StopActorAsync(child);
        await BeforeRestartActorAsync(child);

        await DisposeObjectAsync(child.Actor);
        await ResetMailboxAsync(child);

        Log.CreatingNewActorInstance(Logger);
        child.Actor = ActorFactory.CreateActor(child.ActorType);
        child.Actor.Context = new ActorContext(child.Reference);
        Log.ActorCreatedWithSuccess(Logger);

        await child.Process.DisposeAsync();
        Log.CreateNewProcess(Logger);
        child.Process = new ActorProcess(child.Actor, child.Mailbox);
        child.Process.Failure += HandleFailure;
        child.Process.Terminate += HandleTermination;

        child.Process.Start(
            new LocalTellMessage(new InitializeActor()),
            new LocalTellMessage(new AfterRestartActor())
        );
        Log.ActorProcessStarted(Logger);
    }

    /// <summary>
    /// Calls the actor's <see cref="IActor.BeforeRestartAsync"/> method before restarting.
    /// </summary>
    /// <param name="child">The metadata of the actor being restarted.</param>
    /// <returns>A task representing the operation.</returns>
    protected virtual async ValueTask BeforeRestartActorAsync(Child child)
    {
        try
        {
            Log.CallBeforeRestartActor(Logger);
            await child.Actor.BeforeRestartAsync();
            Log.SuccessBeforeRestartActor(Logger);
        }
        catch (Exception ex)
        {
            Log.ErrorDuringCallBeforeRestartActor(Logger, ex);
        }
    }

    /// <summary>
    /// Handles failure events from child actor processes.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The failure event arguments.</param>
    protected virtual void HandleFailure(object? sender, ActorFailureEventArgs args)
    {
        Log.HandlingFailedActor(Logger, args.Exception);
        Context.Self.Tell(new ActorFailed(args.Actor, args.Message, args.Exception));
    }

    /// <summary>
    /// Handles termination events from child actor processes.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The termination event arguments.</param>
    protected virtual void HandleTermination(object? sender, ActorTerminateEventArgs args)
    {
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
    /// Resets the mailbox for a supervisor actor during restart.
    /// </summary>
    /// <param name="child">The metadata of the actor.</param>
    /// <returns>A task representing the reset operation.</returns>
    protected virtual async ValueTask ResetMailboxAsync(Child child)
    {
        if (child.IsSupervisor)
        {
            await child.Mailbox.CleanAsync();
        }
    }

    /// <summary>
    /// Creates a new child actor from the given specification and registers it in the children list.
    /// </summary>
    /// <param name="specification">The specification defining the actor to create.</param>
    /// <param name="reference">The local actor reference to associate with the child.</param>
    /// <returns>The metadata for the created child actor.</returns>
    protected virtual Child CreateActor(
        IChildSpecification specification,
        LocalActorReference reference
    )
    {
        var actor = ActorFactory.CreateActor(specification.ActorType);
        actor.Context = new ActorContext(reference);

        var process = new ActorProcess(actor, specification.Mailbox);
        process.Failure += HandleFailure;
        process.Terminate += HandleTermination;

        var metadata = new Child(
            actor,
            specification.Mailbox,
            process,
            reference,
            specification.RestartPolicy
        );
        Children = Children.Add(metadata);

        process.Start(new LocalTellMessage(new InitializeActor()));

        return metadata;
    }

    /// <summary>
    /// Called during supervisor initialization to set up child actors.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    protected abstract ValueTask OnInitializeAsync(CancellationToken cancellationToken = default);

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

            metadata.Actor = null!;
            metadata.Process = null!;
            metadata.Metadata.Clear();
        }

        Children = [];
    }

    private static partial class Log
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

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed actor processed")]
        public static partial void FailedActorProcessed(ILogger logger, Exception exception);

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
        public static partial void ResettingActor(ILogger logger);

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
            Message = "Resetting restart counter for actor type '{ActorType}' after restart window elapsed"
        )]
        public static partial void ResettingActorRestartCounter(ILogger logger, Type actorType);

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
}
