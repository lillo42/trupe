namespace Trupe.Exceptions;

/// <summary>
/// Exception thrown when attempting to initialize a supervisor that has already been initialized.
/// </summary>
/// <remarks>
/// This exception prevents duplicate initialization of supervisor strategies, which could lead to
/// inconsistent supervision behavior. Each supervisor should only be initialized once during its lifecycle.
/// </remarks>
/// <param name="message">The message that describes the initialization error.</param>
public class SupervisorAlreadyInitializedException(string message) : TrupeException(message);
