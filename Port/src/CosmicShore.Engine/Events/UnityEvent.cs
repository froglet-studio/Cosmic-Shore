using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 arg0);
    public delegate void UnityAction<T0, T1>(T0 arg0, T1 arg1);
    public delegate void UnityAction<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2);
    public delegate void UnityAction<T0, T1, T2, T3>(T0 arg0, T1 arg1, T2 arg2, T3 arg3);

    /// <summary>Whether a persistent call fires (original numeric values).</summary>
    public enum UnityEventCallState { Off = 0, EditorAndRuntime = 1, RuntimeOnly = 2 }

    /// <summary>Persistent-call argument modes (original numeric values).</summary>
    public enum PersistentListenerMode { EventDefined = 0, Void = 1, Object = 2, Int = 3, Float = 4, String = 5, Bool = 6 }

    /// <summary>
    /// Shared event machinery with the original's two listener tiers: PERSISTENT calls
    /// (authored in the inspector, wired by the content loader) run first and survive
    /// <see cref="RemoveAllListeners"/>; RUNTIME listeners (AddListener) follow.
    /// Port deviation, kept on purpose: a throwing listener is logged and the remaining
    /// listeners still run (the original aborts the invoke on the first exception).
    /// </summary>
    [Serializable]
    public abstract class UnityEventBase
    {
        protected readonly List<Delegate> m_Persistent = new();
        protected readonly List<Delegate> m_Runtime = new();

        /// <summary>Content-loader entry point for an inspector-authored call.</summary>
        public void AddPersistentListener(Delegate call) { if (call != null) m_Persistent.Add(call); }

        public int GetPersistentEventCount() => m_Persistent.Count;
        public string GetPersistentMethodName(int index) => index >= 0 && index < m_Persistent.Count ? m_Persistent[index].Method.Name : string.Empty;
        public Object GetPersistentTarget(int index) => index >= 0 && index < m_Persistent.Count ? m_Persistent[index].Target as Object : null;
        public void SetPersistentListenerState(int index, UnityEventCallState state) { }

        public void RemoveAllListeners() => m_Runtime.Clear();

        /// <summary>Total listener count (persistent + runtime) — port diagnostic.</summary>
        public int GetListenerCount() => m_Persistent.Count + m_Runtime.Count;

        protected void AddRuntime(Delegate call)
        {
            if (call is null) throw new ArgumentNullException(nameof(call));
            m_Runtime.Add(call);
        }

        protected void RemoveRuntime(Delegate call)
        {
            for (int i = m_Runtime.Count - 1; i >= 0; i--)
                if (m_Runtime[i].Equals(call)) { m_Runtime.RemoveAt(i); return; }
        }

        protected Delegate[] Snapshot()
        {
            var all = new Delegate[m_Persistent.Count + m_Runtime.Count];
            m_Persistent.CopyTo(all, 0);
            m_Runtime.CopyTo(all, m_Persistent.Count);
            return all;
        }

        protected static void Guard(Action call)
        {
            try { call(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    [Serializable]
    public class UnityEvent : UnityEventBase
    {
        public void AddListener(UnityAction call) => AddRuntime(call);
        public void RemoveListener(UnityAction call) => RemoveRuntime(call);

        public void Invoke()
        {
            foreach (var d in Snapshot())
                if (d is UnityAction a) Guard(() => a()); else Guard(() => d.DynamicInvoke());
        }
    }

    [Serializable]
    public class UnityEvent<T0> : UnityEventBase
    {
        public void AddListener(UnityAction<T0> call) => AddRuntime(call);
        public void RemoveListener(UnityAction<T0> call) => RemoveRuntime(call);

        public void Invoke(T0 arg0)
        {
            foreach (var d in Snapshot())
                if (d is UnityAction<T0> a) Guard(() => a(arg0)); else Guard(() => d.DynamicInvoke(arg0));
        }
    }

    [Serializable]
    public class UnityEvent<T0, T1> : UnityEventBase
    {
        public void AddListener(UnityAction<T0, T1> call) => AddRuntime(call);
        public void RemoveListener(UnityAction<T0, T1> call) => RemoveRuntime(call);

        public void Invoke(T0 arg0, T1 arg1)
        {
            foreach (var d in Snapshot())
                if (d is UnityAction<T0, T1> a) Guard(() => a(arg0, arg1)); else Guard(() => d.DynamicInvoke(arg0, arg1));
        }
    }

    [Serializable]
    public class UnityEvent<T0, T1, T2> : UnityEventBase
    {
        public void AddListener(UnityAction<T0, T1, T2> call) => AddRuntime(call);
        public void RemoveListener(UnityAction<T0, T1, T2> call) => RemoveRuntime(call);

        public void Invoke(T0 arg0, T1 arg1, T2 arg2)
        {
            foreach (var d in Snapshot())
                if (d is UnityAction<T0, T1, T2> a) Guard(() => a(arg0, arg1, arg2)); else Guard(() => d.DynamicInvoke(arg0, arg1, arg2));
        }
    }

    [Serializable]
    public class UnityEvent<T0, T1, T2, T3> : UnityEventBase
    {
        public void AddListener(UnityAction<T0, T1, T2, T3> call) => AddRuntime(call);
        public void RemoveListener(UnityAction<T0, T1, T2, T3> call) => RemoveRuntime(call);

        public void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            foreach (var d in Snapshot())
                if (d is UnityAction<T0, T1, T2, T3> a) Guard(() => a(arg0, arg1, arg2, arg3)); else Guard(() => d.DynamicInvoke(arg0, arg1, arg2, arg3));
        }
    }
}
