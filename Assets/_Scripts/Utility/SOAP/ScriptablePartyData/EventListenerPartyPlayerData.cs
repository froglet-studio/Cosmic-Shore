using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.Soap
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(PartyPlayerData))]
    public class EventListenerPartyPlayerData : EventListenerGeneric<PartyPlayerData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<PartyPlayerData>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<PartyPlayerData>
        {
            [SerializeField] private ScriptableEventPartyPlayerData _scriptableEvent = null;
            public override ScriptableEvent<PartyPlayerData> ScriptableEvent => _scriptableEvent;

            [SerializeField] private PartyPlayerDataUnityEvent _response = null;
            public override UnityEvent<PartyPlayerData> Response => _response;
        }

        [System.Serializable]
        public class PartyPlayerDataUnityEvent : UnityEvent<PartyPlayerData>
        {
        }
    }
}
