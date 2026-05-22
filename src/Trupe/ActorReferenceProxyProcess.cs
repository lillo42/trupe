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

public class ActorReferenceProxyProcessor(
    Uri name,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
    IServiceProvider provider
) : IActorReference, IDisposable
{
    public Uri Name => name;

    public event EventHandler<ActorReferenceTerminatedEventArgs>? Terminated;

    public TResponse Ask<TResponse>(object request, TimeSpan? timeout = null)
    {
        return Ask<TResponse>(request, null, timeout);
    }

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

    public Task<TResponse> AskAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default
    )
    {
        return AskAsync<TResponse>(request, null, cancellationToken);
    }

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

    public void Tell(object message, TimeSpan? timeout = null)
    {
        Tell(message, null, timeout);
    }

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

    public ValueTask TellAsync(object message, CancellationToken cancellationToken = default)
    {
        return TellAsync(message, null, cancellationToken);
    }

    public async ValueTask TellAsync(
        object message,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        var actorMessage = new TellMessage(message, metadata ?? [], CancellationToken.None);
        await ExecuteAsync(actorMessage, cancellationToken);
    }

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

    public void Stop()
    {
        Tell(new Stop());
    }

    public async Task StopAsync()
    {
        await AskAsync<object?>(new Stop());
    }

    public async Task KillAsync()
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var registry = sp.GetRequiredService<IActorProcessRegistry>();
        var process = registry.Get(this);
        await process.KillAsync();
    }

    public void MarkAsTerminate(TerminatedReason reason)
    {
        Terminated?.Invoke(this, new ActorReferenceTerminatedEventArgs(this, reason));
    }

    public void Dispose()
    {
        using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var registry = sp.GetRequiredService<IActorProcessRegistry>();
        registry.Remove(this);
    }
}
