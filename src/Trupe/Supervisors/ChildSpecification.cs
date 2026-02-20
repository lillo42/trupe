using System;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Supervisors;
using Trupe.Mailboxes;

namespace Trupe.Supervisors;

/// <summary>
/// Defines the specification for creating a child actor within a supervisor.
/// </summary>
/// <param name="ActorType">The type of actor to create.</param>
public record ChildSpecification(Type ActorType) : IChildSpecification
{
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
