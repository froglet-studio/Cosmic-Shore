using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs a mode preview <b>inside the arcade modal's window</b>. Selecting a card starts the
    /// preview on its own: the mode's arena stands up as a satellite cell, the player's vessel is
    /// relocated into it, and the <b>AI flies it</b> — the window shows a game already being
    /// played, the way the old video showed one. Tapping the window takes the stick from the AI;
    /// tapping outside (or Escape / gamepad Start) gives it back, and the AI keeps flying. The
    /// window never grows, the modal never closes, and the menu scene behind it never changes.
    ///
    /// <para><b>The AI retarget is load-bearing.</b> <see cref="AIPilot"/> carries a serialized
    /// <c>CellRuntimeDataSO</c> — the scene's shared asset — so a vessel relocated 120k units into
    /// a satellite arena would keep hunting the MENU cell's crystals and immediately fly back out
    /// of the arena, leaving the window showing a lone vessel in empty space. The session
    /// retargets the pilot onto the satellite's own runtime instance for the duration and restores
    /// it on the way out.</para>
    ///
    /// <para><b>Local only.</b> No <c>NetworkObject</c> is created and <see cref="GameDataSO"/>'s
    /// launch fields are never written — Menu_Main hosts the party and that asset is the real
    /// launch config. The vessel is the player's OWN menu vessel: there is exactly one local pilot
    /// at any moment, which the occlusion corridor and speed tunnel (single-writer globals bound
    /// to the local pilot) require.</para>
    ///
    /// <para><b>Mass is conserved, and the teardown is POOL-SAFE.</b> The arena is created by an
    /// explicit player action and struck by one; the strike goes through
    /// <see cref="Cell.StrikeSatelliteWorld"/>, which returns pooled prisms to their pool and
    /// hands back the instantiated remainder for a frame-sliced drain — never a bare
    /// <c>Destroy</c>, which corrupts the pool's accounting and with it every trail in the scene
    /// (Docs/ECOSYSTEM.md §19; Docs/ModePreview/ARCHITECTURE.md).</para>
    /// </summary>
    public class ModePreviewSession : MonoBehaviour
    {
        enum State { Idle = 0, Standing = 1, Live = 2, Striking = 3 }

        [Header("Menu wiring")]
        [SerializeField, Tooltip("Cell prefab the satellite arena is instantiated from. Leave empty " +
                                 "to clone whatever prefab the scene's own cell came from.")]
        GameObject cellPrefab;

        [SerializeField, Tooltip("Menu vessel spawner, used to fly the mode's own hull. Leave empty " +
                                 "to keep whatever the player is already flying.")]
        MenuServerPlayerVesselInitializer vesselInitializer;

        [SerializeField, Tooltip("HUD shown beside the window once the player takes control " +
                                 "(objective, progress, timer). Optional.")]
        ModePreviewHUD hud;

        [Header("Placement")]
        [SerializeField, Tooltip("How far from the menu world the satellite arena is parked. Must " +
                                 "stay well beyond every gameplay camera's far clip (8000 in " +
                                 "Menu_Main) so the menu view can never see it.")]
        float arenaDistance = 120000f;

        [Header("Timing")]
        [SerializeField, Tooltip("Seconds to wait for the arena's world to finish building before " +
                                 "giving up and showing 'preview not available'.")]
        float buildTimeoutSeconds = 45f;

        [SerializeField, Tooltip("Seconds to let the arena settle after it builds, before the " +
                                 "vessel is placed and the window goes live.")]
        float settleSeconds = 0.5f;

        [Inject] GameDataSO gameData;
        [Inject] MenuFreestyleEventsContainerSO freestyleEvents;

        readonly ModePreviewArena _arena = new();

        State _state = State.Idle;
        ModePreviewWindow _window;
        ModePreviewDefinitionSO _definition;

        // The intensity the standing arena was built for. Held here rather than read live so a
        // rebuild is decided by comparing what IS standing against what was asked for, never by
        // whatever the config asset happens to say at the moment the check runs.
        int _intensity = 1;
        ModePreviewRunner _runner;
        bool _runnerStarted;
        bool _autoStartPending;
        Pose _vesselHomePose;
        bool _hasVesselHome;
        VesselClassType _modeVessel = VesselClassType.Any;
        VesselClassType _restoreVesselClass = VesselClassType.Any;
        CellRuntimeDataSO _restoreAICellData;
        bool _hasAIRetarget;
        bool _navigationWasEnabled = true;
        CancellationTokenSource _cts;
        bool _subscribed;

        /// <summary>Raised when a preview stops, for any reason.</summary>
        public event Action<GameModes, ModePreviewOutcome> OnPreviewEnded;

        /// <summary>True from the moment an arena starts standing until its strike completes.</summary>
        public bool IsActive => _state != State.Idle;

        /// <summary>The mode being previewed, or <see cref="GameModes.Random"/> when idle.</summary>
        public GameModes ActiveMode { get; private set; } = GameModes.Random;

        // ── Lifecycle ────────────────────────────────────────────────────────

        // Start, not OnEnable: [Inject] fields land after Awake and before Start.
        void Start() => Subscribe();

        void OnDestroy()
        {
            Unsubscribe();
            Detach();
            AbortHard();
        }

        void Subscribe()
        {
            if (_subscribed || gameData == null) return;
            gameData.OnLaunchGame.OnRaised += HandleLaunchRequested;

            // Entering freestyle (the lava lamp) while a preview holds the vessel would leave
            // the player flying 120k units from the world they think they are in, with the
            // gameplay camera still rendering into a window nobody can see. Normally the modal
            // closing stops the preview first, but this is the guarantee, not the usual path.
            if (freestyleEvents && freestyleEvents.OnGameStateTransitionStart)
                freestyleEvents.OnGameStateTransitionStart.OnRaised += HandleFreestyleEntered;

            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || gameData == null) return;
            gameData.OnLaunchGame.OnRaised -= HandleLaunchRequested;

            if (freestyleEvents && freestyleEvents.OnGameStateTransitionStart)
                freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleFreestyleEntered;

            _subscribed = false;
        }

        void HandleFreestyleEntered() => Stop(ModePreviewOutcome.Abandoned);

        void Update()
        {
            // Auto-start driver. Deferred to Update rather than fired inside SetDefinition so a
            // card change lands AFTER the previous arena's strike has fully completed (state
            // returns to Idle), and so a click that arrives before the local vessel exists just
            // waits instead of failing.
            if (!_autoStartPending || _state != State.Idle) return;
            if (!_definition || !_definition.CanTestFlight) { _autoStartPending = false; return; }

            var player = gameData?.LocalPlayer;
            if (player?.Vessel == null || !player.IsLocalUser) return;   // not ready yet - retry

            _autoStartPending = false;
            StartArena(player);
        }

        // ── Attachment (the modal owns both ends) ────────────────────────────

        /// <summary>
        /// Bind to the modal's preview window. The modal mediates because the window lives inside
        /// its prefab while this lives in the scene, and a prefab cannot hold a scene reference.
        /// </summary>
        public void Attach(ModePreviewWindow window)
        {
            if (_window == window) return;

            Detach();
            _window = window;
            if (!_window) return;

            _window.OnFocusRequested += HandleFocusRequested;
            _window.OnFocusReleased += HandleFocusReleased;
        }

        /// <summary>Unbind from the window. Anything running keeps running until Stop.</summary>
        public void Detach()
        {
            if (!_window) { _window = null; return; }

            _window.OnFocusRequested -= HandleFocusRequested;
            _window.OnFocusReleased -= HandleFocusReleased;
            _window = null;
        }

        /// <summary>
        /// A card was selected: preview <paramref name="definition"/>, starting on its own —
        /// no tap required, the AI flies until the player takes over. A null (or unflyable)
        /// definition shows the honest "preview not available" state instead. Switching cards
        /// strikes the standing arena first; a satellite cell is the expensive half of this
        /// feature and must never outlive the card it belongs to.
        /// </summary>
        public void SetDefinition(ModePreviewDefinitionSO definition, VesselClassType modeVessel,
                                  int intensity = 1)
        {
            _modeVessel = modeVessel;
            intensity = Mathf.Max(1, intensity);

            // A re-arm on the SAME card is the common case (the player nudged the intensity row),
            // and it must only pay for a rebuild when the arena would actually differ: standing a
            // satellite costs a multi-second cell build plus a networked hull swap, so rebuilding
            // an identical world would make the intensity row feel broken while changing nothing
            // on screen. A mode whose intensity is not an arena at all - Skim Race's track length,
            // the Maelstrom's pool - authors one cell and is never rebuilt.
            bool sameCard = _definition == definition && _state is State.Standing or State.Live;
            bool sameArena = !definition || !definition.ArenaVariesByIntensity ||
                             definition.ResolveCell(intensity) == definition.ResolveCell(_intensity);
            if (sameCard && sameArena)
            {
                _intensity = intensity;
                return;
            }

            if (IsActive) Stop(ModePreviewOutcome.Abandoned);
            _definition = definition;
            _intensity = intensity;

            if (definition && definition.CanTestFlight)
            {
                _window?.ShowLoading(definition.Mode.ToString());
                _autoStartPending = true;
            }
            else
            {
                _window?.ShowUnavailable();
                _autoStartPending = false;
            }
        }

        // ── Standing the arena ───────────────────────────────────────────────

        void StartArena(IPlayer player)
        {
            var template = ResolveTemplateCell(player);
            if (!template)
            {
                CSDebug.LogWarning("[ModePreview] No active Cell to clone a satellite from - " +
                                   "preview unavailable.");
                _window?.ShowUnavailable();
                return;
            }

            _state = State.Standing;
            ActiveMode = _definition.Mode;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            StandAsync(template, _cts.Token).Forget();
        }

        async UniTaskVoid StandAsync(Cell template, CancellationToken ct)
        {
            var definition = _definition;

            try
            {
                var prefab = cellPrefab ? cellPrefab : template.gameObject;
                var origin = Vector3.right * arenaDistance;

                var config = definition.ResolveCell(_intensity);
                if (!_arena.Stand(definition, config, template, prefab, origin))
                    throw new InvalidOperationException("the arena could not be stood up");

                // The mode's own hull. Flying Rampage as a Squirrel teaches nothing about
                // Rampage, and RequestSwap already preserves pose, speed and domain.
                await SwapVessel(ResolveVessel(definition), remember: true, ct);

                // The satellite builds without a veil (it is beside the menu, not instead of
                // it), so we wait on the cell rather than on a screen hold.
                if (!await WaitWhile(() => _arena.Cell && _arena.Cell.IsSwappingConfig, ct))
                    throw new TimeoutException(
                        $"{(config ? config.CellName : definition.Mode.ToString())} never finished building");

                if (settleSeconds > 0f)
                    await UniTask.Delay((int)(settleSeconds * 1000f),
                        ignoreTimeScale: true, cancellationToken: ct);

                // Relocate the vessel and point its autopilot at the ARENA's runtime data -
                // without this the AI keeps hunting the menu cell's crystals 120k units away
                // and flies straight back out of the arena.
                ParkVesselInArena(definition);
                RetargetAIToArena();

                if (!HandCameraToWindow())
                    throw new InvalidOperationException("no gameplay camera to lend");

                _state = State.Live;
                _window?.GoLive();
                // No TakeFocus here: the AI flies. The window is a game already in progress;
                // the tap is what makes it yours.
            }
            catch (OperationCanceledException)
            {
                // Superseded or destroyed - whoever cancelled owns the unwind.
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[ModePreview] Preview of {definition.Mode} failed: {e.Message}.");
                _state = State.Live;      // so Stop() has something to unwind
                Stop(ModePreviewOutcome.Abandoned);
                _window?.ShowUnavailable();
            }
        }

        // ── Focus (who holds the stick) ──────────────────────────────────────

        void HandleFocusRequested()
        {
            if (_state != State.Live) return;

            var player = gameData?.LocalPlayer;
            if (player?.Vessel == null) return;

            // Take the stick from the AI.
            player.Vessel.ToggleAIPilot(false);
            player.InputController?.SetPause(false);

            // The pad flies the ship, so it must stop driving the UI at the same time. The
            // EventSystem half; direct device polls (the modal's B-to-close) check
            // ModePreviewWindow.AnyHasFocus instead.
            if (EventSystem.current)
            {
                _navigationWasEnabled = EventSystem.current.sendNavigationEvents;
                EventSystem.current.sendNavigationEvents = false;
            }

            _window?.GrantFocus();

            // The objective starts counting from the player's first take-over - the AI's
            // warm-up flight is a demo, not their progress.
            if (!_runnerStarted) StartRunner(_definition);
        }

        void HandleFocusReleased()
        {
            var player = gameData?.LocalPlayer;
            if (player?.Vessel != null)
            {
                // Hand the stick back to the AI - the preview keeps playing in the window,
                // exactly as it did before the tap.
                player.InputController?.SetPause(true);
                player.Vessel.ToggleAIPilot(true);
            }

            if (EventSystem.current)
                EventSystem.current.sendNavigationEvents = _navigationWasEnabled;
        }

        // ── Stopping ─────────────────────────────────────────────────────────

        /// <summary>
        /// Stop the preview and put everything back. Idempotent. The unwind is a single
        /// SERIALIZED sequence (~a second: vessel home, hull swap AWAITED, arena struck and
        /// drained) and the session stays in <c>Striking</c> until every step lands — which is
        /// what lets the auto-start driver re-enter the NEXT preview safely: the first playtest's
        /// "leave and come back and everything goes to chaos" was this teardown racing the next
        /// entry, most concretely the fire-and-forget restore swap still holding
        /// <c>MenuServerPlayerVesselInitializer.IsSwapping</c> while the next preview's
        /// <c>RequestSwap</c> arrived and was silently dropped.
        /// </summary>
        public void Stop(ModePreviewOutcome outcome = ModePreviewOutcome.Abandoned)
        {
            if (_state is State.Idle or State.Striking) return;

            var mode = ActiveMode;
            _state = State.Striking;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            StopRunner();
            if (hud) hud.Hide();

            _window?.ReleaseFocus();          // routes through HandleFocusReleased → AI back on

            StopAsync(mode, outcome).Forget();
        }

        async UniTaskVoid StopAsync(GameModes mode, ModePreviewOutcome outcome)
        {
            var ct = this.GetCancellationTokenOnDestroy();

            try
            {
                // Pen the local trail up across the teleport: a spawner left live for even a
                // frame after SetPose lays a prism bridging 120k units of empty space.
                SetLocalTrailPaused(true);

                CameraManager.Instance?.EndWindowedPlayerCamera();
                RestoreAITarget();
                ReturnVesselHome();

                // AWAITED, not fire-and-forget: the next preview cannot be allowed to start
                // until the hull restore has fully landed, or its own swap request is dropped
                // by the initializer's in-flight guard.
                await RestoreVessel(ct);

                SetLocalTrailPaused(false);

                // Pool-safe retire + frame-sliced drain. Never a bare Destroy: pooled prisms
                // destroyed outright corrupt the pool and break every trail in the scene -
                // which is precisely how the first teardown killed the lava lamp.
                var retiring = _arena.BeginStrike();
                if (retiring)
                {
                    const int PrismsPerFrame = 500;
                    var prisms = retiring.GetComponentsInChildren<Prism>(true);
                    for (int i = 0; i < prisms.Length; i++)
                    {
                        if (prisms[i]) Destroy(prisms[i].gameObject);
                        if ((i + 1) % PrismsPerFrame == 0)
                            await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                    Destroy(retiring);
                }
            }
            catch (OperationCanceledException)
            {
                // Scene teardown mid-unwind - the unload destroys the remainder.
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[ModePreview] Unwinding the {mode} preview hit: {e.Message}.");
            }
            finally
            {
                _arena.FinishStrike();
                SetLocalTrailPaused(false);
                _state = State.Idle;
                ActiveMode = GameModes.Random;
                OnPreviewEnded?.Invoke(mode, outcome);
            }
        }

        /// <summary>
        /// Pen-up for the LOCAL vessel's trail spawner. Last-writer-wins with the painting toy's
        /// pen (same flag), which is acceptable here: the window is seconds wide and the runner
        /// re-asserts its pen at the next stroke boundary.
        /// </summary>
        void SetLocalTrailPaused(bool paused)
        {
            var controller = gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselPrismController;
            if (controller) controller.SetSpawnerPaused(paused);
        }

        /// <summary>Drop everything with no unwind. Teardown only.</summary>
        void AbortHard()
        {
            if (_state == State.Idle) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            StopRunner();
            _arena.FinishStrike();
            SetLocalTrailPaused(false);
            _state = State.Idle;
            ActiveMode = GameModes.Random;
        }

        void HandleLaunchRequested() => AbortHard();

        // ── Vessel + AI bookkeeping ──────────────────────────────────────────

        void ParkVesselInArena(ModePreviewDefinitionSO definition)
        {
            var vessel = gameData?.LocalPlayer?.Vessel;
            if (vessel?.VesselStatus == null) return;

            if (!_hasVesselHome)
            {
                var t = vessel.VesselStatus.Transform;
                _vesselHomePose = new Pose(t.position, t.rotation);
                _hasVesselHome = true;
            }

            vessel.SetPose(_arena.SpawnPose(definition));
        }

        void ReturnVesselHome()
        {
            if (!_hasVesselHome) return;

            var vessel = gameData?.LocalPlayer?.Vessel;
            if (vessel != null) vessel.SetPose(_vesselHomePose);
            _hasVesselHome = false;
        }

        void RetargetAIToArena()
        {
            var pilot = ResolveAIPilot();
            if (!pilot || _arena.RuntimeInstance == null) return;

            if (!_hasAIRetarget)
            {
                _restoreAICellData = pilot.CellData;
                _hasAIRetarget = true;
            }

            pilot.RetargetCell(_arena.RuntimeInstance);
        }

        void RestoreAITarget()
        {
            if (!_hasAIRetarget) return;
            _hasAIRetarget = false;

            var pilot = ResolveAIPilot();
            if (pilot) pilot.RetargetCell(_restoreAICellData);
            _restoreAICellData = null;
        }

        AIPilot ResolveAIPilot()
        {
            var vessel = gameData?.LocalPlayer?.Vessel;
            return vessel?.VesselStatus?.AIPilot;
        }

        /// <summary>
        /// The hull the preview flies. The definition wins when it names one; otherwise
        /// <see cref="VesselClassType.Any"/> defers to the mode's own vessel list, which is what
        /// a vessel-locked mode already declares on its <c>SO_ArcadeGame</c>.
        /// </summary>
        VesselClassType ResolveVessel(ModePreviewDefinitionSO definition) =>
            definition.Vessel != VesselClassType.Any ? definition.Vessel : _modeVessel;

        async UniTask SwapVessel(VesselClassType target, bool remember, CancellationToken ct)
        {
            var player = gameData?.LocalPlayer;
            if (!vesselInitializer || player?.Vessel == null) return;
            if (target is VesselClassType.Any or VesselClassType.Random) return;

            // An in-flight swap must FINISH first: RequestSwap silently drops a request while
            // one is running (its _isSwapping guard), which is how a rapid leave-and-re-enter
            // ended up flying the wrong hull.
            await WaitWhile(() => vesselInitializer && vesselInitializer.IsSwapping, ct);

            var current = player.Vessel.VesselStatus.VesselType;
            if (target == current) return;

            if (remember) _restoreVesselClass = current;

            vesselInitializer.RequestSwap(target);
            await WaitWhile(() => vesselInitializer && vesselInitializer.IsSwapping, ct);
        }

        async UniTask RestoreVessel(CancellationToken ct)
        {
            var target = _restoreVesselClass;
            _restoreVesselClass = VesselClassType.Any;
            if (target is VesselClassType.Any or VesselClassType.Random) return;

            await SwapVessel(target, remember: false, ct);
        }

        bool HandCameraToWindow()
        {
            var manager = CameraManager.Instance;
            var vessel = gameData?.LocalPlayer?.Vessel;
            var texture = _window ? _window.LiveTexture : null;
            if (!manager || vessel?.VesselStatus == null || !texture) return false;

            return manager.BeginWindowedPlayerCamera(vessel.VesselStatus.CameraFollowTarget, texture) != null;
        }

        // ── Objective ────────────────────────────────────────────────────────

        void StartRunner(ModePreviewDefinitionSO definition)
        {
            if (!definition) return;
            if (!_runner) _runner = gameObject.AddComponent<ModePreviewRunner>();

            _runnerStarted = true;
            _runner.Begin(gameData?.LocalPlayer?.RoundStats, definition, HandleRunnerFinished);
            if (hud) hud.Show(_runner, definition);
        }

        void StopRunner()
        {
            _runnerStarted = false;
            if (_runner) _runner.Stop();
        }

        /// <summary>
        /// The objective completing does not tear anything down — the window is a place you can
        /// keep flying in. It just stops counting.
        /// </summary>
        void HandleRunnerFinished(ModePreviewOutcome outcome) { }

        /// <summary>Kept for the HUD's API surface; nothing binds it to a button any more.</summary>
        public void RequestExit() => Stop();

        // ── Helpers ──────────────────────────────────────────────────────────

        Cell ResolveTemplateCell(IPlayer player)
        {
            var origin = player.Vessel.VesselStatus != null
                ? player.Vessel.VesselStatus.Transform.position
                : transform.position;

            // Unity's lifetime-aware operator, not ??: a destroyed Cell is non-null by reference.
            var containing = Cell.FindCellContaining(origin);
            return containing ? containing : Cell.FindNearestActiveCell(origin);
        }

        /// <summary>
        /// Poll until <paramref name="condition"/> is false or the build timeout elapses; false
        /// on timeout. Written out rather than composed from a timeout combinator because a
        /// wedged build is an ordinary outcome here, not an exceptional one. Unscaled, because
        /// the menu is free to touch timeScale around it.
        /// </summary>
        async UniTask<bool> WaitWhile(Func<bool> condition, CancellationToken ct)
        {
            float elapsed = 0f;
            while (condition())
            {
                if (elapsed >= buildTimeoutSeconds) return false;
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            return true;
        }
    }
}
