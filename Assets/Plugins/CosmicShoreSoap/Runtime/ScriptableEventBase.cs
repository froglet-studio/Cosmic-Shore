using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Soap
{
    /// <summary>
    /// Base of the event channels. Subscribers are runtime state only. The project enters
    /// Play mode without a domain reload (Enter Play Mode Options), so a C# handler whose
    /// owner never unsubscribed would otherwise survive into the next Play press, closing
    /// over destroyed objects; every subscriber is therefore dropped at both edit/play
    /// boundaries. Listener components re-register in their own Awake / OnEnable.
    /// </summary>
    public abstract class ScriptableEventBase : SoapAsset
    {
#pragma warning disable CS0169, CS0414, CS0649
        [SerializeField, HideInInspector] bool _debugLogEnabled; // kept for the asset format
#pragma warning restore CS0169, CS0414, CS0649

        /// <summary>Drop every C# subscriber and listener component.</summary>
        protected abstract void ClearRuntimeSubscribers();

#if UNITY_EDITOR
        void OnEnable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

        void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change is PlayModeStateChange.ExitingEditMode or PlayModeStateChange.EnteredEditMode)
                ClearRuntimeSubscribers();
        }
#endif
    }
}
