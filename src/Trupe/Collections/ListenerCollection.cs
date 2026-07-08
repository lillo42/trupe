using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

#if NET10_0_OR_GREATER
using System.Threading;
#endif

namespace Trupe.Collections;

/// <summary>
/// Represents a thread-safe collection of listeners, providing mechanisms to add, remove, and safely invoke actions across all registered listeners.
/// </summary>
public abstract class ListenerCollection<T> : ICollection<T>
{
#if NET10_0_OR_GREATER
    private readonly Lock _locker = new();
#else
    private readonly object _locker = new();
#endif
    private ImmutableList<T> _listeners = [];
    
    /// <inheritdoc/>
    public int Count => _listeners.Count;
    
    /// <inheritdoc/>
    public bool IsReadOnly => false;
    
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _listeners.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Adds a listener to the collection.
    /// </summary>
    /// <param name="item">The listener to add.</param>
    /// <returns>An <see cref="IDisposable"/> that can be used to unregister the listener.</returns>
    public IDisposable Add(T item)
    {
        lock (_locker)
        {
            _listeners = _listeners.Add(item);
            return new UnRegisterListener(this, item);
        }
    }
    
    void ICollection<T>.Add(T item) => Add(item);

    /// <inheritdoc/>
    public void Clear()
    {
        _listeners = _listeners.Clear();
    }

    /// <inheritdoc/>
    public bool Contains(T item)
    {
        return _listeners.Contains(item);
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        _listeners.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        lock (_locker)
        {
            _listeners = _listeners.Remove(item);
            return _listeners.Count != 0;
        }
    }

    /// <summary>
    /// Invokes the specified action on all registered listeners.
    /// Safely handles exceptions thrown by listeners, automatically removing those that throw an <see cref="ObjectDisposedException"/>.
    /// </summary>
    /// <param name="action">The action to invoke on each listener.</param>
    protected void Invoke(Action<T> action)
    {
        var listeners = _listeners;
        foreach (var listener in listeners)
        {
            try
            {
                action.Invoke(listener);
            }
            catch (ObjectDisposedException)
            {
                Remove(listener);
            }
            catch (Exception)
            {
                // Ignore any error
            }
        }
    }

    private sealed class UnRegisterListener(ListenerCollection<T> collection, T obj) : IDisposable
    {
        public void Dispose()
        {
            collection.Remove(obj);
        }
    }
}