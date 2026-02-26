using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;
using CosmicShore.Utility.SOAP;
namespace CosmicShore.Utility.SOAP
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(PartyInviteData))]
    public class EventListenerPartyInviteData : EventListenerGeneric<PartyInviteData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<PartyInviteData>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<PartyInviteData>
        {
            [SerializeField] private ScriptableEventPartyInviteData _scriptableEvent = null;
            public override ScriptableEvent<PartyInviteData> ScriptableEvent => _scriptableEvent;

            [SerializeField] private PartyInviteDataUnityEvent _response = null;
            public override UnityEvent<PartyInviteData> Response => _response;
        }

        [System.Serializable]
        public class PartyInviteDataUnityEvent : UnityEvent<PartyInviteData>
        {
        }
    }
}
