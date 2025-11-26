using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(SilhouetteData))]
    public class EventListenerSilhouetteData : EventListenerGeneric<SilhouetteData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<SilhouetteData>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<SilhouetteData>
        {
            [SerializeField] private ScriptableEventSilhouetteData _scriptableEvent = null;
            public override ScriptableEvent<SilhouetteData> ScriptableEvent => _scriptableEvent;
            [SerializeField] private SilhouetteDataUnityEvent _response = null;
            public override UnityEvent<SilhouetteData> Response => _response;
        }

        [System.Serializable]
        public class SilhouetteDataUnityEvent : UnityEvent<SilhouetteData>
        {
        }
    }
}