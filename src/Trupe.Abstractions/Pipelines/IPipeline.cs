using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public interface IPipeline
{
    ValueTask ExecuteAsync(IPipelineContext contex);
}
