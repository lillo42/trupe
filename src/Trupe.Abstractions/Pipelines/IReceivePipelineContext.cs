namespace Trupe.Abstractions.Pipelines;

public interface IReceivePipelineContext : IPipelineContext
{
    IActor Actor { get; }
    IActorContext ActorContext { get; }
}
