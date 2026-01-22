using Trupe.ActorReferences;

namespace Trupe;

/// <summary>
/// Represents the execution context for the message currently being processed by an actor.
/// </summary>
/// <remarks>
/// This context is injected into the actor's message handling logic. It provides access to the actor's
/// own identity and a mechanism to return data to the caller when the Request-Response (<c>Ask</c>) pattern is used.
/// </remarks>
public interface IActorContext
{
    /// Gets the reference to the current actor instance.
    /// <summary>
    /// </summary>
    /// <value>
    /// The <see cref="IActorReference"/> representing "this" actor.
    /// </value>
    /// <remarks>
    /// Use this property if the actor needs to pass its own address to other actors
    /// (e.g., "send the reply to me here") or to send messages to itself.
    /// </remarks>
    IActorReference Self { get; }

    /// <summary>
    /// Gets or sets the response payload to be returned to the sender.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the current message was sent via <see cref="IActorReference.Ask{TRequest, TResponse}"/>,
    /// setting this property will result in the sender's awaited task completing with this value.
    /// </para>
    /// <para>
    /// If the message was sent via <see cref="IActorReference.Tell{TMessage}"/> (Fire-and-Forget),
    /// setting this property usually has no effect, as there is no receiver waiting for a result.
    /// </para>
    /// </remarks>
    object? Response { get; set; }
}
