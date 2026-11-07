using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Soap
{
    /// <summary>Runs an authored UnityEvent when a <see cref="ScriptableEventNoParam"/> is raised.</summary>
    [AddComponentMenu("Soap/Event Listener (No Param)")]
    public class EventListenerNoParam : EventListenerBase
    {
        [SerializeField] EventResponse[] _eventResponses = Array.Empty<EventResponse>();

        readonly Dictionary<ScriptableEventNoParam, EventResponse> _byChannel = new();

        protected override void ToggleRegistration(bool register)
        {
            foreach (var response in _eventResponses)
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

        internal void OnEventRaised(ScriptableEventNoParam channel)
        {
            var response = _byChannel[channel];
            if (response.Delay > 0f) RespondLater(response.Delay, response.Response.Invoke);
            else response.Response?.Invoke();
        }

        [Serializable]
        internal struct EventResponse
        {
            [Min(0)] [Tooltip("Seconds to wait before responding.")]
            public float Delay;
            public ScriptableEventNoParam ScriptableEvent;
            public UnityEvent Response;
        }
    }
}
