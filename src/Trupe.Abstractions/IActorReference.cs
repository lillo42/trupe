using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Trupe.Abstractions;

/// <summary>
/// Defines the contract for communicating with an actor through message passing.
/// Supports fire-and-forget (Tell) and request-response (Ask) patterns.
/// </summary>
public interface IActorReference
{
    /// <summary>
    /// Gets the unique URI identifying this actor.
    /// </summary>
    Uri Name { get; }

    /// <summary>
    /// Sends a stop message to the actor using fire-and-forget semantics.
    /// </summary>
    void Stop();

    /// <summary>
    /// Asynchronously sends a stop message to the actor and waits for acknowledgement.
    /// </summary>
    /// <returns>A task representing the stop operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Forcefully kills the actor process without waiting for graceful shutdown.
    /// </summary>
    /// <returns>A task representing the kill operation.</returns>
    Task KillAsync();

    /// <summary>
    /// Marks this actor reference as terminated with the specified reason.
    /// This is for internal use by the actor infrastructure.
    /// </summary>
    /// <param name="reason">The reason for termination.</param>
    void MarkAsTerminate(TerminatedReason reason);

    /// <summary>
    /// Sends a request message to the actor and synchronously waits for a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the expected response.</typeparam>
    /// <param name="request">The request message to send to the actor.</param>
    /// <param name="timeout">
    /// Optional timeout for the operation. If not specified, uses the default ask timeout
    /// configured in the actor system. If the timeout expires, a <see cref="TimeoutException"/> is thrown.
    /// </param>
    /// <returns>The response from the actor.</returns>
    /// <remarks>
    /// <para>
    /// This method blocks the calling thread until a response is received or the timeout expires.
    /// For non-blocking alternatives, use <see cref="AskAsync{TResponse}(object, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// The ask pattern creates a temporary actor to handle the response, which is automatically
    /// cleaned up after the response is received or the timeout expires.
    /// </para>
    /// <exception cref="TimeoutException">Thrown when the specified timeout expires before receiving a response.</exception>
    /// </remarks>
    TResponse Ask<TResponse>(object request, TimeSpan? timeout = null);

    /// <summary>
    /// Sends a request message with metadata to the actor and synchronously waits for a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the expected response.</typeparam>
    /// <param name="request">The request message to send to the actor.</param>
    /// <param name="metadata">Optional key-value metadata to attach to the message.</param>
    /// <param name="timeout">
    /// Optional timeout for the operation. If the timeout expires, a <see cref="TimeoutException"/> is thrown.
    /// </param>
    /// <returns>The response from the actor.</returns>
    TResponse Ask<TResponse>(
        object request,
        Dictionary<string, object?>? metadata = null,
        TimeSpan? timeout = null
    );

    /// <summary>
    /// Asynchronously sends a request message to the actor and waits for a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the expected response.</typeparam>
    /// <param name="request">The request message to send to the actor.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation.
    /// If cancelled, the operation throws <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation,
    /// containing the response from the actor.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the preferred method for request-response patterns in async contexts,
    /// as it doesn't block the calling thread and integrates well with async/await.
    /// </para>
    /// <para>
    /// Like <see cref="Ask{TResponse}(object, TimeSpan?)"/>, this method creates
    /// a temporary actor to handle the response, which is automatically cleaned up.
    /// </para>
    /// </remarks>
    Task<TResponse> AskAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously sends a request message with metadata to the actor and waits for a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the expected response.</typeparam>
    /// <param name="request">The request message to send to the actor.</param>
    /// <param name="metadata">Optional key-value metadata to attach to the message.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> containing the response from the actor.</returns>
    Task<TResponse> AskAsync<TResponse>(
        object request,
        Dictionary<string, object?>? metadata,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sends a message to the actor using fire-and-forget semantics with a timeout
    /// for message delivery to the mailbox.
    /// </summary>
    /// <param name="message">The message to send to the actor.</param>
    /// <param name="timeout">
    /// The maximum time to wait for the message to be enqueued in the actor's mailbox.
    /// If <see langword="null"/>, the method will wait indefinitely.
    /// </param>
    void Tell(object message, TimeSpan? timeout = null);

    /// <summary>
    /// Sends a message with metadata to the actor using fire-and-forget semantics.
    /// </summary>
    /// <param name="message">The message to send to the actor.</param>
    /// <param name="metadata">Optional key-value metadata to attach to the message.</param>
    /// <param name="timeout">
    /// The maximum time to wait for the message to be enqueued in the actor's mailbox.
    /// If <see langword="null"/>, the method will wait indefinitely.
    /// </param>
    void Tell(object message, Dictionary<string, object?>? metadata, TimeSpan? timeout = null);

    /// <summary>
    /// Asynchronously sends a message to the actor using fire-and-forget semantics
    /// with cancellation support.
    /// </summary>
    /// <param name="message">The message to send to the actor.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the enqueue operation.
    /// Note: Cancellation only affects message delivery to the mailbox, not message processing.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when the message has been queued
    /// in the actor's mailbox or when the operation is cancelled.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This overload provides cancellation support for the message delivery operation.
    /// If the cancellation token is triggered before the message is enqueued, the
    /// operation will be cancelled and the message will not be delivered.
    /// </para>
    /// <para>
    /// Once the message is successfully enqueued, cancellation no longer applies
    /// and the actor will process the message normally.
    /// </para>
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the provided <paramref name="cancellationToken"/>
    /// before the message is successfully enqueued.
    /// </exception>
    ValueTask TellAsync(object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously sends a message with metadata to the actor using fire-and-forget semantics.
    /// </summary>
    /// <param name="message">The message to send to the actor.</param>
    /// <param name="metadata">Optional key-value metadata to attach to the message.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the enqueue operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the message has been queued.</returns>
    ValueTask TellAsync(
        object message,
        Dictionary<string, object?>? metadata,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Registers a listener to receive notifications when this actor reference is terminated.
    /// </summary>
    /// <param name="listener">The listener to register.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, automatically unregisters the listener.</returns>
    IDisposable Register(IActorReferenceListener listener);

    /// <summary>
    /// Unregisters a previously registered termination listener.
    /// </summary>
    /// <param name="listener">The listener to unregister.</param>
    void UnRegister(IActorReferenceListener listener);
}
