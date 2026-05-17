using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public interface ISendMiddleware
{
    ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next);
}
