using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Represents the pipeline that processes outgoing messages through a chain of send middleware.
/// </summary>
public interface ISendPipeline : IPipeline
{
    /// <summary>
    /// Executes the send pipeline for the given context.
    /// </summary>
    /// <param name="context">The context containing the message, target, and metadata for this execution.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask ExecuteAsync(ISendPipelineContext context);
}
