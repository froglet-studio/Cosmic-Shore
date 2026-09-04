using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(GameToastData))]
    public class EventListenerGameToastData : EventListenerGeneric<GameToastData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<GameToastData>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<GameToastData>
        {
            [SerializeField] private ScriptableEventGameToastData _scriptableEvent = null;
            public override ScriptableEvent<GameToastData> ScriptableEvent => _scriptableEvent;

            [SerializeField] private GameToastDataUnityEvent _response = null;
            public override UnityEvent<GameToastData> Response => _response;
        }

        [System.Serializable]
        public class GameToastDataUnityEvent : UnityEvent<GameToastData>
        {
        }
    }
}
