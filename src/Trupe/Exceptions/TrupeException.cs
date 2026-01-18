using System;

namespace Trupe.Exceptions;

/// <summary>
/// Abstract exception class for all exceptions thrown within the Trupe actor system.
/// </summary>
/// <remarks>
/// This abstract class serves as the foundation for all custom exceptions in the Trupe actor model implementation.
/// By deriving all system exceptions from this base class, developers can easily catch and handle Trupe-specific
/// exceptions while distinguishing them from general .NET exceptions.
///
/// The Trupe actor system uses exceptions to communicate various error conditions including:
/// - Actor lifecycle failures (startup, shutdown, restart)
/// - Message handling errors
/// - Mailbox processing issues
/// - Actor system infrastructure problems
/// - Communication failures between actors
///
/// Concrete exception classes should inherit from this base class to maintain consistency and enable
/// system-wide exception handling policies.
/// </remarks>
public abstract class TrupeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrupeException"/> class.
    /// </summary>
    /// <remarks>
    /// This protected constructor is intended for use by derived exception classes that do not need
    /// to provide a specific error message. It initializes the exception with default values.
    /// </remarks>
    protected TrupeException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrupeException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">
    /// The message that describes the error condition. This should provide meaningful information
    /// about the specific failure within the actor system context.
    /// </param>
    /// <remarks>
    /// Derived exception classes should use this constructor when they need to provide a custom
    /// error message that helps with debugging and logging within the actor system.
    /// </remarks>
    protected TrupeException(string? message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrupeException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">
    /// The message that describes the error condition. This should provide meaningful information
    /// about the specific failure within the actor system context.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception. This is typically used when
    /// wrapping lower-level exceptions (such as .NET framework exceptions) within Trupe-specific exceptions.
    /// </param>
    /// <remarks>
    /// This constructor is particularly useful when the Trupe actor system encounters underlying
    /// infrastructure failures (e.g., network issues, serialization errors, etc.) and needs to
    /// wrap them in a Trupe-specific exception while preserving the original exception context
    /// for diagnostic purposes.
    /// </remarks>
    protected TrupeException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
