using System;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Object = UnityEngine.Object;

namespace Obvious.Soap
{
    [CreateAssetMenu(fileName = "scriptable_event_noParam.asset", menuName = "Soap/ScriptableEvents/No Parameters")]
    public class ScriptableEventNoParam : ScriptableEventBase, IDrawObjectsInInspector
    {
        private readonly List<EventListenerNoParam> _eventListeners = new List<EventListenerNoParam>();

        // [Cosmic Shore patch] O(1) membership guard for RegisterListener/UnregisterListener.
        // The List.Contains scan was quadratic at prism scale: every prism prefab carries
        // EventListenerNoParam components wired to shared pool-flush events, so a 25k-prism
        // environment lay performed ~600M reference compares (Load Time Insights). The List is
        // kept as the source of truth so Raise() iteration order is unchanged.
        private readonly HashSet<EventListenerNoParam> _eventListenerLookup = new HashSet<EventListenerNoParam>();

        private readonly List<Object> _listenersObjects = new List<Object>();

        private Action _onRaised = null;
        
        /// <summary>
        /// Action raised when this event is raised.
        /// </summary>
        public event Action OnRaised
        {
            add
            {
                _onRaised += value;

                var listener = value.Target as Object;
                if (listener != null && !_listenersObjects.Contains(listener))
                    _listenersObjects.Add(listener);
            }
            remove
            {
                _onRaised -= value;

                var listener = value.Target as Object;
                if (_listenersObjects.Contains(listener))
                    _listenersObjects.Remove(listener);
            }
        }

        // [Cosmic Shore patch] see ScriptableEventBase — play-mode boundary reset for
        // domain-reload-disabled editor sessions.
        protected override void ClearRuntimeSubscribers()
        {
            _onRaised = null;
            _listenersObjects.Clear();
            _eventListeners.Clear();
            _eventListenerLookup.Clear();
        }

        /// <summary>
        /// Raise the event
        /// </summary>
        public void Raise()
        {
            if (!Application.isPlaying)
                return;

            for (var i = _eventListeners.Count - 1; i >= 0; i--)
                _eventListeners[i].OnEventRaised(this, _debugLogEnabled);

            _onRaised?.Invoke();

#if UNITY_EDITOR
            //As this uses reflection, I only allow it to be called in Editor.
            //If you want to display debug in builds, delete the #if UNITY_EDITOR
            if (_debugLogEnabled)
                Debug();
#endif
        }
        
        internal void RegisterListener(EventListenerNoParam listener)
        {
            if (_eventListenerLookup.Add(listener))
                _eventListeners.Add(listener);
        }

        internal void UnregisterListener(EventListenerNoParam listener)
        {
            if (_eventListenerLookup.Remove(listener))
                _eventListeners.Remove(listener);
        }
        
        /// <summary>
        /// Get all objects that are listening to this event.
        /// </summary>
        public List<Object> GetAllObjects()
        {
            var allObjects = new List<Object>(_eventListeners);
            allObjects.AddRange(_listenersObjects);
            return allObjects;
        }

        private void Debug()
        {
            if (_onRaised == null)
                return;
            var delegates = _onRaised.GetInvocationList();
            foreach (var del in delegates)
            {
                var sb = new StringBuilder();
                sb.Append("<color=#f75369>[Event] </color>");
                sb.Append(name);
                sb.Append(" => ");
                sb.Append(del.GetMethodInfo().Name);
                sb.Append("()");
                var monoBehaviour = del.Target as MonoBehaviour;
                UnityEngine.Debug.Log(sb.ToString(), monoBehaviour?.gameObject);
            }
        }

        public override void Reset()
        {
            _debugLogEnabled = false;
        }

        public override Type GetGenericType => typeof(void);
    }
}