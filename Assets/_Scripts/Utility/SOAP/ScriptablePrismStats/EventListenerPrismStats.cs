using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(PrismStats))]
    public class EventListenerPrismStats : EventListenerGeneric<PrismStats>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<PrismStats>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<PrismStats>
        {
            [SerializeField] private ScriptableEventPrismStats _scriptableEvent = null;
            public override ScriptableEvent<PrismStats> ScriptableEvent => _scriptableEvent;
            [SerializeField] private PrismStatsUnityEvent _response = null;
            public override UnityEvent<PrismStats> Response => _response;
        }

        [System.Serializable]
        public class PrismStatsUnityEvent : UnityEvent<PrismStats>
        {
        }
    }
}