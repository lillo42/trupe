using System;

namespace Trupe.Abstractions.Pipelines;

public interface IReceivePipelineFactory
{
    IReceivePipeline Create(Type actorType, Type messageType);
}
