using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(AbilityStats))]
    public class EventListenerAbilityStats : EventListenerGeneric<AbilityStats>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<AbilityStats>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<AbilityStats>
        {
            [SerializeField] private ScriptableEventAbilityStats _scriptableEvent = null;
            public override ScriptableEvent<AbilityStats> ScriptableEvent => _scriptableEvent;
            [SerializeField] private AbilityStatsUnityEvent _response = null;
            public override UnityEvent<AbilityStats> Response => _response;
        }

        [System.Serializable]
        public class AbilityStatsUnityEvent : UnityEvent<AbilityStats>
        {
        }
    }
}