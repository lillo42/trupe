using System;
using System.Collections.Generic;

namespace Trupe.Abstractions.Options;

public class PipelineOptions
{
    public List<PipelineMiddlewareConfiguration> Middlewares { get; set; } = [];
}

public class PipelineMiddlewareConfiguration
{
    public int Order { get; set; }
    public object? Metadata { get; set; }
    public Type MiddlewareType { get; set; } = typeof(object);
    public Type? ActorType { get; set; }
    public Type? MessageType { get; set; }
}
