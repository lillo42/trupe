using System;
using System.Diagnostics.CodeAnalysis;
using Trupe.Abstractions.Extensions;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Base attribute for declaring middleware on actor classes or handler methods.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MiddlewareAttribute"/> class with the specified execution order.
/// </remarks>
/// <param name="order">The execution order of this middleware. Lower values execute first.</param>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true
)]
public abstract class MiddlewareAttribute(int order) : Attribute, IMiddlewareConfiguration
{
    /// <summary>
    /// Gets the execution order of this middleware. Lower values execute first.
    /// </summary>
    public int Order { get; } = order;

    /// <summary>
    /// Gets the metadata associated with this middleware. Defaults to the attribute instance itself.
    /// </summary>
    public virtual object? Metadata => this;

    private MiddlewareScope? _scope;

    /// <summary>
    /// Gets or sets the scope indicating whether this middleware applies to send, receive, or both pipelines.
    /// When not explicitly set, the scope is inferred from the interfaces implemented by <see cref="MiddlewareType"/>.
    /// </summary>
    public virtual MiddlewareScope Scope
    {
        get
        {
            if (_scope.HasValue)
            {
                return _scope.Value;
            }

            var scope = MiddlewareScope.None;
            if (MiddlewareType.IsSendMiddleware())
            {
                scope |= MiddlewareScope.Send;
            }

            if (MiddlewareType.IsReceiveMiddleware())
            {
                scope |= MiddlewareScope.Receive;
            }

            if (scope == MiddlewareScope.None)
            {
                throw new InvalidOperationException(
                    $"Unable to determine middleware scope for {MiddlewareType.FullName}. Please specify the scope explicitly."
                );
            }

            return scope;
        }
        set { _scope = value; }
    }

    /// <summary>
    /// Gets the concrete type of the middleware implementation this attribute registers.
    /// </summary>
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )]
    public abstract Type MiddlewareType { get; }
}
