using System;
using System.Collections.Generic;

namespace Trupe.Abstractions.Options;

/// <summary>
/// Configuration options for assembling the middleware pipeline.
/// </summary>
public class PipelineOptions
{
    /// <summary>
    /// Gets or sets the ordered list of middleware configurations that compose the pipeline.
    /// </summary>
    public List<PipelineMiddlewareConfiguration> Middlewares { get; set; } = [];
}

/// <summary>
/// Describes a single middleware registration within the pipeline, including ordering and type constraints.
/// </summary>
public class PipelineMiddlewareConfiguration
{
    /// <summary>
    /// Gets or sets the execution order of this middleware in the pipeline. Lower values execute first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets optional metadata associated with this middleware instance.
    /// </summary>
    public object? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the concrete type of the middleware to instantiate.
    /// </summary>
    public Type MiddlewareType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the actor type this middleware targets, or <c>null</c> to apply to all actors.
    /// </summary>
    public Type? ActorType { get; set; }

    /// <summary>
    /// Gets or sets the message type this middleware targets, or <c>null</c> to apply to all messages.
    /// </summary>
    public Type? MessageType { get; set; }
}
