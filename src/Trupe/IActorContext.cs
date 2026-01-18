namespace Trupe;

/// <summary>
/// Provides context and communication capabilities for an actor within the Actor model system.
/// </summary>
/// <remarks>
/// This interface represents the context in which an actor operates, providing essential methods
/// for message passing and interaction with other actors in the system.
/// </remarks>
public interface IActorContext
{
    void Response(object response);
}

