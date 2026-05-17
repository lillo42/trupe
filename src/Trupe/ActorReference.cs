using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;
using Trupe.Messages;
using Trupe.Pipelines;

namespace Trupe;

public class ActorReference(Type actorType, IServiceProvider provider, IMailbox mailbox)
    : IActorReference
{
    /// <inheritdoc />
    public event EventHandler<TerminatedEventArgs>? Terminated;

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
    public bool Equals(IActorReference? other)
    {
        return ReferenceEquals(this, other);
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

    public void Terminate(string? reason)
    {
        Terminated?.Invoke(this, new TerminatedEventArgs(this, reason));
    }

    private async ValueTask ExecuteAsync(
        IMessage message,
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var pipelineFactory = sp.GetRequiredService<ISendPipelineFactory>();
        var pipelineContextFactory = sp.GetRequiredService<ISendPipelineContextFactory>();

        var pipeline = pipelineFactory.Create(actorType, message.Payload.GetType());
        var context = pipelineContextFactory.Create(
            this,
            actorType,
            message,
            [new MailboxMetadata(mailbox)],
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
}
