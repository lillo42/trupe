using System;
using Trupe.Abstractions.Mailboxes;

namespace Trupe.Abstractions.Supervisors;

/// <summary>
/// Defines the specification for a child actor managed by a supervisor.
/// </summary>
public interface IChildSpecification
{
    /// <summary>
    /// Gets the type of the actor to be created.
    /// </summary>
    Type ActorType { get; }

    /// <summary>
    /// Gets or sets the mailbox used for message delivery to the child actor.
    /// Defaults to <see cref="ChannelMailbox"/>.
    /// </summary>
    IMailbox Mailbox { get; set; }

    /// <summary>
    /// Gets or sets the restart policy for the child actor.
    /// </summary>
    RestartPolicy RestartPolicy { get; set; }
}
