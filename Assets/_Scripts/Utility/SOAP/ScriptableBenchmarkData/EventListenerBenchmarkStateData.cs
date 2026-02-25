using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Soap
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(BenchmarkStateData))]
    public class EventListenerBenchmarkStateData : EventListenerGeneric<BenchmarkStateData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<BenchmarkStateData>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<BenchmarkStateData>
        {
            [SerializeField] private ScriptableEventBenchmarkStateData _scriptableEvent = null;
            public override ScriptableEvent<BenchmarkStateData> ScriptableEvent => _scriptableEvent;

            [SerializeField] private BenchmarkStateDataUnityEvent _response = null;
            public override UnityEvent<BenchmarkStateData> Response => _response;
        }

        [System.Serializable]
        public class BenchmarkStateDataUnityEvent : UnityEvent<BenchmarkStateData>
        {
        }
    }
}
