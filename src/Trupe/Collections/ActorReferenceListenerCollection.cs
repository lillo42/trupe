using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Trupe.Abstractions;

namespace Trupe.Collections;

/// <summary>
/// A thread-safe collection of <see cref="IActorReferenceListener"/> instances that supports
/// adding, removing, and notifying listeners about actor reference termination events.
/// </summary>
public class ActorReferenceListenerCollection : IEnumerable<IActorReferenceListener>
{
    private readonly object _locker = new();
    private readonly List<IActorReferenceListener> _listeners = [];

    /// <summary>
    /// Gets the number of listeners currently registered in this collection.
    /// </summary>
    public int Count => _listeners.Count;

    /// <summary>
    /// Adds a listener to the collection.
    /// </summary>
    /// <param name="item">The listener to add.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, removes the listener from the collection.</returns>
    public IDisposable Add(IActorReferenceListener item)
    {
        lock (_locker)
        {
            _listeners.Add(item);
            return new UnRegisterListiner(this, item);
        }
    }

    /// <summary>
    /// Removes all listeners from the collection.
    /// </summary>
    public void Clear()
    {
        lock (_locker)
        {
            _listeners.Clear();
        }
    }

    /// <summary>
    /// Determines whether the collection contains the specified listener.
    /// </summary>
    /// <param name="item">The listener to locate.</param>
    /// <returns><see langword="true"/> if found; otherwise <see langword="false"/>.</returns>
    public bool Contains(IActorReferenceListener item)
    {
        return _listeners.Contains(item);
    }

    /// <summary>
    /// Copies the listeners to an array, starting at the specified array index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
    public void CopyTo(IActorReferenceListener[] array, int arrayIndex)
    {
        lock (_locker)
        {
            _listeners.CopyTo(array, arrayIndex);
        }
    }

    /// <summary>
    /// Removes the specified listener from the collection.
    /// </summary>
    /// <param name="item">The listener to remove.</param>
    /// <returns><see langword="true"/> if the listener was found and removed; otherwise <see langword="false"/>.</returns>
    public bool Remove(IActorReferenceListener item)
    {
        lock (_locker)
        {
            return _listeners.Remove(item);
        }
    }

    /// <summary>
    /// Notifies all registered listeners that the specified actor reference has terminated.
    /// Exceptions thrown by individual listeners are silently swallowed.
    /// </summary>
    /// <param name="reference">The actor reference that was terminated.</param>
    /// <param name="reason">The reason for termination.</param>
    public void InvokeOnTerminated(IActorReference reference, TerminatedReason reason)
    {
        var array = GetArray();
        foreach (var listener in array)
        {
            try
            {
                listener.OnTerminated(reference, reason);
            }
            catch
            {
                // Ignore any error
            }
        }
    }

    private IActorReferenceListener[] GetArray()
    {
        lock (_locker)
        {
            return _listeners.ToArray();
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the registered listeners.
    /// </summary>
    public IEnumerator<IActorReferenceListener> GetEnumerator()
    {
        var array = GetArray();
        return array.AsEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// An <see cref="IDisposable"/> token that removes its associated listener from the collection when disposed.
    /// </summary>
    public class UnRegisterListiner(
        ActorReferenceListenerCollection collection,
        IActorReferenceListener listener
    ) : IDisposable
    {
        /// <summary>
        /// Removes the listener from the collection.
        /// </summary>
        public void Dispose()
        {
            collection.Remove(listener);
        }
    }
}
