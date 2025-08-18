using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(ShipHUDData))]
    public class EventListenerShipHUDData : EventListenerGeneric<ShipHUDData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<ShipHUDData>[] EventResponses => _eventResponses;
        [System.Serializable]
        public class EventResponse : EventResponse<ShipHUDData>
        {
            [SerializeField] private ScriptableEventShipHUDData _scriptableEvent = null;
            public override ScriptableEvent<ShipHUDData> ScriptableEvent => _scriptableEvent;
            [SerializeField] private ShipHUDDataUnityEvent _response = null;
            public override UnityEvent<ShipHUDData> Response => _response;
        }
        [System.Serializable]
        public class ShipHUDDataUnityEvent : UnityEvent<ShipHUDData>
        {
            
        }
    }
}
