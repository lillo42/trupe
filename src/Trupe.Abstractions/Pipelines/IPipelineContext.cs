using System;
using System.Collections.Generic;
using System.Threading;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Provides shared contextual data available to middleware during pipeline execution.
/// </summary>
public interface IPipelineContext
{
    /// <summary>
    /// Gets the message being processed by the pipeline.
    /// </summary>
    IMessage Message { get; }

    /// <summary>
    /// Gets or sets a dictionary for storing arbitrary data that flows through the pipeline.
    /// </summary>
    Dictionary<string, object?> Items { get; set; }

    /// <summary>
    /// Gets the metadata collection associated with this pipeline execution.
    /// </summary>
    PipelineMetadataCollection Metadata { get; }

    /// <summary>
    /// Gets the scoped service provider for resolving dependencies during pipeline execution.
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the cancellation token that signals when the pipeline execution should be aborted.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
