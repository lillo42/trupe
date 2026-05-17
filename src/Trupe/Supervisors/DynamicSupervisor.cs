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

            await StopActorAsync(child);
            await DisposeObjectAsync(child.Actor);
            await DisposeObjectAsync(child.Actor.Context);

            await child.Process.DisposeAsync();

            child.Actor = null!;
            child.Process = null!;
        }
    }

    /// <summary>
    /// Adds a child actor by sending an <see cref="AddActor"/> command to this supervisor.
    /// </summary>
    /// <param name="specification">The specification defining the child actor to create.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    protected override IActorReference AddChild(IChildSpecification specification)
    {
        var actorRef = new ActorReference(
            specification.ActorType,
            Context.ServiceProvider,
            specification.Mailbox
        );
        Context.Self.Tell(new AddActor(specification, actorRef));

        return actorRef;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Removes temporary actors from the children list after failure.
    /// </remarks>
    protected override async Task OnActorFailedAsync(
        Child child,
        IMessage message,
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        await base.OnActorFailedAsync(child, message, exception, cancellationToken);

        if (child.RestartPolicy == RestartPolicy.Temporary)
        {
            Children = Children.Remove(child);

            await DisposeObjectAsync(child.Actor);
            await DisposeObjectAsync(child.Actor.Context);

            child.Actor = null!;
            child.Process = null!;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Removes non-permanent actors from the children list after termination.
    /// </remarks>
    protected override async ValueTask OnActorTerminatedAsync(
        Child child,
        string? reason,
        CancellationToken cancellationToken = default
    )
    {
        await base.OnActorTerminatedAsync(child, reason, cancellationToken);
        if (child.RestartPolicy != RestartPolicy.Permanent)
        {
            Children = Children.Remove(child);

            await DisposeObjectAsync(child.Actor);
            await DisposeObjectAsync(child.Actor.Context);

            child.Actor = null!;
            child.Process = null!;
        }
    }

    /// <inheritdoc />
    protected override ValueTask<IActorReference> AddChildAsync(
        IChildSpecification specification,
        CancellationToken cancellationToken = default
    )
    {
        var actorRef = new ActorReference(
            specification.ActorType,
            Context.ServiceProvider,
            specification.Mailbox
        );

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
