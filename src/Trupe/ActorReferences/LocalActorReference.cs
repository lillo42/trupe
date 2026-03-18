using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.Mailboxes;
using Trupe.Messages;

namespace Trupe.ActorReferences;

/// <summary>
/// Represents a reference to a local actor within the current process.
/// This implementation provides direct communication with an actor's mailbox
/// without network overhead or serialization.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LocalActorReference"/> is the primary implementation used for
/// intra-process actor communication in the Trupe framework. It provides
/// high-performance message passing by directly enqueuing messages to the
/// actor's mailbox.
/// </para>
/// <para>
/// This struct is immutable and thread-safe for concurrent use.
/// </para>
/// <para>
/// Note: This reference type is only valid for actors running within the
/// same process. For remote actors, use a different <see cref="IActorReference"/>
/// implementation.
/// </para>
/// </remarks>
/// <param name="mailbox">The mailbox where messages will be enqueued for the actor.</param>
public class LocalActorReference(IMailbox mailbox) : IActorReference
{
    private readonly IMailbox _mailbox = mailbox;

    /// <inheritdoc />
    public event EventHandler<TerminatedEventArgs>? OnTerminate;

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// This implementation provides a synchronous wrapper around the asynchronous
    /// <see cref="AskAsync{TResponse}(object, CancellationToken)"/> method.
    /// It blocks the calling thread until a response is received or the timeout expires.
    /// </para>
    /// <para>
    /// For local actors, this method provides optimal performance by attempting
    /// to complete synchronously when possible, falling back to asynchronous
    /// completion only when necessary.
    /// </para>
    /// <para>
    /// Note: Calling this method on the actor's own dispatcher thread may cause
    /// deadlocks. Use <see cref="AskAsync{TResponse}(object, CancellationToken)"/>
    /// when calling from within an actor's message processing logic.
    /// </para>
    /// </remarks>
    public TResponse Ask<TResponse>(object request, TimeSpan? timeout = null)
    {
        return Ask<TResponse>(request, null, timeout);
    }

    /// <inheritdoc />
    public TResponse Ask<TResponse>(
        object request,
        Dictionary<string, object>? metadata,
        TimeSpan? timeout = null
    )
    {
        var cts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        try
        {
            var result = AskAsync<TResponse>(request, metadata, cts.Token);
            if (result.IsCompletedSuccessfully)
            {
                return result.Result;
            }

            return result.AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException(
                $"Ask operation timed out after {timeout?.TotalMilliseconds ?? 0} ms.",
                ex
            );
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// This implementation uses <see cref="LocalAskMessage"/> to wrap the request
    /// and provide response tracking. The message is enqueued to the actor's mailbox,
    /// and the method awaits the response asynchronously.
    /// </para>
    /// <para>
    /// A temporary response handler is created internally by the <see cref="LocalAskMessage"/>
    /// to manage the response promise. This handler is automatically cleaned up
    /// when the response is received or the cancellation token is triggered.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidCastException">
    /// Thrown when the actor's response cannot be cast to the expected <typeparamref name="TResponse"/> type.
    /// This typically indicates a protocol mismatch between the requesting code and the actor's behavior.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the provided <paramref name="cancellationToken"/>.
    /// </exception>
    public ValueTask<TResponse> AskAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default
    )
    {
        return AskAsync<TResponse>(request, null, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> AskAsync<TResponse>(
        object request,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        var message = new LocalAskMessage(request, metadata ?? [], cancellationToken);

        await _mailbox.EnqueueAsync(message, cancellationToken);

        var response = await message.AsTask();

        if (response is TResponse val)
        {
            return val;
        }

        throw new InvalidCastException(
            $"Cannot cast response of type {response?.GetType().FullName ?? "null"} to {typeof(TResponse).FullName}."
        );
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// This implementation creates a <see cref="CancellationTokenSource"/> with the specified
    /// timeout and uses it to call the cancellable <see cref="TellAsync{TMessage}(TMessage, CancellationToken)"/>
    /// method. If the timeout expires, a <see cref="TimeoutException"/> is thrown.
    /// </para>
    /// <para>
    /// The method attempts to complete synchronously when possible to minimize overhead.
    /// If the underlying <see cref="ValueTask"/> is already completed successfully, it returns
    /// immediately without additional context switching.
    /// </para>
    /// </remarks>
    public void Tell(object message, TimeSpan? timeout = null)
    {
        Tell(message, null, timeout);
    }

    /// <inheritdoc />
    public void Tell(object message, Dictionary<string, object>? metadata, TimeSpan? timeout = null)
    {
        var cts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        try
        {
            var task = TellAsync(message, null, cts.Token);
            if (task.IsCompletedSuccessfully)
            {
                return;
            }

            task.AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException(
                $"Tell operation timed out after {timeout?.TotalMilliseconds ?? 0} ms.",
                ex
            );
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// This implementation wraps the message in a <see cref="LocalTellMessage"/> and
    /// enqueues it directly to the actor's mailbox. The method completes when the
    /// message is successfully queued for delivery or when the operation is cancelled.
    /// </para>
    /// <para>
    /// The cancellation token allows for cooperative cancellation of the enqueue operation.
    /// If the token is cancelled before the message is enqueued, the operation will throw
    /// an <see cref="OperationCanceledException"/>. Once enqueued, the message is guaranteed
    /// to be processed (unless the actor system shuts down).
    /// </para>
    /// <para>
    /// Note: Cancellation only affects the delivery of the message to the mailbox, not
    /// the processing of the message by the actor. If the message is successfully enqueued
    /// before cancellation occurs, the actor will still process it.
    /// </para>
    /// <para>
    /// This method uses <see cref="ValueTask"/> to avoid heap allocations in the
    /// common case where enqueueing completes synchronously.
    /// </para>
    /// <para>
    /// For fire-and-forget messaging without cancellation support, use the overload
    /// without the <paramref name="cancellationToken"/> parameter.
    /// </para>
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the provided <paramref name="cancellationToken"/>
    /// before the message is successfully enqueued.
    /// </exception>
    public ValueTask TellAsync(object message, CancellationToken cancellationToken = default)
    {
        return TellAsync(message, null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask TellAsync(
        object message,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        return _mailbox.EnqueueAsync(
            new LocalTellMessage(message, metadata ?? [], CancellationToken.None),
            cancellationToken
        );
    }

    /// <summary>
    /// Terminates this actor reference and raises the <see cref="OnTerminate"/> event.
    /// </summary>
    /// <param name="reason">An optional reason describing why the actor was terminated.</param>
    public void Terminate(string? reason)
    {
        OnTerminate?.Invoke(this, new TerminatedEventArgs(this, reason));
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _mailbox.GetHashCode();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is IActorReference other && Equals(other);
    }

    /// <inheritdoc />
    public bool Equals(IActorReference? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is not LocalActorReference localReference)
        {
            return false;
        }

        return localReference._mailbox == _mailbox;
    }
}
