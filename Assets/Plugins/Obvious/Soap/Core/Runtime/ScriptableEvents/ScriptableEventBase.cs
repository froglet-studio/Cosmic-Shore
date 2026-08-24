using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Obvious.Soap
{
    public abstract class ScriptableEventBase : ScriptableBase
    {
        [Tooltip("Enable console logs when this event is raised.")]
        [SerializeField]
        protected bool _debugLogEnabled = false;
        public bool DebugLogEnabled => _debugLogEnabled;

#if UNITY_EDITOR
        // [Cosmic Shore patch] Events had no play-mode lifecycle at all (ScriptableVariable has
        // one), so with Enter Play Mode Options' domain reload disabled the private _onRaised
        // delegate survives every Play press and any C# handler whose owner forgot to
        // unsubscribe stacks one copy per session, each closing over dead scene/service state.
        // Mirror the variable's playModeStateChanged lifecycle: clear runtime subscriber state
        // both entering and leaving play. Component listeners re-register in their own OnEnable
        // and C# subscribers re-subscribe during play init, so a clear at these two boundaries
        // can only ever drop stale handlers.
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
                ClearRuntimeSubscribers();
        }
#endif

        /// <summary>
        /// [Cosmic Shore patch] Drop every runtime subscriber (C# delegates and component
        /// listeners). Called at both editor play-mode boundaries so subscriptions never leak
        /// across play sessions when domain reload is disabled.
        /// </summary>
        protected abstract void ClearRuntimeSubscribers();
    }
}
