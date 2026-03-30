using System;
using System.Collections.Generic;
using System.Threading;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public record PipelineContext(
    IMessage Message,
    IServiceProvider ServiceProvider,
    PipelineMetadataCollection Metadata,
    CancellationToken CancellationToken
) : IPipelineContext
{
    public Dictionary<string, object?> Items { get; set; } = [];
}
