using System;
using System.Threading.Tasks;

namespace Trupe.Messages;

/// <summary>
/// Represents a "Request-Response" message envelope.
/// </summary>
/// <remarks>
/// <para>
/// This interface acts as a promise (similar to <see cref="TaskCompletionSource{TResult}"/>).
/// It carries the payload to the actor and provides mechanisms for the actor to
/// return a result or an exception back to the waiting sender.
/// </para>
/// </remarks>
public interface IAskMessage : IMessage
{
    /// <summary>
    /// Gets the task that represents the pending response from the actor.
    /// </summary>
    /// <returns>A <see cref="Task{Object}"/> that the sender awaits to receive the result.</returns>
    Task<object?> AsTask();

    /// <summary>
    /// Completes the associated task successfully with the provided result.
    /// </summary>
    /// <param name="result">The response object to return to the sender.</param>
    void SetResult(object? result);

    /// <summary>
    /// Completes the associated task with a failed state.
    /// </summary>
    /// <param name="exception">The exception that occurred during actor processing.</param>
    void SetException(Exception exception);
}
