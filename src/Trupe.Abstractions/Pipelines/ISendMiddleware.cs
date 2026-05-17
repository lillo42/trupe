using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public interface ISendMiddleware : IMiddleware
{
    ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next);
}
