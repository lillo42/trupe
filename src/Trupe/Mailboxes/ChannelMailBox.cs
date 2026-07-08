using System;
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
public class ChannelMailbox : IMailbox
{
    private readonly Channel<IMessage> _channel;

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

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(
        IMessage message,
        CancellationToken cancellationToken = default
    )
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IMessage?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (
                await _channel.Reader.WaitToReadAsync(cancellationToken)
                && _channel.Reader.TryRead(out var message)
            )
            {
                return message;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
