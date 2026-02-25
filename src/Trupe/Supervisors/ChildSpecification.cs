using System;
using System.Diagnostics.CodeAnalysis;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Supervisors;
using Trupe.Mailboxes;

namespace Trupe.Supervisors;

/// <summary>
/// Defines the specification for a child actor managed by a supervisor,
/// including its type, mailbox, and restart policy.
/// </summary>
/// <param name="ActorType">The type of actor to create.</param>
public record ChildSpecification() : IChildSpecification
{
    public ChildSpecification(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType
    )
        : this()
    {
        ActorType = actorType;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type ActorType { get; init; }

    /// <summary>
    /// Gets or sets the mailbox used for message delivery to the child actor.
    /// Defaults to <see cref="ChannelMailbox"/>.
    /// </summary>
    public IMailbox Mailbox { get; set; } = new ChannelMailbox();

    /// <summary>
    /// Gets or sets the restart policy for the child actor.
    /// Defaults to <see cref="RestartPolicy.Permanent"/>.
    /// </summary>
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.Permanent;
}
