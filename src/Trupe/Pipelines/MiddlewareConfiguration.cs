using System;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Represents a runtime middleware configuration entry used by the pipeline registry.
/// </summary>
/// <param name="Order">The execution order of the middleware; lower values execute first.</param>
/// <param name="Metadata">Optional metadata object to pass to the middleware at runtime.</param>
/// <param name="MiddlewareType">The type of the middleware to instantiate.</param>
public record MiddlewareConfiguration(int Order, object? Metadata, Type MiddlewareType)
    : IMiddlewareConfiguration
{
    /// <summary>
    /// Gets or sets the actor type this middleware is scoped to, or <c>null</c> for all actors.
    /// </summary>
    public Type? ActorType { get; set; }

    /// <summary>
    /// Gets or sets the message type this middleware is scoped to, or <c>null</c> for all messages.
    /// </summary>
    public Type? MessageType { get; set; }

    /// <summary>
    /// Gets or sets the pipeline scope (send, receive, or both) that this middleware applies to.
    /// </summary>
    public MiddlewareScope Scope { get; set; }
}
