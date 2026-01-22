using System.Threading;
using System.Threading.Tasks;

namespace Trupe;

/// <summary>
/// Represents the base contract for an actor in the actor model system.
///
/// Actors are the fundamental building blocks of Trupe's concurrent and distributed
/// computation model. Each actor encapsulates state and behavior, communicating
/// exclusively through asynchronous message passing.
/// </summary>
/// <remarks>
/// Actors in Trupe follow the principles of the Actor Model:
/// - Isolation: Actors don't share state, only messages
/// - Concurrency: Actors process messages one at a time
/// - Location transparency: Actors can be local or remote
/// - Supervision: Parent actors can supervise child actors
///
/// The dual-handler approach provides flexibility:
/// - Type-safe handlers for better compile-time checking
/// - Untyped handlers for dynamic scenarios and AOT compatibility
/// </remarks>
public interface IActor
{
    /// <summary>
    /// Gets the actor's execution context, providing access to actor system services
    /// and enabling communication with other actors.
    /// </summary>
    /// <value>
    /// An <see cref="IActorContext"/> instance that provides the actor's execution context.
    /// This context is used for all interactions with the actor system.
    /// </value>
    IActorContext Context { get; }

    /// <summary>
    /// Fallback message handler for processing incoming messages.
    /// </summary>
    /// <param name="message">The message to process, or null if no message is provided.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests. Defaults to default(CancellationToken).</param>
    /// <returns>A  <see cref="ValueTask"/> representing the asynchronous handling of the message.</returns>
    /// <remarks>
    /// This method serves as a fallback handler when:
    /// 1. The actor doesn't implement the IHandleActorMessage&lt;TMessage&gt; interface for the specific message type
    /// 2. When using Native (Ahead-Of-Time) compilation where generic interface discovery might be limited
    ///
    /// This provides a flexible fallback mechanism while still allowing for strongly-typed
    /// message handling through the IHandleActorMessage interface.
    /// </remarks>
    ValueTask HandleAsync(object? message, CancellationToken cancellationToken = default);
}
