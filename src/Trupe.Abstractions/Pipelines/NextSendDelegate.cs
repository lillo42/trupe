using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

public delegate ValueTask NextSendDelegate(ISendPipelineContext context);
