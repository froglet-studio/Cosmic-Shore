using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(TrailBlockEventData))]
    public class EventListenerTrailBlockEventData : EventListenerGeneric<TrailBlockEventData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<TrailBlockEventData>[] EventResponses => _eventResponses;
        [System.Serializable]
        public class EventResponse : EventResponse<TrailBlockEventData>
        {
            [SerializeField] private ScriptableEventTrailBlockEventData _scriptableEvent = null;
            public override ScriptableEvent<TrailBlockEventData> ScriptableEvent => _scriptableEvent;
            [SerializeField] private TrailBlockEventDataUnityEvent _response = null;
            public override UnityEvent<TrailBlockEventData> Response => _response;
        }
        [System.Serializable]
        public class TrailBlockEventDataUnityEvent : UnityEvent<TrailBlockEventData>
        {
            
        }
    }
}
