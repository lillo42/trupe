using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public interface ISendPipeline : IPipeline
{
    ValueTask ExecuteAsync(ISendPipelineContext context);
}
