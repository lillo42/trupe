using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Trupe.ActorReferences;
using Trupe.Factories;
using Trupe.Mailboxes;
using Trupe.Supervisors.Commands;

namespace Trupe.Supervisors;

/// <summary>
/// A supervisor that allows dynamic addition of child actors at runtime.
/// Uses the <see cref="Strategy.OneForOne"/> supervision strategy.
/// </summary>
/// <param name="actorFactory">The factory used to create child actors.</param>
/// <param name="logger">The logger instance for logging supervisor activities.</param>
public abstract class DynamicSupervisor(IActorFactory actorFactory, ILogger logger)
    : Supervisor(actorFactory, logger),
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
        var metadata = Actors.FirstOrDefault(x => x.Actor == message.Actor);

        if (metadata != null)
        {
            Actors = Actors.Remove(metadata);

            await StopActorAsync(metadata);
            await DisposeObjectAsync(metadata.Actor);
            await metadata.Process.DisposeAsync();

            metadata.Actor = null!;
            metadata.Process = null!;
        }
    }

    /// <inheritdoc />
    protected override IActorReference AddChild(Type actorType, IMailbox mailbox)
    {
        var actorRef = new LocalActorReference(mailbox);
        Context.Self.Tell(new AddActor(actorType, mailbox, actorRef));

        return actorRef;
    }

    /// <inheritdoc />
    /// <remarks>
    /// In addition to the base behavior, if the <see cref="Supervisor.Restart"/> policy is
    /// <see cref="RestartPolicy.Transient"/>, the terminated actor is removed from the
    /// supervised actors list instead of being restarted.
    /// </remarks>
    public override async ValueTask HandleAsync(
        ActorTerminated message,
        CancellationToken cancellationToken = default
    )
    {
        await base.HandleAsync(message, cancellationToken);

        if (Restart == RestartPolicy.Transient)
        {
            var metadata = Actors.FirstOrDefault(x => x.Actor == message.Actor);
            if (metadata != null)
            {
                Actors = Actors.Remove(metadata);
            }
        }
    }

    /// <inheritdoc />
    protected override ValueTask<IActorReference> AddChildAsync(
        Type actorType,
        IMailbox mailbox,
        CancellationToken cancellationToken = default
    )
    {
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
    /// Removes a child actor from this supervisor by sending a <see cref="RemoveChild"/> command.
    /// </summary>
    /// <param name="reference">The actor reference identifying the child actor to remove.</param>
    protected virtual void RemoveActor(IActorReference reference)
    {
        var metadata = Actors.FirstOrDefault(x => x.Reference == reference);
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
        var metadata = Actors.FirstOrDefault(x => x.Reference == reference);
        if (metadata != null)
        {
            return Context.Self.TellAsync(new RemoveChild(metadata.Actor), cancellationToken);
        }

        return ValueTask.CompletedTask;
    }
}
