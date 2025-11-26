using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(Quaternion))]
    public class EventListenerQuaternion : EventListenerGeneric<Quaternion>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<Quaternion>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<Quaternion>
        {
            [SerializeField] private ScriptableEventQuaternion _scriptableEvent = null;
            public override ScriptableEvent<Quaternion> ScriptableEvent => _scriptableEvent;
            [SerializeField] private QuaternionUnityEvent _response = null;
            public override UnityEvent<Quaternion> Response => _response;
        }

        [System.Serializable]
        public class QuaternionUnityEvent : UnityEvent<Quaternion>
        {
        }
    }
}