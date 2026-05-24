using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.SystemMessages;
using Trupe.Extensions;
using Trupe.Guards;

namespace Trupe;

/// <summary>
/// Provides a concrete implementation of the execution context for an actor.
/// </summary>
/// <remarks>
/// This class is instantiated by the actor infrastructure (typically within the message processing loop)
/// <para>
/// before invoking the actor's behavior. It holds the state specific to the processing of a single message,
/// such as the response to be sent back to the caller.
/// </para>
/// <para>
/// Instances of this class are generally short-lived and are discarded after the message has been processed.
/// </para>
/// </remarks>
/// <param name="Self">The reference to the actor this context belongs to.</param>
/// <param name="Scope">The DI scope associated with this context. Disposed when the context is disposed.</param>
public record ActorContext(IActorReference Self, IServiceScope Scope)
    : IActorContext,
        IActorReferenceListener,
        IAsyncDisposable
{
    private readonly Dictionary<IActorReference, IDisposable> _deathWatch = [];

    private bool _isDisposed;

    /// <summary>
    /// Initializes a new <see cref="ActorContext"/> with pre-existing metadata entries.
    /// </summary>
    /// <param name="self">The reference to the actor this context belongs to.</param>
    /// <param name="metadata">Initial metadata key-value pairs to copy into this context.</param>
    /// <param name="scope">The DI scope associated with this context.</param>
    public ActorContext(
        IActorReference self,
        Dictionary<string, object?> metadata,
        IServiceScope scope
    )
        : this(self, scope)
    {
        Metadata = new Dictionary<string, object?>(metadata);
    }

    /// <summary>
    /// Gets or sets the response object to be returned to the sender of the current message.
    /// </summary>
    /// <remarks>
    /// When the actor logic sets this property, the infrastructure will automatically capture the value
    /// and use it to complete the <see cref="System.Threading.Tasks.Task"/> associated with an <c>IAskMessage</c>.
    /// Defaults to <c>null</c>.
    /// </remarks>
    public object? Response { get; set; }

    /// <summary>
    /// Gets the actor-scoped metadata dictionary.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets the scoped service provider derived from the associated <see cref="Scope"/>.
    /// </summary>
    public IServiceProvider ServiceProvider { get; set; } = Scope.ServiceProvider;

    /// <summary>
    /// Gets the unique URI name of the actor.
    /// </summary>
    public Uri Name => Self.Name;

    /// <summary>
    /// Asynchronously disposes the associated DI scope.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);

        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsync(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _deathWatch.ForEach(x => x.Value.Dispose());
            _deathWatch.Clear();
            if (Scope is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                Scope.Dispose();
            }
        }

        _isDisposed = true;
    }

    public void OnTerminated(IActorReference reference, TerminatedReason reason)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorContext));

        Self.Tell(new ActorTerminated(reference, reason));
    }

    public void DeathWatch(IActorReference reference)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorContext));

        if (!_deathWatch.ContainsKey(reference))
        {
            _deathWatch.Add(reference, reference.Register(this));
        }
    }

    public void UnWatchDeath(IActorReference reference)
    {
        ObjectDisposedGuard.ThrowIf(_isDisposed, nameof(ActorContext));

        if (_deathWatch.TryGetValue(reference, out var disposable))
        {
            _deathWatch.Remove(reference);

            disposable.Dispose();
        }
    }
}
