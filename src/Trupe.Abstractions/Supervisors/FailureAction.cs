namespace Trupe.Abstractions.Supervisors;

/// <summary>
/// Defines the actions a supervisor can take when a child actor fails.
/// </summary>
public enum FailureAction
{
    /// <summary>
    /// Restart the failed actor, resetting its state and resuming message processing.
    /// </summary>
    Restart,

    /// <summary>
    /// Stop the failed actor permanently without restarting it.
    /// </summary>
    Stop,

    /// <summary>
    /// Escalate the failure to the parent supervisor for handling.
    /// </summary>
    Escalate,

    /// <summary>
    /// Resume the actor without restarting, allowing it to continue processing messages.
    /// </summary>
    Resume,
}
