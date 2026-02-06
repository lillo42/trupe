using System;
using Trupe.ActorReferences;
using Trupe.Mailboxes;

namespace Trupe.Supervisors.Commands;

/// <summary>
/// Command to add a new child actor to the supervisor.
/// </summary>
/// <param name="ActorType">The type of actor to create.</param>
/// <param name="Mailbox">The mailbox to use for the new actor.</param>
/// <param name="Reference">The pre-created actor reference for the new actor.</param>
/// <remarks>
/// This command is used internally by supervisors to create child actors.
/// The reference is created before the actor to allow immediate use of the reference
/// while the actor creation is processed asynchronously.
/// </remarks>
public record AddActor(Type ActorType, IMailbox Mailbox, LocalActorReference Reference);
