using System;
using Trupe.Abstractions.Extensions;

namespace Trupe.Abstractions.Pipelines;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true
)]
public abstract class MiddlewareAttribute : Attribute, IMiddlewareConfiguration
{
    protected MiddlewareAttribute(int order)
    {
        Order = order;
    }

    public int Order { get; }

    public virtual object? Metadata => this;

    private MiddlewareScope? _scope;
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
    public abstract Type MiddlewareType { get; }
}
