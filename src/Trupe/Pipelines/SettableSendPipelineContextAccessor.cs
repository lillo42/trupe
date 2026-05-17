using System.Threading;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Provides ambient access to the current <see cref="ISendPipelineContext"/> using async-local storage.
/// </summary>
public class SettableSendPipelineContextAccessor : ISendPipelineContextAccessor
{
    private readonly AsyncLocal<ISendPipelineContext?> _sendPipelineContext = new();

    /// <summary>
    /// Gets or sets the current send pipeline context for the executing async flow.
    /// </summary>
    public ISendPipelineContext? SendContext
    {
        get { return _sendPipelineContext.Value; }
        set { _sendPipelineContext.Value = value; }
    }
}
