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
public record ChildSpecification : IChildSpecification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChildSpecification"/> class with the specified actor type.
    /// </summary>
    /// <param name="actorType">The type of actor to create. Must have public constructors.</param>
    public ChildSpecification(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType
    )
    {
        ActorType = actorType;
    }

    /// <summary>
    /// Gets the type of actor to create. Must have public constructors.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type ActorType { get; }

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
