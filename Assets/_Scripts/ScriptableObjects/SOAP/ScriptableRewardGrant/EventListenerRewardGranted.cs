using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(RewardGranted))]
    public class EventListenerRewardGranted : EventListenerGeneric<RewardGranted>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<RewardGranted>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<RewardGranted>
        {
            [SerializeField] private ScriptableEventRewardGranted _scriptableEvent = null;
            public override ScriptableEvent<RewardGranted> ScriptableEvent => _scriptableEvent;
            [SerializeField] private RewardGrantedUnityEvent _response = null;
            public override UnityEvent<RewardGranted> Response => _response;
        }

        [System.Serializable]
        public class RewardGrantedUnityEvent : UnityEvent<RewardGranted>
        {
        }
    }
}
