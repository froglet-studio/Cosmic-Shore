using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.SOAP
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(PipData))]
    public class EventListenerPipData : EventListenerGeneric<PipData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<PipData>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<PipData>
        {
            [SerializeField] private ScriptableEventPipData _scriptableEvent = null;
            public override ScriptableEvent<PipData> ScriptableEvent => _scriptableEvent;

            [SerializeField] private PipEventDataUnityEvent _response = null;
            public override UnityEvent<PipData> Response => _response;
        }

        [System.Serializable]
        public class PipEventDataUnityEvent : UnityEvent<PipData>
        {

        }
    }
}


