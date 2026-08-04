// UnityEngine stays OUTSIDE the guard: the class declaration below is unconditional, so
// MonoBehaviour must resolve in Release too (where neither symbol is defined). Guarding this
// one using is what broke the Release player build with CS0246 - see DiagnosticsHUD, which
// this type mirrors, for the same layout.
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Unity.Netcode;
using UnityEngine.SceneManagement;
#endif

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Runtime host for <see cref="LoadInsights"/>. Auto-spawns in the Editor and Development
    /// builds (stripped from Release, mirroring <see cref="DiagnosticsHUD"/>) and stays dormant
    /// until Record Insight Mode is armed. It contributes everything the static recorder can't do
    /// alone:
    ///
    /// • per-frame sampling while a load records (frame count, worst frame, stall attribution),
    /// • error/exception capture during the load,
    /// • an in-flight snapshot every few seconds — so a force-killed 10-minute load still leaves
    ///   a report — plus recovery of that snapshot on the next run,
    /// • the CLIENT-side recording trigger: pure clients never run SceneLoader.LaunchGame, so the
    ///   host watches Netcode scene events and starts the recording when the server pulls this
    ///   client into a game scene,
    /// • safety rails: abort on return-to-menu, application quit, or a 15-minute timeout.
    ///
    /// Dev builds can arm recording without the editor via the -csmloadinsights command-line flag.
    /// </summary>
    public class LoadInsightsRuntime : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const float InFlightFlushSeconds = 5f;
        const float TimeoutMs = 15f * 60f * 1000f;

        static LoadInsightsRuntime s_instance;

        NetworkSceneManager _hookedSceneManager;
        float _nextFlushAt;
        int _clientSceneSpan = -1;   // Netcode Load → local LoadComplete (client-side scene load)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (Environment.CommandLine != null &&
                Environment.CommandLine.Contains("-csmloadinsights", StringComparison.OrdinalIgnoreCase))
                LoadInsights.Armed = true;
            EnsureSpawned();
        }

        /// <summary>Idempotent spawn — also callable from the editor tab when arming mid-play.</summary>
        public static void EnsureSpawned()
        {
            if (s_instance != null) return;
            var go = new GameObject("[LoadInsightsRuntime]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<LoadInsightsRuntime>();
        }

        void Awake()
        {
            s_instance = this;
            LoadInsights.HostAvailable = true;
            LoadInsights.MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            LoadInsights.RecoverInFlight();

            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnLogMessage;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceived -= OnLogMessage;
            UnhookNetcode();
            if (s_instance == this)
            {
                LoadInsights.HostAvailable = false;
                s_instance = null;
            }
        }

        void OnApplicationQuit()
        {
            // Editor: fires when Play Mode stops. A final in-flight snapshot has already been
            // written recently; still finalize properly so the report isn't marked interrupted.
            LoadInsights.AbortLoad("play session ended");
        }

        void Update()
        {
            MaintainNetcodeHook();

            if (!LoadInsights.IsRecording) return;

            LoadInsights.RecordFrame(Time.unscaledDeltaTime * 1000f);

            if (Time.unscaledTime >= _nextFlushAt)
            {
                _nextFlushAt = Time.unscaledTime + InFlightFlushSeconds;
                LoadInsights.SaveInFlight();
            }

            if (LoadInsights.ElapsedMs > TimeoutMs)
                LoadInsights.AbortLoad("timeout — still not playable after 15 minutes");
        }

        // ── Unity scene events ──────────────────────────

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!LoadInsights.IsRecording) return;

            LoadInsights.Mark($"Unity scene loaded: {scene.name} ({mode})");

            // A load recording that lands back in a non-game scene was abandoned by the player
            // (or failed and fell back) — close it out honestly instead of timing the menu.
            if (!IsGameScene(scene.name))
                LoadInsights.AbortLoad($"landed in non-game scene '{scene.name}'");
        }

        // ── Netcode scene events (the client-side trigger) ──

        void MaintainNetcodeHook()
        {
            var nm = NetworkManager.Singleton;
            var sm = nm != null ? nm.SceneManager : null;
            if (ReferenceEquals(sm, _hookedSceneManager)) return;

            UnhookNetcode();
            if (sm == null) return;
            sm.OnSceneEvent += OnNetcodeSceneEvent;
            _hookedSceneManager = sm;
        }

        void UnhookNetcode()
        {
            if (_hookedSceneManager == null) return;
            try { _hookedSceneManager.OnSceneEvent -= OnNetcodeSceneEvent; }
            catch { /* NetworkManager may already be disposed */ }
            _hookedSceneManager = null;
        }

        void OnNetcodeSceneEvent(SceneEvent ev)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            switch (ev.SceneEventType)
            {
                case SceneEventType.Load:
                    // Pure clients never run SceneLoader.LaunchGame — the server-driven scene pull
                    // IS their load start. (Hosts already began at InvokeGameLaunch; BeginLoad is
                    // a no-op while recording is active.)
                    if (!nm.IsServer && LoadInsights.Armed && !LoadInsights.IsRecording && IsGameScene(ev.SceneName))
                        LoadInsights.BeginLoad($"Netcode scene load (client) — {ev.SceneName}");
                    LoadInsights.Mark($"Netcode scene load begins: {ev.SceneName}");
                    if (!nm.IsServer)
                    {
                        LoadInsights.End(_clientSceneSpan);
                        _clientSceneSpan = LoadInsights.Begin(LoadInsightCategory.SceneLoad,
                            $"Client scene load ({ev.SceneName})");
                    }
                    break;

                case SceneEventType.LoadComplete:
                    LoadInsights.Mark($"Netcode scene load complete: {ev.SceneName} (client {ev.ClientId})");
                    if (!nm.IsServer && ev.ClientId == nm.LocalClientId)
                    {
                        LoadInsights.End(_clientSceneSpan);
                        _clientSceneSpan = -1;
                    }
                    break;

                case SceneEventType.LoadEventCompleted:
                    LoadInsights.Mark($"Netcode scene load completed on ALL clients: {ev.SceneName}");
                    break;

                case SceneEventType.Synchronize:
                    LoadInsights.Mark($"Netcode synchronize begins (client {ev.ClientId})");
                    break;

                case SceneEventType.SynchronizeComplete:
                    LoadInsights.Mark($"Netcode synchronize complete (client {ev.ClientId})");
                    break;
            }
        }

        void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (string.IsNullOrEmpty(condition)) return;

            string message = condition.Length > 220 ? condition[..220] + "…" : condition;
            LoadInsights.RecordError(type.ToString(), message);
        }

        /// <summary>Anything that isn't an app-shell scene counts as a game scene.</summary>
        static bool IsGameScene(string sceneName) =>
            sceneName != "Menu_Main" && sceneName != "Authentication" && sceneName != "Bootstrap";
#endif
    }
}
