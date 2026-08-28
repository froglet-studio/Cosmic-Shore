using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(BootStatusRequest))]
    public class EventListenerBootStatusRequest : EventListenerGeneric<BootStatusRequest>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<BootStatusRequest>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<BootStatusRequest>
        {
            [SerializeField] private ScriptableEventBootStatusRequest _scriptableEvent = null;
            public override ScriptableEvent<BootStatusRequest> ScriptableEvent => _scriptableEvent;

            [SerializeField] private BootStatusRequestUnityEvent _response = null;
            public override UnityEvent<BootStatusRequest> Response => _response;
        }

        [System.Serializable]
        public class BootStatusRequestUnityEvent : UnityEvent<BootStatusRequest>
        {
        }
    }
}
