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
    /// Runs a mode preview <b>inside the arcade modal's window</b>. The window never grows, the
    /// modal never closes, and the menu scene behind it never changes — clicking the window simply
    /// moves input from the UI to the vessel, and clicking away moves it back.
    ///
    /// <para><b>How the game gets into a window.</b> Three existing pieces, no new ones:</para>
    /// <list type="bullet">
    /// <item><see cref="ModePreviewArena"/> stands the mode's own cell up as a SATELLITE far from
    /// the menu world — the menu's cell is untouched, so there is no swap and nothing on screen
    /// changes.</item>
    /// <item><c>CameraManager.BeginWindowedPlayerCamera</c> points the ordinary gameplay rig at the
    /// vessel and renders it into the window's texture. It is the real gameplay camera, so the
    /// occlusion corridor and the speed tunnel come with it for free.</item>
    /// <item>Focus is the input handoff freestyle already performs — AI off, input unpaused,
    /// <c>sendNavigationEvents</c> off — <b>without</b> the fades, the camera blend or the
    /// <c>MainMenuState</c> change, because none of those belong to a windowed preview.</item>
    /// </list>
    ///
    /// <para><b>Local only.</b> No <c>NetworkObject</c> is created and <see cref="GameDataSO"/>'s
    /// launch fields are never written — Menu_Main hosts the party and that asset is the real
    /// launch config. The vessel is the player's OWN menu vessel, relocated for the duration:
    /// there is exactly one local pilot at any moment, which is what the occlusion corridor and
    /// speed tunnel (single-writer globals bound to the local pilot) require. A second vessel
    /// would be a second local pilot, which this platform does not support.</para>
    ///
    /// <para><b>Mass is conserved.</b> The arena is created by an explicit player action and struck
    /// by one; nothing runs on a clock and nothing is culled (Docs/ECOSYSTEM.md §19).</para>
    ///
    /// <para><b>One way out.</b> Every route — releasing focus, changing card, closing the modal,
    /// launching the real game, leaving the menu, teardown — funnels through <see cref="Stop"/>.</para>
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

        [SerializeField, Tooltip("HUD shown beside the window while a preview is live (objective, " +
                                 "progress, timer). Optional.")]
        ModePreviewHUD hud;

        [Header("Placement")]
        [SerializeField, Tooltip("How far from the menu world the satellite arena is parked. Must " +
                                 "stay well beyond every gameplay camera's far clip (8000 in " +
                                 "Menu_Main) so the menu view can never see it.")]
        float arenaDistance = 120000f;

        [Header("Timing")]
        [SerializeField, Tooltip("Seconds to wait for the arena's world to finish building before " +
                                 "giving up. A wedged build must not leave the window stuck.")]
        float buildTimeoutSeconds = 45f;

        [SerializeField, Tooltip("Seconds to let the arena settle after it builds, before the vessel " +
                                 "is placed and the objective starts counting.")]
        float settleSeconds = 0.5f;

        [Inject] GameDataSO gameData;

        readonly ModePreviewArena _arena = new();

        State _state = State.Idle;
        ModePreviewWindow _window;
        ModePreviewDefinitionSO _definition;
        ModePreviewRunner _runner;
        Pose _vesselHomePose;
        bool _hasVesselHome;
        VesselClassType _modeVessel = VesselClassType.Any;
        VesselClassType _restoreVesselClass = VesselClassType.Any;
        bool _navigationWasEnabled = true;
        CancellationTokenSource _cts;
        bool _subscribed;

        /// <summary>Raised whenever a preview stops, for any reason.</summary>
        public event Action<GameModes, ModePreviewOutcome> OnPreviewEnded;

        /// <summary>True from the moment an arena starts standing until it is struck.</summary>
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
            // Teardown, not a player exit: the scene is going away, so just let go.
            AbortHard();
        }

        void Subscribe()
        {
            if (_subscribed || gameData == null) return;
            gameData.OnLaunchGame.OnRaised += HandleLaunchRequested;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || gameData == null) return;
            gameData.OnLaunchGame.OnRaised -= HandleLaunchRequested;
            _subscribed = false;
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

        /// <summary>Unbind from the window and stop anything running.</summary>
        public void Detach()
        {
            if (!_window) { _window = null; return; }

            _window.OnFocusRequested -= HandleFocusRequested;
            _window.OnFocusReleased -= HandleFocusReleased;
            _window = null;
        }

        /// <summary>
        /// Arm the window for <paramref name="definition"/>. Switching to a different mode strikes
        /// whatever arena is standing — a satellite cell is the expensive half of this feature and
        /// must never outlive the card it belongs to.
        /// </summary>
        public void SetDefinition(ModePreviewDefinitionSO definition, VesselClassType modeVessel)
        {
            _modeVessel = modeVessel;
            if (_definition == definition) return;

            if (IsActive) Stop(ModePreviewOutcome.Abandoned);
            _definition = definition;
        }

        // ── Focus ────────────────────────────────────────────────────────────

        void HandleFocusRequested()
        {
            if (!_definition || !_definition.CanTestFlight) return;

            switch (_state)
            {
                case State.Idle:
                    StartArena();
                    break;
                case State.Live:
                    // The arena is already standing from an earlier focus - clicking back in is
                    // instant, which is the whole reason it is not struck on every release.
                    TakeFocus();
                    break;
            }
        }

        void HandleFocusReleased() => GiveBackFocus();

        /// <summary>Move input from the UI to the vessel. The visual half is the window's.</summary>
        void TakeFocus()
        {
            var player = gameData?.LocalPlayer;
            if (player?.Vessel == null) return;

            player.Vessel.ToggleAIPilot(false);
            player.InputController?.SetPause(false);

            // The pad flies the ship, so it must stop driving the UI at the same time. Exactly the
            // gate ScreenSwitcher applies for freestyle - a focused window is the same situation
            // in a smaller frame.
            if (EventSystem.current)
            {
                _navigationWasEnabled = EventSystem.current.sendNavigationEvents;
                EventSystem.current.sendNavigationEvents = false;
            }

            if (_window) _window.GrantFocus();
        }

        /// <summary>Move input back to the UI and let the vessel fly itself.</summary>
        void GiveBackFocus()
        {
            var player = gameData?.LocalPlayer;
            if (player?.Vessel != null)
            {
                player.InputController?.SetPause(true);
                player.Vessel.ToggleAIPilot(true);
            }

            if (EventSystem.current)
                EventSystem.current.sendNavigationEvents = _navigationWasEnabled;
        }

        // ── Standing the arena ───────────────────────────────────────────────

        void StartArena()
        {
            var player = gameData?.LocalPlayer;
            if (player?.Vessel == null || !player.IsLocalUser)
            {
                CSDebug.LogWarning("[ModePreview] No locally-owned menu vessel yet - preview refused.");
                return;
            }

            var template = ResolveTemplateCell(player);
            if (!template)
            {
                CSDebug.LogWarning("[ModePreview] No active Cell to clone a satellite from - refused.");
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

                if (!_arena.Stand(definition, template, prefab, origin))
                    throw new InvalidOperationException("the arena could not be stood up");

                // The mode's own hull. Flying Rampage as a Squirrel teaches nothing about
                // Rampage, and RequestSwap already preserves pose, speed and domain.
                await SwapVessel(ResolveVessel(definition), remember: true, ct);

                // The satellite builds its world without a veil (it is beside the menu, not
                // instead of it), so we wait on the cell rather than on a screen hold.
                if (!await WaitWhile(() => _arena.Cell && _arena.Cell.IsSwappingConfig, ct))
                    throw new TimeoutException("the arena's world never finished building");

                if (settleSeconds > 0f)
                    await UniTask.Delay((int)(settleSeconds * 1000f),
                        ignoreTimeScale: true, cancellationToken: ct);

                ParkVesselInArena(definition);
                if (!HandCameraToWindow()) throw new InvalidOperationException("no gameplay camera to lend");

                _window?.GoLive();
                StartRunner(definition);

                _state = State.Live;
                TakeFocus();
            }
            catch (OperationCanceledException)
            {
                // Superseded or destroyed - whoever cancelled owns the unwind.
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[ModePreview] Preview of {definition.Mode} failed: {e.Message}. " +
                                 "Returning the window to its idle model.");
                _state = State.Live;      // so Stop() has something to unwind
                Stop(ModePreviewOutcome.Abandoned);
            }
        }

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

        /// <summary>
        /// The hull the preview flies. The definition wins when it names one; otherwise
        /// <see cref="VesselClassType.Any"/> defers to the mode's own vessel list, which is what a
        /// vessel-locked mode already declares on its <c>SO_ArcadeGame</c>.
        /// </summary>
        VesselClassType ResolveVessel(ModePreviewDefinitionSO definition) =>
            definition.Vessel != VesselClassType.Any ? definition.Vessel : _modeVessel;

        async UniTask SwapVessel(VesselClassType target, bool remember, CancellationToken ct)
        {
            var player = gameData?.LocalPlayer;
            if (!vesselInitializer || player?.Vessel == null) return;
            if (target is VesselClassType.Any or VesselClassType.Random) return;

            var current = player.Vessel.VesselStatus.VesselType;
            if (target == current) return;

            if (remember) _restoreVesselClass = current;

            vesselInitializer.RequestSwap(target);
            await WaitWhile(() => vesselInitializer && vesselInitializer.IsSwapping, ct);
        }

        async UniTaskVoid RestoreVessel()
        {
            var target = _restoreVesselClass;
            _restoreVesselClass = VesselClassType.Any;
            if (target is VesselClassType.Any or VesselClassType.Random) return;

            await SwapVessel(target, remember: false, this.GetCancellationTokenOnDestroy());
        }

        void ReturnVesselHome()
        {
            if (!_hasVesselHome) return;

            var vessel = gameData?.LocalPlayer?.Vessel;
            if (vessel != null) vessel.SetPose(_vesselHomePose);
            _hasVesselHome = false;
        }

        bool HandCameraToWindow()
        {
            var manager = CameraManager.Instance;
            var vessel = gameData?.LocalPlayer?.Vessel;
            var texture = _window ? _window.LiveTexture : null;
            if (!manager || vessel?.VesselStatus == null || !texture) return false;

            return manager.BeginWindowedPlayerCamera(vessel.VesselStatus.CameraFollowTarget, texture) != null;
        }

        // ── Stopping ─────────────────────────────────────────────────────────

        /// <summary>Stop the preview and put everything back. Idempotent.</summary>
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

            GiveBackFocus();
            if (_window)
            {
                _window.ReleaseFocus();
                _window.GoIdle();
            }

            CameraManager.Instance?.EndWindowedPlayerCamera();
            ReturnVesselHome();
            _arena.Strike();

            // Give the hull back. Fire-and-forget: the swap is a networked round-trip and the
            // window is already idle, so nothing downstream waits on it.
            RestoreVessel().Forget();

            _state = State.Idle;
            ActiveMode = GameModes.Random;
            OnPreviewEnded?.Invoke(mode, outcome);
        }

        /// <summary>Drop everything with no unwind. Teardown only.</summary>
        void AbortHard()
        {
            if (_state == State.Idle) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            StopRunner();
            _arena.Strike();
            _state = State.Idle;
            ActiveMode = GameModes.Random;
        }

        void HandleLaunchRequested() => AbortHard();

        // ── Objective ────────────────────────────────────────────────────────

        void StartRunner(ModePreviewDefinitionSO definition)
        {
            if (!_runner) _runner = gameObject.AddComponent<ModePreviewRunner>();

            _runner.Begin(gameData?.LocalPlayer?.RoundStats, definition, HandleRunnerFinished);
            if (hud) hud.Show(_runner, definition, this);
        }

        void StopRunner()
        {
            if (_runner) _runner.Stop();
        }

        /// <summary>
        /// The objective completing does NOT tear the arena down: the window is a place you can
        /// keep flying in, and yanking the world away the instant a counter fills would be the
        /// full-screen mode-launcher behaviour in miniature. It just stops counting.
        /// </summary>
        void HandleRunnerFinished(ModePreviewOutcome outcome) { }

        /// <summary>Exit button on the preview HUD.</summary>
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
        /// Poll until <paramref name="condition"/> is false or the build timeout elapses; false on
        /// timeout. Written out rather than composed from a timeout combinator because a wedged
        /// build is an ordinary outcome here, not an exceptional one. Unscaled, because the menu is
        /// free to touch timeScale around it.
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
