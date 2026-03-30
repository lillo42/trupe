using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;

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
/// <param name="self">The reference to the actor this context belongs to.</param>
/// <param name="scope">The DI scope associated with this context. Disposed when the context is disposed.</param>
public record ActorContext(IActorReference Self, IServiceScope Scope)
    : IActorContext,
        IAsyncDisposable
{
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

    public Dictionary<string, object?> Metadata { get; } = [];

    public IServiceProvider ServiceProvider { get; } = Scope.ServiceProvider;

    public ValueTask DisposeAsync()
    {
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
}
