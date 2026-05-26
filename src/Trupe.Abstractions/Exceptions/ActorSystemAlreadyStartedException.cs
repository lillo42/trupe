namespace Trupe.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when an attempt is made to start an actor system that has already been started.
/// </summary>
public class ActorSystemAlreadyStartedException()
    : TrupeException("Actor System already started") { }
