using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
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

    private static readonly Counter<int> EnqueueCounter = TrupeDiagnostics.Meter.CreateCounter<int>("mailbox.enqueue",
        unit: "{operations}",
        description: "Number of messages enqueued into the mailbox.");

    private static readonly Counter<int> DequeueCounter = TrupeDiagnostics.Meter.CreateCounter<int>("mailbox.dequeue",
        unit: "{operations}",
        description: "Number of messages dequeued from the mailbox.");

    private static readonly Gauge<long> MailboxLength = TrupeDiagnostics.Meter.CreateGauge<long>("mailbox.length",
        unit: "{messages}",
        description: "Current number of messages waiting in the mailbox.");

    private static readonly Histogram<long> EnqueueDuration = TrupeDiagnostics.Meter.CreateHistogram<long>(
        "mailbox.enqueue.duration",
        unit: "ms",
        description: "Duration of mailbox enqueue operations in milliseconds.");

    private static readonly Histogram<long> DequeueDuration = TrupeDiagnostics.Meter.CreateHistogram<long>(
        "mailbox.dequeue.duration",
        unit: "ms",
        description: "Duration of mailbox dequeue operations in milliseconds.");

    private long _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelMailbox"/>.
    /// </summary>
    public ChannelMailbox()
        : this(-1)
    {
    }

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
    public IEnumerable<KeyValuePair<string, object?>> Metadata { get; set; } = [];

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(
        IMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var activity =
            TrupeDiagnostics.ActivitySource.StartActivity("mailbox.enqueue", ActivityKind.Internal, null, Metadata);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _channel.Writer.WriteAsync(message, cancellationToken);

            stopwatch.Stop();
            var length = Interlocked.Increment(ref _length);

            MailboxLength.Record(length, Metadata.ToArray());
            activity?.SetStatus(ActivityStatusCode.Ok, "Message enqueued successfully.");
            EnqueueCounter.Add(1, Metadata.ToArray());
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, "Failed to enqueue message.");
        }
        finally
        {
            EnqueueDuration.Record(stopwatch.ElapsedMilliseconds, Metadata.ToArray());
            activity?.Dispose();
        }
    }


    /// <inheritdoc />
    public async ValueTask<IMessage?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var activity =
            TrupeDiagnostics.ActivitySource.StartActivity("mailbox.dequeue", ActivityKind.Internal, null, Metadata);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (
                await _channel.Reader.WaitToReadAsync(cancellationToken)
                && _channel.Reader.TryRead(out var message)
            )
            {
                stopwatch.Stop();

                var length = Interlocked.Decrement(ref _length);
                
                activity?.SetStatus(ActivityStatusCode.Ok, "Message dequeued successfully.");
                DequeueCounter.Add(1, Metadata.ToArray());
                MailboxLength.Record(length, Metadata.ToArray());
                return message;
            }

            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Ok, "No message available.");
            return null;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, "Dequeue cancelled.");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, "Failed to dequeue message.");
            throw;
        }
        finally
        {
            DequeueDuration.Record(stopwatch.ElapsedMilliseconds, Metadata.ToArray());
            activity?.Dispose();
        }
    }
}