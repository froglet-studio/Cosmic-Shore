using System;

namespace CosmicShore.Engine.Soap
{
    /// <summary>
    /// Decoupled event channel — the port's replacement for the SOAP ScriptableEvent.
    /// Fail-loud policy carries over: consumers must not null-guard serialized channel
    /// references; a missing channel should throw at the raise/subscribe site.
    /// Listeners run inline on the raising thread (same contract as the original —
    /// raise only from the main thread).
    /// </summary>
    public abstract class ScriptableEventBase : ScriptableObject { }

    public class ScriptableEventNoParam : ScriptableEventBase
    {
        public event Action OnRaised;

        public void Raise() => OnRaised?.Invoke();
    }

    public class ScriptableEvent<T> : ScriptableEventBase
    {
        public event Action<T> OnRaised;

        public T LastValue { get; private set; }

        public void Raise(T param)
        {
            LastValue = param;
            OnRaised?.Invoke(param);
        }
    }

    // Common concrete event types, mirroring the original asset menu set.
    public class ScriptableEventBool : ScriptableEvent<bool> { }
    public class ScriptableEventInt : ScriptableEvent<int> { }
    public class ScriptableEventFloat : ScriptableEvent<float> { }
    public class ScriptableEventString : ScriptableEvent<string> { }
    public class ScriptableEventUlong : ScriptableEvent<ulong> { }
    public class ScriptableEventVector3 : ScriptableEvent<Vector3> { }
}
