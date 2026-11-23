using System;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Soap
{
    [AddComponentMenu("Soap/Event Listener (String)")]
    public class EventListenerString : EventListenerGeneric<string>
    {
        [SerializeField] EventResponse[] _eventResponses = Array.Empty<EventResponse>();
        protected override EventResponse<string>[] EventResponses => _eventResponses;

        [Serializable]
        public class EventResponse : EventResponse<string>
        {
            [SerializeField] ScriptableEventString _scriptableEvent = null;
            [SerializeField] StringUnityEvent _response = null;
            public override ScriptableEvent<string> ScriptableEvent => _scriptableEvent;
            public override UnityEvent<string> Response => _response;
        }

        [Serializable] public class StringUnityEvent : UnityEvent<string> { }
    }
}
