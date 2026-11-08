using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Runs authored UnityEvent responses when a <see cref="ScriptableEvent{T}"/> is raised.
    /// A concrete listener serializes an array of its own <c>EventResponse</c> (derived from
    /// <see cref="EventResponse{U}"/>) and returns it from <see cref="EventResponses"/>.
    /// </summary>
    public abstract class EventListenerGeneric<T> : EventListenerBase
    {
        protected virtual EventResponse<T>[] EventResponses => null;

        readonly Dictionary<ScriptableEvent<T>, EventResponse<T>> _byChannel = new();

        protected override void ToggleRegistration(bool register)
        {
            var responses = EventResponses;
            if (responses == null) return;

            foreach (var response in responses)
            {
                // No null guard: an unwired channel throws here, as every SOAP field must.
                var channel = response.ScriptableEvent;
                if (register)
                {
                    channel.RegisterListener(this);
                    _byChannel.TryAdd(channel, response);
                }
                else
                {
                    channel.UnregisterListener(this);
                    _byChannel.Remove(channel);
                }
            }
        }

        internal void OnEventRaised(ScriptableEvent<T> channel, T value)
        {
            var response = _byChannel[channel];
            if (response.Delay > 0f) RespondLater(response.Delay, () => response.Response?.Invoke(value));
            else response.Response?.Invoke(value);
        }

        [Serializable]
        public class EventResponse<U>
        {
            [Min(0)] [Tooltip("Seconds to wait before responding.")]
            public float Delay;

            public virtual ScriptableEvent<U> ScriptableEvent => null;
            public virtual UnityEvent<U> Response => null;
        }
    }
}
