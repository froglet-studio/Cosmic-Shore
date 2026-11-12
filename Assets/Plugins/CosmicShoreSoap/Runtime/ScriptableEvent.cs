using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Soap
{
    /// <summary>
    /// An event channel carrying a <typeparamref name="T"/>. <see cref="Raise"/> runs every
    /// <see cref="EventListenerGeneric{T}"/> component (newest first), then every C#
    /// subscriber of <see cref="OnRaised"/> in subscription order. Raising outside Play mode
    /// does nothing. Raise from the main thread only.
    /// Never null-guard a serialized channel field: an unwired channel must fail loud.
    /// </summary>
    public abstract class ScriptableEvent<T> : ScriptableEventBase
    {
        [SerializeField, HideInInspector] protected T _debugValue; // kept for the asset format

        readonly SoapListenerSet<EventListenerGeneric<T>> _listeners = new();
        readonly List<Object> _subscriberOwners = new();
        Action<T> _onRaised;

        /// <summary>Runs after the listener components each time the channel is raised.</summary>
        public event Action<T> OnRaised
        {
            add
            {
                _onRaised += value;
                SoapSubscribers.Track(_subscriberOwners, value);
            }
            remove
            {
                _onRaised -= value;
                SoapSubscribers.Untrack(_subscriberOwners, value);
            }
        }

        public void Raise(T param)
        {
            if (!Application.isPlaying) return;

            // A response may unregister listeners mid-raise; the bound check keeps the walk safe.
            var listeners = _listeners.Items;
            for (int i = listeners.Count - 1; i >= 0; i--)
                if (i < listeners.Count) listeners[i].OnEventRaised(this, param);

            _onRaised?.Invoke(param);
        }

        /// <summary>Every listener component and every Unity object that owns a C# subscriber.</summary>
        public List<Object> GetAllObjects() => SoapSubscribers.All(_listeners.Items, _subscriberOwners);

        internal void RegisterListener(EventListenerGeneric<T> listener) => _listeners.Add(listener);
        internal void UnregisterListener(EventListenerGeneric<T> listener) => _listeners.Remove(listener);

        protected override void ClearRuntimeSubscribers()
        {
            _onRaised = null;
            _subscriberOwners.Clear();
            _listeners.Clear();
        }
    }
}
