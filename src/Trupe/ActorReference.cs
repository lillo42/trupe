using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions;
using Trupe.Collections;
using Trupe.Guards;

namespace Trupe;

/// <summary>
/// A decorator around an <see cref="IActorReference"/> that resolves references via the actor process registry.
/// Supports creating references by name or URI for lookup-based resolution.
/// </summary>
public class ActorReference : IActorReference, IDisposable, IActorReferenceListener
{
    private bool _isDisposed;
    private readonly IActorReference _inner;
    private readonly ActorReferenceListenerCollection _collection;

    /// <summary>
    /// Creates a new actor reference wrapping the specified inner reference.
    /// </summary>
    /// <param name="inner">The inner actor reference to delegate to.</param>
    public ActorReference(IActorReference inner)
    {
        _collection = [];

        _inner = inner;
        _inner.Register(this);
    }

    /// <summary>
    /// Creates a new actor reference by resolving the name from the default registry.
    /// </summary>
    /// <param name="name">The actor name used for registry lookup.</param>
    public ActorReference(string name)
        : this(new Uri($"trupe://localhost/{name}"), ActorProcessRegistry.Instance)
    {
    }

    /// <summary>
    /// Creates a new actor reference by resolving the URI from the default registry.
    /// </summary>
    /// <param name="name">The actor URI used for registry lookup.</param>
    public ActorReference(Uri name)
        : this(name, ActorProcessRegistry.Instance)
    {
    }

    /// <summary>
    /// Creates a new actor reference by resolving the name from the specified registry.
    /// </summary>
    /// <param name="name">The actor name used for registry lookup.</param>
    /// <param name="registry">The registry to resolve the reference from.</param>
    public ActorReference(string name, IActorProcessRegistry registry)
        : this(new Uri($"trupe://localhost/{name}"), registry)
    {
    }

    /// <summary>
    /// Creates a new actor reference by resolving the URI from the specified registry.
    /// </summary>
    /// <param name="name">The actor URI used for registry lookup.</param>
    /// <param name="registry">The registry to resolve the reference from.</param>
    public ActorReference(Uri name, IActorProcessRegistry registry)
        : this(registry.GetReference(name))
    {
    }

    /// <inheritdoc />
    public Uri Name => _inner.Name;

    /// <inheritdoc />
    public TResponse? Ask<TResponse>(
        object request,
        Dictionary<string, object?>? metadata = null,
        TimeSpan? timeout = null
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));

        return _inner.Ask<TResponse>(request, metadata, timeout);
    }

    /// <inheritdoc />
    public Task<TResponse?> AskAsync<TResponse>(
        object request,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));

        return _inner.AskAsync<TResponse>(request, metadata, cancellationToken);
    }

    /// <inheritdoc />
    public void Tell(
        object message,
        Dictionary<string, object?>? metadata = null,
        TimeSpan? timeout = null
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));

        _inner.Tell(message, metadata, timeout);
    }

    /// <inheritdoc />
    public ValueTask TellAsync(
        object message,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));

        return _inner.TellAsync(message, metadata, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources held by this actor reference.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release managed resources; otherwise <see langword="false"/>.</param>
    private void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            ObjectDisposedGuard.Do(() => _inner.UnRegister(this));
            _collection.Clear();
        }

        _isDisposed = true;
    }

    /// <inheritdoc />
    public void Stop()
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));
        _inner.Stop();
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));
        await _inner.StopAsync();
    }

    /// <inheritdoc />
    public async Task KillAsync()
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));
        await _inner.KillAsync();
    }

    /// <inheritdoc />
    public void MarkAsTerminate(TerminatedReason reason)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));
        _inner.MarkAsTerminate(reason);
    }

    /// <inheritdoc />
    public IDisposable Register(IActorReferenceListener listener)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));

        return _collection.Add(listener);
    }

    /// <inheritdoc />
    public void UnRegister(IActorReferenceListener listener)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));

        _collection.Remove(listener);
    }

    /// <inheritdoc />
    public void OnTerminated(IActorReference reference, TerminatedReason reason)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorReference));

        _collection.InvokeOnTerminated(this, reason);
    }
}