namespace Trupe.Supervisors.Commands;

/// <summary>
/// Command to add a pre-created child actor to a supervisor.
/// </summary>
/// <param name="Child">The child actor metadata to add to the supervisor's children list.</param>
public record AddActor(Child Child);
