using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(CellPhase))]
    public class EventListenerCellPhase : EventListenerGeneric<CellPhase>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<CellPhase>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<CellPhase>
        {
            [SerializeField] private ScriptableEventCellPhase _scriptableEvent = null;
            public override ScriptableEvent<CellPhase> ScriptableEvent => _scriptableEvent;

            [SerializeField] private CellPhaseUnityEvent _response = null;
            public override UnityEvent<CellPhase> Response => _response;
        }

        [System.Serializable]
        public class CellPhaseUnityEvent : UnityEvent<CellPhase>
        {
        }
    }
}
