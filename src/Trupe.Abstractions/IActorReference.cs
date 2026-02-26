using System;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions.Events;

namespace Trupe.Abstractions;

/// <summary>
/// Represents a reference to an actor in the Trupe actor system.
/// Actor references are the primary mechanism for communicating with actors,
/// providing location transparency and allowing message passing between actors.
/// </summary>
/// <remarks>
/// <para>
/// Actor references are lightweight, serializable proxies that can be passed
/// between actors and across network boundaries. They implement the actor's
/// address and communication patterns.
/// </para>
/// <para>
/// This interface supports both fire-and-forget (<see cref="Tell{TMessage}"/>)
/// and request-response (<see cref="Ask{TResponse}"/>) messaging patterns.
/// </para>
/// </remarks>
public interface IActorReference : IEquatable<IActorReference>
{
    /// <summary>
    /// Occurs when the referenced actor is terminated.
    /// </summary>
    /// <remarks>
    /// Subscribers are notified when the actor stops, allowing dependent components
    /// to react to actor lifecycle changes (e.g., cleanup or restart logic).
    /// </remarks>
    event EventHandler<TerminatedEventArgs>? OnTerminate;

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
    ValueTask<TResponse> AskAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sends a message to the actor using fire-and-forget semantics with a timeout
    /// for message delivery to the mailbox.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to send. Must be a non-nullable type.</typeparam>
    /// <param name="message">The message to send to the actor.</param>
    /// <param name="timeout">
    /// The maximum time to wait for the message to be enqueued in the actor's mailbox.
    /// If <see langword="null"/>, the method will wait indefinitely.
    /// </param>
    void Tell<TMessage>(TMessage message, TimeSpan? timeout = null)
        where TMessage : notnull;

    /// <summary>
    /// Asynchronously sends a message to the actor using fire-and-forget semantics
    /// with cancellation support.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to send. Must be a non-nullable type.</typeparam>
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
    ValueTask TellAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : notnull;
}
