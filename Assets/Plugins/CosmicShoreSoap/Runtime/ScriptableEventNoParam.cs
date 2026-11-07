using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Soap
{
    /// <summary>
    /// An event channel with no payload. <see cref="Raise"/> runs every
    /// <see cref="EventListenerNoParam"/> component (newest first), then every C# subscriber
    /// of <see cref="OnRaised"/> in subscription order. Raising outside Play mode does
    /// nothing. Raise from the main thread only.
    /// Never null-guard a serialized channel field: an unwired channel must fail loud.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_NoParam", menuName = "ScriptableObjects/Events/NoParam")]
    public class ScriptableEventNoParam : ScriptableEventBase
    {
        readonly SoapListenerSet<EventListenerNoParam> _listeners = new();
        readonly List<Object> _subscriberOwners = new();
        Action _onRaised;

        /// <summary>Runs after the listener components each time the channel is raised.</summary>
        public event Action OnRaised
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

        public void Raise()
        {
            if (!Application.isPlaying) return;

            // A response may unregister listeners mid-raise; the bound check keeps the walk safe.
            var listeners = _listeners.Items;
            for (int i = listeners.Count - 1; i >= 0; i--)
                if (i < listeners.Count) listeners[i].OnEventRaised(this);

            _onRaised?.Invoke();
        }

        /// <summary>Every listener component and every Unity object that owns a C# subscriber.</summary>
        public List<Object> GetAllObjects() => SoapSubscribers.All(_listeners.Items, _subscriberOwners);

        internal void RegisterListener(EventListenerNoParam listener) => _listeners.Add(listener);
        internal void UnregisterListener(EventListenerNoParam listener) => _listeners.Remove(listener);

        protected override void ClearRuntimeSubscribers()
        {
            _onRaised = null;
            _subscriberOwners.Clear();
            _listeners.Clear();
        }
    }
}
