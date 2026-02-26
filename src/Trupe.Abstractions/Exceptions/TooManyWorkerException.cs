namespace Trupe.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when attempting to create more worker actors than the configured maximum.
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
