using System.Threading;
using System.Threading.Tasks;

namespace Trupe;

/// <summary>
/// Defines a strongly-typed message handler for a specific message type.
///
/// Actors can implement this generic interface to provide strongly-typed message handling
/// for specific message types, enabling better compile-time checking, IDE support,
/// and performance optimizations through direct method calls.
///
/// The actor system's message dispatcher will automatically detect implementations of
/// this interface and use them instead of the fallback <see cref="IActor.HandleAsync"/> method
/// when available (except in Native AOT compilation scenarios).
/// </summary>
/// <typeparam name="TMessage">
/// The type of message this handler can process. This should be a concrete message type
/// that follows the actor system's messaging conventions (typically immutable data types).
/// </typeparam>
/// <remarks>
/// Benefits of using <see cref="IHandleActorMessage{TMessage}"/>:
/// - Type safety: Compile-time checking of message types
/// - Performance: Avoids pattern matching and type checking overhead
/// - Clarity: Clear intent about which messages an actor can handle
/// - Testability: Easier to unit test specific message handlers
///
/// Actors can implement multiple <see cref="IHandleActorMessage{TMessage}"/> interfaces
/// to handle different message types:
/// <code>
/// public class UserActor : Actor,
///     IHandleActorMessage<CreateUser>,
///     IHandleActorMessage<UpdateUser>,
///     IHandleActorMessage<DeleteUser>
/// {
///     // Implement each handler separately
///     public ValueTask HandleAsync(CreateUser message, CancellationToken cancellationToken) { ... }
///     public ValueTask HandleAsync(UpdateUser message, CancellationToken cancellationToken) { ... }
///     public ValueTask HandleAsync(DeleteUser message, CancellationToken cancellationToken) { ... }
/// }
/// </code>
///
/// Note: In Native AOT compilation environments, the system may not be able to
/// discover and use these interfaces at runtime, falling back to the untyped
/// <see cref="IActor.HandleAsync"/> method instead.
/// </remarks>
public interface IHandleActorMessage<TMessage>
{
    /// <summary>
    /// Handles a message of the specified type.
    /// </summary>
    /// <param name="message">The strongly-typed message to process.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests. Defaults to default(CancellationToken).</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous handling of the message.</returns>
    /// <remarks>
    /// This method is called by the actor system when a message of type TMessage
    /// is delivered to an actor that implements this interface. The actor should
    /// process the message and perform any necessary state changes or side effects.
    /// </remarks>
    ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken = default);
}
