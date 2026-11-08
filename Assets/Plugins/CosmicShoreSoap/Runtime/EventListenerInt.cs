using System;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Soap
{
    [AddComponentMenu("Soap/Event Listener (Int)")]
    public class EventListenerInt : EventListenerGeneric<int>
    {
        [SerializeField] EventResponse[] _eventResponses = Array.Empty<EventResponse>();
        protected override EventResponse<int>[] EventResponses => _eventResponses;

        [Serializable]
        public class EventResponse : EventResponse<int>
        {
            [SerializeField] ScriptableEventInt _scriptableEvent = null;
            [SerializeField] IntUnityEvent _response = null;
            public override ScriptableEvent<int> ScriptableEvent => _scriptableEvent;
            public override UnityEvent<int> Response => _response;
        }

        [Serializable] public class IntUnityEvent : UnityEvent<int> { }
    }
}
