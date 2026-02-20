namespace Trupe.Exceptions;

/// <summary>
/// Exception thrown when attempting to create more worker actors than the configured maximum
/// in a <see cref="Trupe.Supervisors.PartitionSupervisor{TActor}"/>.
/// </summary>
public class TooManyWorkerException : TrupeException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TooManyWorkerException"/> class.
    /// </summary>
    public TooManyWorkerException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TooManyWorkerException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TooManyWorkerException(string message)
        : base(message) { }
}
