using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(FaunaWaveData))]
    public class EventListenerFaunaWave : EventListenerGeneric<FaunaWaveData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<FaunaWaveData>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<FaunaWaveData>
        {
            [SerializeField] private ScriptableEventFaunaWave _scriptableEvent = null;
            public override ScriptableEvent<FaunaWaveData> ScriptableEvent => _scriptableEvent;

            [SerializeField] private FaunaWaveUnityEvent _response = null;
            public override UnityEvent<FaunaWaveData> Response => _response;
        }

        [System.Serializable]
        public class FaunaWaveUnityEvent : UnityEvent<FaunaWaveData>
        {
        }
    }
}
