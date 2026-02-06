using System;
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
    : Supervisor(actorFactory, logger)
{
    /// <inheritdoc />
    protected sealed override Strategy Strategy => Strategy.OneForOne;

    /// <inheritdoc />
    protected override IActorReference AddChild(Type actorType, IMailbox mailbox)
    {
        var actorRef = new LocalActorReference(mailbox);
        Context.Self.Tell(new AddActor(actorType, mailbox, actorRef));

        return actorRef;
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
}
