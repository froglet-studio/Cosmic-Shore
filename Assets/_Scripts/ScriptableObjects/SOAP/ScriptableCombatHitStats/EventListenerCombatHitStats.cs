using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(CombatHitStats))]
    public class EventListenerCombatHitStats : EventListenerGeneric<CombatHitStats>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<CombatHitStats>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<CombatHitStats>
        {
            [SerializeField] private ScriptableEventCombatHitStats _scriptableEvent = null;
            public override ScriptableEvent<CombatHitStats> ScriptableEvent => _scriptableEvent;
            [SerializeField] private CombatHitStatsUnityEvent _response = null;
            public override UnityEvent<CombatHitStats> Response => _response;
        }

        [System.Serializable]
        public class CombatHitStatsUnityEvent : UnityEvent<CombatHitStats>
        {
        }
    }
}
