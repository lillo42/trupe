using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Trupe.Abstractions;

namespace Trupe.Collections;

public class ActorReferenceListenerCollection : IEnumerable<IActorReferenceListener>
{
    private readonly object _locker = new();
    private readonly List<IActorReferenceListener> _listeners = [];

    public int Count => _listeners.Count;

    public IDisposable Add(IActorReferenceListener item)
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

    public bool Contains(IActorReferenceListener item)
    {
        return _listeners.Contains(item);
    }

    public void CopyTo(IActorReferenceListener[] array, int arrayIndex)
    {
        lock (_locker)
        {
            _listeners.CopyTo(array, arrayIndex);
        }
    }

    public bool Remove(IActorReferenceListener item)
    {
        lock (_locker)
        {
            return _listeners.Remove(item);
        }
    }

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

    public IEnumerator<IActorReferenceListener> GetEnumerator()
    {
        var array = GetArray();
        return array.AsEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public class UnRegisterListiner(
        ActorReferenceListenerCollection collection,
        IActorReferenceListener listener
    ) : IDisposable
    {
        public void Dispose()
        {
            collection.Remove(listener);
        }
    }
}
