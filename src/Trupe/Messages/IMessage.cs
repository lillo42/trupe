using System.Threading;

namespace Trupe.Messages;

/// <summary>
/// Represents a message that can be sent between actors in the Trupe actor system.
/// This is the base interface for all actor messages, providing common message metadata.
/// </summary>
/// <remarks>
/// <para>
/// Messages are the fundamental unit of communication between actors. All messages
/// in the Trupe system implement this interface, which provides access to the
/// message payload and cancellation semantics.
/// </para>
/// <para>
/// The <see cref="Payload"/> property allows for type-erased message handling while
/// maintaining strong typing at the API boundaries through generic methods.
/// </para>
/// <para>
/// Implementations of this interface should be immutable to ensure thread-safe
/// message passing between actors.
/// </para>
/// </remarks>
public interface IMessage
{
    /// <summary>
    /// Gets the actual payload sent to the actor.
    /// </summary>
    /// <value>The user-defined message object.</value>
    object Payload { get; }

    /// <summary>
    /// Gets the cancellation token associated with the message.
    /// </summary>
    /// <value>
    /// A <see cref="CancellationToken"/> that can be used to observe cancellation
    /// requests for the message processing. May be <see cref="CancellationToken.None"/>
    /// for messages that do not support cancellation.
    /// </value>
    /// <remarks>
    /// <para>
    /// If this token is triggered, the actor may choose to skip processing this message.
    /// </para>
    /// <para>
    /// This token allows for cooperative cancellation of message processing.
    /// Actors should periodically check this token during long-running operations
    /// and throw <see cref="OperationCanceledException"/> when cancellation is requested.
    /// </para>
    /// <para>
    /// For fire-and-forget messages (<see cref="ITellMessage"/>), this token typically
    /// represents the cancellation of the message delivery operation. For request-response
    /// messages (<see cref="IAskMessage"/>), this token represents the cancellation
    /// of the entire request-response cycle.
    /// </para>
    /// <para>
    /// When the token is cancelled, actors should clean up resources and respond
    /// appropriately (e.g., by completing the ask with a cancellation exception).
    /// </para>
    /// </remarks>
    CancellationToken CancellationToken { get; }
}
