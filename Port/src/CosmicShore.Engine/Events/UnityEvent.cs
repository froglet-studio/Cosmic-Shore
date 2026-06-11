using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Events
{
    // First-party serializable event types. The "UnityEvent" name is kept deliberately so
    // the hundreds of ported declarations (e.g. `class FooUnityEvent : UnityEvent<Foo>`)
    // compile verbatim — only the using directive changes. Persistent (inspector-wired)
    // listeners arrive with the asset/scene pipeline; runtime listeners work today.
    // Exceptions are isolated per listener (logged, remaining listeners still run).

    [Serializable]
    public class UnityEvent
    {
        readonly List<Action> _listeners = new();

        public void AddListener(Action call)
        {
            if (call is null) throw new ArgumentNullException(nameof(call));
            _listeners.Add(call);
        }

        public void RemoveListener(Action call) => _listeners.Remove(call);
        public void RemoveAllListeners() => _listeners.Clear();
        public int GetListenerCount() => _listeners.Count;

        public void Invoke()
        {
            foreach (var listener in _listeners.ToArray())
            {
                try { listener(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

    [Serializable]
    public class UnityEvent<T0>
    {
        readonly List<Action<T0>> _listeners = new();

        public void AddListener(Action<T0> call)
        {
            if (call is null) throw new ArgumentNullException(nameof(call));
            _listeners.Add(call);
        }

        public void RemoveListener(Action<T0> call) => _listeners.Remove(call);
        public void RemoveAllListeners() => _listeners.Clear();
        public int GetListenerCount() => _listeners.Count;

        public void Invoke(T0 arg0)
        {
            foreach (var listener in _listeners.ToArray())
            {
                try { listener(arg0); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

    [Serializable]
    public class UnityEvent<T0, T1>
    {
        readonly List<Action<T0, T1>> _listeners = new();

        public void AddListener(Action<T0, T1> call)
        {
            if (call is null) throw new ArgumentNullException(nameof(call));
            _listeners.Add(call);
        }

        public void RemoveListener(Action<T0, T1> call) => _listeners.Remove(call);
        public void RemoveAllListeners() => _listeners.Clear();
        public int GetListenerCount() => _listeners.Count;

        public void Invoke(T0 arg0, T1 arg1)
        {
            foreach (var listener in _listeners.ToArray())
            {
                try { listener(arg0, arg1); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
