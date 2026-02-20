using System;

namespace Trupe.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when an ask operation fails within the actor system.
/// </summary>
/// <remarks>
/// This exception serves as a base class for exceptions related to the ask pattern,
/// which is used when an actor sends a message and expects a response. Failures can occur due to:
/// - Timeout while waiting for a response
/// - The target actor being unavailable or stopped
/// - The target actor throwing an exception while processing the request
/// </remarks>
public class AskException : TrupeException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AskException"/> class.
    /// </summary>
    protected AskException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AskException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    protected AskException(string? message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AskException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected AskException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
