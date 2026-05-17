using System;
using System.Collections.Generic;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public record SendPipelineContext(
    IActorReference Target,
    Type ActorType,
    IMessage Message,
    IServiceProvider ServiceProvider,
    PipelineMetadataCollection Metadata,
    CancellationToken CancellationToken
) : ISendPipelineContext
{
    public Dictionary<string, object?> Items { get; set; } = [];
}
