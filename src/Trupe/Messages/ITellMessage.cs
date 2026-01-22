namespace Trupe.Messages;

/// <summary>
/// Represents a "Fire-and-Forget" message envelope.
/// </summary>
/// <remarks>
/// Messages implementing this interface are one-way; the sender does not wait for a response,
/// and the actor does not need to send a reply.
/// </remarks>
public interface ITellMessage : IMessage { }
