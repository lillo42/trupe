using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Represents the pipeline that processes incoming messages through a chain of receive middleware.
/// </summary>
public interface IReceivePipeline : IPipeline
{
    /// <summary>
    /// Executes the receive pipeline for the given context.
    /// </summary>
    /// <param name="context">The context containing the message, actor, and metadata for this execution.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask ExecuteAsync(IReceivePipelineContext context);
}
