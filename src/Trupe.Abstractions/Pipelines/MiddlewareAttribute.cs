using System;

namespace Trupe.Abstractions.Pipelines;

public abstract class MiddlewareAttribute : Attribute, IMiddlewareConfiguration
{
    public int Order { get; }

    protected MiddlewareAttribute(int order)
    {
        Order = order;
    }

    public abstract Type MiddlewareType { get; }

    public object? Metadata => this;
}
