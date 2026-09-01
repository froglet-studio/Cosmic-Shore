using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// The thing that PLAYS THE GAME when nobody is sitting at the keyboard.
    ///
    /// The training runner optimizes pilots; this drives the game flow around it.
    /// They are separate because they fail differently: a wedged GA is a bad
    /// generation, a wedged game flow is an empty night.
    ///
    /// Three jobs, all of them re-asserted rather than done once — that is the
    /// whole design, and it is why the first cut stalled:
    ///
    ///  1. HOLD EVERY VESSEL ON AUTOPILOT. The host's player is a HUMAN player
    ///     whose vessel we flip to autopilot. The platform disagrees, twice per
    ///     match: <see cref="Player.StartPlayer"/> un-pauses input for any player
    ///     whose <c>IsInitializedAsAI</c> is false, and
    ///     <c>MultiplayerMiniGameControllerBase.EnsureLocalHumanCanMove</c> does it
    ///     again explicitly at every countdown end. An un-paused
    ///     <see cref="InputController"/> writes the SAME <see cref="IInputStatus"/>
    ///     the <see cref="AIPilot"/> writes, every frame — so the AI's steering was
    ///     being overwritten with a resting keyboard. Flipping autopilot once at
    ///     scene load cannot survive that; re-asserting every frame can.
    ///
    ///  2. PRESS GO. Every mode gates its turn behind the Ready button. We press
    ///     the REAL button (<c>MiniGameHUDView.ReadyButton.onClick</c>) rather than
    ///     calling the controller directly, so every gate a human would wait for —
    ///     the connecting panel, the arena build, the pre-game cinematic, the
    ///     per-round unlock — still holds. Calling the controller is the fallback
    ///     for a scene with no HUD wired, and it is announced when it happens.
    ///
    ///  3. PRESS PLAY AGAIN. On <c>OnMiniGameEnd</c>, after a settle delay long
    ///     enough for the runner to bank the episode, ask the controller for a
    ///     replay. For HexRace that is a networked scene reload; the driver is
    ///     DontDestroyOnLoad and simply picks the next scene up.
    ///
    /// Plus a stall watchdog, because "set and forget" means the loop has to
    /// survive the one match that ends in a way nobody predicted.
    /// </summary>
    public class TrainingMatchDriver : MonoBehaviour
    {
        [Header("Tempo")]
        [Tooltip("Seconds to wait after a match ends before asking for a replay. Gives the runner time to bank the episode and flush the archive.")]
        public float PostMatchSeconds = 2.5f;

        [Tooltip("Minimum seconds between two Ready presses. Guards against double-firing while the HUD settles.")]
        public float ReadyPressCooldown = 1.5f;

        [Tooltip("If the Ready BUTTON never appears this long after the scene loads, call the controller directly instead.")]
        public float ReadyButtonGraceSeconds = 20f;

        [Tooltip("If no turn has started and nothing has changed for this long, force a replay. The overnight anti-wedge.")]
        public float StallSeconds = 150f;

        [Tooltip("Game speed multiplier for unattended runs. 1 = realtime. Physics stays at its authored step, so the cost is CPU, not fidelity.")]
        [Range(1f, 4f)] public float TimeScale = 1f;

        GameDataSO _gameData;
        bool _eventsHooked;

        // Per-scene, re-resolved on every load (a replay is a fresh scene).
        MiniGameControllerBase _controller;
        MiniGameHUDView _hudView;
        float _sceneLoadedAt;
        bool _usedControllerFallback;

        float _lastReadyPressAt = -999f;
        float _replayDueAt;
        bool _replayPending;
        float _lastProgressAt;

        // One log line per transition, never per frame.
        bool _loggedHoldingAutopilot;
        int _loggedPlayerCount = -1;

        public void Configure(GameDataSO gameData)
        {
            _gameData = gameData;
            HookEvents();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneLoadedAt = Time.unscaledTime;
            _lastProgressAt = Time.unscaledTime;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnhookEvents();
            RestoreTimeScale();
        }

        void HookEvents()
        {
            if (_eventsHooked || _gameData == null) return;
            if (_gameData.OnMiniGameEnd != null) _gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;
            _eventsHooked = true;
        }

        void UnhookEvents()
        {
            if (!_eventsHooked || _gameData == null) return;
            if (_gameData.OnMiniGameEnd != null) _gameData.OnMiniGameEnd.OnRaised -= HandleMiniGameEnd;
            _eventsHooked = false;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Everything below is per-scene state. A replay reload gives us a new
            // controller, a new HUD and a new set of vessels.
            _controller = null;
            _hudView = null;
            _sceneLoadedAt = Time.unscaledTime;
            _usedControllerFallback = false;
            _replayPending = false;
            _lastReadyPressAt = -999f;
            _lastProgressAt = Time.unscaledTime;
            _loggedHoldingAutopilot = false;
            _loggedPlayerCount = -1;
        }

        void HandleMiniGameEnd()
        {
            if (_replayPending) return;
            _replayPending = true;
            _replayDueAt = Time.unscaledTime + Mathf.Max(0.5f, PostMatchSeconds);
            MarkProgress();
            Debug.Log($"[TrainingMatchDriver] Match ended — replay in {PostMatchSeconds:0.0}s.");
        }

        void MarkProgress() => _lastProgressAt = Time.unscaledTime;

        // ── Frame ──────────────────────────────────
        // LateUpdate, deliberately: InputController and AIPilot both write the shared
        // IInputStatus from Update with no execution-order relationship between them.
        // Asserting the pause here means InputController.Update returns at its own top
        // on every subsequent frame, so the AI's writes are the only ones that land.
        void LateUpdate()
        {
            if (_gameData == null) return;

            ApplyTimeScale();
            HoldAllPilotsOnAutopilot();

            if (_replayPending)
            {
                if (Time.unscaledTime >= _replayDueAt)
                {
                    _replayPending = false;
                    RequestReplay("match ended");
                }
                return;
            }

            if (_gameData.IsTurnRunning)
            {
                MarkProgress();
                return;
            }

            TryPressReady();
            CheckStall();
        }

        void ApplyTimeScale()
        {
            float wanted = Mathf.Clamp(TimeScale, 1f, 4f);
            if (!Mathf.Approximately(Time.timeScale, wanted) && Time.timeScale > 0f)
                Time.timeScale = wanted;
        }

        void RestoreTimeScale()
        {
            if (Time.timeScale > 0f) Time.timeScale = 1f;
        }

        /// <summary>
        /// Every player in the match flies on autopilot with its human input muted.
        /// Cheap and idempotent: <see cref="AIPilot.StartAIPilot"/> is only called on
        /// the transition, because it clears and restarts every ability coroutine and
        /// calling it per frame would mean no ability ever completes.
        /// </summary>
        void HoldAllPilotsOnAutopilot()
        {
            var players = _gameData.Players;
            if (players == null) return;

            if (players.Count != _loggedPlayerCount)
            {
                _loggedPlayerCount = players.Count;
                Debug.Log($"[TrainingMatchDriver] Roster now {players.Count} player(s).");
            }

            int flying = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.Vessel == null) continue;

                var status = p.Vessel.VesselStatus;
                if (status == null || status.AIPilot == null) continue;

                if (!status.AutoPilotEnabled)
                {
                    // Configure BEFORE starting: the backfilled AI got this from the spawn
                    // pipeline and the host's vessel never did, so without it the host pilot
                    // races on the prefab's authored seek/skill instead of the match's. A
                    // pilot set up differently from the ones it races is a confound, not an
                    // opponent — and the rule lives in one place so it cannot drift.
                    ServerPlayerVesselInitializerWithAI.ConfigureAIPilotForMode(status.AIPilot, _gameData);
                    p.Vessel.ToggleAIPilot(true);
                    Debug.Log($"[TrainingMatchDriver] '{p.Name}' → autopilot.");
                }

                // The mute. Re-asserted rather than set once: StartPlayer and
                // EnsureLocalHumanCanMove both clear it at every countdown end.
                var inputStatus = p.InputStatus;
                if (inputStatus != null && !inputStatus.Paused && p.InputController != null)
                    p.InputController.SetPause(true);

                flying++;
            }

            if (flying > 0 && !_loggedHoldingAutopilot)
            {
                _loggedHoldingAutopilot = true;
                Debug.Log($"[TrainingMatchDriver] Holding {flying} vessel(s) on autopilot with human input muted.");
            }
        }

        void TryPressReady()
        {
            if (Time.unscaledTime - _lastReadyPressAt < ReadyPressCooldown) return;

            var button = ResolveReadyButton();
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
            {
                _lastReadyPressAt = Time.unscaledTime;
                MarkProgress();
                Debug.Log("[TrainingMatchDriver] Pressing GO (Ready button).");
                button.onClick.Invoke();
                return;
            }

            // No button, or it never became live. The HUD may not be wired in this
            // scene at all — say so once, then drive the controller directly.
            if (_usedControllerFallback) return;
            if (Time.unscaledTime - _sceneLoadedAt < ReadyButtonGraceSeconds) return;

            var controller = ResolveController();
            if (controller == null) return;

            _usedControllerFallback = true;
            _lastReadyPressAt = Time.unscaledTime;
            MarkProgress();
            Debug.LogWarning($"[TrainingMatchDriver] Ready button never became interactable within " +
                             $"{ReadyButtonGraceSeconds:0}s — calling {controller.GetType().Name}.OnReadyClicked() directly.");
            controller.OnReadyClicked();
        }

        void CheckStall()
        {
            if (Time.unscaledTime - _lastProgressAt < StallSeconds) return;

            Debug.LogWarning($"[TrainingMatchDriver] No turn started for {StallSeconds:0}s — forcing a replay to keep the loop alive.");
            MarkProgress();
            RequestReplay("stall watchdog");
        }

        void RequestReplay(string reason)
        {
            var controller = ResolveController();
            if (controller == null)
            {
                Debug.LogWarning($"[TrainingMatchDriver] Replay requested ({reason}) but no MiniGameControllerBase in the scene.");
                return;
            }

            MarkProgress();
            Debug.Log($"[TrainingMatchDriver] Requesting replay ({reason}).");
            controller.RequestReplay();
        }

        MiniGameControllerBase ResolveController()
        {
            if (_controller != null) return _controller;
            _controller = FindAnyObjectByType<MiniGameControllerBase>(FindObjectsInactive.Include);
            return _controller;
        }

        MiniGameHUDView ResolveHudView()
        {
            if (_hudView != null) return _hudView;
            _hudView = FindAnyObjectByType<MiniGameHUDView>(FindObjectsInactive.Include);
            return _hudView;
        }

        Button ResolveReadyButton()
        {
            var view = ResolveHudView();
            return view != null ? view.ReadyButton : null;
        }
    }
}
