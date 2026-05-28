using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions;

namespace Trupe;

/// <summary>
/// A dead letter actor reference that throws <see cref="NotImplementedException"/> for all operations.
/// Used as a placeholder when an actor reference cannot be resolved from the registry.
/// </summary>
/// <param name="name">The URI identifying this dead letter reference.</param>
public class DeadLetterActorReference(Uri name) : IActorReference
{
    /// <inheritdoc />
    public Uri Name => name;

    /// <inheritdoc />
    public TResponse Ask<TResponse>(object request, TimeSpan? timeout = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public TResponse Ask<TResponse>(
        object request,
        Dictionary<string, object?>? metadata = null,
        TimeSpan? timeout = null
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<TResponse> AskAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<TResponse> AskAsync<TResponse>(
        object request,
        Dictionary<string, object?>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task KillAsync()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void MarkAsTerminate(TerminatedReason reason)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IDisposable Register(IActorReferenceListener listener)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Stop()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Tell(object message, TimeSpan? timeout = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Tell(
        object message,
        Dictionary<string, object?>? metadata,
        TimeSpan? timeout = null
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask TellAsync(object message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask TellAsync(
        object message,
        Dictionary<string, object?>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void UnRegister(IActorReferenceListener listener)
    {
        throw new NotImplementedException();
    }
}
