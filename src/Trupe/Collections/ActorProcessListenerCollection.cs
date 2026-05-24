using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;

namespace Trupe.Collections;

public class ActorProcessListenerCollection : IEnumerable<IActorProcessListener>
{
    private readonly object _locker = new();
    private readonly List<IActorProcessListener> _listeners = [];

    public int Count => _listeners.Count;

    public IDisposable Add(IActorProcessListener item)
    {
        lock (_locker)
        {
            _listeners.Add(item);
            return new UnRegisterListiner(this, item);
        }
    }

    public void Clear()
    {
        lock (_locker)
        {
            _listeners.Clear();
        }
    }

    public bool Contains(IActorProcessListener item)
    {
        return _listeners.Contains(item);
    }

    public void CopyTo(IActorProcessListener[] array, int arrayIndex)
    {
        lock (_locker)
        {
            _listeners.CopyTo(array, arrayIndex);
        }
    }

    public bool Remove(IActorProcessListener item)
    {
        lock (_locker)
        {
            return _listeners.Remove(item);
        }
    }

    public void InvokeOnFailed(IActorProcess process, IMessage message, Exception exception)
    {
        var array = GetArray();
        foreach (var listener in array)
        {
            try
            {
                listener.OnFailed(process, message, exception);
            }
            catch
            {
                // Ignore any error
            }
        }
    }

    public void InvokeOnStopped(IActorProcess process, TerminatedReason reason)
    {
        var array = GetArray();
        foreach (var listener in array)
        {
            try
            {
                listener.OnStopped(process, reason);
            }
            catch
            {
                // Ignore any error
            }
        }
    }

    private IActorProcessListener[] GetArray()
    {
        lock (_locker)
        {
            return _listeners.ToArray();
        }
    }

    public IEnumerator<IActorProcessListener> GetEnumerator()
    {
        var array = GetArray();
        return array.AsEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public class UnRegisterListiner(
        ActorProcessListenerCollection collection,
        IActorProcessListener listener
    ) : IDisposable
    {
        public void Dispose()
        {
            collection.Remove(listener);
        }
    }
}
