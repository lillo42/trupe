using System;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Mailboxes;
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
/// <param name="name">The unique URI identifier of the local actor.</param>
/// <param name="mailbox">The mailbox where messages will be enqueued for the actor.</param>
public class LocalActorReference(Uri name, IMailbox mailbox) : IActorReference
{
    /// <inheritdoc/>
    public Uri Name => name;

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// This implementation provides a synchronous wrapper around the asynchronous
    /// <see cref="AskAsync{TRequest, TResponse}(TRequest, CancellationToken)"/> method.
    /// It blocks the calling thread until a response is received or the timeout expires.
    /// </para>
    /// <para>
    /// For local actors, this method provides optimal performance by attempting
    /// to complete synchronously when possible, falling back to asynchronous
    /// completion only when necessary.
    /// </para>
    /// <para>
    /// Note: Calling this method on the actor's own dispatcher thread may cause
    /// deadlocks. Use <see cref="AskAsync{TRequest, TResponse}(TRequest, CancellationToken)"/>
    /// when calling from within an actor's message processing logic.
    /// </para>
    /// </remarks>
    public TResponse Ask<TRequest, TResponse>(TRequest request, TimeSpan? timeout = null)
        where TRequest : notnull
    {
        var cts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        try
        {
            var result = AskAsync<TRequest, TResponse>(request, cts.Token);
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
    public async ValueTask<TResponse> AskAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default
    )
        where TRequest : notnull
    {
        var message = new LocalAskMessage(request, cancellationToken);

        await mailbox.EnqueueAsync(message, cancellationToken);

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
    public void Tell<TMessage>(TMessage message, TimeSpan? timeout = null)
        where TMessage : notnull
    {
        var cts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        try
        {
            var task = TellAsync(message, cts.Token);
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
    public async ValueTask TellAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : notnull
    {
        await mailbox.EnqueueAsync(
            new LocalTellMessage(message, CancellationToken.None),
            cancellationToken
        );
    }
}
