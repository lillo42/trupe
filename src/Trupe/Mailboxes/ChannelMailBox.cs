using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Trupe.Mailboxes;

/// <summary>
/// Mailbox implementation using <see cref="Channel{T}"/> for actor message queuing.
/// </summary>
/// <remarks>
/// This class provides a high-performance, asynchronous mailbox implementation for actors
/// using .NET's Channel<T> infrastructure. It supports both unbounded and bounded channel
/// configurations with customizable behavior when the channel is full.
///
/// The mailbox is designed for single-reader, multiple-writer scenarios which aligns with
/// the actor model's requirement that only one actor processes messages from its mailbox
/// at a time, while multiple actors can send messages to it concurrently.
/// </remarks>
public class ChannelMailBox : IMailbox
{
    private readonly Channel<IMessage> _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelMailBox"/>.
    /// </summary>
    public ChannelMailBox()
        : this(-1) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelMailBox"/> class with
    /// specified capacity and full mode behavior.
    /// </summary>
    /// <param name="maxSize">
    /// The maximum number of messages the mailbox can hold. When the mailbox
    /// reaches this capacity, behavior is determined by <paramref name="fullMode"/>.
    ///
    /// Special values:
    /// - <c>0</c> or less: Creates an unbounded mailbox (no capacity limits)
    /// - Positive integer: Creates a bounded mailbox with that capacity
    /// </param>
    /// <param name="fullMode">
    /// Determines the behavior when a bounded mailbox reaches its capacity.
    /// This parameter is only meaningful when <paramref name="maxSize"/> is positive.
    ///
    /// Available modes:
    /// - <see cref="BoundedChannelFullMode.Wait"/>: Writers wait until space is available
    ///   (default, provides backpressure)
    /// - <see cref="BoundedChannelFullMode.DropNewest"/>: New messages are dropped when full
    /// - <see cref="BoundedChannelFullMode.DropOldest"/>: Oldest messages are dropped when full
    /// - <see cref="BoundedChannelFullMode.DropWrite"/>: New messages are dropped when full
    ///
    /// <b>Trade-offs:</b>
    /// - <c>Wait</c>: Provides backpressure but can cause deadlocks if not managed
    /// - <c>DropNewest</c>: May drop important recent messages
    /// - <c>DropOldest</c>: May drop unprocessed but potentially important messages
    /// - <c>DropWrite</c>: Same as DropNewest but with different internal behavior
    ///
    /// Defaults to <see cref="BoundedChannelFullMode.Wait"/>.
    /// </param>
    public ChannelMailBox(
        int maxSize,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait
    )
    {
        if (maxSize <= 0)
        {
            _channel = Channel.CreateUnbounded<IMessage>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
            );
        }
        else
        {
            _channel = Channel.CreateBounded<IMessage>(
                new BoundedChannelOptions(maxSize)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = fullMode,
                }
            );
        }
    }

    /// <summary>
    /// Asynchronously enqueues a message into the mailbox.
    /// </summary>
    /// <param name="message">The message to enqueue. Must not be null.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous enqueue operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="message"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <exception cref="ChannelClosedException">
    /// Thrown when attempting to write to a closed channel (e.g., when the
    /// actor has been stopped and its mailbox closed).
    /// </exception>
    /// <remarks>
    /// For bounded channels, this method may wait if the channel is full depending on the
    /// <see cref="BoundedChannelFullMode"/> configuration. For unbounded channels, this
    /// operation typically completes synchronously unless the channel is closed.
    /// </remarks>
    public async ValueTask EnqueueAsync(
        IMessage message,
        CancellationToken cancellationToken = default
    )
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    /// <summary>
    /// Returns an async enumerator that can be used to consume messages from the mailbox.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerator{IMessage}"/> that can be used in
    /// <c>await foreach</c> loops to process messages as they arrive.
    /// </returns>
    /// <remarks>
    /// This method is called by the actor runtime to process messages from the mailbox.
    /// The enumerator will block asynchronously when no messages are available and resume
    /// when new messages are enqueued. This implements the core actor message processing loop.
    ///
    /// The mailbox is designed for single-reader access, ensuring that only one actor
    /// processes messages from this mailbox at any given time.
    /// </remarks>
    public IAsyncEnumerator<IMessage> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        return _channel
            .Reader.ReadAllAsync(cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
    }
}
