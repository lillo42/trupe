using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
    private static readonly Counter<int> DeadLetterCounter = TrupeDiagnostics.Meter.CreateCounter<int>(
        "actor-reference.dead-letter",
        unit: "{operations}",
        description: "Number of messages sent to a dead letter actor reference.");

    /// <inheritdoc />
    public Uri Name => name;

    /// <inheritdoc />
    public TResponse? Ask<TResponse>(
        object request,
        Dictionary<string, object?>? metadata = null,
        TimeSpan? timeout = null
    )
    {
        DeadLetterCounter.Add(1,
            new KeyValuePair<string, object?>("actor", name),
            new KeyValuePair<string, object?>("operation", "ask"),
            new KeyValuePair<string, object?>("message.payload.type", request.GetType()));
        return default;
    }

    /// <inheritdoc />
    public Task<TResponse?> AskAsync<TResponse>(
        object request,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default
    )
    {
        DeadLetterCounter.Add(1,
            new KeyValuePair<string, object?>("actor", name),
            new KeyValuePair<string, object?>("operation", "ask"),
            new KeyValuePair<string, object?>("message.payload.type", request.GetType()));
        return Task.FromResult<TResponse?>(default);
    }

    /// <inheritdoc />
    public Task KillAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void MarkAsTerminate(TerminatedReason reason)
    {
    }

    /// <inheritdoc />
    public IDisposable Register(IActorReferenceListener listener)
    {
        return new DeadLetterRegister();
    }

    /// <inheritdoc />
    public void Stop()
    {
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Tell(
        object message,
        Dictionary<string, object?>? metadata = null,
        TimeSpan? timeout = null
    )
    {
        DeadLetterCounter.Add(1,
            new KeyValuePair<string, object?>("actor", name),
            new KeyValuePair<string, object?>("operation", "tell"),
            new KeyValuePair<string, object?>("message.payload.type", message.GetType()));
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask TellAsync(
        object message,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default
    )
    {
        DeadLetterCounter.Add(1,
            new KeyValuePair<string, object?>("actor", name),
            new KeyValuePair<string, object?>("operation", "tell"),
            new KeyValuePair<string, object?>("message.payload.type", message.GetType()));
        return new ValueTask();
    }

    /// <inheritdoc />
    public void UnRegister(IActorReferenceListener listener)
    {
    }
    
    private class DeadLetterRegister : IDisposable
    {
        public void Dispose()
        {
            
        }
    }
}
