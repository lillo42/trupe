using System;
using System.Collections.Generic;

namespace Trupe.Abstractions;

/// <summary>
/// Represents the execution context for the message currently being processed by an actor.
/// </summary>
/// <remarks>
/// This context is injected into the actor's message handling logic. It provides access to the actor's
/// own identity and a mechanism to return data to the caller when the Request-Response (<c>Ask</c>) pattern is used.
/// </remarks>
public interface IActorContext
{
    /// <summary>
    /// Gets the reference to the current actor instance.
    /// </summary>
    /// <value>
    /// The <see cref="IActorReference"/> representing "this" actor.
    /// </value>
    /// <remarks>
    /// Use this property if the actor needs to pass its own address to other actors
    /// (e.g., "send the reply to me here") or to send messages to itself.
    /// </remarks>
    IActorReference Self { get; }

    /// <summary>
    /// Gets the unique URI identifying the current actor.
    /// </summary>
    Uri Name { get; }

    /// <summary>
    /// Gets or sets the response payload to be returned to the sender.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the current message was sent via <see cref="IActorReference.Ask{TResponse}"/>,
    /// setting this property will result in the sender's awaited task completing with this value.
    /// </para>
    /// <para>
    /// If the message was sent via <see cref="IActorReference.Tell"/> (Fire-and-Forget),
    /// setting this property usually has no effect, as there is no receiver waiting for a result.
    /// </para>
    /// </remarks>
    object? Response { get; set; }

    /// <summary>
    /// Gets or sets a dictionary of arbitrary metadata associated with the current message context.
    /// </summary>
    /// <remarks>
    /// Metadata is propagated through the pipeline and can be used by middlewares or the actor
    /// to carry contextual information alongside the message payload.
    /// </remarks>
    Dictionary<string, object?> Metadata { get; set; }

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> scoped to the current message being processed.
    /// </summary>
    /// <remarks>
    /// A new scope is created for each message, so services resolved through this provider
    /// follow the standard scoped lifetime. The scope is disposed after message processing completes.
    /// </remarks>
    IServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// Registers a death watch on the specified actor reference.
    /// When that actor terminates, an <c>ActorTerminated</c> message will be sent to the current actor.
    /// </summary>
    /// <param name="reference">The actor reference to watch.</param>
    void DeathWatch(IActorReference reference);

    /// <summary>
    /// Removes a previously registered death watch on the specified actor reference.
    /// </summary>
    /// <param name="reference">The actor reference to stop watching.</param>
    void UnWatchDeath(IActorReference reference);
}
