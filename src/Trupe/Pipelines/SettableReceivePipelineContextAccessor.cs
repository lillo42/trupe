using System.Threading;

namespace Trupe.Abstractions.Pipelines;

public class SettableReceivePipelineContextAccessor : IReceivePipelineContextAccessor
{
    private readonly AsyncLocal<IReceivePipelineContext?> _receivePipelineContext = new();
    public IReceivePipelineContext? ReceiveContext
    {
        get { return _receivePipelineContext.Value; }
        set { _receivePipelineContext.Value = value; }
    }
}
