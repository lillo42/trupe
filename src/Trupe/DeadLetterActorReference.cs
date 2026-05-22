using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;

namespace Trupe;

public class DeadLetterActorReference(Uri name) : IActorReference
{
    public Uri Name => name;

    public event EventHandler<ActorReferenceTerminatedEventArgs>? Terminated;

    public TResponse Ask<TResponse>(object request, TimeSpan? timeout = null)
    {
        throw new NotImplementedException();
    }

    public TResponse Ask<TResponse>(
        object request,
        Dictionary<string, object>? metadata = null,
        TimeSpan? timeout = null
    )
    {
        throw new NotImplementedException();
    }

    public Task<TResponse> AskAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<TResponse> AskAsync<TResponse>(
        object request,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public Task KillAsync()
    {
        throw new NotImplementedException();
    }

    public void MarkAsTerminate(TerminatedReason reason)
    {
        throw new NotImplementedException();
    }

    public void Stop()
    {
        throw new NotImplementedException();
    }

    public Task StopAsync()
    {
        throw new NotImplementedException();
    }

    public void Tell(object message, TimeSpan? timeout = null)
    {
        throw new NotImplementedException();
    }

    public void Tell(object message, Dictionary<string, object>? metadata, TimeSpan? timeout = null)
    {
        throw new NotImplementedException();
    }

    public ValueTask TellAsync(object message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask TellAsync(
        object message,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }
}
