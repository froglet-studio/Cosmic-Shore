using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(Transform))]
    public class EventListenerTransform : EventListenerGeneric<Transform>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<Transform>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<Transform>
        {
            [SerializeField] private ScriptableEventTransform _scriptableEvent = null;
            public override ScriptableEvent<Transform> ScriptableEvent => _scriptableEvent;
            [SerializeField] private TransformUnityEvent _response = null;
            public override UnityEvent<Transform> Response => _response;
        }

        [System.Serializable]
        public class TransformUnityEvent : UnityEvent<Transform>
        {
        }
    }
}