using System.Threading;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Provides ambient access to the current <see cref="IReceivePipelineContext"/> using async-local storage.
/// </summary>
public class SettableReceivePipelineContextAccessor : IReceivePipelineContextAccessor
{
    private readonly AsyncLocal<IReceivePipelineContext?> _receivePipelineContext = new();

    /// <summary>
    /// Gets or sets the current receive pipeline context for the executing async flow.
    /// </summary>
    public IReceivePipelineContext? ReceiveContext
    {
        get { return _receivePipelineContext.Value; }
        set { _receivePipelineContext.Value = value; }
    }
}
