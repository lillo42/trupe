using System;
using System.Collections.Generic;
using System.Threading;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Pipelines;

public interface IPipelineContext
{
    IMessage Message { get; }

    Dictionary<string, object?> Items { get; set; }

    PipelineMetadataCollection Metadata { get; }

    IServiceProvider ServiceProvider { get; }

    CancellationToken CancellationToken { get; }
}
