using System.Threading;
using System.Threading.Tasks;
using Trupe.Exceptions;

namespace Trupe;

/// <summary>
/// Abstract base class for actors in the Trupe actor model system.
///
/// Provides a foundation for actor implementations with sensible defaults
/// and common functionality. Derived classes should override message handling
/// methods to implement specific actor behavior.
///
/// This abstract class implements the <see cref="IActor"/> interface and serves
/// as the primary base class for user-defined actors in Trupe.
/// </summary>
/// <remarks>
/// Actors should inherit from this class and implement either:
/// 1. Type-safe handlers by implementing <see cref="IHandleActorMessage{TMessage}"/> interfaces
/// 2. Override the virtual <see cref="HandleAsync"/> method for untyped message handling
/// 3. A combination of both, where type-safe handlers take precedence
///
/// The actor system guarantees that only one message is processed at a time per actor,
/// ensuring thread-safe access to the actor's internal state without the need for locks.
/// </remarks>
/// <example>
/// Basic actor implementation with type-safe handlers:
/// <code>
/// public class CalculatorActor : Actor, IHandleActorMessage<AddNumbers>
/// {
///     private int _total = 0;
///
///     public ValueTask HandleAsync(AddNumbers message, CancellationToken cancellationToken)
///     {
///         _total += message.Value;
///         Context.Response(_total);
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
///
/// Actor implementation with fallback handler:
/// <code>
/// public class LoggingActor : Actor
/// {
///     public override ValueTask HandleAsync(object? message, CancellationToken cancellationToken)
///     {
///         Console.WriteLine($"Received: {message}");
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public abstract class Actor : IActor
{
    /// <summary>
    /// Gets the context associated with this actor instance.
    /// </summary>
    /// <value>
    /// The actor context that provides access to actor system services, message routing,
    /// and communication capabilities with other actors.
    /// </value>
    /// <remarks>
    /// This property is set internally by the actor system when the actor is created and started.
    /// The context enables the actor to send responses, access system information, and interact
    /// with the actor runtime environment.
    /// </remarks>
    public IActorContext Context { get; set; } = null!;

    /// <summary>
    /// Handles incoming messages that are not processed by strongly-typed handlers.
    /// </summary>
    /// <remarks>
    /// 1. The actor doesn't implement <see cref="IHandleActorMessage{TMessage}"/> for the specific message type
    /// 2. No matching strongly-typed handler is found for the incoming message
    /// </remarks>
    public virtual ValueTask HandleAsync(
        object? message,
        CancellationToken cancellationToken = default
    )
    {
        throw new UnhandleMessageException(message, this);
    }
}
