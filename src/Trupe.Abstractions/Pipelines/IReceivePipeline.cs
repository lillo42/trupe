using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public interface IReceivePipeline : IPipeline
{
    ValueTask ExecuteAsync(IReceivePipelineContext context);
}
