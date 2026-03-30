using System;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public record MiddlewareConfiguration(int Order, object? Metadata, Type MiddlewareType)
    : IMiddlewareConfiguration
{
    public Type? ActorType { get; set; }
    public Type? MessageType { get; set; }
}
