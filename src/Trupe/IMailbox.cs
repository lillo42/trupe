using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Trupe;

/// <summary>
/// Represents an actor's mailbox for asynchronous message delivery in the Trupe actor system.
///
/// The mailbox is a critical component of the actor model that ensures:
/// - Messages are delivered to actors in the order they are received (FIFO)
/// - Asynchronous, non-blocking message processing
/// - Backpressure handling through async enumeration
/// - Thread-safe message queuing and dequeuing
///
/// Mailboxes implement the actor's message queue, decoupling message reception
/// from message processing and allowing actors to process messages at their own pace.
/// </summary>
/// <remarks>
/// The mailbox pattern in Trupe follows these principles:
/// 1. <b>Concurrency Safety</b>: Multiple producers (message senders) can safely
///    enqueue messages concurrently without data corruption.
/// 2. <b>Single Consumer</b>: Only the owning actor consumes messages from its mailbox,
///    ensuring the actor processes messages sequentially.
/// 3. <b>Flow Control</b>: The async enumerable pattern naturally provides backpressure
///    - if the actor processes messages slowly, the enumeration will wait.
/// 4. <b>Lifecycle Integration</b>: Mailboxes are aware of actor lifecycle and can
///    reject messages when the actor is stopped or restarting.
///
/// The <see cref="IAsyncEnumerable{T}"/> implementation allows the actor's message
/// loop to efficiently wait for messages without blocking threads.
/// </remarks>
public interface IMailbox : IAsyncEnumerable<IMessage>
{
    /// <summary>
    /// Asynchronously adds a message to the end of the mailbox queue.
    ///
    /// This method is thread-safe and can be called concurrently from multiple
    /// threads or actors without additional synchronization.
    ///
    /// The mailbox may apply various policies during enqueueing:
    /// - <b>Capacity limits</b>: May block or apply backpressure when the mailbox is full
    /// - <b>Priority handling</b>: Some mailboxes may support priority messages
    /// - <b>Dropping policies</b>: May drop messages under certain conditions (e.g., when full)
    /// - <b>Suspension</b>: May reject messages when the actor is suspended or restarting
    ///
    /// The method returns a <see cref="ValueTask"/> to support both synchronous
    /// (in-memory) and asynchronous (persistent, remote) enqueue operations.
    /// </summary>
    /// <param name="message">
    /// The message to enqueue. Must not be null. The message contains:
    /// - The payload (actual message content)
    /// - Sender information
    /// - Optional metadata (priority, timestamp, etc.)
    /// - Cancellation token for the delivery operation
    ///
    /// The mailbox takes ownership of the message and is responsible for
    /// its lifecycle until it is consumed by the actor.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to abort the enqueue operation.
    /// </param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous enqueue operation.</returns>
    ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken = default);
}
