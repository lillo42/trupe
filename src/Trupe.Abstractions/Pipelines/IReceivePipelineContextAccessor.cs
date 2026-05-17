namespace Trupe.Abstractions.Pipelines;

public interface IReceivePipelineContextAccessor
{
    IReceivePipelineContext? ReceiveContext { get; }
}
