using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns a <b>Test Flight</b>: the short, single-player, in-menu taste of a game mode that
    /// replaces the arcade card's preview video.
    ///
    /// <para><b>It builds nothing of its own.</b> Every step is an existing, shipped path:</para>
    /// <list type="bullet">
    /// <item><see cref="MenuCrystalClickHandler.ToggleTransition"/> does the whole chrome /
    /// camera / input handoff — which is also what gives the preview <c>ScreenSwitcher</c>'s
    /// input gate (<c>EventSystem.sendNavigationEvents = false</c>) for free, so the player is
    /// never flying a vessel and driving the UI with the same stick.</item>
    /// <item><see cref="MenuServerPlayerVesselInitializer.RequestSwap"/> puts them in the mode's
    /// hull, preserving pose, speed and domain.</item>
    /// <item><see cref="Cell.RequestCellSwap"/> — the ONE sanctioned runtime world-swap door —
    /// suctions the menu world away and blooms the mode's own world in behind the standard
    /// <c>EnvironmentLoadVeil</c>. This is the Wanderway pattern (leave, play, come back), and
    /// it is what keeps the collider budget flat: the preview REPLACES the menu world rather
    /// than standing a second ecology up beside it.</item>
    /// </list>
    ///
    /// <para><b>Local only.</b> Nothing here carries a <c>NetworkObject</c> and nothing writes
    /// <see cref="GameDataSO"/>'s launch fields — Menu_Main runs a live host with party members,
    /// and <c>GameDataSO</c> is the real launch config that syncs to them. A party member keeps
    /// flying the menu world while you fly the preview, exactly as they already do when you pick
    /// a different world with the Cell Selector toy.</para>
    ///
    /// <para><b>Mass is conserved.</b> A cell swap is an explicit, player-initiated world change
    /// — the same class of event as a scene load — and it is the only thing that removes this
    /// mass. Nothing here runs on a clock, ages a prism out, or culls a population
    /// (Docs/ECOSYSTEM.md §19). The preview's own duration ends the FLIGHT, not the world.</para>
    ///
    /// <para><b>One way out.</b> Every exit — the HUD button, gamepad Start, a screen change, the
    /// objective completing, the timer, launching the real game, leaving the menu, app teardown —
    /// funnels through the single idempotent <see cref="End"/>. That is deliberately copied from
    /// <c>WanderwayRun</c>: a "leave the world and come back" feature dies of the exit path
    /// nobody remembered.</para>
    /// </summary>
    public class ModePreviewSession : MonoBehaviour
    {
        enum State { Idle = 0, Entering = 1, Flying = 2, Exiting = 3 }

        [Header("Menu wiring")]
        [SerializeField, Tooltip("The freestyle toggle. Its ToggleTransition does the chrome fade, " +
                                 "camera blend, autopilot handoff and input gating - a preview must " +
                                 "never reimplement any of that.")]
        MenuCrystalClickHandler freestyleHandler;

        [SerializeField, Tooltip("Menu vessel spawner, used to put the player in the mode's own hull. " +
                                 "Leave empty to skip vessel swapping entirely.")]
        MenuServerPlayerVesselInitializer vesselInitializer;

        [SerializeField, Tooltip("HUD shown for the duration of the flight (objective, progress, timer, " +
                                 "exit). Optional - without it the flight still runs and still ends.")]
        ModePreviewHUD hud;

        [Header("Timing")]
        [SerializeField, Tooltip("Seconds to wait for a vessel swap or a cell swap before giving up " +
                                 "and unwinding. A wedged swap must not strand the player in a menu " +
                                 "with no chrome.")]
        float swapTimeoutSeconds = 45f;

        [SerializeField, Tooltip("Seconds to let the newly bloomed world settle before the vessel is " +
                                 "placed and the objective starts counting.")]
        float settleSeconds = 0.5f;

        [Inject] GameDataSO gameData;

        readonly ModePreviewRequest _request = new();

        State _state = State.Idle;
        ModePreviewRunner _runner;
        CellConfigDataSO _restoreCellConfig;
        VesselClassType _restoreVesselClass = VesselClassType.Any;
        GameObject _structure;
        Cell _cell;
        CancellationTokenSource _cts;
        bool _subscribed;

        /// <summary>Raised when a flight ends, whatever the reason. The arcade UI reopens on this.</summary>
        public event Action<GameModes, ModePreviewOutcome> OnPreviewEnded;

        /// <summary>True from the moment a flight is requested until the menu world is back.</summary>
        public bool IsActive => _state != State.Idle;

        /// <summary>The mode currently being previewed, or <see cref="GameModes.Random"/> when idle.</summary>
        public GameModes ActiveMode { get; private set; } = GameModes.Random;

        // ── Lifecycle ────────────────────────────────────────────────────────

        // Subscribe in Start, not OnEnable: [Inject] fields land after Awake and before Start,
        // so OnEnable would see a null gameData on the first enable (the same reason
        // ToyboxController subscribes here).
        void Start() => Subscribe();

        void OnDestroy()
        {
            Unsubscribe();
            // Teardown, not an exit the player chose: the scene is going away, so unwinding the
            // world would be racing its own destruction. Just stop counting.
            AbortHard();
        }

        void Subscribe()
        {
            if (_subscribed || gameData == null) return;

            // Launching the real game ends any preview immediately - the scene is about to be
            // replaced and a half-unwound preview must not ride into it.
            gameData.OnLaunchGame.OnRaised += HandleLaunchRequested;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || gameData == null) return;

            gameData.OnLaunchGame.OnRaised -= HandleLaunchRequested;
            _subscribed = false;
        }

        void Update()
        {
            if (_state != State.Flying) return;

            // Losing freestyle IS the exit, and it is the one that needs no wiring: gamepad
            // Start, the on-screen Volume/Pause exit and anything else that drops control all
            // reach us here. Tested as a STATE rather than as a falling edge, so a drop that
            // lands mid-entry (before this ever saw a true) is caught too. Watched only while
            // Flying, and our own exit sets Exiting BEFORE it toggles freestyle off, so this can
            // never re-enter itself.
            if (!freestyleHandler || !freestyleHandler.IsInFreestyle)
                End(ModePreviewOutcome.Abandoned);
        }

        // ── Entry ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fly <paramref name="definition"/>. Returns false (and does nothing) when a preview is
        /// already running, the definition cannot be flown, or the menu is not in a state where
        /// control can be handed over.
        /// </summary>
        public bool TryBegin(ModePreviewDefinitionSO definition)
        {
            if (_state != State.Idle) return false;

            if (!definition || !definition.CanTestFlight)
            {
                CSDebug.LogWarning("[ModePreview] Test Flight requested with no flyable definition - ignored.");
                return false;
            }

            if (!freestyleHandler)
            {
                CSDebug.LogWarning("[ModePreview] No MenuCrystalClickHandler wired - a preview cannot " +
                                   "take control of the vessel, so the flight is refused rather than " +
                                   "leaving the player in a menu with no chrome.");
                return false;
            }

            var player = gameData != null ? gameData.LocalPlayer : null;
            if (player?.Vessel == null || !player.IsLocalUser)
            {
                CSDebug.LogWarning("[ModePreview] No locally-owned menu vessel yet - Test Flight refused.");
                return false;
            }

            _cell = ResolveCell(player);
            if (!_cell)
            {
                CSDebug.LogWarning("[ModePreview] No active Cell in the menu - nothing to swap, " +
                                   "Test Flight refused.");
                return false;
            }

            if (_cell.IsSwappingConfig)
            {
                CSDebug.LogWarning("[ModePreview] The cell is already swapping worlds - Test Flight refused.");
                return false;
            }

            _request.Definition = definition;
            ActiveMode = definition.Mode;
            _state = State.Entering;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            EnterAsync(_cts.Token).Forget();
            return true;
        }

        async UniTaskVoid EnterAsync(CancellationToken ct)
        {
            var definition = _request.Definition;
            var player = gameData.LocalPlayer;

            _restoreCellConfig = _cell.Config;
            _restoreVesselClass = player.Vessel.VesselStatus.VesselType;

            CSDebug.Log($"[ModePreview] Test Flight → {definition.Mode} " +
                        $"(world: {definition.PreviewCell.CellName}, " +
                        $"restoring: {(_restoreCellConfig ? _restoreCellConfig.CellName : "none")}).");

            try
            {
                // 1. Take control. This fades the arcade chrome out, blends the camera onto the
                //    vessel, un-pauses input and - through ScreenSwitcher - stops the pad from
                //    double-driving the UI. Skipped when the player is somehow already flying.
                if (!freestyleHandler.IsInFreestyle)
                {
                    freestyleHandler.ToggleTransition();
                    if (!await WaitWhile(() => freestyleHandler && !freestyleHandler.IsInFreestyle, ct))
                        throw new TimeoutException("the menu never handed control to the vessel");
                }

                // 2. The mode's own hull. A vessel-locked mode declares it; anything else keeps
                //    what the player is already flying.
                var targetVessel = ResolveVessel(definition);
                if (vesselInitializer && targetVessel != VesselClassType.Any &&
                    targetVessel != VesselClassType.Random &&
                    targetVessel != _restoreVesselClass)
                {
                    vesselInitializer.RequestSwap(targetVessel);
                    if (!await WaitWhile(() => vesselInitializer && vesselInitializer.IsSwapping, ct))
                        throw new TimeoutException($"the swap to {targetVessel} never finished");
                }

                // 3. The mode's own world. The cell suctions the menu world away and blooms the
                //    mode's in behind the standard veil - continuity of existence at both ends.
                if (!_cell.RequestCellSwap(definition.PreviewCell))
                    throw new InvalidOperationException("Cell refused the preview world swap.");

                if (!await WaitWhile(() => _cell && _cell.IsSwappingConfig, ct))
                    throw new TimeoutException($"{definition.PreviewCell.CellName} never finished building");

                if (settleSeconds > 0f)
                    await UniTask.Delay((int)(settleSeconds * 1000f),
                        ignoreTimeScale: true, cancellationToken: ct);

                // 4. Open on the framing the real mode opens on.
                PlaceVesselForPreview(definition);

                // 5. Gameplay-bearing structure the CELL does not own (hoops, goals, a track).
                //    Local prop only - it must never carry a NetworkObject.
                SpawnStructure(definition);

                // 6. Start counting.
                StartRunner(definition);

                _state = State.Flying;
            }
            catch (OperationCanceledException)
            {
                // Destroyed or superseded - teardown owns the unwind.
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[ModePreview] Test Flight into {definition.Mode} failed: {e.Message}. " +
                                 "Unwinding back to the menu world.");
                _state = State.Flying;      // so End() has something to unwind
                End(ModePreviewOutcome.Abandoned);
            }
        }

        // ── Exit ─────────────────────────────────────────────────────────────

        /// <summary>Leave the preview. Safe to call from a UI button and safe to call twice.</summary>
        public void RequestExit() => End(ModePreviewOutcome.Abandoned);

        /// <summary>
        /// The ONE way out. Idempotent, and a no-op while idle or already unwinding, so the exit
        /// it triggers (dropping freestyle) can never feed back in as a second exit.
        /// </summary>
        public void End(ModePreviewOutcome outcome)
        {
            if (_state is State.Idle or State.Exiting) return;

            var mode = ActiveMode;
            _state = State.Exiting;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            StopRunner();
            DespawnStructure();

            if (hud) hud.Hide();

            ExitAsync(mode, outcome).Forget();
        }

        async UniTaskVoid ExitAsync(GameModes mode, ModePreviewOutcome outcome)
        {
            var ct = this.GetCancellationTokenOnDestroy();

            try
            {
                // Let a completed objective read for a beat before the world goes.
                if (outcome == ModePreviewOutcome.Completed)
                    await UniTask.Delay(1250, ignoreTimeScale: true, cancellationToken: ct);

                // Put the menu world back. Same door, same suction-and-bloom.
                if (_cell && _restoreCellConfig && !_cell.IsSwappingConfig)
                {
                    _cell.RequestCellSwap(_restoreCellConfig);
                    await WaitWhile(() => _cell && _cell.IsSwappingConfig, ct);
                }

                // Give the hull back. The menu vessel the player chose is theirs, not the mode's.
                var player = gameData != null ? gameData.LocalPlayer : null;
                if (vesselInitializer && player?.Vessel != null &&
                    _restoreVesselClass != VesselClassType.Any &&
                    _restoreVesselClass != player.Vessel.VesselStatus.VesselType)
                {
                    vesselInitializer.RequestSwap(_restoreVesselClass);
                    await WaitWhile(() => vesselInitializer && vesselInitializer.IsSwapping, ct);
                }

                // Hand the menu back its chrome, camera and input, and WAIT for the blend to
                // land: IsInFreestyle only goes false at the end of the menu transition, so
                // this is also what lets OnPreviewEnded fire against a settled menu instead of
                // reopening a modal over a half-faded screen. Already false means the player
                // dropped freestyle themselves and that transition has already finished.
                if (freestyleHandler && freestyleHandler.IsInFreestyle)
                {
                    freestyleHandler.ToggleTransition();
                    await WaitWhile(() => freestyleHandler && freestyleHandler.IsInFreestyle, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Scene teardown - nothing left to restore.
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[ModePreview] Unwinding the {mode} Test Flight hit: {e.Message}.");
            }
            finally
            {
                ResetSessionState();
                OnPreviewEnded?.Invoke(mode, outcome);
            }
        }

        /// <summary>
        /// Drop everything with no unwind. For teardown only (scene change, destroy) - the world
        /// is going away on its own and racing it would be worse than leaving it.
        /// </summary>
        void AbortHard()
        {
            if (_state == State.Idle) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            StopRunner();
            DespawnStructure();
            if (hud) hud.Hide();
            ResetSessionState();
        }

        void ResetSessionState()
        {
            _state = State.Idle;
            ActiveMode = GameModes.Random;
            _request.Definition = null;
            _restoreCellConfig = null;
            _restoreVesselClass = VesselClassType.Any;
            _cell = null;
        }

        void HandleLaunchRequested() => AbortHard();

        // ── Pieces ───────────────────────────────────────────────────────────

        void StartRunner(ModePreviewDefinitionSO definition)
        {
            if (!_runner) _runner = gameObject.AddComponent<ModePreviewRunner>();

            var player = gameData.LocalPlayer;
            _runner.Begin(player?.RoundStats, definition, HandleRunnerFinished);

            if (hud) hud.Show(_runner, definition, this);
        }

        void StopRunner()
        {
            if (_runner) _runner.Stop();
        }

        void HandleRunnerFinished(ModePreviewOutcome outcome) => End(outcome);

        void PlaceVesselForPreview(ModePreviewDefinitionSO definition)
        {
            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel == null || !_cell) return;

            // The nucleus radius is the same anchor ServerPlayerVesselInitializer uses, so a
            // preview opens on the framing the real mode opens on. A cell with no nucleus
            // reports 0, which the offset alone still turns into a sane standoff.
            float radius = _cell.ExpectedNucleusWorldRadius + Mathf.Max(0f, definition.SpawnDistanceOutsideNucleus);
            var pose = CellSpawnFormation.Build(1, _cell.transform.position, radius)[0];

            vessel.SetPose(pose);
        }

        void SpawnStructure(ModePreviewDefinitionSO definition)
        {
            DespawnStructure();
            if (!definition.StructurePrefab || !_cell) return;

            if (definition.StructurePrefab.GetComponentInChildren<Unity.Netcode.NetworkObject>(true))
            {
                CSDebug.LogError($"[ModePreview] '{definition.StructurePrefab.name}' carries a " +
                                 "NetworkObject. A preview is strictly local - Menu_Main hosts the " +
                                 "party, so spawning it would land the preview's structure on every " +
                                 "party member. Skipped.");
                return;
            }

            _structure = Instantiate(definition.StructurePrefab, _cell.transform.position,
                                     Quaternion.identity);
            _structure.name = $"ModePreviewStructure ({definition.Mode})";
        }

        void DespawnStructure()
        {
            if (!_structure) return;
            Destroy(_structure);
            _structure = null;
        }

        Cell ResolveCell(IPlayer player)
        {
            var origin = player.Vessel.VesselStatus != null
                ? player.Vessel.VesselStatus.Transform.position
                : transform.position;

            // Unity's lifetime-aware operator, not ??: a destroyed Cell is non-null by reference.
            var containing = Cell.FindCellContaining(origin);
            return containing ? containing : Cell.FindNearestActiveCell(origin);
        }

        /// <summary>
        /// The hull the preview flies. The definition wins when it names one; otherwise
        /// <see cref="VesselClassType.Any"/> means "ask the mode", which is the vessel list a
        /// vessel-locked mode already declares on its <c>SO_ArcadeGame</c>.
        /// </summary>
        VesselClassType ResolveVessel(ModePreviewDefinitionSO definition)
        {
            if (definition.Vessel != VesselClassType.Any) return definition.Vessel;
            return _request.ModeVessel;
        }

        /// <summary>
        /// Tell the session which hull the mode itself locks to, before <see cref="TryBegin"/>.
        /// The arcade modal already holds the mode's <c>SO_ArcadeGame</c>, so it passes the
        /// answer down rather than making this class reach back into the UI for it.
        /// </summary>
        public void SetModeVessel(VesselClassType vessel) => _request.ModeVessel = vessel;


        /// <summary>
        /// Poll <paramref name="condition"/> until it is false or <see cref="swapTimeoutSeconds"/>
        /// elapses; returns false on timeout. Written out rather than composed from a timeout
        /// combinator because a wedged swap is an ordinary outcome here, not an exceptional one -
        /// the answer the caller needs is "did it finish", and a preview that cannot finish must
        /// unwind rather than strand the player in a menu with no chrome. Unscaled, because the
        /// menu is free to touch timeScale around it.
        /// </summary>
        async UniTask<bool> WaitWhile(Func<bool> condition, CancellationToken ct)
        {
            float elapsed = 0f;
            while (condition())
            {
                if (elapsed >= swapTimeoutSeconds) return false;
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            return true;
        }

        sealed class ModePreviewRequest
        {
            public ModePreviewDefinitionSO Definition;
            public VesselClassType ModeVessel = VesselClassType.Any;
        }
    }
}
