using System.Threading;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class SettablePipelineContextAccessor : IPipelineContextAccessor
{
    private readonly AsyncLocal<IPipelineContext> _context = new();
    public IPipelineContext? PipelineContext
    {
        get { return _context.Value; }
        set { _context.Value = value!; }
    }
}
