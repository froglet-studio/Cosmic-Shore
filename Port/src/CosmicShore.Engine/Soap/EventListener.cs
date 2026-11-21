using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Engine.Events;
using CosmicShore.Engine.Serialization;

namespace CosmicShore.Engine.Soap
{
    /// <summary>
    /// Base for scene-bound SOAP listeners (original contract, per the package's documented
    /// inspector options):
    ///   • <b>Binding</b> — UNTIL_DESTROY (the default) subscribes in Awake and stays
    ///     subscribed while the object is disabled, until OnDestroy; UNTIL_DISABLE follows
    ///     OnEnable/OnDisable.
    ///   • <b>Disable After Subscribing</b> — the GameObject switches itself off once Awake
    ///     has subscribed, so a hidden panel can still react to the event that shows it.
    /// Ported `EventListenerFoo` subclasses compile verbatim apart from using directives.
    /// </summary>
    public abstract class EventListenerBase : MonoBehaviour
    {
        protected enum Binding { UNTIL_DESTROY = 0, UNTIL_DISABLE = 1 }

        [SerializeField] protected Binding _binding = Binding.UNTIL_DESTROY;
        [SerializeField] protected bool _disableAfterSubscribing = false;

        protected abstract void ToggleRegistration(bool toggle);

        protected virtual void Awake()
        {
            if (_binding == Binding.UNTIL_DESTROY) ToggleRegistration(true);
            gameObject.SetActive(!_disableAfterSubscribing);
        }

        protected virtual void OnEnable()
        {
            if (_binding == Binding.UNTIL_DISABLE) ToggleRegistration(true);
        }

        protected virtual void OnDisable()
        {
            if (_binding == Binding.UNTIL_DISABLE) ToggleRegistration(false);
        }

        protected virtual void OnDestroy()
        {
            if (_binding == Binding.UNTIL_DESTROY) ToggleRegistration(false);
        }

        /// <summary>Run a response after its authored delay (immediately when the delay is zero).</summary>
        protected void Respond(float delay, Action invoke)
        {
            if (delay <= 0f) { invoke(); return; }
            // A coroutine needs an active host; an inactive listener waits on the loop's clock instead.
            if (isActiveAndEnabled) StartCoroutine(DelayThen(delay, invoke));
            else DelayHost.Get().StartCoroutine(DelayThen(delay, () => { if (this) invoke(); }));
        }

        /// <summary>A hidden, scene-surviving coroutine host for responses whose listener is inactive.</summary>
        sealed class DelayHost : MonoBehaviour
        {
            static DelayHost s_host;
            public static DelayHost Get()
            {
                if (s_host) return s_host;
                var go = new GameObject("[Soap delayed responses]");
                DontDestroyOnLoad(go);
                return s_host = go.AddComponent<DelayHost>();
            }
        }

        static IEnumerator DelayThen(float delay, Action invoke)
        {
            yield return new WaitForSeconds(delay);
            invoke();
        }
    }

    /// <summary>
    /// Pairs a <see cref="ScriptableEvent{T}"/> channel with a <see cref="UnityEvent{T}"/>
    /// response. Concrete listener classes expose serialized arrays of these.
    /// </summary>
    [Serializable]
    public abstract class EventResponse<T>
    {
        [Min(0)] public float Delay;
        public abstract ScriptableEvent<T> ScriptableEvent { get; }
        public abstract UnityEvent<T> Response { get; }
    }

    public abstract class EventListenerGeneric<T> : EventListenerBase, IScriptableEventListener<T>
    {
        protected virtual EventResponse<T>[] EventResponses => null;

        readonly Dictionary<ScriptableEvent<T>, EventResponse<T>> _byEvent = new();

        protected override void ToggleRegistration(bool toggle)
        {
            var responses = EventResponses;
            if (responses is null) return;

            foreach (var response in responses)
            {
                // Fail-loud policy: an unwired ScriptableEvent throws here rather than
                // silently dropping notifications.
                var channel = response.ScriptableEvent;
                if (toggle)
                {
                    channel.RegisterListener(this);
                    _byEvent.TryAdd(channel, response);
                }
                else
                {
                    channel.UnregisterListener(this);
                    _byEvent.Remove(channel);
                }
            }
        }

        void IScriptableEventListener<T>.OnEventRaised(ScriptableEvent<T> channel, T value)
        {
            if (!_byEvent.TryGetValue(channel, out var response)) return;
            Respond(response.Delay, () => response.Response?.Invoke(value));
        }
    }

    // ── The package's built-in listener components ───────────────────────────
    // Serialized layout matches the original assets: a `_eventResponses` array whose
    // entries carry `Delay`, the channel and the UnityEvent response (older assets name
    // those two `ScriptableEvent` / `Response`).

    public class EventListenerNoParam : EventListenerBase, IScriptableEventListener
    {
        [SerializeField] EventResponse[] _eventResponses = null;
        readonly Dictionary<ScriptableEventNoParam, EventResponse> _byEvent = new();

        [Serializable]
        public class EventResponse
        {
            [Min(0)] public float Delay;
            [FormerlySerializedAs("ScriptableEvent")] [SerializeField] ScriptableEventNoParam _scriptableEvent = null;
            [FormerlySerializedAs("Response")] [SerializeField] UnityEvent _response = null;
            public ScriptableEventNoParam ScriptableEvent => _scriptableEvent;
            public UnityEvent Response => _response;
        }

        protected override void ToggleRegistration(bool toggle)
        {
            if (_eventResponses is null) return;
            foreach (var response in _eventResponses)
            {
                var channel = response.ScriptableEvent;
                if (toggle)
                {
                    channel.RegisterListener(this);
                    _byEvent.TryAdd(channel, response);
                }
                else
                {
                    channel.UnregisterListener(this);
                    _byEvent.Remove(channel);
                }
            }
        }

        void IScriptableEventListener.OnEventRaised(ScriptableEventNoParam channel)
        {
            if (!_byEvent.TryGetValue(channel, out var response)) return;
            Respond(response.Delay, () => response.Response?.Invoke());
        }
    }

    public class EventListenerBool : EventListenerGeneric<bool>
    {
        [FormerlySerializedAs("m_eventResponses")] [SerializeField] EventResponse[] _eventResponses = null;
        protected override EventResponse<bool>[] EventResponses => _eventResponses;

        [Serializable]
        public class EventResponse : EventResponse<bool>
        {
            [FormerlySerializedAs("mScriptableEvent")] [SerializeField] ScriptableEventBool _scriptableEvent = null;
            [FormerlySerializedAs("m_response")] [SerializeField] BoolUnityEvent _response = null;
            public override ScriptableEvent<bool> ScriptableEvent => _scriptableEvent;
            public override UnityEvent<bool> Response => _response;
        }

        [Serializable] public class BoolUnityEvent : UnityEvent<bool> { }
    }

    public class EventListenerInt : EventListenerGeneric<int>
    {
        [FormerlySerializedAs("m_eventResponses")] [SerializeField] EventResponse[] _eventResponses = null;
        protected override EventResponse<int>[] EventResponses => _eventResponses;

        [Serializable]
        public class EventResponse : EventResponse<int>
        {
            [FormerlySerializedAs("mScriptableEvent")] [SerializeField] ScriptableEventInt _scriptableEvent = null;
            [FormerlySerializedAs("m_response")] [SerializeField] IntUnityEvent _response = null;
            public override ScriptableEvent<int> ScriptableEvent => _scriptableEvent;
            public override UnityEvent<int> Response => _response;
        }

        [Serializable] public class IntUnityEvent : UnityEvent<int> { }
    }

    public class EventListenerFloat : EventListenerGeneric<float>
    {
        [FormerlySerializedAs("m_eventResponses")] [SerializeField] EventResponse[] _eventResponses = null;
        protected override EventResponse<float>[] EventResponses => _eventResponses;

        [Serializable]
        public class EventResponse : EventResponse<float>
        {
            [FormerlySerializedAs("mScriptableEvent")] [SerializeField] ScriptableEventFloat _scriptableEvent = null;
            [FormerlySerializedAs("m_response")] [SerializeField] FloatUnityEvent _response = null;
            public override ScriptableEvent<float> ScriptableEvent => _scriptableEvent;
            public override UnityEvent<float> Response => _response;
        }

        [Serializable] public class FloatUnityEvent : UnityEvent<float> { }
    }

    public class EventListenerString : EventListenerGeneric<string>
    {
        [FormerlySerializedAs("m_eventResponses")] [SerializeField] EventResponse[] _eventResponses = null;
        protected override EventResponse<string>[] EventResponses => _eventResponses;

        [Serializable]
        public class EventResponse : EventResponse<string>
        {
            [FormerlySerializedAs("mScriptableEvent")] [SerializeField] ScriptableEventString _scriptableEvent = null;
            [FormerlySerializedAs("m_response")] [SerializeField] StringUnityEvent _response = null;
            public override ScriptableEvent<string> ScriptableEvent => _scriptableEvent;
            public override UnityEvent<string> Response => _response;
        }

        [Serializable] public class StringUnityEvent : UnityEvent<string> { }
    }

    public class EventListenerVector3 : EventListenerGeneric<Vector3>
    {
        [FormerlySerializedAs("m_eventResponses")] [SerializeField] EventResponse[] _eventResponses = null;
        protected override EventResponse<Vector3>[] EventResponses => _eventResponses;

        [Serializable]
        public class EventResponse : EventResponse<Vector3>
        {
            [FormerlySerializedAs("mScriptableEvent")] [SerializeField] ScriptableEventVector3 _scriptableEvent = null;
            [FormerlySerializedAs("m_response")] [SerializeField] Vector3UnityEvent _response = null;
            public override ScriptableEvent<Vector3> ScriptableEvent => _scriptableEvent;
            public override UnityEvent<Vector3> Response => _response;
        }

        [Serializable] public class Vector3UnityEvent : UnityEvent<Vector3> { }
    }
}
