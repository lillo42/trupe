using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public delegate ValueTask NextDelegate(IPipelineContext context);
