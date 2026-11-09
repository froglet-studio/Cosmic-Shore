using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The director of <c>Voyage.unity</c>, the voyage's own scene (Garrett, 2026-11-01: "it will also allow you to load in
    /// its own clean scene without any freestyle baggage"): no toybox, no lava-lamp life, no app shell, no freestyle toggle.
    ///
    /// <list type="number">
    /// <item><b>Start.</b> The squad's pilot spawns at the harbour's berths (<see cref="berths"/>, the scene's fixed start),
    /// in the hull they flew in the menu, and is handed the stick as soon as the vessel is ready.</item>
    /// <item><b>Sail.</b> The request the Voyage window made (<see cref="VoyageLaunch.Take"/>) is sailed by the
    /// <see cref="VoyageHarbour"/>: a job to its destination, or the Ark escort through its worlds.</item>
    /// <item><b>Stop.</b> The voyage ends where it ends (docked at home, the Ark fallen, the escort's last world reached),
    /// and the scene returns to the menu a few seconds later. At a job's stop the pilot leaves with the HUD's exit (or the
    /// pause menu); the squad anchors there, and the Voyage window offers the next jobs from that spot.</item>
    /// </list>
    ///
    /// <para>Party: the host's scene load brings the party along; each friend's harbour follows the host's course
    /// (<see cref="VoyageCourseLink"/>), and the host's return to the menu brings everyone back.</para>
    /// </summary>
    public sealed class VoyageScene : MonoBehaviour
    {
        [Header("The harbour")]
        [SerializeField, Tooltip("Where the squad's pilots start, in seat order: the harbour's berths. Each faces the " +
                                 "heading the Ark departs on.")]
        Transform[] berths;

        [SerializeField, Min(1), Tooltip("Game intensity for the voyage scene's cell.")]
        int intensity = 1;

        [Header("Flight")]
        [SerializeField, Tooltip("The flight HUD's canvas groups, shown once the pilot has the stick.")]
        CanvasGroup[] flightGroups;

        [SerializeField, Tooltip("The scene camera's controller; its blend sets how long the hand-over takes.")]
        MainMenuCameraController cameraController;

        [SerializeField, Min(0f), Tooltip("Hand-over time when no camera controller is wired.")]
        float handOverSeconds = 1.5f;

        [Header("The end")]
        [SerializeField, Min(0f), Tooltip("Seconds between a voyage ending and the return to the menu (the end banner).")]
        float returnDelaySeconds = 6f;

        [SerializeField, Min(5f), Tooltip("A request that has not set sail after this long returns to the menu.")]
        float sailTimeoutSeconds = 90f;

        [Inject] GameDataSO _gameData;
        [Inject] MenuFreestyleEventsContainerSO _flightEvents;
        [Inject] Reflex.Core.Container _container;

        static VoyageScene s_current;

        VoyageHarbour _harbour;
        VoyageLaunchRequest _request;
        Transform _root;
        bool _flying, _controlTaken, _sailed, _returning, _leaving;
        CancellationTokenSource _cts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_current = null;

        /// <summary>The voyage scene's director while it is loaded, else null.</summary>
        public static VoyageScene Current => s_current;

        /// <summary>The harbour of this scene (null until the scene has started).</summary>
        public VoyageHarbour Harbour => _harbour;

        /// <summary>True once the pilot has the stick and until they leave.</summary>
        public bool IsFlying => _flying;

        void Awake() => s_current = this;

        void Start()
        {
            _cts = new CancellationTokenSource();
            ConfigureGameData();
            StandHarbour();
            if (_gameData)
            {
                _gameData.OnClientReady.OnRaised += HandleClientReady;
                if (_gameData.OnPlayerPairInitialized)
                    _gameData.OnPlayerPairInitialized.OnRaised += HandlePlayerPairInitialized;
            }
            VoyageHarbour.Changed += HandleHarbourChanged;
            _gameData?.InitializeGame();
            if (_gameData?.LocalPlayer?.Vessel != null) HandleClientReady();
        }

        void ConfigureGameData()
        {
            if (!_gameData) return;
            if (berths is { Length: > 0 }) _gameData.SetSpawnPositions(berths);
            _gameData.SelectedIntensity.Value = intensity;
            // A voyage is no arena match, and the last card's per-hull starting levels do not follow the pilot here: the
            // squad seeds its own elements (SquadDeployment).
            _gameData.PublishStartingElements(null);
            _gameData.IsArenaMatch = false;
            _gameData.OpponentAIVesselClass = VesselClassType.Random;
        }

        void StandHarbour()
        {
            var settings = VoyageToyDefinitionSO.Load();
            if (!settings)
            {
                CSDebug.LogWarning($"[VoyageScene] No voyage settings at Resources/{VoyageToyDefinitionSO.ResourcePath} - nothing can sail.");
                return;
            }
            _root = new GameObject("VoyageRoot").transform;
            _root.SetParent(transform, false);
            var initializer = FindFirstObjectByType<MenuServerPlayerVesselInitializer>();
            var context = new ToyContext
            {
                GameData = _gameData,
                VesselInitializer = initializer,
                VesselPrefabContainer = initializer ? initializer.VesselPrefabContainer : null,
                Container = _container,
                IsFreestyleActive = () => _flying,
            };
            _harbour = VoyageHarbour.Create(settings, _root, context);
        }

        // ── Start: the stick, then the voyage ────────────────────────────────

        void HandleClientReady()
        {
            if (_controlTaken) return;
            _controlTaken = true;
            TakeControlAsync(_cts.Token).Forget();
        }

        /// <summary>A friend's vessel paired on this machine: it flies (the menu would have parked it on autopilot).</summary>
        void HandlePlayerPairInitialized(ulong playerNetId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;
            foreach (var p in _gameData.Players)
                if (p != null && p.PlayerNetId == playerNetId && !p.IsLocalUser && p.Vessel.IsAlive()) p.StartPlayer();
        }

        async UniTaskVoid TakeControlAsync(CancellationToken ct)
        {
            // One frame, so the scene camera has taken its first frame from the berth before the hand-over blend starts.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            var player = _gameData ? _gameData.LocalPlayer : null;
            if (player?.Vessel == null || !player.IsLocalUser) return;

            _request = IsHost() ? VoyageLaunch.Take() : null;
            bool aiLocal = _request is { AiLocal: true };

            player.StartPlayer();
            // The menu's initializer spawns every hull on autopilot; a human seat is the human's from the first frame.
            if (!aiLocal) player.Vessel.ToggleAIPilot(false);
            _flightEvents?.OnGameStateTransitionStart.Raise();
            ShowFlightHud(true);
            float blend = cameraController ? cameraController.ActiveTransitionDuration : handOverSeconds;
            await UniTask.Delay((int)(blend * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
            if (!aiLocal) player.InputController?.SetPause(false);
            _flying = true;
            _flightEvents?.OnGameStateTransitionEnd.Raise();

            if (_request == null)
            {
                if (IsHost())
                    CSDebug.LogWarning("[VoyageScene] No voyage was requested (the scene was opened directly?) - fly, " +
                                       "and leave to return to the menu.");
                return;
            }
            Sail(_request);
            WatchSailAsync(ct).Forget();
        }

        void Sail(VoyageLaunchRequest request)
        {
            if (!_harbour) return;
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[VoyageScene] Sailing {request}.");
            if (request.Kind == VoyageLaunchKind.Escort)
                _harbour.SetSailEscort(request.Difficulty, request.Squad, request.AiLocal);
            else
                _harbour.SetSail(request.Job);
        }

        /// <summary>A voyage that never got underway (no vessel, nothing authored) must not strand the pilot here.</summary>
        async UniTaskVoid WatchSailAsync(CancellationToken ct)
        {
            float deadline = Time.unscaledTime + sailTimeoutSeconds;
            while (!_sailed && Time.unscaledTime < deadline)
            {
                if (_harbour && _harbour.IsUnderway) _sailed = true;
                await UniTask.Delay(250, DelayType.UnscaledDeltaTime, cancellationToken: ct);
            }
            if (_sailed || _returning) return;
            CSDebug.LogWarning($"[VoyageScene] {_request} did not set sail within {sailTimeoutSeconds:0}s - back to the menu.");
            ReturnToMenu(0f);
        }

        void HandleHarbourChanged()
        {
            if (!_harbour) return;
            if (_harbour.IsUnderway)
            {
                _sailed = true;
                return;
            }
            // The voyage is over (docked, fallen, the escort's last world reached, or given up): the stop is shown, then
            // the menu.
            if (_sailed) ReturnToMenu(_leaving ? 0.5f : returnDelaySeconds);
        }

        // ── Leaving ──────────────────────────────────────────────────────────

        /// <summary>
        /// The pilot leaves the voyage (the HUD's exit): at a stop the squad anchors there, mid-leg the voyage is given up,
        /// and the scene returns to the menu. A party friend follows the host and cannot leave on their own.
        /// </summary>
        public void Leave()
        {
            if (_returning || _leaving) return;
            if (!IsHost())
            {
                CSDebug.LogWarning("[VoyageScene] A friend follows the host's voyage; the host's return brings the party back.");
                return;
            }
            _leaving = true;
            if (_harbour && _harbour.IsUnderway)
            {
                _harbour.GiveUp("the pilot left the voyage");
                if (_harbour.IsUnderway) return;   // the run ends a beat later; HandleHarbourChanged returns
            }
            ReturnToMenu(0.5f);
        }

        void ReturnToMenu(float delay)
        {
            if (_returning) return;
            _returning = true;
            ReturnAsync(delay, _cts.Token).Forget();
        }

        async UniTaskVoid ReturnAsync(float delay, CancellationToken ct)
        {
            if (delay > 0f) await UniTask.Delay((int)(delay * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
            _flying = false;
            var player = _gameData ? _gameData.LocalPlayer : null;
            if (player?.Vessel.IsAlive() == true) player.InputController?.SetPause(true);
            ShowFlightHud(false);
            if (!IsHost()) return;
            var loader = FindAnyObjectByType<SceneLoader>(FindObjectsInactive.Include);
            if (loader) loader.ReturnToMainMenu();
            else CSDebug.LogWarning("[VoyageScene] No SceneLoader (Bootstrap not loaded?) - cannot return to the menu.");
        }

        void ShowFlightHud(bool show)
        {
            if (flightGroups == null) return;
            foreach (var group in flightGroups)
            {
                if (!group) continue;
                group.alpha = show ? 1f : 0f;
                group.interactable = show;
                group.blocksRaycasts = show;
            }
        }

        static bool IsHost()
        {
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.IsServer;
        }

        void OnDestroy()
        {
            VoyageHarbour.Changed -= HandleHarbourChanged;
            if (_gameData)
            {
                _gameData.OnClientReady.OnRaised -= HandleClientReady;
                if (_gameData.OnPlayerPairInitialized)
                    _gameData.OnPlayerPairInitialized.OnRaised -= HandlePlayerPairInitialized;
            }
            // Left some other way (the pause menu's Main Menu, a dropped session): a voyage still out ends here, so a job
            // at its stop is anchored and saved rather than lost with the scene.
            if (_harbour && _harbour.IsUnderway)
            {
                try { _harbour.GiveUp("the voyage scene unloaded"); }
                catch (System.Exception e) { CSDebug.LogWarning($"[VoyageScene] Ending the voyage on unload threw: {e.Message}"); }
            }
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (s_current == this) s_current = null;
        }
    }
}
