using System.Threading;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Pipelines;

public interface IReceivePipelineContextFactory
{
    IReceivePipelineContext Create(
        IActor actor,
        IActorContext actorContext,
        IMessage message,
        object?[] metadata,
        CancellationToken cancellationToken
    );
}
