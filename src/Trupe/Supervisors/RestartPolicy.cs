namespace Trupe.Supervisors;

/// <summary>
/// Defines the restart policies for supervised actors.
/// </summary>
public enum RestartPolicy
{
    /// <summary>
    /// The actor is always restarted regardless of the termination reason.
    /// </summary>
    Permanent,

    /// <summary>
    /// The actor is restarted only if it terminates abnormally (with an error).
    /// </summary>
    Transient,

    /// <summary>
    /// The actor is never restarted, regardless of the termination reason.
    /// </summary>
    Temporary,
}
