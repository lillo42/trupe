namespace Trupe.Abstractions.Supervisors;

/// <summary>
/// Defines the supervision strategy used when a child actor fails.
/// </summary>
public enum Strategy
{
    /// <summary>
    /// Only the failed actor is affected by the supervision action.
    /// Other sibling actors continue running normally.
    /// </summary>
    OneForOne,

    /// <summary>
    /// All sibling actors are affected when one fails.
    /// If one actor fails, all actors under this supervisor receive the same action.
    /// </summary>
    AllForOne,
}
