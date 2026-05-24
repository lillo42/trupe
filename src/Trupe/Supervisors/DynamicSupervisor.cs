using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Supervisors;
using Trupe.Supervisors.Commands;

namespace Trupe.Supervisors;

/// <summary>
/// A supervisor that allows dynamic addition of child actors at runtime.
/// Uses the <see cref="Strategy.OneForOne"/> supervision strategy.
/// </summary>
/// <param name="logger">The logger instance for logging supervisor activities.</param>
public abstract class DynamicSupervisor(ILogger logger)
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
        var child = Children.FirstOrDefault(x => x.Actor == message.Actor);

        if (child != null)
        {
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
        await base.OnActorProcessFailedAsync(child, message, exception, cancellationToken);

        if (child.RestartPolicy == RestartPolicy.Temporary)
        {
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
    /// Removes non-permanent actors from the children list after they are stopped.
    /// </remarks>
    protected override async ValueTask OnActorProcessStoppedAsync(
        Child child,
        TerminatedReason reason,
        CancellationToken cancellationToken = default
    )
    {
        await base.OnActorProcessStoppedAsync(child, reason, cancellationToken);
        if (child.RestartPolicy != RestartPolicy.Permanent)
        {
            Children = Children.Remove(child);

            var ctx = child.Actor.Context;
            await DisposeObjectAsync(child.Process);
            await DisposeObjectAsync(child.Actor);
            await DisposeObjectAsync(ctx);

            child.Actor = null!;
            child.Process = null!;
        }
    }

    /// <summary>
    /// Removes a child actor from this supervisor by sending a <see cref="RemoveChild"/> command.
    /// </summary>
    /// <param name="reference">The actor reference identifying the child actor to remove.</param>
    protected virtual void RemoveActor(IActorReference reference)
    {
        var metadata = Children.FirstOrDefault(x => x.Reference == reference);
        if (metadata != null)
        {
            Context.Self.Tell(new RemoveChild(metadata.Actor));
        }
    }

    /// <summary>
    /// Asynchronously removes a child actor from this supervisor by sending a <see cref="RemoveChild"/> command.
    /// </summary>
    /// <param name="reference">The actor reference identifying the child actor to remove.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the removal command has been sent.</returns>
    protected virtual ValueTask RemoveActorAsync(
        IActorReference reference,
        CancellationToken cancellationToken = default
    )
    {
        var child = Children.FirstOrDefault(x => x.Reference == reference);
        if (child != null)
        {
            return Context.Self.TellAsync(new RemoveChild(child.Actor), cancellationToken);
        }

        return new ValueTask();
    }
}
