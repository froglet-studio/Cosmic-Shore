using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Soap
{
    /// <summary>
    /// Decoupled event channel — the port's replacement for the SOAP ScriptableEvent.
    /// Fail-loud policy carries over: consumers must not null-guard serialized channel
    /// references; a missing channel should throw at the raise/subscribe site.
    /// Listeners run inline on the raising thread (same contract as the original —
    /// raise only from the main thread).
    ///
    /// Dispatch order (original contract): listener COMPONENTS first, newest-registered
    /// first, then C# subscribers to <c>OnRaised</c> in subscription order.
    /// </summary>
    public abstract class ScriptableEventBase : ScriptableObject { }

    /// <summary>A listener component registered on a parameterless channel.</summary>
    public interface IScriptableEventListener
    {
        void OnEventRaised(ScriptableEventNoParam channel);
    }

    /// <summary>A listener component registered on a typed channel.</summary>
    public interface IScriptableEventListener<T>
    {
        void OnEventRaised(ScriptableEvent<T> channel, T value);
    }

    public class ScriptableEventNoParam : ScriptableEventBase
    {
        readonly List<IScriptableEventListener> _listeners = new();
        readonly HashSet<IScriptableEventListener> _lookup = new();

        public event Action OnRaised;

        public void Raise()
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
                if (i < _listeners.Count) _listeners[i].OnEventRaised(this);
            OnRaised?.Invoke();
        }

        public void RegisterListener(IScriptableEventListener listener)
        {
            if (_lookup.Add(listener)) _listeners.Add(listener);
        }

        public void UnregisterListener(IScriptableEventListener listener)
        {
            if (_lookup.Remove(listener)) _listeners.Remove(listener);
        }

        public int ListenerCount => _listeners.Count;
    }

    public class ScriptableEvent<T> : ScriptableEventBase
    {
        readonly List<IScriptableEventListener<T>> _listeners = new();
        readonly HashSet<IScriptableEventListener<T>> _lookup = new();

        public event Action<T> OnRaised;

        public T LastValue { get; private set; }

        public void Raise(T param)
        {
            LastValue = param;
            // A response may unregister listeners mid-raise; the bound check keeps the walk safe.
            for (int i = _listeners.Count - 1; i >= 0; i--)
                if (i < _listeners.Count) _listeners[i].OnEventRaised(this, param);
            OnRaised?.Invoke(param);
        }

        public void RegisterListener(IScriptableEventListener<T> listener)
        {
            if (_lookup.Add(listener)) _listeners.Add(listener);
        }

        public void UnregisterListener(IScriptableEventListener<T> listener)
        {
            if (_lookup.Remove(listener)) _listeners.Remove(listener);
        }

        public int ListenerCount => _listeners.Count;
    }

    // Concrete event types mirroring the original SOAP package's built-in set.
    // Game-specific payload events (incl. ScriptableEventUlong, which the original
    // package lacked) live in the ported game code under CosmicShore.ScriptableObjects.
    public class ScriptableEventBool : ScriptableEvent<bool> { }
    public class ScriptableEventInt : ScriptableEvent<int> { }
    public class ScriptableEventFloat : ScriptableEvent<float> { }
    public class ScriptableEventString : ScriptableEvent<string> { }
    public class ScriptableEventVector3 : ScriptableEvent<Vector3> { }
}
