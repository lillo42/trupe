using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;
using Trupe.Abstractions.SystemMessages;
using Trupe.Messages;
using Trupe.Pipelines;

namespace Trupe;

/// <summary>
/// An actor reference implementation that proxies messages through the send pipeline
/// before delivering them to the actor's mailbox. Handles both Tell (fire-and-forget)
/// and Ask (request-response) patterns.
/// </summary>
/// <param name="name">The unique URI identifying this actor.</param>
/// <param name="actorType">The type of the actor, used for pipeline resolution.</param>
/// <param name="provider">The service provider for resolving pipeline dependencies.</param>
public class ActorReferenceProxyProcessor(
    Uri name,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
    IServiceProvider provider
) : IActorReference, IDisposable
{
    /// <inheritdoc />
    public Uri Name => name;

    /// <inheritdoc />
    public event EventHandler<ActorReferenceTerminatedEventArgs>? Terminated;

    /// <inheritdoc />
    public TResponse Ask<TResponse>(object request, TimeSpan? timeout = null)
    {
        return Ask<TResponse>(request, null, timeout);
    }

    /// <inheritdoc />
    public TResponse Ask<TResponse>(
        object request,
        Dictionary<string, object>? metadata,
        TimeSpan? timeout = null
    )
    {
        using var cts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        var task = AskAsync<TResponse>(request, metadata, cts.Token);
        return task.GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task<TResponse> AskAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default
    )
    {
        return AskAsync<TResponse>(request, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResponse> AskAsync<TResponse>(
        object request,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        var actorMessage = new AskMessage(request, metadata ?? [], cancellationToken);
        await ExecuteAsync(actorMessage, cancellationToken);

        var response = await actorMessage.AsTask();
        if (response != null)
        {
            return (TResponse)response;
        }

        return default!;
    }

    /// <inheritdoc />
    public void Tell(object message, TimeSpan? timeout = null)
    {
        Tell(message, null, timeout);
    }

    /// <inheritdoc />
    public void Tell(object message, Dictionary<string, object>? metadata, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        var val = TellAsync(message, metadata, cts.Token);
        if (!val.IsCompleted)
        {
            val.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc />
    public ValueTask TellAsync(object message, CancellationToken cancellationToken = default)
    {
        return TellAsync(message, null, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask TellAsync(
        object message,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        var actorMessage = new TellMessage(message, metadata ?? [], CancellationToken.None);
        await ExecuteAsync(actorMessage, cancellationToken);
    }

    /// <summary>
    /// Raises the <see cref="Terminated"/> event with the specified reason.
    /// </summary>
    /// <param name="reason">The reason for termination.</param>
    public void Terminate(TerminatedReason? reason)
    {
        Terminated?.Invoke(this, new ActorReferenceTerminatedEventArgs(this, reason));
    }

    private async ValueTask ExecuteAsync(
        IMessage message,
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var registry = sp.GetRequiredService<IActorProcessRegistry>();
        var pipelineFactory = sp.GetRequiredService<ISendPipelineFactory>();
        var pipelineContextFactory = sp.GetRequiredService<ISendPipelineContextFactory>();

        var pipeline = pipelineFactory.Create(actorType, message.Payload.GetType());
        var context = pipelineContextFactory.Create(
            this,
            actorType,
            message,
            [new ActorProcessMetadata(registry.Get(this))],
            cancellationToken
        );

        var accessor = sp.GetRequiredService<SettableSendPipelineContextAccessor>();
        accessor.SendContext = context;

        try
        {
            await pipeline.ExecuteAsync(context);
        }
        finally
        {
            accessor.SendContext = null;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        Tell(new Stop());
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        await AskAsync<object?>(new Stop());
    }

    /// <inheritdoc />
    public async Task KillAsync()
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var registry = sp.GetRequiredService<IActorProcessRegistry>();
        var process = registry.Get(this);
        await process.KillAsync();
    }

    /// <inheritdoc />
    public void MarkAsTerminate(TerminatedReason reason)
    {
        Terminated?.Invoke(this, new ActorReferenceTerminatedEventArgs(this, reason));
    }

    /// <summary>
    /// Disposes this actor reference by removing it from the process registry.
    /// </summary>
    public void Dispose()
    {
        using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var registry = sp.GetRequiredService<IActorProcessRegistry>();
        registry.Remove(this);
    }
}
