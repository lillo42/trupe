using System;
using System.Threading.Tasks;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions;

/// <summary>
/// Defines the contract for an actor process that manages the execution lifecycle
/// and message processing for an actor instance.
/// </summary>
public interface IActorProcess
{
    /// <summary>
    /// Gets or sets the actor instance managed by this process.
    /// </summary>
    IActor Actor { get; set; }

    /// <summary>
    /// Gets or sets the mailbox used for message delivery to the actor.
    /// </summary>
    IMailbox Mailbox { get; set; }

    /// <summary>
    /// Starts the actor process, optionally processing the provided initial messages
    /// before consuming from the mailbox.
    /// </summary>
    /// <param name="messages">Initial messages to process before entering the main loop.</param>
    /// <returns>A task representing the start operation.</returns>
    Task StartAsync(params IMessage[] messages);

    /// <summary>
    /// Forcefully stops the actor process by cancelling its execution loop.
    /// </summary>
    /// <returns>A task representing the kill operation.</returns>
    Task KillAsync();

    IDisposable Register(IActorProcessListener listing);

    void UnRegister(IActorProcessListener listing);
}
