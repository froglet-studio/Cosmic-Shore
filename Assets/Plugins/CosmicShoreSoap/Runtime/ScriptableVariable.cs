using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Soap
{
    /// <summary>
    /// A shared value in an asset. Writers set <see cref="Value"/>; readers read it or
    /// subscribe to <see cref="OnValueChanged"/>, which runs only when the value really
    /// changes. Set it from the main thread only.
    ///
    /// Each Play session starts from the value saved in the asset and returns to it when Play
    /// stops (and, with Reset On = Scene Loaded, whenever a scene loads in Single mode).
    /// Changes made while playing never mark the asset dirty, so they are never saved to disk.
    /// </summary>
    public abstract class ScriptableVariable<T> : ScriptableVariableBase
    {
        [Tooltip("The value. Play mode starts from this value and returns to it when Play stops.")]
        [SerializeField] protected T _value;

#pragma warning disable CS0169, CS0414, CS0649
        [SerializeField, HideInInspector] bool _debugLogEnabled; // kept for the asset format
        [SerializeField, HideInInspector] bool _saved;           // kept for the asset format
        [SerializeField, HideInInspector] T _defaultValue;       // kept for the asset format
#pragma warning restore CS0169, CS0414, CS0649

        [Tooltip("Scene Loaded: back to its starting value whenever a scene loads in Single mode.\n" +
                 "Application Starts: keeps its value across scene loads.")]
        [SerializeField] ResetType _resetOn = ResetType.SceneLoaded;

        T _initialValue;
#pragma warning disable CS0414 // read only by the editor's play-mode restore
        bool _sessionStarted;
#pragma warning restore CS0414

        /// <summary>Runs with the new value each time <see cref="Value"/> changes.</summary>
        public event Action<T> OnValueChanged;

        /// <summary>The value before the change, while <see cref="OnValueChanged"/> runs.</summary>
        public T PreviousValue { get; private set; }

        public virtual T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                _value = value;
                NotifyChanged();
#if UNITY_EDITOR
                // Only an edit made outside Play mode is meant to reach the asset on disk.
                if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
            }
        }

        /// <summary>Return to the value this Play session started with.</summary>
        public void ResetToInitialValue()
        {
            Value = _initialValue;
            PreviousValue = _initialValue;
        }

        public override string ToString() => $"{name} : {_value}";

        public static implicit operator T(ScriptableVariable<T> variable) => variable.Value;

        void NotifyChanged()
        {
            OnValueChanged?.Invoke(_value);
            PreviousValue = _value;
        }

        void Awake()
        {
            // Keep the runtime value alive when no loaded scene references this asset.
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // An asset first loaded during Play never saw ExitingEditMode: start its session now,
            // or Play stopping would "restore" it to default(T).
            if (EditorApplication.isPlayingOrWillChangePlaymode) BeginSession();
#else
            BeginSession();
#endif
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_resetOn == ResetType.SceneLoaded && mode == LoadSceneMode.Single)
                ResetToInitialValue();
        }

        void BeginSession()
        {
            _initialValue = _value;
            PreviousValue = _value;
            _sessionStarted = true;
            // Play mode runs without a domain reload: drop handlers left over from the last session.
            OnValueChanged = null;
        }

#if UNITY_EDITOR
        void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                BeginSession();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                // Handlers first, so a dead play-session handler never hears the restore.
                OnValueChanged = null;
                if (!_sessionStarted) return;
                _value = _initialValue;
                PreviousValue = _initialValue;
                _sessionStarted = false;
            }
        }

        /// <summary>An edit in the inspector notifies subscribers like a code write does.</summary>
        protected virtual void OnValidate()
        {
            if (EqualityComparer<T>.Default.Equals(_value, PreviousValue)) return;
            NotifyChanged();
        }
#endif
    }
}
