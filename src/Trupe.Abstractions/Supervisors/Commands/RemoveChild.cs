namespace Trupe.Abstractions.Supervisors.Commands;

/// <summary>
/// Command to remove a child actor from a supervisor.
/// </summary>
/// <param name="Actor">The actor instance to remove, or <see langword="null"/> to indicate no specific actor.</param>
/// <remarks>
/// This command is sent to a supervisor to request the removal of a child actor
/// from its supervision tree.
/// </remarks>
public record RemoveChild(IActor? Actor);
