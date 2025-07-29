using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.SOAP
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(InputEvents))]
    public class EventListenerInputEvents : EventListenerGeneric<InputEvents>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<InputEvents>[] EventResponses => _eventResponses;
        [System.Serializable]
        public class EventResponse : EventResponse<InputEvents>
        {
            [SerializeField] private ScriptableEventInputEvents _scriptableEvent = null;
            public override ScriptableEvent<InputEvents> ScriptableEvent => _scriptableEvent;
            [SerializeField] private InputEventsUnityEvent _response = null;
            public override UnityEvent<InputEvents> Response => _response;
        }
        [System.Serializable]
        public class InputEventsUnityEvent : UnityEvent<InputEvents>
        {
            
        }
    }
}
