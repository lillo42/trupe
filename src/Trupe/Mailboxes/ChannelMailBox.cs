using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;

namespace Trupe.Mailboxes;

/// <summary>
/// An <see cref="IMailbox"/> implementation backed by <see cref="Channel{T}"/> for high-performance,
/// asynchronous actor message queuing.
/// </summary>
/// <remarks>
/// <para>
/// Supports both unbounded and bounded channel configurations with customizable
/// behavior when the channel is full (see <see cref="BoundedChannelFullMode"/>).
/// </para>
/// <para>
/// The mailbox is configured for single-reader, multiple-writer access, which aligns
/// with the actor model's guarantee that only one actor processes its mailbox at a time
/// while multiple actors can send messages concurrently.
/// </para>
/// </remarks>
public class ChannelMailbox : IMailbox, IEquatable<IMailbox>
{
    private readonly int _maxSize;
    private readonly BoundedChannelFullMode _fullMode;

    private Channel<IMessage> _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelMailbox"/>.
    /// </summary>
    public ChannelMailbox()
        : this(-1) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelMailbox"/> class with
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
    public ChannelMailbox(
        int maxSize,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait
    )
    {
        _maxSize = maxSize;
        _fullMode = fullMode;
        _channel = CreateChannel();
    }

    private Channel<IMessage> CreateChannel()
    {
        if (_maxSize <= 0)
        {
            return Channel.CreateUnbounded<IMessage>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
            );
        }
        else
        {
            return Channel.CreateBounded<IMessage>(
                new BoundedChannelOptions(_maxSize)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = _fullMode,
                }
            );
        }
    }

    /// <inheritdoc />
    public async ValueTask CleanAsync()
    {
        _channel = CreateChannel();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _channel.GetHashCode();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is IMailbox other && Equals(other);
    }

    /// <inheritdoc />
    public bool Equals(IMailbox? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is ChannelMailbox otherChannel)
        {
            return _channel == otherChannel._channel;
        }

        return false;
    }
}
