using System;

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

            var isSend = MiddlewareType.IsAssignableFrom(typeof(ISendMiddleware));
            var isReceive = MiddlewareType.IsAssignableFrom(typeof(IReceiveMiddleware));

            if (isSend && isReceive)
            {
                return MiddlewareScope.Both;
            }
            else if (isSend)
            {
                return MiddlewareScope.Send;
            }
            else if (isReceive)
            {
                return MiddlewareScope.Receive;
            }

            throw new InvalidOperationException(
                $"Unable to determine middleware scope for {MiddlewareType.FullName}. Please specify the scope explicitly."
            );
        }
        set { _scope = value; }
    }
    public abstract Type MiddlewareType { get; }
}
