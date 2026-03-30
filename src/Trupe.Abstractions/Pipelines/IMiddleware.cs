using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public interface IMiddleware
{
    ValueTask InvokeAsync(IPipelineContext context, NextDelegate next);
}
