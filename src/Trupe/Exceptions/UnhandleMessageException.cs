namespace Trupe.Exceptions;

/// <summary>
/// Exception thrown when an actor is unable to handle a received message.
/// </summary>
/// <remarks>
/// This exception is raised when an actor receives a message that it doesn't have a handler for,
/// typically occurring when:
/// - The actor doesn't implement the appropriate <see cref="IHandleActorMessage{TMessage}"/> interface for the message type
/// - No matching message handler method is found for the incoming message
/// - The actor's fallback <see cref="IActor.Handle(object?, CancellationToken)"/> method is called but not overridden
///
/// This exception helps identify unhandled message scenarios during development and debugging
/// of actor-based systems, making it easier to understand message flow and handler registration issues.
/// </remarks>
public class UnhandleMessageException(object? value, IActor actor)
    : TrupeException($"Actor not able to handle '{value?.GetType().FullName ?? "null"}'")
{
    /// <summary>
    /// Gets the actor that was unable to handle the message.
    /// </summary>
    /// <value>
    /// The <see cref="IActor"/> instance that received the unhandled message.
    /// </value>
    /// <remarks>
    /// This property provides access to the actor context, allowing for diagnostic purposes
    /// such as logging the actor's identity, state, or other contextual information when the exception occurs.
    /// </remarks>
    public IActor Actor { get; } = actor;

    /// <summary>
    /// Gets the message value that could not be handled by the actor.
    /// </summary>
    /// <value>
    /// The unhandled message object, or <see langword="null"/> if the message was null.
    /// </value>
    /// <remarks>
    /// This property contains the original message that caused the exception, which can be useful
    /// for debugging, logging, or implementing custom error handling strategies such as dead letter queues.
    /// The message type is preserved, allowing inspection of its structure and content.
    /// </remarks>
    public object? Value { get; } = value;
}
