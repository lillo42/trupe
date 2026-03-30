using System;
using System.Threading;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Pipelines;

public interface IPipelineContextFactory
{
    IPipelineContext Create(
        IMessage message,
        Type actorType,
        object?[] metadata,
        CancellationToken cancellationToken
    );
}
