using System.Collections.Generic;
using CosmicShore.Utility.Singleton;
using UnityEngine;

namespace CosmicShore.Integrations.Architectures.MessageSystem
{
    public class BaseEvent
    {
        public string Name => GetType().Name;
    }
    
    public delegate void MessageHandlerDelegate(BaseEvent message);

    public class MessagingSystem : SingletonPersistent<MessagingSystem>
    {
        
        private readonly Dictionary<string, List<MessageHandlerDelegate>> _listenerDict = new();
        private readonly Queue<BaseEvent> _messageQueue = new();
        private const float MaxQueueProcessingTime = 0.16667f;

        public bool AddEventListener(System.Type type, MessageHandlerDelegate handler)
        {
            if (type == null)
            {
                Debug.Log("MessagingSystem: AttachListener failed due to no message type specified");
                return false;
            }

            string msgName = type.Name;

            if (!_listenerDict.ContainsKey(msgName))
            {
                _listenerDict.Add(msgName, new List<MessageHandlerDelegate>());
            }

            var listenerList = _listenerDict[msgName];
            if (listenerList.Contains(handler))
            {
                return false; // listener already in list
            }

            listenerList.Add(handler);
            return true;
        }

        public bool DispatchEvent(BaseEvent msg)
        {
            if (!_listenerDict.ContainsKey(msg.Name))
            {
                return false;
            }
            _messageQueue.Enqueue(msg);
            return true;
        }

        void Update()
        {
            var timer = 0.0f;
            while (_messageQueue.Count > 0)
            {
                if (MaxQueueProcessingTime > 0.0f)
                {
                    if (timer > MaxQueueProcessingTime)
                        return;
                }

                BaseEvent msg = _messageQueue.Dequeue();
                if (!TriggerMessage(msg))
                    Debug.Log("Error when processing message: " + msg.Name);

                if (MaxQueueProcessingTime > 0.0f)
                    timer += Time.deltaTime;
            }
        }

        private bool TriggerMessage(BaseEvent msg)
        {
            var msgName = msg.Name;
            if (_listenerDict.TryGetValue(msgName, out var listenerList))
            {
                listenerList.ForEach(t => t(msg));
                return true; 
            }
            
            Debug.Log("MessagingSystem: Message \"" + msgName + "\" has no listeners!");
            return false; // no listeners for message so ignore it
        }

        public bool RemoveEventListener(System.Type type, MessageHandlerDelegate handler)
        {
            if (type == null)
            {
                Debug.Log("MessagingSystem: DetachListener failed due to no message type specified");
                return false;
            }

            var msgName = type.Name;

            if (!_listenerDict.ContainsKey(type.Name))
            {
                return false;
            }

            var listenerList = _listenerDict[msgName];
            if (!listenerList.Contains(handler))
            {
                return false;
            }

            listenerList.Remove(handler);
            return true;
        }

    }
}