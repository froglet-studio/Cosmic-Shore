using CosmicShore.Engine;
using CosmicShore.Engine.Events;
using CosmicShore.Engine.Soap;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(VesselClassType))]
    public class EventListenerShipClassType : EventListenerGeneric<VesselClassType>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<VesselClassType>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<VesselClassType>
        {
            [SerializeField] private ScriptableEventShipClassType _scriptableEvent = null;
            public override ScriptableEvent<VesselClassType> ScriptableEvent => _scriptableEvent;

            [SerializeField] private ShipClassTypeUnityEvent _response = null;
            public override UnityEvent<VesselClassType> Response => _response;
        }

        [System.Serializable]
        public class ShipClassTypeUnityEvent : UnityEvent<VesselClassType>
        {
        
        }
    }
}
