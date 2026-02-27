using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [AddComponentMenu("Soap/EventListeners/EventListener" + nameof(FriendData))]
    public class EventListenerFriendData : EventListenerGeneric<FriendData>
    {
        [SerializeField] private EventResponse[] _eventResponses = null;
        protected override EventResponse<FriendData>[] EventResponses => _eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<FriendData>
        {
            [SerializeField] private ScriptableEventFriendData _scriptableEvent = null;
            public override ScriptableEvent<FriendData> ScriptableEvent => _scriptableEvent;

            [SerializeField] private FriendDataUnityEvent _response = null;
            public override UnityEvent<FriendData> Response => _response;
        }

        [System.Serializable]
        public class FriendDataUnityEvent : UnityEvent<FriendData>
        {
        }
    }
}
