using System;

namespace Trupe.Abstractions.Pipelines;

public interface ISendPipelineFactory
{
    ISendPipeline Create(Type actorType, Type messageType);
}
