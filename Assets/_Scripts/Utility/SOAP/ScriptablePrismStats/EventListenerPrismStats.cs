using CosmicShore.Game.Managers;
using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;
using CosmicShore.Utility.SOAP.ScriptableAbilityStats;
using CosmicShore.Utility.SOAP.ScriptableCrystalStats;
using CosmicShore.Utility.SOAP.ScriptableQuaternion;
using CosmicShore.Utility.SOAP.ScriptableShipHUDData;
using CosmicShore.Utility.SOAP.ScriptableSilhouetteData;
using CosmicShore.Utility.SOAP.ScriptableTransform;
namespace CosmicShore.Utility.SOAP.ScriptablePrismStats
{
    [AddComponentMenu("Soap/EventListeners/EventListener"+nameof(PrismStats))]
    public class EventListenerPrismStats : EventListenerGeneric<PrismStats>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<PrismStats>[] EventResponses => _eventResponses;
        [System.Serializable]
        public class EventResponse : EventResponse<PrismStats>
        {
            [SerializeField] private ScriptableEventPrismStats _scriptableEvent = null;
            public override ScriptableEvent<PrismStats> ScriptableEvent => _scriptableEvent;
            [SerializeField] private PrismStatsUnityEvent _response = null;
            public override UnityEvent<PrismStats> Response => _response;
        }
        [System.Serializable]
        public class PrismStatsUnityEvent : UnityEvent<PrismStats>
        {
            
        }
    }
}
