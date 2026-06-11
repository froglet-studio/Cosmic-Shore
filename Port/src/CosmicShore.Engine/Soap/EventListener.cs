using System;
using System.Collections.Generic;
using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.Soap
{
    /// <summary>
    /// Base for scene-bound SOAP listeners: subscribes its responses while enabled.
    /// Replaces the Obvious.Soap listener components — ported `EventListenerFoo`
    /// subclasses compile verbatim apart from using directives.
    /// </summary>
    public abstract class EventListenerBase : MonoBehaviour
    {
        protected abstract void ToggleRegistration(bool toggle);

        void OnEnable() => ToggleRegistration(true);
        void OnDisable() => ToggleRegistration(false);
    }

    /// <summary>
    /// Pairs a <see cref="ScriptableEvent{T}"/> channel with a <see cref="UnityEvent{T}"/>
    /// response. Concrete listener classes expose serialized arrays of these.
    /// </summary>
    [Serializable]
    public abstract class EventResponse<T>
    {
        public abstract ScriptableEvent<T> ScriptableEvent { get; }
        public abstract UnityEvent<T> Response { get; }
    }

    public abstract class EventListenerGeneric<T> : EventListenerBase
    {
        protected virtual EventResponse<T>[] EventResponses => null;

        readonly Dictionary<EventResponse<T>, Action<T>> _registered = new();

        protected override void ToggleRegistration(bool toggle)
        {
            var responses = EventResponses;
            if (responses is null) return;

            foreach (var response in responses)
            {
                // Fail-loud policy: unwired ScriptableEvent/Response references throw here
                // rather than silently dropping notifications.
                if (toggle)
                {
                    if (_registered.ContainsKey(response)) continue;
                    Action<T> handler = response.Response.Invoke;
                    _registered[response] = handler;
                    response.ScriptableEvent.OnRaised += handler;
                }
                else if (_registered.TryGetValue(response, out var handler))
                {
                    response.ScriptableEvent.OnRaised -= handler;
                    _registered.Remove(response);
                }
            }
        }
    }
}
