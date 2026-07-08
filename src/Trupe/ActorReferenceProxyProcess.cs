using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;
using Trupe.Abstractions.SystemMessages;
using Trupe.Collections;
using Trupe.Guards;
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
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )]
        Type actorType,
    IServiceProvider provider
) : IActorReference, IDisposable
{
    private readonly ActorReferenceListenerCollection _collection = [];

    private bool _isDisposed;

    /// <inheritdoc />
    public Uri Name => name;

    /// <inheritdoc />
    public TResponse? Ask<TResponse>(
        object request,
        Dictionary<string, object?>? metadata = null,
        TimeSpan? timeout = null
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        using var cts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        var task = AskAsync<TResponse>(request, metadata, cts.Token);
        return task.GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<TResponse?> AskAsync<TResponse>(
        object request,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

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
    public void Tell(
        object message,
        Dictionary<string, object?>? metadata = null,
        TimeSpan? timeout = null
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        using var cts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        TellAsync(message, metadata, cts.Token).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask TellAsync(
        object message,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        var actorMessage = new TellMessage(message, metadata ?? [], CancellationToken.None);
        await ExecuteAsync(actorMessage, cancellationToken);
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
            [new ActorProcessMetadata(registry.GetProcess(this))],
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
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        Tell(new Stop());
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        await AskAsync<object?>(new Stop());
    }

    /// <inheritdoc />
    public async Task KillAsync()
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var registry = sp.GetRequiredService<IActorProcessRegistry>();
        var process = registry.GetProcess(this);
        await process.KillAsync();
    }

    /// <inheritdoc />
    public void MarkAsTerminate(TerminatedReason reason)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        _collection.InvokeOnTerminated(this, reason);
    }

    /// <summary>
    /// Disposes this actor reference by removing it from the process registry.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);

        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _collection.Clear();

            using var scope = provider.CreateScope();
            var sp = scope.ServiceProvider;

            var registry = sp.GetRequiredService<IActorProcessRegistry>();
            registry.UnRegister(this);
        }

        _isDisposed = true;
    }

    /// <inheritdoc />
    public IDisposable Register(IActorReferenceListener listener)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        return _collection.Add(listener);
    }

    /// <inheritdoc />
    public void UnRegister(IActorReferenceListener listener)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReferenceProxyProcessor));

        _collection.Remove(listener);
    }
}
