// Ported verbatim from Assets/_Scripts/System/Bootstrap/ApplicationLifecycleManager.cs
// (bootstrap arc 2026-07-08 — replaces the former ApplicationLifecycleManager.Statics.cs
// shell, whose IsQuitting flag + OnAppPaused/OnAppQuitting statics live here now, on the
// upstream class shape). Mechanical substitutions (README): Reflex.Attributes →
// CosmicShore.Engine.Injection; UnityEngine.SceneManagement →
// CosmicShore.Engine.SceneManagement; UnityEngine → CosmicShore.Engine.
//
// FULLY LIVE: the dual static-C#-event + SOAP raise pipeline, the sceneLoaded /
// sceneUnloaded bridge, the IsQuitting shutdown guard, and the domain-reload static
// reset. The OnApplication{Pause,Focus,Quit} entry points are Unity player-loop
// messages the port's host layer raises when the app shell lands (nothing dispatches
// them automatically yet — same standing as the pre-port statics shell, where the
// events existed but no OS hook raised them); tests drive them directly.

using System;
using CosmicShore.ScriptableObjects;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Centralized application lifecycle event dispatcher.
    /// Place on the persistent Bootstrap root so it survives scene loads.
    ///
    /// Raises both static C# events (for legacy / programmatic subscribers)
    /// and SOAP events via <see cref="ApplicationLifecycleEventsContainerSO"/>
    /// (for inspector-wired listeners and decoupled architecture).
    /// </summary>
    public class ApplicationLifecycleManager : MonoBehaviour
    {
        [Inject] ApplicationLifecycleEventsContainerSO _lifecycleEvents;

        /// <summary>
        /// Fired when the app is paused (true) or resumed (false).
        /// Mobile: triggered by backgrounding/foregrounding.
        /// </summary>
        public static event Action<bool> OnAppPaused;

        /// <summary>
        /// Fired when the app gains (true) or loses (false) focus.
        /// Desktop: alt-tab, overlay windows, etc.
        /// </summary>
        public static event Action<bool> OnAppFocusChanged;

        /// <summary>
        /// Fired once when the application is about to quit.
        /// Use for save operations and cleanup.
        /// </summary>
        public static event Action OnAppQuitting;

        /// <summary>
        /// Fired when a new scene finishes loading. Passes the loaded scene and load mode.
        /// </summary>
        public static event Action<Scene, LoadSceneMode> OnSceneLoaded;

        /// <summary>
        /// Fired just before a scene is unloaded.
        /// </summary>
        public static event Action<Scene> OnSceneUnloading;

        static bool _isQuitting;

        /// <summary>
        /// Check this to guard against late operations during shutdown
        /// (e.g., avoiding Instantiate calls during OnDestroy).
        /// </summary>
        public static bool IsQuitting => _isQuitting;

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        void OnApplicationPause(bool pauseStatus)
        {
            OnAppPaused?.Invoke(pauseStatus);
            _lifecycleEvents?.OnAppPaused.Raise(pauseStatus);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            OnAppFocusChanged?.Invoke(hasFocus);
            _lifecycleEvents?.OnAppFocusChanged.Raise(hasFocus);
        }

        void OnApplicationQuit()
        {
            _isQuitting = true;
            OnAppQuitting?.Invoke();
            _lifecycleEvents?.OnAppQuitting.Raise();
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            OnSceneLoaded?.Invoke(scene, mode);
            _lifecycleEvents?.OnSceneLoaded.Raise(scene.name);
        }

        void HandleSceneUnloaded(Scene scene)
        {
            OnSceneUnloading?.Invoke(scene);
            _lifecycleEvents?.OnSceneUnloading.Raise(scene.name);
        }

        /// <summary>
        /// Reset static state on domain reload (editor play mode toggling).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _isQuitting = false;
            OnAppPaused = null;
            OnAppFocusChanged = null;
            OnAppQuitting = null;
            OnSceneLoaded = null;
            OnSceneUnloading = null;
        }
    }
}
