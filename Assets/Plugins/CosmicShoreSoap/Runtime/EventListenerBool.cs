using System;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Soap
{
    [AddComponentMenu("Soap/Event Listener (Bool)")]
    public class EventListenerBool : EventListenerGeneric<bool>
    {
        [SerializeField] EventResponse[] _eventResponses = Array.Empty<EventResponse>();
        protected override EventResponse<bool>[] EventResponses => _eventResponses;

        [Serializable]
        public class EventResponse : EventResponse<bool>
        {
            [SerializeField] ScriptableEventBool _scriptableEvent = null;
            [SerializeField] BoolUnityEvent _response = null;
            public override ScriptableEvent<bool> ScriptableEvent => _scriptableEvent;
            public override UnityEvent<bool> Response => _response;
        }

        [Serializable] public class BoolUnityEvent : UnityEvent<bool> { }
    }
}
