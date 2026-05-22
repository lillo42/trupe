using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trupe.Abstractions;
using Trupe.Abstractions.Supervisors.Commands;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Supervisors;
using Trupe.Abstractions.SystemMessages;
using Trupe.Messages;
using System.Diagnostics.CodeAnalysis;

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
        IAsyncDisposable
{
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
    public virtual async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        await OnInitializeAsync(cancellationToken);
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
    public virtual async ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var child in Children)
        {
            child.Process.Failed -= OnActorProcessFailed;
            child.Process.Stopped -= OnActorProcessStopped;
            
            var ctx = child.Actor.Context;
            await DisposeObjectAsync(child.Actor);
            await DisposeObjectAsync(ctx);
            await DisposeObjectAsync(child.Process);

            child.Actor = null!;
            child.Process = null!;
            child.Metadata.Clear();
        }

        Children = [];
    }

    /// <inheritdoc />
    /// <remarks>
    /// Routes <see cref="ActorProcessFailed"/> and <see cref="ActorProcessStopped"/>
    /// messages to their respective typed handlers before falling back to the base implementation.
    /// </remarks>
    public virtual async ValueTask HandleAsync(
        object? message,
        CancellationToken cancellationToken = default
    )
    {
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
            await base.HandleAsync(message, cancellationToken);
        }
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
        var child = Children.FirstOrDefault(x => x.Actor == message.Process);
        if (child == null)
        {
            return;
        }

        await OnActorProcessFailedAsync(
            child,
            message.Message,
            message.Exception,
            cancellationToken
        );
    }

    /// <summary>
    /// Handles the <see cref="ActorProcessStopped"/> message when a child actor process is stopped.
    /// </summary>
    /// <param name="message">The stopped information including the process and reason.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the stopped handling operation.</returns>
    public virtual async ValueTask HandleAsync(
        ActorProcessStopped  message,
        CancellationToken cancellationToken = default
    )
    {
        var child = Children.FirstOrDefault(x => x.Actor == message.Process);
        if (child == null)
        {
            return;
        }

        await OnActorProcessStoppedAsync(child, message.Reason);
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
        if (child.RestartPolicy == RestartPolicy.Temporary)
        {
            await StopAsync(child);
            return;
        }

        ResetCounter(child);

        var action = ResolveFailureAction(child, exception);
        if (action == FailureAction.Restart) { }
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
        if (Strategy == Strategy.OneForOne)
        {
            await StopActorAsync(child, TerminatedReason.Stopped);
        }
        else if (Strategy == Strategy.AllForOne)
        {
            await Task.WhenAll(Children.Select(x => StopActorAsync(x, TerminatedReason.Stopped)));
        }
    }

    /// <summary>
    /// Stops a single actor process and marks its reference as terminated.
    /// </summary>
    /// <param name="metadata">The metadata of the actor to stop.</param>
    /// <param name="reason">The reason for termination.</param>
    /// <returns>A task representing the stop operation.</returns>
    protected virtual async Task StopActorAsync(Child metadata, TerminatedReason reason)
    {
        await metadata.Process.KillAsync();
        metadata.Actor.Context.Self.MarkAsTerminate(reason);
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
        await child.Process.StartAsync();
    }

    /// <summary>
    /// Applies the restart action to the failed actor(s) based on the supervision strategy.
    /// </summary>
    /// <param name="child">The metadata of the failed child actor.</param>
    /// <returns>A task representing the restart operation.</returns>
    protected virtual async Task RestartAsync(Child child)
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
    /// Resets an actor by stopping, disposing, and recreating it with a fresh instance.
    /// </summary>
    /// <param name="child">The metadata of the actor to reset.</param>
    /// <returns>A task representing the reset operation.</returns>
    protected virtual async Task ResetActorAsync(Child child)
    {
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
            await child.Actor.BeforeRestartAsync();
        }
        catch (Exception ex) { }
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
        child.Process.Failed += OnActorProcessFailed;
        child.Process.Stopped += OnActorProcessStopped;

        await child.Process.StartAsync(new TellMessage(new InitializeActor(), []));
    }

    /// <summary>
    /// Handles failure events from child actor processes by forwarding them as messages to the supervisor.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The failure event arguments.</param>
    protected virtual void OnActorProcessFailed(object? sender, ActorProcessFailedEvetArgs args)
    {
        Context.Self.Tell(new ActorProcessFailed(args.Process, args.Message, args.Exception));
    }

    /// <summary>
    /// Handles stopped events from child actor processes by forwarding them as messages to the supervisor.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The stopped event arguments.</param>
    protected virtual void OnActorProcessStopped(object? sender, ActorProcessStoppedEventArgs args)
    {
        Context.Self.Tell(new ActorProcessStopped(args.Process, args.Reason));
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
            await ResetActorAsync(child);
        }
        else
        {
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
        GC.SuppressFinalize(this);

        await using var sp = Context.ServiceProvider.CreateAsyncScope();
        var registry = sp.ServiceProvider.GetRequiredService<IActorProcessRegistry>();

        foreach (var child in Children)
        {
            registry.Remove(child.Actor.Context.Self);

            child.Process.Failed -= OnActorProcessFailed;
            child.Process.Stopped -= OnActorProcessStopped;

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
}
