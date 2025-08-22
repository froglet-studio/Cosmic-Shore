using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(ArcadeData))]
    public class EventListenerArcadeData : EventListenerGeneric<ArcadeData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<ArcadeData>[] EventResponses => _eventResponses;
        [System.Serializable]
        public class EventResponse : EventResponse<ArcadeData>
        {
            [SerializeField] private ScriptableEventArcadeData _scriptableEvent = null;
            public override ScriptableEvent<ArcadeData> ScriptableEvent => _scriptableEvent;
            [SerializeField] private ArcadeDataUnityEvent _response = null;
            public override UnityEvent<ArcadeData> Response => _response;
        }
        [System.Serializable]
        public class ArcadeDataUnityEvent : UnityEvent<ArcadeData>
        {
            
        }
    }
}
