using System.Threading;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class SettableSendPipelineContextAccessor : ISendPipelineContextAccessor
{
    private readonly AsyncLocal<ISendPipelineContext?> _sendPipelineContext = new();
    public ISendPipelineContext? SendContext
    {
        get { return _sendPipelineContext.Value; }
        set { _sendPipelineContext.Value = value; }
    }
}
