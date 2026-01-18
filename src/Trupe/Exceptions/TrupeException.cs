using System;

namespace Trupe.Exceptions;

public abstract class TrupeException : Exception
{
    protected TrupeException() { }

    protected TrupeException(string? message)
        : base(message) { }

    protected TrupeException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
