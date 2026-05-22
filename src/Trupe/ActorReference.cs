using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;

namespace Trupe;

/// <summary>
/// A decorator around an <see cref="IActorReference"/> that resolves references via the actor process registry.
/// Supports creating references by name or URI for lookup-based resolution.
/// </summary>
public class ActorReference : IActorReference, IDisposable
{
    private readonly IActorReference _inner;

    /// <summary>
    /// Creates a new actor reference wrapping the specified inner reference.
    /// </summary>
    /// <param name="inner">The inner actor reference to delegate to.</param>
    public ActorReference(IActorReference inner)
    {
        _inner = inner;
        _inner.Terminated += OnTerminated;
    }

    /// <summary>
    /// Creates a new actor reference by resolving the name from the default registry.
    /// </summary>
    /// <param name="name">The actor name used for registry lookup.</param>
    public ActorReference(string name)
        : this(new Uri($"trupe://localhost/{name}"), ActorProcessRegistry.Instance) { }

    /// <summary>
    /// Creates a new actor reference by resolving the URI from the default registry.
    /// </summary>
    /// <param name="name">The actor URI used for registry lookup.</param>
    public ActorReference(Uri name)
        : this(name, ActorProcessRegistry.Instance) { }

    /// <summary>
    /// Creates a new actor reference by resolving the name from the specified registry.
    /// </summary>
    /// <param name="name">The actor name used for registry lookup.</param>
    /// <param name="registry">The registry to resolve the reference from.</param>
    public ActorReference(string name, IActorProcessRegistry registry)
        : this(new Uri($"trupe://localhost/{name}"), registry) { }

    /// <summary>
    /// Creates a new actor reference by resolving the URI from the specified registry.
    /// </summary>
    /// <param name="name">The actor URI used for registry lookup.</param>
    /// <param name="registry">The registry to resolve the reference from.</param>
    public ActorReference(Uri name, IActorProcessRegistry registry)
        : this(registry.GetReference(name)) { }

    /// <inheritdoc />
    public Uri Name => _inner.Name;

    /// <inheritdoc />
    public event EventHandler<ActorReferenceTerminatedEventArgs>? Terminated;

    /// <inheritdoc />
    public TResponse Ask<TResponse>(object request, TimeSpan? timeout = null)
    {
        return _inner.Ask<TResponse>(request, timeout);
    }

    /// <inheritdoc />
    public TResponse Ask<TResponse>(
        object request,
        Dictionary<string, object>? metadata,
        TimeSpan? timeout = null
    )
    {
        return _inner.Ask<TResponse>(request, metadata, timeout);
    }

    /// <inheritdoc />
    public Task<TResponse> AskAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default
    )
    {
        return _inner.AskAsync<TResponse>(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResponse> AskAsync<TResponse>(
        object request,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        return _inner.AskAsync<TResponse>(request, metadata, cancellationToken);
    }

    /// <inheritdoc />
    public void Tell(object message, TimeSpan? timeout = null)
    {
        _inner.Tell(message, timeout);
    }

    /// <inheritdoc />
    public void Tell(object message, Dictionary<string, object>? metadata, TimeSpan? timeout = null)
    {
        _inner.Tell(message, metadata, timeout);
    }

    /// <inheritdoc />
    public ValueTask TellAsync(object message, CancellationToken cancellationToken = default)
    {
        return _inner.TellAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask TellAsync(
        object message,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default
    )
    {
        return _inner.TellAsync(message, metadata, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _inner.Terminated -= OnTerminated;
    }

    private void OnTerminated(object? sender, ActorReferenceTerminatedEventArgs args)
    {
        Terminated?.Invoke(this, new ActorReferenceTerminatedEventArgs(this, args.Reason));
    }

    /// <inheritdoc />
    public void Stop()
    {
        _inner.Stop();
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        await _inner.StopAsync();
    }

    /// <inheritdoc />
    public Task KillAsync()
    {
        return _inner.KillAsync();
    }

    /// <inheritdoc />
    public void MarkAsTerminate(TerminatedReason reason)
    {
        _inner.MarkAsTerminate(reason);
    }
}
