using System.Collections.Generic;
using System.Threading;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;

namespace Trupe.Messages;

/// <summary>
/// Represents a concrete "fire-and-forget" message intended for a local actor.
/// </summary>
/// <remarks>
/// This record wraps the user's data payload for delivery to an <see cref="IMailbox"/>.
/// It carries no mechanism for returning a result to the sender.
/// </remarks>
/// <param name="Payload">The actual content/data of the message sent by the user.</param>
/// <param name="Metadata">The metadata dictionary attached to the message by the sender.</param>
/// <param name="CancellationToken">
/// A token that can be used to cancel the delivery or processing of the message
/// while it is waiting in the mailbox queue.
/// </param>
public record LocalTellMessage(
    object Payload,
    Dictionary<string, object> Metadata,
    CancellationToken CancellationToken = default
) : ITellMessage;
