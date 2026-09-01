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

        [Tooltip("How long to wait for the turn to actually start after pressing GO before pressing again. " +
                 "MUST exceed the pre-turn countdown (~4s) — a shorter wait re-presses mid-countdown and " +
                 "restarts it from 3, forever.")]
        public float TurnStartTimeoutSeconds = 15f;

        [Tooltip("Seconds between replay attempts if the scene has not changed. The replay is a networked " +
                 "scene load and can be refused; one silent failure must not end the night.")]
        public float ReplayRetrySeconds = 20f;

        [Tooltip("If the Ready BUTTON never becomes live this long AFTER the arena finishes building, call the controller directly instead.")]
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
        float _arenaReadyAt;
        bool _usedControllerFallback;

        float _pressedGoAt;
        float _lastProgressAt;

        // THE GAME-OVER LATCH. Once a match has ended, the driver's only remaining job
        // in this scene is to get a replay; it must never press GO again. Without this
        // the loop could re-press into a finished match, and because a finished HexRace
        // leaves the objective already satisfied, the restarted turn ends on its first
        // frame — which reads on screen as the countdown replaying every few seconds
        // and never getting anywhere. Cleared only by a scene load.
        bool _gameOver;
        float _replayDueAt;
        float _replayRetryAt;
        int _replayAttempts;

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
            _arenaReadyAt = 0f;
            _usedControllerFallback = false;
            _gameOver = false;
            _replayAttempts = 0;
            _pressedGoAt = 0f;
            _lastProgressAt = Time.unscaledTime;
            _loggedHoldingAutopilot = false;
            _loggedPlayerCount = -1;
        }

        void HandleMiniGameEnd()
        {
            if (_gameOver) return;
            _gameOver = true;
            _replayAttempts = 0;
            _replayDueAt = Time.unscaledTime + Mathf.Max(0.5f, PostMatchSeconds);
            MarkProgress();
            Debug.Log($"[TrainingMatchDriver] Match ended — no more GO in this scene; replay in {PostMatchSeconds:0.0}s.");
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

            // Match over: the ONLY remaining action in this scene is getting a replay.
            // Never GO — see the _gameOver comment.
            if (_gameOver)
            {
                TickReplay();
                return;
            }

            if (_gameData.IsTurnRunning)
            {
                _pressedGoAt = 0f;
                MarkProgress();
                return;
            }

            // A build in progress IS progress. Ribcage lays 20k prisms before its
            // connecting panel releases, which is minutes on a slow machine — without
            // this the stall watchdog would fire mid-build and replay a scene that was
            // loading perfectly well, forever.
            if (!ArenaIsReady())
            {
                MarkProgress();
                _arenaReadyAt = 0f;
                return;
            }

            // The fallback's grace runs from ARENA-READY, not from scene load: a heavy
            // arena can take longer to build than the grace itself, and measuring from
            // the load would skip straight past the real button the instant it appeared.
            if (_arenaReadyAt <= 0f) _arenaReadyAt = Time.unscaledTime;

            // Pressed GO already? Give the countdown room to finish. The pre-turn
            // countdown is ~4 seconds and DOTween KILLS and restarts its sequence on
            // every BeginCountdown, so a press faster than that never lets it complete —
            // the number falls back to 3 and the turn never starts.
            if (_pressedGoAt > 0f && Time.unscaledTime - _pressedGoAt < TurnStartTimeoutSeconds)
                return;

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
            var button = ResolveReadyButton();
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
            {
                _pressedGoAt = Time.unscaledTime;
                MarkProgress();
                Debug.Log("[TrainingMatchDriver] Pressing GO (Ready button).");
                button.onClick.Invoke();
                return;
            }

            // No button, or it never became live. The HUD may not be wired in this
            // scene at all — say so once, then drive the controller directly.
            if (_usedControllerFallback) return;
            if (_arenaReadyAt <= 0f || Time.unscaledTime - _arenaReadyAt < ReadyButtonGraceSeconds) return;

            var controller = ResolveController();
            if (controller == null) return;

            _usedControllerFallback = true;
            _pressedGoAt = Time.unscaledTime;
            MarkProgress();
            Debug.LogWarning($"[TrainingMatchDriver] Ready button never became interactable within " +
                             $"{ReadyButtonGraceSeconds:0}s of the arena finishing — calling " +
                             $"{controller.GetType().Name}.OnReadyClicked() directly (once).");
            controller.OnReadyClicked();
        }

        /// <summary>
        /// Post-match: ask for a replay, and keep asking if the scene does not change.
        /// A replay is a networked scene load and can be refused (another scene event in
        /// flight, a controller mid-reset); one silent refusal must not end the night.
        /// Every attempt is announced, and after several the driver says plainly that it
        /// is stuck rather than retrying in silence forever.
        /// </summary>
        void TickReplay()
        {
            float now = Time.unscaledTime;
            if (now < _replayDueAt) return;
            if (_replayAttempts > 0 && now < _replayRetryAt) return;

            _replayAttempts++;
            _replayRetryAt = now + Mathf.Max(5f, ReplayRetrySeconds);
            MarkProgress();

            if (_replayAttempts > 1)
                Debug.LogWarning($"[TrainingMatchDriver] Scene still '{SceneManager.GetActiveScene().name}' after " +
                                 $"{_replayAttempts - 1} replay request(s) — asking again.");

            RequestReplay(_replayAttempts == 1 ? "match ended" : $"replay retry #{_replayAttempts}");

            if (_replayAttempts == 4)
                Debug.LogError("[TrainingMatchDriver] Replay is not taking effect. The loop is stuck on a " +
                               "finished match — check that the mode's controller is the server's and that " +
                               "its scene is in Build Settings.");
        }

        /// <summary>
        /// "Has this machine finished building the arena?" — the platform's own answer
        /// (<see cref="IPlayer.IsArenaReady"/>, set by MiniGameHUD when the connecting
        /// panel releases, and by its no-panel branch too).
        ///
        /// Needed because the Ready button's RESTING state differs between the two
        /// canvases: GameCanvas-HexRace ships it inactive, but the shared
        /// GameCanvas.prefab — used by ten scenes — ships it ACTIVE, and the HUD only
        /// hides it once its async setup reaches ToggleReadyButton(false). A poll that
        /// trusted visibility alone would press GO during the load in exactly those
        /// scenes and start the countdown over a half-built arena.
        /// </summary>
        bool ArenaIsReady()
        {
            var local = _gameData.LocalPlayer;
            return local != null && local.IsArenaReady;
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
