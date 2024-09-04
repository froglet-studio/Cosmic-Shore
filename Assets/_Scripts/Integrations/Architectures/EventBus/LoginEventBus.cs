using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Integrations.Architectures.EventBus
{
    [Serializable]
    public enum LoginType
    {
        Success, Fail, Other
    }
    
    public class LoginEventBus: IDisposable
    {
        private static readonly IDictionary<LoginType, UnityEvent> Events =
            new Dictionary<LoginType, UnityEvent>();

        public static void Subscribe(LoginType loginType, UnityAction listener)
        {
            if (Events.TryGetValue(loginType, out var currentEvent))
            {
                currentEvent.AddListener(listener);
            }
            else
            {
                currentEvent = new UnityEvent();
                currentEvent.AddListener(listener);
                Events.Add(loginType, currentEvent);
            }
        }

        public static void Unsubscribe(LoginType loginType, UnityAction listener)
        {
            if (Events.TryGetValue(loginType, out var currentEvent))
            {
                currentEvent.RemoveListener(listener);
            }
        }

        public static void Publish(LoginType loginType)
        {
            if (Events.TryGetValue(loginType, out var currentEvent))
            {
                currentEvent.Invoke();
            }
        }

        public void Dispose()
        {
            Events.Clear();
            Debug.Log("LoginEventBus - Events cleared.");
        }
    }
}
