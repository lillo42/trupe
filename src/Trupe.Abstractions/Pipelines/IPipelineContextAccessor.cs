namespace Trupe.Abstractions.Pipelines;

public interface IPipelineContextAccessor
{
    IPipelineContext? PipelineContext { get; }
}
