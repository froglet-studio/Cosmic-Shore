using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(ApplicationState))]
    public class EventListenerApplicationState : EventListenerGeneric<ApplicationState>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<ApplicationState>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<ApplicationState>
        {
            [SerializeField] private ScriptableEventApplicationState _scriptableEvent = null;
            public override ScriptableEvent<ApplicationState> ScriptableEvent => _scriptableEvent;

            [SerializeField] private ApplicationStateUnityEvent _response = null;
            public override UnityEvent<ApplicationState> Response => _response;
        }

        [System.Serializable]
        public class ApplicationStateUnityEvent : UnityEvent<ApplicationState>
        {
        }
    }
}
