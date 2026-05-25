using System;
using System.Diagnostics.CodeAnalysis;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Describes the configuration of a middleware within a pipeline, including its ordering, scope, and type.
/// </summary>
public interface IMiddlewareConfiguration
{
    /// <summary>
    /// Gets the execution order of this middleware. Lower values execute first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets optional metadata associated with this middleware instance.
    /// </summary>
    object? Metadata { get; }

    /// <summary>
    /// Gets the concrete type of the middleware implementation.
    /// </summary>
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )]
    Type MiddlewareType { get; }

    /// <summary>
    /// Gets the scope indicating whether this middleware applies to send, receive, or both pipelines.
    /// </summary>
    MiddlewareScope Scope { get; }
}
