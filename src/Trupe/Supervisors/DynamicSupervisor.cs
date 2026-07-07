using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Supervisors;
using Trupe.Abstractions.Supervisors.Commands;
using Trupe.Abstractions.Supervisors.Events;
using Trupe.Supervisors.Commands;

namespace Trupe.Supervisors;

/// <summary>
/// A supervisor that allows dynamic addition of child actors at runtime.
/// Uses the <see cref="Strategy.OneForOne"/> supervision strategy.
/// </summary>
/// <param name="logger">The logger instance for logging supervisor activities.</param>
public abstract partial class DynamicSupervisor(ILogger logger)
    : Supervisor(logger),
        IHandleActorMessage<RemoveChild>
{
    /// <inheritdoc />
    protected sealed override Strategy Strategy => Strategy.OneForOne;

    /// <summary>
    /// Always returns <see langword="false"/> since dynamic supervisors allow adding children at any time.
    /// </summary>
    protected override bool Initialized => false;

    /// <summary>
    /// Handles the <see cref="RemoveChild"/> message by removing the specified child actor
    /// from the supervisor, stopping it, and disposing of its resources.
    /// </summary>
    /// <param name="message">The message containing the actor to remove.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    public async ValueTask HandleAsync(
        RemoveChild message,
        CancellationToken cancellationToken = default
    )
    {
        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.RemovingChild(Logger);
            var child = Children.FirstOrDefault(x => x.Actor == message.Actor);

            if (child == null)
            {
                Log.ChildNotFoundForRemoval(Logger);
                return;
            }

            Log.DisposingChild(Logger, child.Actor.GetType(), child.Actor.Context.Name);
            Children = Children.Remove(child);

            var ctx = child.Actor.Context;
            await DisposeObjectAsync(child.Process);
            await DisposeObjectAsync(child.Actor);
            await DisposeObjectAsync(ctx);

            child.Actor = null!;
            child.Process = null!;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Removes temporary actors from the children list after failure.
    /// </remarks>
    protected override async Task OnActorProcessFailedAsync(
        Child child,
        IMessage message,
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.HandlingActorProcessFailed(Logger, child.Actor.GetType(), child.Actor.Context.Name);
            await base.OnActorProcessFailedAsync(child, message, exception, cancellationToken);

            if (child.RestartPolicy == RestartPolicy.Temporary)
            {
                Log.RemovingTemporaryChildAfterFailure(
                    Logger,
                    child.Actor.GetType(),
                    child.Actor.Context.Name
                );
                Children = Children.Remove(child);

                var ctx = child.Actor.Context;
                await DisposeObjectAsync(child.Process);
                await DisposeObjectAsync(child.Actor);
                await DisposeObjectAsync(ctx);

                child.Actor = null!;
                child.Process = null!;
                Log.TemporaryChildRemovedAfterFailure(Logger);
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Removes non-permanent actors from the children list after they are stopped.
    /// </remarks>
    protected override async ValueTask OnActorProcessStoppedAsync(
        Child child,
        TerminatedReason reason,
        CancellationToken cancellationToken = default
    )
    {
        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.HandlingActorProcessStopped(
                Logger,
                child.Actor.GetType(),
                child.Actor.Context.Name,
                reason
            );
            await base.OnActorProcessStoppedAsync(child, reason, cancellationToken);

            if (child.RestartPolicy != RestartPolicy.Permanent)
            {
                Log.RemovingNonPermanentChildAfterStop(
                    Logger,
                    child.Actor.GetType(),
                    child.Actor.Context.Name
                );
                Children = Children.Remove(child);

                var ctx = child.Actor.Context;
                await DisposeObjectAsync(child.Process);
                await DisposeObjectAsync(child.Actor);
                await DisposeObjectAsync(ctx);

                child.Actor = null!;
                child.Process = null!;
                Log.NonPermanentChildRemovedAfterStop(Logger);
            }
        }
    }

    /// <summary>
    /// Removes a child actor from this supervisor by sending a <see cref="RemoveChild"/> command.
    /// </summary>
    /// <param name="reference">The actor reference identifying the child actor to remove.</param>
    protected virtual void RemoveActor(IActorReference reference)
    {
        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.SchedulingRemoveActor(Logger);
            var metadata = Children.FirstOrDefault(x => x.Reference == reference);
            if (metadata == null)
            {
                Log.ChildNotFoundForRemoval(Logger);
                return;
            }

            Context.Self.Tell(new RemoveChild(metadata.Actor));
        }
    }

    /// <summary>
    /// Asynchronously removes a child actor from this supervisor by sending a <see cref="RemoveChild"/> command.
    /// </summary>
    /// <param name="reference">The actor reference identifying the child actor to remove.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the removal command has been sent.</returns>
    protected virtual async ValueTask RemoveActorAsync(
        IActorReference reference,
        CancellationToken cancellationToken = default
    )
    {
        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.SchedulingRemoveActor(Logger);
            var child = Children.FirstOrDefault(x => x.Reference == reference);
            if (child == null)
            {
                Log.ChildNotFoundForRemoval(Logger);
                return;
            }

            await Context.Self.TellAsync(new RemoveChild(child.Actor), cancellationToken);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "Removing child actor")]
        public static partial void RemovingChild(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Disposing {ActorType} actor '{ActorName}'")]
        public static partial void DisposingChild(ILogger logger, Type actorType, Uri actorName);

        [LoggerMessage(LogLevel.Information, "Child actor removed successfully")]
        public static partial void ChildRemoved(ILogger logger);

        [LoggerMessage(LogLevel.Warning, "Child actor not found for removal")]
        public static partial void ChildNotFoundForRemoval(ILogger logger);

        [LoggerMessage(
            LogLevel.Debug,
            "Handling failed process for {ActorType} actor '{ActorName}'"
        )]
        public static partial void HandlingActorProcessFailed(
            ILogger logger,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(
            LogLevel.Information,
            "Removing temporary {ActorType} actor '{ActorName}' after failure"
        )]
        public static partial void RemovingTemporaryChildAfterFailure(
            ILogger logger,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(LogLevel.Information, "Temporary child actor removed after failure")]
        public static partial void TemporaryChildRemovedAfterFailure(ILogger logger);

        [LoggerMessage(
            LogLevel.Debug,
            "Handling stopped process for {ActorType} actor '{ActorName}', reason: {Reason}"
        )]
        public static partial void HandlingActorProcessStopped(
            ILogger logger,
            Type actorType,
            Uri actorName,
            TerminatedReason reason
        );

        [LoggerMessage(
            LogLevel.Information,
            "Removing non-permanent {ActorType} actor '{ActorName}' after stop"
        )]
        public static partial void RemovingNonPermanentChildAfterStop(
            ILogger logger,
            Type actorType,
            Uri actorName
        );

        [LoggerMessage(LogLevel.Information, "Non-permanent child actor removed after stop")]
        public static partial void NonPermanentChildRemovedAfterStop(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Scheduling child actor removal")]
        public static partial void SchedulingRemoveActor(ILogger logger);
    }
}
