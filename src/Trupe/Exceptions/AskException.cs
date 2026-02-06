using System;

namespace Trupe.Exceptions;

public class AskException : TrupeException
{
    protected AskException() { }

    protected AskException(string? message)
        : base(message) { }

    protected AskException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
