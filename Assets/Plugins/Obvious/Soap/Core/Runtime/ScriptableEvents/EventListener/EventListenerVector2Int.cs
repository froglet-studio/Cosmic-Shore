using UnityEngine;
using UnityEngine.Events;

namespace Obvious.Soap
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(Vector2Int))]
    public class EventListenerVector2Int : EventListenerGeneric<Vector2Int>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<Vector2Int>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<Vector2Int>
        {
            [SerializeField] private ScriptableEventVector2Int _scriptableEvent = null;
            public override ScriptableEvent<Vector2Int> ScriptableEvent => _scriptableEvent;

            [SerializeField] private Vector2IntUnityEvent _response = null;
            public override UnityEvent<Vector2Int> Response => _response;
        }

        [System.Serializable]
        public class Vector2IntUnityEvent : UnityEvent<Vector2Int>
        {
            
        }
    }
}