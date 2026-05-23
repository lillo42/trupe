using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.SystemMessages;

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
        IAsyncDisposable
{
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
    /// Registers a death watch on the specified actor reference. When the watched actor terminates,
    /// a <see cref="ActorTerminated"/> message is sent to this actor.
    /// </summary>
    /// <param name="reference">The actor reference to watch.</param>
    public void DeathWatch(IActorReference reference)
    {
        reference.Terminated += OnDeathWatch;
    }

    /// <summary>
    /// Asynchronously disposes the associated DI scope.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (Scope is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }
        else
        {
            Scope.Dispose();
            return new ValueTask();
        }
    }

    /// <summary>
    /// Unregisters a death watch on the specified actor reference.
    /// </summary>
    /// <param name="reference">The actor reference to stop watching.</param>
    public void UnWatchDeath(IActorReference reference)
    {
        reference.Terminated -= OnDeathWatch;
    }

    private void OnDeathWatch(object? sender, ActorReferenceTerminatedEventArgs args)
    {
        Self.Tell(new ActorTerminated(args.Reference, TerminatedReason.Stopped));
    }
}
