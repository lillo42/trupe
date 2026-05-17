using System;

namespace Trupe.Abstractions.Pipelines;

public interface ISendPipelineContext : IPipelineContext
{
    Type ActorType { get; }
    IActorReference Target { get; }
}
