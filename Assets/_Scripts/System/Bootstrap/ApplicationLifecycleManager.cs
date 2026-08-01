using System;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        /// Fired once when the user has ASKED to quit and the quit has been
        /// deferred, giving subscribers a short bounded window to finish
        /// outbound network work while the app is still fully alive.
        ///
        /// <para>
        /// <see cref="OnAppQuitting"/> is too late for anything asynchronous: it
        /// fires from <c>OnApplicationQuit</c>, after teardown has begun, so an
        /// <c>async void</c> continuation is not guaranteed to run. That is why
        /// a quitting player used to stay in the UGS presence lobby - and
        /// therefore in every other player's online list - until the service
        /// reaped them.
        /// </para>
        /// </summary>
        public static event Action OnAppQuitRequested;

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

        /// <summary>
        /// How long the quit is held open for subscribers of
        /// <see cref="OnAppQuitRequested"/> to finish. Sized for one UGS
        /// round-trip on a poor connection; the app quits at the deadline
        /// whether or not they finished, so a hung request can never trap the
        /// user in a game that will not close.
        /// </summary>
        const float QUIT_DRAIN_SECONDS = 1.5f;

        /// <summary>
        /// Set once the drain has run, so the second <see cref="Application.wantsToQuit"/>
        /// callback (raised by our own <see cref="Application.Quit"/>) is allowed
        /// through instead of deferring again forever.
        /// </summary>
        static bool _quitApproved;

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            Application.wantsToQuit += HandleWantsToQuit;

#if UNITY_EDITOR
            // Stopping play mode does NOT raise Application.wantsToQuit, so in
            // the editor - which is where MPPM virtual players live, and where
            // multiplayer is actually exercised - the graceful-departure path
            // above never ran. A stopped virtual player therefore lingered in
            // every peer's online list until the UGS reap (~30 s), which is
            // precisely the symptom that made "turn off an instance" look broken.
            UnityEditor.EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
#endif
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            Application.wantsToQuit -= HandleWantsToQuit;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor equivalent of <see cref="HandleWantsToQuit"/>.
        ///
        /// <para>
        /// <c>ExitingPlayMode</c> fires while the play-mode domain is still
        /// alive, so subscribers can still dispatch an outbound request - but
        /// unlike <see cref="Application.wantsToQuit"/> the exit CANNOT be
        /// deferred, so there is no drain window and the request is genuinely
        /// best-effort. It is still far better than nothing: the UGS leave is
        /// usually dispatched, and a dispatched leave is fanned out to every peer
        /// immediately rather than waiting on the service's disconnect reap.
        /// </para>
        /// </summary>
        void HandlePlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
            if (_quitApproved) return;   // wantsToQuit already ran the drain

            _quitApproved = true;        // suppress a duplicate raise on the way out
            OnAppQuitRequested?.Invoke();
            _lifecycleEvents?.OnAppQuitRequested.Raise();
        }
#endif

        /// <summary>
        /// Defers the quit exactly once so subscribers can complete outbound
        /// network work, then lets it proceed.
        ///
        /// <para>
        /// Returning <c>false</c> cancels the quit; the app keeps running
        /// normally, which is what makes an awaited UGS leave possible at all.
        /// <see cref="DrainThenQuitAsync"/> then re-requests the quit, and this
        /// callback sees <see cref="_quitApproved"/> and lets it through.
        /// </para>
        ///
        /// <para>
        /// This is a best-effort courtesy, not a guarantee: a hard kill, a
        /// crash, or an OS-initiated termination never reaches this callback. It
        /// covers the ordinary cases - the quit button, alt-F4, and (in most
        /// Unity versions) stopping play mode - which is where the "ghost player
        /// lingering in everyone's online list" symptom actually comes from.
        /// </para>
        /// </summary>
        bool HandleWantsToQuit()
        {
            if (_quitApproved) return true;

            OnAppQuitRequested?.Invoke();
            _lifecycleEvents?.OnAppQuitRequested.Raise();

            DrainThenQuitAsync().Forget();
            return false;
        }

        async UniTaskVoid DrainThenQuitAsync()
        {
            // Unscaled: a paused game (Time.timeScale == 0) must still quit.
            await UniTask.Delay(
                TimeSpan.FromSeconds(QUIT_DRAIN_SECONDS),
                DelayType.UnscaledDeltaTime,
                PlayerLoopTiming.Update);

            _quitApproved = true;
            Application.Quit();
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
            _quitApproved = false;
            OnAppPaused = null;
            OnAppFocusChanged = null;
            OnAppQuitting = null;
            OnAppQuitRequested = null;
            OnSceneLoaded = null;
            OnSceneUnloading = null;
        }
    }
}
