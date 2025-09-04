using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(CrystalStats))]
    public class EventListenerCrystalStats : EventListenerGeneric<CrystalStats>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<CrystalStats>[] EventResponses => _eventResponses;
        [System.Serializable]
        public class EventResponse : EventResponse<CrystalStats>
        {
            [SerializeField] private ScriptableEventCrystalStats _scriptableEvent = null;
            public override ScriptableEvent<CrystalStats> ScriptableEvent => _scriptableEvent;
            [SerializeField] private CrystalStatsUnityEvent _response = null;
            public override UnityEvent<CrystalStats> Response => _response;
        }
        [System.Serializable]
        public class CrystalStatsUnityEvent : UnityEvent<CrystalStats>
        {
            
        }
    }
}
