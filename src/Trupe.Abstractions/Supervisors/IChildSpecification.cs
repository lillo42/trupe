using System;
using System.Diagnostics.CodeAnalysis;
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
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )]
    Type ActorType { get; }

    /// <summary>
    /// Gets or sets the restart policy for the child actor.
    /// </summary>
    RestartPolicy RestartPolicy { get; set; }

    /// <summary>
    /// Gets or sets the unique name for the child actor.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Gets or sets the factory function used to create mailboxes for the child actor.
    /// </summary>
    Func<IServiceProvider, IMailbox> MailboxFactory { get; set; }
}
