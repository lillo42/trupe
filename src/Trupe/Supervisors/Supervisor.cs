using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    protected virtual TimeSpan RestartWindow { get; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the list of supervised actor metadata.
    /// </summary>
    protected ImmutableList<ActorMetadata> Actors { get; private set; } = [];

    /// <summary>
    /// Gets the references to all child actors managed by this supervisor.
    /// </summary>
    public IEnumerable<IActorReference> Children => Actors.Select(x => x.Reference);

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

    /// <summary>
    /// Handles the <see cref="AddActor"/> command to create and register a new child actor.
    /// </summary>
    /// <param name="message">The command containing actor creation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public virtual ValueTask HandleAsync(AddActor message, CancellationToken cancellationToken)
    {
        CreateActor(message.ActorType, message.Mailbox, message.Reference);

        return ValueTask.CompletedTask;
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
        var metadata = Actors.FirstOrDefault(x => x.Actor == message.Actor);
        if (metadata == null)
        {
            return;
        }

        ResetCounter(metadata);

        var action = GetFailureAction(metadata, message.Exception);
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
            askMessage.SetCanceled();
        }

        if (
            message.Exception is EscalateFailureException
            {
                ActorMessage: IAskMessage escalateAskMessage
            }
        )
        {
            escalateAskMessage.SetCanceled();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Disposes all child actors and clears the actor list.
    /// </remarks>
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
    /// Adds a child actor of the specified type with a default mailbox.
    /// </summary>
    /// <typeparam name="TActor">The type of actor to create.</typeparam>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected IActorReference AddChild<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActor
    >()
        where TActor : IActor
    {
        return AddChild<TActor>(new ChannelMailbox());
    }

    /// <summary>
    /// Adds a child actor of the specified type with a custom mailbox.
    /// </summary>
    /// <typeparam name="TActor">The type of actor to create.</typeparam>
    /// <param name="mailbox">The mailbox to use for the actor.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected IActorReference AddChild<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActor
    >(IMailbox mailbox)
        where TActor : IActor
    {
        return AddChild(typeof(TActor), mailbox);
    }

    /// <summary>
    /// Adds a child actor of the specified type with a default mailbox.
    /// </summary>
    /// <param name="actorType">The type of actor to create.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected IActorReference AddChild(Type actorType)
    {
        return AddChild(actorType, new ChannelMailbox());
    }

    /// <summary>
    /// Adds a child actor of the specified type with a custom mailbox.
    /// </summary>
    /// <param name="actorType">The type of actor to create.</param>
    /// <param name="mailbox">The mailbox to use for the actor.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected IActorReference AddChild(Type actorType, IMailbox mailbox)
    {
        if (_initialized)
        {
            throw new SupervisorAlreadyInitializedException(
                "Supervisor already initialized, it's preemptive"
            );
        }

        var actorRef = new LocalActorReference(mailbox);
        Context.Self.Tell(new AddActor(actorType, mailbox, actorRef));

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
    protected ValueTask<IActorReference> AddChildAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActor
    >(CancellationToken cancellationToken = default)
        where TActor : IActor
    {
        return AddChildAsync<TActor>(new ChannelMailbox(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously adds a child actor of the specified type with a custom mailbox.
    /// </summary>
    /// <typeparam name="TActor">The type of actor to create.</typeparam>
    /// <param name="mailbox">The mailbox to use for the actor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected ValueTask<IActorReference> AddChildAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActor
    >(IMailbox mailbox, CancellationToken cancellationToken = default)
        where TActor : IActor
    {
        return AddChildAsync(typeof(TActor), mailbox, cancellationToken);
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
    protected ValueTask<IActorReference> AddChildAsync(
        Type actorType,
        CancellationToken cancellationToken = default
    )
    {
        return AddChildAsync(actorType, new ChannelMailbox(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously adds a child actor of the specified type with a custom mailbox.
    /// </summary>
    /// <param name="actorType">The type of actor to create.</param>
    /// <param name="mailbox">The mailbox to use for the actor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected ValueTask<IActorReference> AddChildAsync(
        Type actorType,
        IMailbox mailbox,
        CancellationToken cancellationToken = default
    )
    {
        if (_initialized)
        {
            throw new SupervisorAlreadyInitializedException(
                "Supervisor already initialized, it's preemptive"
            );
        }

        var actorRef = new LocalActorReference(mailbox);

        var val = Context.Self.TellAsync(
            new AddActor(actorType, mailbox, actorRef),
            cancellationToken
        );

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
        metadata.Process.Failure -= HandleFailure;
        await metadata.Process.StopAsync();
    }

    /// <summary>
    /// Applies the resume action, allowing the actor to continue processing messages.
    /// </summary>
    /// <param name="metadata">The metadata of the actor to resume.</param>
    /// <returns>A task representing the resume operation.</returns>
    protected virtual async Task ApplyResumeAsync(ActorMetadata metadata)
    {
        await metadata.Process.StopAsync();
        metadata.Process.Start();
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
        await StopActorAsync(metadata);
        await BeforeRestartActorAsync(metadata);

        await DisposeObjectAsync(metadata.Actor);

        await ResetMailboxAsync(metadata);

        metadata.Actor = ActorFactory.CreateActor(metadata.ActorType);
        metadata.Actor.Context = new ActorContext(metadata.Reference);

        metadata.Process = new ActorProcess(metadata.Actor, metadata.Mailbox);
        metadata.Process.Failure += HandleFailure;
        metadata.Process.Start(
            new LocalTellMessage(new InitializeActor()),
            new LocalTellMessage(new AfterRestartActor())
        );
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
            await metadata.Actor.BeforeRestartAsync();
        }
        catch (Exception ex)
        {
            LoggerMessages.WarningRestartingActor(Logger, metadata.ActorType, ex);
        }
    }

    /// <summary>
    /// Handles failure events from child actor processes.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The failure event arguments.</param>
    protected virtual void HandleFailure(object? sender, ActorFailureEventArgs args)
    {
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
    /// Creates and starts a new child actor.
    /// </summary>
    /// <param name="actorType">The type of actor to create.</param>
    /// <param name="mailbox">The mailbox for the actor.</param>
    /// <param name="reference">The actor reference.</param>
    /// <returns>The metadata for the created actor.</returns>
    protected virtual ActorMetadata CreateActor(
        Type actorType,
        IMailbox mailbox,
        LocalActorReference reference
    )
    {
        var actor = ActorFactory.CreateActor(actorType);
        actor.Context = new ActorContext(reference);

        var process = new ActorProcess(actor, mailbox);
        process.Failure += HandleFailure;

        var metadata = new ActorMetadata(actor, mailbox, process, reference);
        Actors = Actors.Add(metadata);

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

    private static partial class LoggerMessages
    {
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Resetting actor counter for actor type {ActorType}."
        )]
        public static partial void ResetingActorCounter(ILogger logger, Type actorType);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Restarting actor of type {ActorType} due to exception"
        )]
        public static partial void WarningRestartingActor(
            ILogger logger,
            Type actorType,
            Exception exception
        );
    }
}
