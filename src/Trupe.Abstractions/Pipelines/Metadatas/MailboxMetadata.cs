using Trupe.Abstractions.Mailboxes;

namespace Trupe.Abstractions.Pipelines.Metadatas;

/// <summary>
/// Pipeline metadata that carries the <see cref="IMailbox"/> used for message delivery.
/// </summary>
/// <param name="Mailbox">The mailbox involved in the pipeline execution.</param>
public record MailboxMetadata(IMailbox Mailbox);
