using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

[AddComponentMenu("Soap/EventListeners/EventListener"+nameof(ShipClassType))]
public class EventListenerShipClassType : EventListenerGeneric<ShipClassType>
{
    [SerializeField] private EventResponse[] _eventResponses = null;
    protected override EventResponse<ShipClassType>[] EventResponses => _eventResponses;

    [System.Serializable]
    public class EventResponse : EventResponse<ShipClassType>
    {
        [SerializeField] private ScriptableEventShipClassType _scriptableEvent = null;
        public override ScriptableEvent<ShipClassType> ScriptableEvent => _scriptableEvent;

        [SerializeField] private ShipClassTypeUnityEvent _response = null;
        public override UnityEvent<ShipClassType> Response => _response;
    }

    [System.Serializable]
    public class ShipClassTypeUnityEvent : UnityEvent<ShipClassType>
    {
        
    }
}
