using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;
using CosmicShore.Core;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(GameplaySFXCategory))]
    public class EventListenerGameplaySFX : EventListenerGeneric<GameplaySFXCategory>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<GameplaySFXCategory>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<GameplaySFXCategory>
        {
            [SerializeField] private ScriptableEventGameplaySFX _scriptableEvent = null;
            public override ScriptableEvent<GameplaySFXCategory> ScriptableEvent => _scriptableEvent;

            [SerializeField] private GameplaySFXUnityEvent _response = null;
            public override UnityEvent<GameplaySFXCategory> Response => _response;
        }

        [System.Serializable]
        public class GameplaySFXUnityEvent : UnityEvent<GameplaySFXCategory>
        {
        }
    }
}
