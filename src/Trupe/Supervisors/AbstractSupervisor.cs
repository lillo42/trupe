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
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Supervisors;
using Trupe.Abstractions.Supervisors.Commands;
using Trupe.Abstractions.SystemMessages;
using Trupe.Guards;
using Trupe.Messages;

namespace Trupe.Supervisors;

/// <summary>
/// Abstract base class for implementing actor supervisors in the Trupe actor system.
/// Provides common supervision logic including failure handling, restart strategies,
/// child lifecycle management, and resource disposal.
/// </summary>
/// <param name="logger">The logger instance for supervisor operations.</param>
public abstract partial class AbstractSupervisor(ILogger logger)
    : Actor,
        ISupervisor,
        IHandleActorMessage<ActorProcessFailed>,
        IHandleActorMessage<ActorProcessStopped>,
        IActorProcessListener,
        IAsyncDisposable
{
    private bool _isDisposed;

    protected virtual bool IsDisposed => _isDisposed;

    /// <summary>
    /// Gets the logger used for supervisor operations.
    /// </summary>
    protected virtual ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the factory used to create actor instances.
    /// </summary>
    protected virtual IActorFactory ActorFactory =>
        Context.ServiceProvider.GetRequiredService<IActorFactory>();

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
    /// Calls <see cref="OnInitializeAsync"/> to allow derived classes to perform initialization logic.
    /// </remarks>
    public override async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.InitializingSupervisor(Logger);

            await OnInitializeAsync(cancellationToken);

            Log.SupervisoInitialized(Logger);
        }
    }

    /// <summary>
    /// Called during supervisor initialization to set up child actors.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
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
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        Log.BeforeRestartSupervisor(Logger, Children.Count);

        foreach (var child in Children)
        {
            var ctx = child.Actor.Context;
            await DisposeObjectAsync(child.Actor);
            await DisposeObjectAsync(ctx);
            await DisposeObjectAsync(child.Process);

            child.Actor = null!;
            child.Process = null!;
            child.Metadata.Clear();
        }

        Children = [];

        Log.BeforeRestartSupervisorCompleted(Logger);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Routes <see cref="ActorProcessFailed"/> and <see cref="ActorProcessStopped"/>
    /// messages to their respective typed handlers before falling back to the base implementation.
    /// </remarks>
    public override async ValueTask HandleAsync(
        object? message,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        Log.HandlingMessage(Logger, message?.GetType());

        if (message is ActorProcessFailed failed)
        {
            await HandleAsync(failed, cancellationToken);
        }
        else if (message is ActorProcessStopped stopped)
        {
            await HandleAsync(stopped, cancellationToken);
        }
        else
        {
            using (Logger.BeginScope("{MessageType}", message?.GetType()))
            using (Logger.BeginScope("{SupervisorName}", Context.Name))
            {
                await base.HandleAsync(message, cancellationToken);
            }
        }

        Log.MessageHandled(Logger, message?.GetType());
    }

    /// <summary>
    /// Handles the <see cref="ActorProcessFailed"/> message when a child actor process encounters an error.
    /// </summary>
    /// <param name="message">The failure information including the process, message, and exception.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the failure handling operation.</returns>
    public virtual async ValueTask HandleAsync(
        ActorProcessFailed message,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        using (Logger.BeginScope("{MessageType}", message.GetType()))
        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.HandlingActorProcessFailed(
                Logger,
                message.Exception,
                message.Message.Payload.GetType(),
                message.Process.Actor.Context.Name
            );

            var child = Children.FirstOrDefault(x => x.Process == message.Process);
            if (child == null)
            {
                Log.ChildNotFound(Logger, message.Process.Actor.Context.Name);
                return;
            }

            await OnActorProcessFailedAsync(
                child,
                message.Message,
                message.Exception,
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Handles the <see cref="ActorProcessStopped"/> message when a child actor process is stopped.
    /// </summary>
    /// <param name="message">The stopped information including the process and reason.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the stopped handling operation.</returns>
    public virtual async ValueTask HandleAsync(
        ActorProcessStopped message,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        using (Logger.BeginScope("{MessageType}", message.GetType()))
        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.HandlingActorProcessStopped(Logger, message.Process.Actor.Context.Name);

            var child = Children.FirstOrDefault(x => x.Process == message.Process);
            if (child == null)
            {
                Log.ChildNotFound(Logger, message.Process.Actor.Context.Name);
                return;
            }

            await OnActorProcessStoppedAsync(
                child,
                message.Reason ?? TerminatedReason.Stopped,
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Called when a child actor process fails. Applies the appropriate failure action based on
    /// restart policy and supervision strategy.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <param name="message">The message that was being processed when the failure occurred.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the failure handling operation.</returns>
    protected virtual async Task OnActorProcessFailedAsync(
        Child child,
        IMessage message,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        if (child.RestartPolicy == RestartPolicy.Temporary)
        {
            Log.TemporaryActorFail(Logger, child.ActorType, child.Actor.Context.Name);
            await StopAsync(child);
            return;
        }

        ResetCounter(child);

        var action = ResolveFailureAction(child, exception);
        Log.ExecutingFailureAction(
            Logger,
            action,
            child.ActorType,
            child.Name,
            child.RestartCount,
            MaxRestarts
        );

        if (action == FailureAction.Restart)
        {
            await RestartAsync(child);
        }
        else if (action == FailureAction.Stop)
        {
            await StopAsync(child);
        }
        else if (action == FailureAction.Escalate)
        {
            await EscalateAsync(child, message, exception);
        }
        else
        {
            await ResumeActorAsync(child);
        }

        if (message is IAskMessage askMessage)
        {
            askMessage.SetCanceled();
        }

        if (exception is EscalateFailureException { ActorMessage: IAskMessage escalateAskMessage })
        {
            escalateAskMessage.SetCanceled();
        }
    }

    /// <summary>
    /// Determines the appropriate failure action for a failed child actor.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>The <see cref="FailureAction"/> to take in response to the failure.</returns>
    protected virtual FailureAction ResolveFailureAction(Child child, Exception exception)
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
    protected virtual async Task StopAsync(Child child)
    {
        Log.StoppingChild(Logger, Strategy);

        if (Strategy == Strategy.OneForOne)
        {
            await StopActorAsync(child, TerminatedReason.Stopped);
        }
        else if (Strategy == Strategy.AllForOne)
        {
            await Task.WhenAll(Children.Select(x => StopActorAsync(x, TerminatedReason.Stopped)));
        }

        Log.StoppingChild(Logger, Strategy);
    }

    /// <summary>
    /// Stops a single actor process and marks its reference as terminated.
    /// </summary>
    /// <param name="child">The metadata of the actor to stop.</param>
    /// <param name="reason">The reason for termination.</param>
    /// <returns>A task representing the stop operation.</returns>
    protected virtual async Task StopActorAsync(Child child, TerminatedReason reason)
    {
        Log.StoppingActor(Logger, child.ActorType, child.Name);

        await child.Process.KillAsync();
        child.Actor.Context.Self.MarkAsTerminate(reason);

        Log.ActorStopped(Logger, child.ActorType, child.Name);
    }

    /// <summary>
    /// Escalates the failure to the parent supervisor by throwing an <see cref="EscalateFailureException"/>.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <param name="message">The message that caused the failure.</param>
    /// <param name="exception">The original exception.</param>
    /// <exception cref="EscalateFailureException">Always thrown to escalate to the parent supervisor.</exception>
    [DoesNotReturn]
    protected virtual async Task EscalateAsync(Child child, IMessage message, Exception exception)
    {
        Log.EscalingToParent(
            Logger,
            exception,
            child.ActorType,
            child.Name,
            message.Payload.GetType()
        );

        throw new EscalateFailureException(
            "Unable to handle actor failure",
            child.Reference,
            message,
            exception
        );
    }

    /// <summary>
    /// Resumes an actor by restarting its message processing loop.
    /// </summary>
    /// <param name="child">The metadata of the actor to resume.</param>
    /// <returns>A task representing the resume operation.</returns>
    protected virtual async Task ResumeActorAsync(Child child)
    {
        Log.ResumingActor(Logger, child.ActorType, child.Name);

        await child.Process.StartAsync();

        Log.ActorResumed(Logger, child.ActorType, child.Name);
    }

    /// <summary>
    /// Applies the restart action to the failed actor(s) based on the supervision strategy.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <returns>A task representing the restart operation.</returns>
    protected virtual async Task RestartAsync(Child child)
    {
        Log.RestartingChild(Logger, Strategy);

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

        Log.ChildRestarted(Logger, Strategy);
    }

    /// <summary>
    /// Resets an actor by stopping, disposing, and recreating it with a fresh instance.
    /// </summary>
    /// <param name="child">The metadata of the actor to reset.</param>
    /// <returns>A task representing the reset operation.</returns>
    protected virtual async Task ResetActorAsync(Child child)
    {
        Log.RestartingActor(Logger, child.ActorType, child.Name);
        var reference = child.Reference;

        await BeforeRestartActorAsync(child);

        var ctx = child.Actor.Context;
        await DisposeObjectAsync(child.Actor);
        await DisposeObjectAsync(ctx);

        var mailbox = await GetOrCreateMailboxAsync(child);

        child.Actor = ActorFactory.CreateActor(child.ActorType);

        child.Process.Actor = child.Actor;
        child.Process.Mailbox = mailbox;

        child.Actor.Context = new ActorContext(
            reference,
            Context.ServiceProvider.CreateAsyncScope()
        );

        await child.Process.StartAsync(
            new TellMessage(new InitializeActor(), []),
            new TellMessage(new AfterRestartActor(), [])
        );

        Log.ActorRestarted(Logger, child.ActorType, child.Name);
    }

    /// <summary>
    /// Calls the actor's <see cref="IActor.BeforeRestartAsync"/> method before restarting.
    /// Exceptions are swallowed to avoid interfering with the restart process.
    /// </summary>
    /// <param name="child">The metadata of the actor being restarted.</param>
    /// <returns>A task representing the operation.</returns>
    protected virtual async ValueTask BeforeRestartActorAsync(Child child)
    {
        try
        {
            Log.CallingBeforeRestartActor(Logger, child.ActorType, child.Name);

            await child.Actor.BeforeRestartAsync();

            Log.BeforeRestartActorCalled(Logger, child.ActorType, child.Name);
        }
        catch (Exception ex)
        {
            Log.ErrorToCallBeforeRestartActor(Logger, ex, child.ActorType, child.Name);
        }
    }

    /// <summary>
    /// Gets or creates a mailbox for a child actor during restart.
    /// By default, returns the existing mailbox from the child's process.
    /// </summary>
    /// <param name="child">The metadata of the actor.</param>
    /// <returns>The mailbox to use for the restarted actor.</returns>
    protected virtual ValueTask<IMailbox> GetOrCreateMailboxAsync(Child child)
    {
        return new ValueTask<IMailbox>(child.Process.Mailbox);
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
            Log.RestartCounterReset(Logger, child.ActorType, child.Name);
            child.RestartCount = 0;
        }
    }

    /// <summary>
    /// Creates a new child actor from the given specification without starting it.
    /// </summary>
    /// <param name="specification">The specification defining the actor to create.</param>
    /// <returns>The metadata for the created child actor.</returns>
    protected virtual Child CreateActor(IChildSpecification specification)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        Log.CreatingActor(Logger, specification.ActorType.GetType(), specification.Name);

        var actor = ActorFactory.CreateActor(specification.ActorType);
        var process = new ActorProcess(
            actor,
            specification.MailboxFactory(Context.ServiceProvider)
        );

        var factory = Context.ServiceProvider.GetRequiredService<IActorReferenceFactory>();
        var reference = factory.Create(specification.Name, process);

        actor.Context = new ActorContext(reference, Context.ServiceProvider.CreateAsyncScope());

        return new Child(
            actor,
            process,
            specification.RestartPolicy,
            specification.MailboxFactory,
            specification.ActorType
        );
    }

    /// <summary>
    /// Starts the actor process and subscribes to its lifecycle events.
    /// </summary>
    /// <param name="child">The child actor to start.</param>
    /// <returns>A task representing the start operation.</returns>
    protected virtual async Task StartActorAsync(Child child)
    {
        Log.StartingActor(Logger, child.ActorType, child.Name);

        child.Process.Register(this);

        await child.Process.StartAsync(new TellMessage(new InitializeActor(), []));

        Log.ActorStarted(Logger, child.ActorType, child.Name);
    }

    public virtual void OnFailed(IActorProcess process, IMessage message, Exception exception)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        Log.ReceivedActorProcessFailed(
            Logger,
            exception,
            message.GetType(),
            process.Actor.GetType(),
            process.Actor.Context.Name
        );

        Context.Self.Tell(new ActorProcessFailed(process, message, exception));
    }

    public virtual void OnStopped(IActorProcess process, TerminatedReason reason)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, GetType().Name);

        Log.ReceivedActorProcessStopped(
            Logger,
            reason,
            process.Actor.GetType(),
            process.Actor.Context.Name
        );

        Context.Self.Tell(new ActorProcessStopped(process, reason));
    }

    /// <summary>
    /// Called when a child actor process is stopped. Restarts permanent actors or
    /// terminates non-permanent ones.
    /// </summary>
    /// <param name="child">The metadata of the stopped child actor.</param>
    /// <param name="reason">The reason for stopping.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the termination handling operation.</returns>
    protected virtual async ValueTask OnActorProcessStoppedAsync(
        Child child,
        TerminatedReason reason,
        CancellationToken cancellationToken = default
    )
    {
        if (child.RestartPolicy == RestartPolicy.Permanent)
        {
            Log.PermanentRestart(Logger, child.ActorType, child.Name);
            await ResetActorAsync(child);
        }
        else
        {
            Log.NonPermanentRestart(Logger, child.ActorType, child.Name);
            await StopActorAsync(child, reason);
        }
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

    /// <inheritdoc />
    /// <remarks>
    /// Disposes all child actors, their contexts and processes, and clears the children list.
    /// </remarks>
    public virtual async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);

        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            await using var sp = Context.ServiceProvider.CreateAsyncScope();
            var registry = sp.ServiceProvider.GetRequiredService<IActorProcessRegistry>();

            foreach (var child in Children)
            {
                registry.UnRegister(child.Actor.Context.Self);

                var ctx = child.Actor.Context;
                await DisposeObjectAsync(child.Process);
                await DisposeObjectAsync(child.Actor);
                await DisposeObjectAsync(ctx);

                child.Actor = null!;
                child.Process = null!;
                child.Metadata.Clear();
            }

            Children = [];
        }

        _isDisposed = true;
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Initializing supervisor")]
        public static partial void InitializingSupervisor(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Supervisor initialized")]
        public static partial void SupervisoInitialized(ILogger logger);

        [LoggerMessage(
            LogLevel.Information,
            "Before restart supervisor, going to dispose {ChildCount} children"
        )]
        public static partial void BeforeRestartSupervisor(ILogger logger, int childCount);

        [LoggerMessage(LogLevel.Information, "Before restart supervisor completed")]
        public static partial void BeforeRestartSupervisorCompleted(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Hanlding {MessageType} message")]
        public static partial void HandlingMessage(ILogger logger, Type? messageType);

        [LoggerMessage(LogLevel.Debug, "{MessageType} message handled with sucess")]
        public static partial void MessageHandled(ILogger logger, Type? messageType);

        [LoggerMessage(
            LogLevel.Information,
            "Handling failed process for {FailedMessageType} message in {ActorName} actor"
        )]
        public static partial void HandlingActorProcessFailed(
            ILogger logger,
            Exception exception,
            Type failedMessageType,
            Uri actorName
        );

        [LoggerMessage(LogLevel.Error, "Child with {ActorName} name not found")]
        public static partial void ChildNotFound(ILogger logger, Uri actorName);

        [LoggerMessage(LogLevel.Information, "Handling stopped process for {ActorName} actor")]
        public static partial void HandlingActorProcessStopped(ILogger logger, Uri actorName);

        [LoggerMessage(
            LogLevel.Information,
            "Temporary child actor failed, stopping {ActorType} with {ActorName} name"
        )]
        public static partial void TemporaryActorFail(
            ILogger logger,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(LogLevel.Debug, "Reset restart counter for {ActorType} actor {ActorName}")]
        public static partial void RestartCounterReset(
            ILogger logger,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(
            LogLevel.Information,
            "Going to apply {FailureAction} in {ActorType} for {ActorName} actor, restart count: {RestartCounter} / {MaxRestarts}"
        )]
        public static partial void ExecutingFailureAction(
            ILogger logger,
            FailureAction failureAction,
            Type actorType,
            Uri actorName,
            int restartCounter,
            int maxRestarts
        );

        [LoggerMessage(LogLevel.Information, "Stopping actor(s) with {Strategy}")]
        public static partial void StoppingChild(ILogger logger, Strategy strategy);

        [LoggerMessage(LogLevel.Information, "Stopped actor(s) with {Strategy}")]
        public static partial void ChildStopped(ILogger logger, Strategy strategy);

        [LoggerMessage(LogLevel.Information, "Stopping {ActorType} actor {ActorName}")]
        public static partial void StoppingActor(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(LogLevel.Information, "{ActorType} actor {ActorName} stopped")]
        public static partial void ActorStopped(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(LogLevel.Information, "Restarting actor(s) with {Strategy}")]
        public static partial void RestartingChild(ILogger logger, Strategy strategy);

        [LoggerMessage(LogLevel.Information, "Restarted actor(s) with {Strategy}")]
        public static partial void ChildRestarted(ILogger logger, Strategy strategy);

        [LoggerMessage(LogLevel.Information, "Restarting {ActorType} actor {ActorName}")]
        public static partial void RestartingActor(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(LogLevel.Information, "{ActorType} actor {ActorName} Restarted")]
        public static partial void ActorRestarted(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(LogLevel.Debug, "Calling Before Restart {ActorType} actor {ActorName}")]
        public static partial void CallingBeforeRestartActor(
            ILogger logger,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(LogLevel.Debug, "Called Before Restart {ActorType} actor {ActorName}")]
        public static partial void BeforeRestartActorCalled(
            ILogger logger,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(
            LogLevel.Error,
            "Error during calling Before Restart {ActorType} actor {ActorName}"
        )]
        public static partial void ErrorToCallBeforeRestartActor(
            ILogger logger,
            Exception exception,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(
            LogLevel.Information,
            "Escalating error to parent, source of error {ActorType} actor {ActorName} with {SourceMessageType}"
        )]
        public static partial void EscalingToParent(
            ILogger logger,
            Exception ex,
            Type actorType,
            Uri actorName,
            Type sourceMessageType
        );

        [LoggerMessage(LogLevel.Information, "Resuming {ActorType} actor {ActorName}")]
        public static partial void ResumingActor(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(LogLevel.Information, "{ActorType} actor {ActorName} resumed")]
        public static partial void ActorResumed(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(
            LogLevel.Information,
            "Creating actor with {ActorType} type and {ActorName} name"
        )]
        public static partial void CreatingActor(ILogger logger, Type actorType, string actorName);

        [LoggerMessage(LogLevel.Information, "Starting {ActorType} actor {ActorName}")]
        public static partial void StartingActor(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(LogLevel.Information, "Started {ActorType} actor {ActorName}")]
        public static partial void ActorStarted(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(
            LogLevel.Debug,
            "Received acttor process failed with {FailedMessageType} for {ActorType} actor {ActorName}"
        )]
        public static partial void ReceivedActorProcessFailed(
            ILogger logger,
            Exception exception,
            Type failedMessageType,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(
            LogLevel.Debug,
            "Received acttor process stopped with {StoppedReason} for {ActorType} actor {ActorName}"
        )]
        public static partial void ReceivedActorProcessStopped(
            ILogger logger,
            TerminatedReason stoppedReason,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(
            LogLevel.Information,
            "Permanent restart policy going to be apply for {ActorType} actor {ActorName}"
        )]
        public static partial void PermanentRestart(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(
            LogLevel.Information,
            "Non-permanent child actor stopped for {ActorType} actor {ActorName}"
        )]
        public static partial void NonPermanentRestart(
            ILogger logger,
            Type actorType,
            Uri actorName
        );
    }
}
