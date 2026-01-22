using Trupe.ActorReferences;

namespace Trupe;

/// <summary>
/// Provides a concrete implementation of the execution context for an actor.
/// </summary>
/// <remarks>
/// This class is instantiated by the actor infrastructure (typically within the message processing loop)
/// <para>
/// before invoking the actor's behavior. It holds the state specific to the processing of a single message,
/// such as the response to be sent back to the caller.
/// </para>
/// <para>
/// Instances of this class are generally short-lived and are discarded after the message has been processed.
/// </para>
/// </remarks>
/// <param name="self">The reference to the actor this context belongs to.</param>
public class ActorContext(IActorReference self) : IActorContext
{
    /// <inheritdoc />
    public IActorReference Self { get; } = self;

    /// <summary>
    /// Gets or sets the response object to be returned to the sender of the current message.
    /// </summary>
    /// <remarks>
    /// When the actor logic sets this property, the infrastructure will automatically capture the value
    /// and use it to complete the <see cref="System.Threading.Tasks.Task"/> associated with an <c>IAskMessage</c>.
    /// Defaults to <c>null</c>.
    /// </remarks>
    public object? Response { get; set; }
}
