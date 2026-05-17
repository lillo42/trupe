using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public interface IReceiveMiddleware : IMiddleware
{
    ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next);
}
