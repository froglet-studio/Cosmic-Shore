using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
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
        /// Raised once when the player has ASKED to quit, while the app is still fully alive. Each subscriber
        /// returns the outbound work it started (the presence lobby and party session leaves); the quit waits
        /// for all of it, at most <see cref="QUIT_DRAIN_SECONDS"/>, then goes through.
        ///
        /// <para>
        /// Block 5 step 9 (Docs/PartySystem/BRANCH_AUDIT_2026-10.md §2; the August presence work's 52b8f5f656).
        /// <see cref="OnAppQuitting"/> is too late for anything asynchronous: it fires from
        /// <c>OnApplicationQuit</c>, after teardown has begun, and Unity does not wait for an async
        /// continuation. That is why a quitting player stayed in every other player's online list until UGS
        /// reaped them (about 30 s): the leave never left.
        /// </para>
        /// </summary>
        public static event Func<UniTask> OnAppQuitRequested;

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
        /// The longest a quit is held for <see cref="OnAppQuitRequested"/>: one UGS round-trip on a poor
        /// connection. The app quits at the deadline whether or not the work finished, so a hung request can
        /// never trap the player in a game that will not close.
        /// </summary>
        public const float QUIT_DRAIN_SECONDS = 1.5f;

        /// <summary>Set once the drain has run, so the quit it re-requests goes through.</summary>
        static bool _quitApproved;

        /// <summary>Set while the drain runs, so a second close request does not start a second one.</summary>
        static bool _quitDraining;

        /// <summary>
        /// How the drained quit is finally made: <see cref="DesktopPlatformServices.Quit"/> (Application.Quit
        /// in a player, the end of Play mode in the Editor, where Application.Quit is ignored). Tests swap it.
        /// </summary>
        internal static Action QuitNow = DesktopPlatformServices.Quit;

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            Application.wantsToQuit += DeferQuitOnce;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            Application.wantsToQuit -= DeferQuitOnce;
        }

        /// <summary>
        /// <see cref="Application.wantsToQuit"/>: true lets the quit through. With nothing subscribed to
        /// <see cref="OnAppQuitRequested"/> it goes at once; otherwise it is refused this once (the app keeps
        /// running, which is what lets an awaited UGS leave finish), the drain runs, and the drain quits.
        /// Best effort: a hard kill, a crash or an OS termination never reaches this, and the UGS reap is the
        /// floor for those.
        /// </summary>
        internal static bool DeferQuitOnce()
        {
            if (_quitApproved) return true;
            var handlers = OnAppQuitRequested;
            if (handlers == null) return true;
            if (_quitDraining) return false;
            _quitDraining = true;
            DrainThenQuitAsync(handlers).Forget();
            return false;
        }

        static async UniTaskVoid DrainThenQuitAsync(Func<UniTask> handlers)
        {
            var work = new List<UniTask>();
            foreach (Func<UniTask> handler in handlers.GetInvocationList())
            {
                try { work.Add(handler()); }
                catch (Exception e) { CSDebug.LogWarning($"[AppLifecycle] A quit handler failed to start: {e.Message}"); }
            }

            try
            {
                // Realtime: a paused game (Time.timeScale 0) must still quit.
                await UniTask.WhenAny(
                    UniTask.WhenAll(work),
                    UniTask.Delay(TimeSpan.FromSeconds(QUIT_DRAIN_SECONDS), DelayType.Realtime));
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[AppLifecycle] Quit work ended with an error; quitting anyway: {e.Message}");
            }

            _quitApproved = true;
            QuitNow();
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
            _quitDraining = false;
            OnAppQuitRequested = null;
            OnAppPaused = null;
            OnAppFocusChanged = null;
            OnAppQuitting = null;
            OnSceneLoaded = null;
            OnSceneUnloading = null;
        }
    }
}
