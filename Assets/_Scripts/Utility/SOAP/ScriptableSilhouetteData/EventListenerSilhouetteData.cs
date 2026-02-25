using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;
using CosmicShore.Utility.SOAP.ScriptableAbilityStats;
using CosmicShore.Utility.SOAP.ScriptableCrystalStats;
using CosmicShore.Utility.SOAP.ScriptablePrismStats;
using CosmicShore.Utility.SOAP.ScriptableQuaternion;
using CosmicShore.Utility.SOAP.ScriptableShipHUDData;
using CosmicShore.Utility.SOAP.ScriptableTransform;
namespace CosmicShore.Utility.SOAP.ScriptableSilhouetteData
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(SilhouetteData))]
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
