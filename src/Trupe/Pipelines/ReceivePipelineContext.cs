using System;
using System.Collections.Generic;
using System.Threading;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Represents the context for a receive pipeline execution, carrying the actor, message, and associated metadata.
/// </summary>
/// <param name="Actor">The actor instance processing the message.</param>
/// <param name="ActorContext">The actor's execution context.</param>
/// <param name="Message">The inbound message being processed.</param>
/// <param name="ServiceProvider">The scoped service provider for this pipeline execution.</param>
/// <param name="Metadata">The collection of pipeline metadata objects.</param>
/// <param name="CancellationToken">The cancellation token for this pipeline execution.</param>
public record ReceivePipelineContext(
    IActor Actor,
    IActorContext ActorContext,
    IMessage Message,
    IServiceProvider ServiceProvider,
    PipelineMetadataCollection Metadata,
    CancellationToken CancellationToken
) : IReceivePipelineContext
{
    /// <summary>
    /// Gets or sets a dictionary of arbitrary key-value items scoped to this pipeline execution.
    /// </summary>
    public Dictionary<string, object?> Items { get; set; } = [];
}
