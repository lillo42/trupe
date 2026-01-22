using System;
using System.Threading;
using System.Threading.Tasks;

namespace Trupe.Messages;

/// <summary>
/// Represents a concrete "Request-Response" message wrapper for local actors.
/// </summary>
/// <remarks>
/// This class bridges the asynchronous gap between the sender (who is awaiting a Task)
/// and the actor (who processes the message and sets the result later).
/// </remarks>
public class LocalAskMessage : IAskMessage
{
    private readonly TaskCompletionSource<object?> _tcs;

    /// <summary>
    /// Gets the actual data payload sent by the user.
    /// </summary>
    public object Payload { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalAskMessage"/> class.
    /// </summary>
    /// <param name="value">The request payload.</param>
    /// <param name="cancellationToken">A token to cancel the request waiting period.</param>
    /// <remarks>
    /// <para>
    /// This constructor initializes the <see cref="TaskCompletionSource{TResult}"/> with
    /// <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>.
    /// </para>
    /// <para>
    /// <strong>Why?</strong> This ensures that when the actor calls <see cref="SetResult"/>,
    /// the continuation (the code awaiting the response) runs on a thread pool thread,
    /// rather than stealing the Actor's thread immediately. This prevents the actor
    /// from being blocked by the caller's post-processing logic.
    /// </para>
    /// </remarks>
    public LocalAskMessage(object value, CancellationToken cancellationToken = default)
    {
        _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Payload = value;
        CancellationToken = cancellationToken;

        CancellationToken.Register(() =>
        {
            _tcs.TrySetCanceled(cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<object?> AsTask()
    {
        return _tcs.Task;
    }

    /// <inheritdoc />
    public void SetResult(object? result)
    {
        _tcs.TrySetResult(result);
    }

    /// <inheritdoc />
    public void SetException(Exception exception)
    {
        _tcs.TrySetException(exception);
    }
}
