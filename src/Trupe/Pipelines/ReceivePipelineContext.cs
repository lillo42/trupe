using System;
using System.Collections.Generic;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public record ReceivePipelineContext(
    IActor Actor,
    IActorContext ActorContext,
    IMessage Message,
    IServiceProvider ServiceProvider,
    PipelineMetadataCollection Metadata,
    CancellationToken CancellationToken
) : IReceivePipelineContext
{
    public Dictionary<string, object?> Items { get; set; } = [];
}
