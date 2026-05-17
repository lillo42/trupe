namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Provides ambient access to the current <see cref="ISendPipelineContext"/>.
/// </summary>
public interface ISendPipelineContextAccessor
{
    /// <summary>
    /// Gets the current send pipeline context, or <c>null</c> if no send pipeline is active.
    /// </summary>
    ISendPipelineContext? SendContext { get; }
}
