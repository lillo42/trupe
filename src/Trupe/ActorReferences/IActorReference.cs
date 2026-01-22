using System;
using System.Threading;
using System.Threading.Tasks;

namespace Trupe.ActorReferences;

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
/// and request-response (<see cref="Ask{TRequest, TResponse}"/>) messaging patterns.
/// </para>
/// </remarks>
public interface IActorReference
{
    /// <summary>
    /// Gets the unique Uniform Resource Identifier (URI) identifier of the referenced actor.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that uniquely identifies the actor within the actor system.
    /// The URI format follows: <c>trupe://[system]/[actor-path]</c>
    /// </value>
    Uri Name { get; }

    /// <summary>
    /// Sends a request message to the actor and synchronously waits for a response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message. Must be a non-nullable type.</typeparam>
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
    /// For non-blocking alternatives, use <see cref="AskAsync{TRequest, TResponse}(TRequest, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// The ask pattern creates a temporary actor to handle the response, which is automatically
    /// cleaned up after the response is received or the timeout expires.
    /// </para>
    /// <exception cref="TimeoutException">Thrown when the specified timeout expires before receiving a response.</exception>
    /// <exception cref="ActorUnavailableException">Thrown when the target actor is unavailable or terminated.</exception>
    /// </remarks>
    TResponse Ask<TRequest, TResponse>(TRequest request, TimeSpan? timeout = null)
        where TRequest : notnull;

    /// <summary>
    /// Asynchronously sends a request message to the actor and waits for a response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message. Must be a non-nullable type.</typeparam>
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
    /// Like <see cref="Ask{TRequest, TResponse}(TRequest, TimeSpan?)"/>, this method creates
    /// a temporary actor to handle the response, which is automatically cleaned up.
    /// </para>
    /// </remarks>
    ValueTask<TResponse> AskAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default
    )
        where TRequest : notnull;

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
