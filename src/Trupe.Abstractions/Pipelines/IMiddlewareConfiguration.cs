using System;

namespace Trupe.Abstractions.Pipelines;

public interface IMiddlewareConfiguration
{
    int Order { get; }

    object? Metadata { get; }

    Type MiddlewareType { get; }
}
