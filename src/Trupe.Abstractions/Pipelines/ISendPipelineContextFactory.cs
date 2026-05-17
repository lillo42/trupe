using System;
using System.Threading;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Pipelines;

public interface ISendPipelineContextFactory
{
    ISendPipelineContext Create(
        IActorReference reference,
        Type actorType,
        IMessage message,
        object?[] metadata,
        CancellationToken cancellationToken
    );
}
