namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Provides ambient access to the current <see cref="IReceivePipelineContext"/>.
/// </summary>
public interface IReceivePipelineContextAccessor
{
    /// <summary>
    /// Gets the current receive pipeline context, or <c>null</c> if no receive pipeline is active.
    /// </summary>
    IReceivePipelineContext? ReceiveContext { get; }
}
