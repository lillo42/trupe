namespace Trupe.Abstractions.SystemMessages;

/// <summary>
/// Marker interface indicating that the message should be processed within the same service scope as the target actor,
/// rather than creating a new scope.
/// </summary>
public interface IUseSameActorScopeServiceMessage { }
