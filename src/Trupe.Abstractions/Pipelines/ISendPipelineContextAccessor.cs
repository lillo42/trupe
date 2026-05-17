namespace Trupe.Abstractions.Pipelines;

public interface ISendPipelineContextAccessor
{
    ISendPipelineContext? SendContext { get; }
}
