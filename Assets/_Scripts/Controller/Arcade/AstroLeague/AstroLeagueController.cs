using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Astro League match director - hypersea soccer on the multiplayer domain-games stack.
    /// Two domains (Jade defends -Z, Ruby defends +Z) slam a server-simulated billiard ball
    /// through the opposing goal portal. Runs the full match loop on top of the shared flow:
    ///
    ///   Ready → shared 3-2-1 countdown (first kickoff count-in) → live play →
    ///   GOAL! celebration → kickoff count-in → ... → full time →
    ///   golden-goal overtime if tied → winner banner → SyncFinalScores → shared scoreboard.
    ///
    /// Server-authoritative throughout: goal attribution (last non-defending striker),
    /// per-player GoalsScored (NetworkVariable on RoundStats), match phase, and the clock all
    /// live on the server; peers receive announcer beats via ClientRpc and domain score sums
    /// via the base controller's domain-sum NetworkVariables. AI strikers steer through
    /// AIPilot.SetExternalTargetProvider with billiard thinking (approach the ball from the
    /// own-goal side so contact drives it goalward).
    /// </summary>
    public class AstroLeagueController : MultiplayerDomainGamesController
    {
        [Header("Astro League")]
        [SerializeField] AstroLeagueSettingsSO settings;
        [Tooltip("Drag AstroLeagueScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
        [SerializeField] ScoringRuleSO rule;
        [SerializeField] AstroLeagueBall ball;
        [SerializeField] AstroLeagueArena arena;
        [Tooltip("The standard Cell whose nucleus is scaled to become the spherical play boundary.")]
        [SerializeField] Cell cell;
        [SerializeField] AstroLeagueMatchMonitor matchMonitor;
        [Tooltip("One goal per active domain. Element order must match GameDataSO.ActiveDomains (Jade, Ruby).")]
        [SerializeField] List<AstroLeagueGoal> goals;
        [Tooltip("Kickoff line anchors, same order as goals: each team parks between its goal and center.")]
        [SerializeField] List<Transform> teamSpawns;

        [Inject] AudioSystem audioSystem;
        [Inject] CameraManager cameraManager;

        // Per-peer goal replay (records the replicated ball flight; plays a ghost on the replay
        // camera while the arena resets). Added at runtime in OnNetworkSpawn - no scene wiring.
        AstroLeagueGoalReplay goalReplay;

        // Attribution identity for the goal-reset prism sweep (matches the ball's attacker name).
        const string FieldResetAttacker = "Astro League";

        // Reusable buffer for the goal-reset prism sweep (per-peer local prism copies).
        static readonly List<Prism> s_fieldClearBuffer = new(512);

        // Intensity scaling: base (authored) positions captured at spawn, multiplied by the scale.
        readonly List<Vector3> _baseGoalLocalPos = new();
        readonly List<Vector3> _baseSpawnLocalPos = new();
        float _currentScale = 1f;

        // The runtime-generated nucleus cage mesh (polytope/cylinder courts) - owned here and destroyed
        // on despawn so it doesn't leak across matches/replays (the scene-reload replay re-generates it).
        Mesh _generatedNucleusMesh;

        // Match config (intensity scale, court shape, goal target) replicated via NetworkVariables rather
        // than a one-shot ClientRpc, so a client that spawns AFTER the server set them still builds the
        // arena (a one-shot RPC missed late joiners - the "not all players see the arena" bug). Every
        // peer reads the current value at spawn AND subscribes to changes, then morphs its own arena.
        readonly NetworkVariable<float> n_IntensityScale =
            new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_BoundaryShape =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_GoalTarget =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> n_CentralGoal =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Last config actually applied, so a re-read + an OnValueChanged don't rebuild twice.
        bool _matchConfigApplied;
        float _appliedScale = -1f;
        AstroLeagueBoundaryShape _appliedShape;
        bool _appliedCentralGoal;

        enum MatchPhase { PreMatch, Kickoff, Live, Celebration, Overtime, Finished }
        MatchPhase phase = MatchPhase.PreMatch;

        bool _finalResultsSent;
        Domains _matchWinner = Domains.Blue;
        CancellationTokenSource matchCts;

        // Last vessels to strike the ball, most recent first (server only).
        // Capacity 2 is enough to resolve own-goal attribution.
        readonly List<IPlayer> _lastStrikers = new(2);

        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom → SyncFinalScores_ClientRpc (SkimRace/Joust/
        // Scurry pattern); suppress the base turn→round→game flow so we don't get a
        // duplicate InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            phase = MatchPhase.PreMatch;
            matchCts = new CancellationTokenSource();

            CaptureBaseLayout();

            // Every peer records + replays locally (the ball trajectory is already replicated).
            if (goalReplay == null) goalReplay = gameObject.AddComponent<AstroLeagueGoalReplay>();
            goalReplay.Configure(ball, settings, cameraManager);

            if (IsServer)
            {
                // Compute scale + court shape from the (server-authoritative) selected intensity and
                // publish them; they replicate to every current AND future peer. Set BEFORE subscribing
                // so the host doesn't fire OnValueChanged per-field and rebuild the arena mid-config.
                n_GoalTarget.Value = settings.goalLimit;
                n_IntensityScale.Value = ScaleForIntensity();
                n_BoundaryShape.Value = (int)ShapeForIntensity();
                n_CentralGoal.Value = CentralGoalForIntensity();

                ball.OnStruckServer += HandleBallStruckServer;
                matchMonitor.OnClockExpired += HandleClockExpiredServer;
            }

            // Every peer (host + clients + late joiners) tracks the replicated match config and morphs
            // its own arena. Subscribe to later deltas (covers a same-tick client whose value arrives
            // after spawn), then apply whatever is already replicated (covers a late joiner, whose
            // OnValueChanged won't fire for the initial value it received with the spawn).
            n_IntensityScale.OnValueChanged += OnMatchConfigChanged;
            n_BoundaryShape.OnValueChanged += OnMatchConfigChanged;
            n_CentralGoal.OnValueChanged += OnCentralGoalChanged;
            n_GoalTarget.OnValueChanged += OnGoalTargetChanged;

            gameData.GoalTargetCount = n_GoalTarget.Value;
            ApplyMatchConfig();
        }

        void OnMatchConfigChanged(float _, float __) => ApplyMatchConfig();
        void OnMatchConfigChanged(int _, int __) => ApplyMatchConfig();
        void OnCentralGoalChanged(bool _, bool __) => ApplyMatchConfig();
        void OnGoalTargetChanged(int _, int v) => gameData.GoalTargetCount = v;

        /// <summary>
        /// Build the arena + morph the nucleus from the replicated match config, skipping a redundant
        /// rebuild if the (scale, shape, central-goal) hasn't changed since the last apply.
        /// </summary>
        void ApplyMatchConfig()
        {
            float scale = n_IntensityScale.Value;
            var shape = (AstroLeagueBoundaryShape)n_BoundaryShape.Value;
            bool centralGoal = n_CentralGoal.Value;
            if (_matchConfigApplied && Mathf.Approximately(scale, _appliedScale)
                && shape == _appliedShape && centralGoal == _appliedCentralGoal)
                return;

            _matchConfigApplied = true;
            _appliedScale = scale;
            _appliedShape = shape;
            _appliedCentralGoal = centralGoal;
            ApplyIntensityScale(scale, shape, centralGoal);
        }

        /// <summary>Captures the authored (intensity-1) goal + team-spawn local positions once.</summary>
        void CaptureBaseLayout()
        {
            if (_baseGoalLocalPos.Count == 0 && goals != null)
                foreach (var g in goals)
                    _baseGoalLocalPos.Add(g != null ? g.transform.localPosition : Vector3.zero);

            if (_baseSpawnLocalPos.Count == 0 && teamSpawns != null)
                foreach (var t in teamSpawns)
                    _baseSpawnLocalPos.Add(t != null ? t.localPosition : Vector3.zero);
        }

        /// <summary>
        /// Arena/ball/layout scale for the selected intensity - the per-intensity table on the
        /// settings asset (default doubles the intensity-1 court to 2x), with the legacy even
        /// ramp as its fallback.
        /// </summary>
        float ScaleForIntensity()
        {
            int intensity = gameData.SelectedIntensity != null
                ? Mathf.Max(1, gameData.SelectedIntensity.Value)
                : 1;
            return settings.ScaleForIntensity(intensity);
        }

        /// <summary>Court geometry for the selected intensity - one ricochet test shape per level.</summary>
        AstroLeagueBoundaryShape ShapeForIntensity()
        {
            int intensity = gameData.SelectedIntensity != null
                ? Mathf.Max(1, gameData.SelectedIntensity.Value)
                : 1;
            return settings.ShapeForIntensity(intensity);
        }

        /// <summary>Whether the selected intensity uses the central shared-goal layout.</summary>
        bool CentralGoalForIntensity()
        {
            int intensity = gameData.SelectedIntensity != null
                ? Mathf.Max(1, gameData.SelectedIntensity.Value)
                : 1;
            return settings.CentralGoalForIntensity(intensity);
        }

        public override void OnNetworkDespawn()
        {
            n_IntensityScale.OnValueChanged -= OnMatchConfigChanged;
            n_BoundaryShape.OnValueChanged -= OnMatchConfigChanged;
            n_CentralGoal.OnValueChanged -= OnCentralGoalChanged;
            n_GoalTarget.OnValueChanged -= OnGoalTargetChanged;

            if (IsServer)
            {
                ball.OnStruckServer -= HandleBallStruckServer;
                matchMonitor.OnClockExpired -= HandleClockExpiredServer;
            }

            matchCts?.Cancel();
            matchCts?.Dispose();
            matchCts = null;

            if (_generatedNucleusMesh != null)
            {
                Destroy(_generatedNucleusMesh);
                _generatedNucleusMesh = null;
            }

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Scale the whole playfield to the match intensity and apply the court shape: rebuild the
        /// arena boundary, resize the ball, push the goals + team spawns out to the scaled goal lines,
        /// and morph the cell nucleus to the boundary geometry. Players reset to the scaled team
        /// positions on every kickoff.
        /// </summary>
        void ApplyIntensityScale(float scale, AstroLeagueBoundaryShape shape, bool centralGoal)
        {
            _currentScale = Mathf.Max(1f, scale);
            CaptureBaseLayout();

            if (arena != null) arena.Build(_currentScale, shape, centralGoal);
            if (ball != null) ball.SetSizeScale(_currentScale);

            // The play boundary IS the cell nucleus: morph the visible nucleus to coincide with the
            // surface the ball bounces off, so the wall you see is the wall the ball hits. A polytope
            // gets the generated convex-hull mesh (keeping the nucleus CageMaterial); the legacy sphere
            // keeps its prefab mesh and is just resized. Both setters are race-proof (cache until the
            // nucleus spawns).
            if (cell != null && arena != null && arena.Boundary != null)
            {
                // EVERY court shape gets the generated cage mesh, including the Sphere. It used to be
                // the exception - resized via SetNucleusWorldRadius, keeping the prefab's plain sphere -
                // which is why the faceted glowing arena cover the other intensities wear was missing
                // on the sphere court. The cover IS this mesh rendered through the nucleus'
                // CageMaterial. Only a degenerate shape that yields no mesh falls back to a radius.
                Mesh cage = arena.Boundary.BuildVisualMesh();
                if (cage != null)
                {
                    if (_generatedNucleusMesh != null) Destroy(_generatedNucleusMesh); // free a prior rebuild
                    _generatedNucleusMesh = cage;
                    cell.SetNucleusMesh(cage);
                }
                else
                {
                    cell.SetNucleusWorldRadius(shape == AstroLeagueBoundaryShape.Sphere
                        ? arena.BoundaryRadius
                        : arena.Boundary.MaxExtent);
                }
            }

            // The nucleus here is a WALL, not a claim. Astro League has no node-control scoring - it
            // borrowed the nucleus mesh to be its ricochet court - and leaving the control zone on
            // made the court's circumscribing radius a fauna SANCTUARY: every prism in the match
            // read as "inside the nucleus", so Cell.IsPreyForHerbivore refused to feed anything on
            // the pitch and the trail-grazing food web could not remove a single prism. Declared
            // AFTER the nucleus is morphed, because the setter re-measures. See Cell.NucleusIsControlZone.
            if (cell != null) cell.NucleusIsControlZone = false;

            Vector3 arenaCenter = arena != null ? arena.Center : transform.position;

            // Ball spawn: only the central shared goal overrides it - off-center in the goal's plane
            // (along X) so the ball doesn't start sitting inside the central goal. Non-central modes keep
            // the ball's authored center spawn.
            if (ball != null && centralGoal)
                ball.SetSpawnPosition(arenaCenter + Vector3.right * (settings.centralBallSpawnOffset * _currentScale));

            // Goal lines and kickoff lines are DERIVED from the court dimensions, not from authored
            // scene positions: the goals belong on the flat caps at ±length/2 by construction, so
            // reading them from the same asset that sizes the court is what lets the playfield be
            // resized with one number and no scene edit. (_baseGoalLocalPos / _baseSpawnLocalPos
            // remain the fallback for a scene with no settings asset wired.)
            float halfLength = arena != null ? arena.ArenaLength * 0.5f : settings.arenaLength * _currentScale * 0.5f;
            float kickoffLine = settings.kickoffLineDistance * _currentScale;

            if (goals != null)
                for (int i = 0; i < goals.Count; i++)
                {
                    if (goals[i] == null) continue;
                    if (centralGoal)
                    {
                        // ONE shared goal: both detectors at the arena center, facing OPPOSITE ways
                        // (±Z, flipped 180° from the end goals) so the PASS DIRECTION decides which
                        // domain scores. goals[0] (Jade) detects +Z passes → credits Ruby; goals[1]
                        // (Ruby) detects -Z passes → credits Jade (own-goal rules still apply). The
                        // arena draws the matching Ruby(+Z)/Jade(-Z) cones.
                        goals[i].transform.position = arenaCenter;
                        Vector3 normal = i == 0 ? Vector3.forward : Vector3.back;
                        goals[i].Configure(ball, arenaCenter, _currentScale, normal, passThrough: true,
                            baseMouthRadius: settings.goalMouthRadius);
                    }
                    else
                    {
                        // Index order matches GameDataSO.ActiveDomains: goals[0] = Jade, defending -Z.
                        float sign = SignForDomainIndex(i);
                        goals[i].transform.position = arenaCenter + Vector3.forward * (sign * halfLength);
                        // Wire the ball + arena center + scale AFTER repositioning so the goal's inward
                        // normal and mouth radius are computed from its final scaled world position.
                        goals[i].Configure(ball, arenaCenter, _currentScale,
                            baseMouthRadius: settings.goalMouthRadius);
                    }
                }

            if (teamSpawns != null)
                for (int i = 0; i < teamSpawns.Count; i++)
                {
                    if (teamSpawns[i] == null) continue;
                    teamSpawns[i].position = arenaCenter + Vector3.forward * (SignForDomainIndex(i) * kickoffLine);
                }
        }

        // ── Fauna: the cleanup crew waits outside until the pitch silts up ───

        // Live exclusion radius (the pen's INNER wall) and where it is heading. Swept rather than
        // snapped so a swarm visibly pours over the wall instead of teleporting through it.
        float _faunaExclusion;
        float _faunaExclusionTarget;
        bool _faunaExclusionSeeded;

        /// <summary>
        /// Per-peer, every frame: hold the cell's fauna outside the court while the pitch is clear
        /// and open the wall once it silts up.
        ///
        /// The "crowded" signal is the cell's OWN volume phase ladder - Calm means the accumulated
        /// trail mass is still under RestlessEnterVolume, Restless and above means it is not. No new
        /// signal is invented for the mode: volume is the spine, and this reads it. The mechanism is
        /// <see cref="Cell.FaunaExclusionRadius"/>, a spatial diet + steering rule (never a wall,
        /// nothing teleported, nothing culled - Docs/ECOSYSTEM.md §22.2b).
        ///
        /// Runs on EVERY peer, not just the server, because fauna and trail prisms are per-peer
        /// local objects (each peer runs its own cell over its own copies) - exactly like the
        /// goal-reset prism sweep. No RPC, no sync.
        /// </summary>
        void UpdateFaunaExclusion()
        {
            if (cell == null || settings == null) return;

            if (!settings.faunaWaitOutsideCourt)
            {
                if (_faunaExclusionSeeded) { cell.FaunaExclusionRadius = 0f; _faunaExclusionSeeded = false; }
                return;
            }

            // Wait for the court before seeding: the exclusion is measured off the boundary, and
            // seeding against a placeholder would sweep the wall outward across the first second.
            // Nothing has hatched yet either way (InitialFaunaSpawnWaitTime).
            if (arena == null || arena.Boundary == null) return;

            float closed = arena.Boundary.MaxExtent * Mathf.Max(0f, settings.faunaExclusionCourtFraction);

            // Calm = the pitch is still clear, keep them out. Restless/Frenzy = it is filling up,
            // let them in. The ladder's own hysteresis (Enter/Exit volumes) debounces the edge for
            // free, so the wall does not flutter around the threshold.
            _faunaExclusionTarget = cell.Phase == CellPhase.Calm ? closed : 0f;

            if (!_faunaExclusionSeeded)
            {
                _faunaExclusionSeeded = true;
                _faunaExclusion = _faunaExclusionTarget;
            }
            else
            {
                float sweep = settings.faunaExclusionSweepSeconds > 0f
                    ? closed / settings.faunaExclusionSweepSeconds * Time.deltaTime
                    : Mathf.Infinity;
                _faunaExclusion = Mathf.MoveTowards(_faunaExclusion, _faunaExclusionTarget, sweep);
            }

            cell.FaunaExclusionRadius = _faunaExclusion;
        }

        void Update() => UpdateFaunaExclusion();

        /// <summary>
        /// Which end of the goal axis a domain index owns. Derived from the AUTHORED scene layout
        /// (goal 0 sits at -Z in the shipping scene) so re-deriving positions can never mirror the
        /// pitch relative to the domain colors the arena paints; falls back to "index 0 defends -Z".
        /// </summary>
        float SignForDomainIndex(int index)
        {
            if (index < _baseGoalLocalPos.Count && Mathf.Abs(_baseGoalLocalPos[index].z) > 0.001f)
                return Mathf.Sign(_baseGoalLocalPos[index].z);
            return index == 0 ? -1f : 1f;
        }

        // ── Match start ──────────────────────────────────────────────────────

        protected override void SetupNewTurn()
        {
            // Server-only in the multiplayer flow (InitializeAfterDelay → SetupNewRound).
            phase = MatchPhase.PreMatch;
            matchMonitor.ConfigureDuration(settings.matchDurationSeconds);
            ball.ResetToCenterServer(); // Frozen showpiece at center until the first kickoff
            base.SetupNewTurn();
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            // The shared 3-2-1 canvas countdown doubles as the first kickoff count-in:
            // park everyone on their team's kickoff line, then go live the moment the
            // base activates players and starts the turn (clock starts via TurnMonitor).
            ParkVesselsForKickoff_ClientRpc();

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            phase = MatchPhase.Live;
            _lastStrikers.Clear();
            ball.SetFrozenServer(false);
            ArmStrikers();
            AnnounceKickoffGo_ClientRpc();
        }

        // ── Goal flow (server) ───────────────────────────────────────────────

        void HandleBallStruckServer(IVessel vessel, float intensity)
        {
            var striker = FindPlayerByVessel(vessel);
            if (striker == null) return;

            // Recoil the striker away from the ball so it bounces back a bit - extra anti-clip
            // insurance on top of the ball's own ejection.
            ApplyVesselRecoil(striker, vessel, intensity);

            if (_lastStrikers.Count > 0 && _lastStrikers[0] == striker) return;
            _lastStrikers.Remove(striker);
            _lastStrikers.Insert(0, striker);
            if (_lastStrikers.Count > 2)
                _lastStrikers.RemoveAt(_lastStrikers.Count - 1);
        }

        /// <summary>
        /// Server: broadcast a backward recoil for the striking vessel. Vessels are
        /// owner-authoritative (ClientNetworkTransform), so the impulse must be applied on the
        /// OWNING peer - the ClientRpc resolves the vessel by NetworkObjectId and only the owner
        /// applies <see cref="VesselTransformer.ModifyVelocity"/>.
        /// </summary>
        void ApplyVesselRecoil(IPlayer striker, IVessel vessel, float intensity)
        {
            // Recoil is OFF by default (vesselRecoilSpeed = 0): anti-clip is already guaranteed by the
            // ball's own depenetration, and a persistent ball bouncing back into a vessel re-fires this
            // every cooldown, stacking toward VesselTransformer.velocityModifierMax (100) and throwing
            // the vessel around ("the ball imparts a force that never diminishes"). Skip the RPC entirely
            // when disabled; dial it up only if you want a subtle "bounce off" juice.
            if (settings.vesselRecoilSpeed <= 0f) return;
            if (vessel?.Transform == null || ball == null) return;
            ulong vesselNetId = striker.VesselNetId;
            if (vesselNetId == 0) return;

            Vector3 away = vessel.Transform.position - ball.transform.position;
            if (away.sqrMagnitude < 0.0001f) return;

            float magnitude = settings.vesselRecoilSpeed * (0.4f + 0.6f * Mathf.Clamp01(intensity));
            RecoilVessel_ClientRpc(vesselNetId, away.normalized, magnitude, settings.vesselRecoilDuration);
        }

        [ClientRpc]
        void RecoilVessel_ClientRpc(ulong vesselNetId, Vector3 direction, float magnitude, float duration)
        {
            if (!gameData.TryGetVesselByNetworkObjectId(vesselNetId, out var vessel)) return;
            if (!vessel.IsNetworkOwner) return; // only the owner actually moves the vessel
            vessel.VesselStatus?.VesselTransformer?.ModifyVelocity(direction * magnitude, duration);
        }

        IPlayer FindPlayerByVessel(IVessel vessel)
        {
            foreach (var p in gameData.Players)
                if (p != null && ReferenceEquals(p.Vessel, vessel))
                    return p;
            return null;
        }

        /// <summary>
        /// Server: the ball crossed a goal line. Attribution: the goal credits the most
        /// recent striker NOT on the defending domain - an own-goal hands the point to the
        /// opponent who last touched it. If no opposing vessel has ever touched the ball,
        /// nobody scores and play resets with a kickoff.
        /// </summary>
        public void HandleGoalServer(AstroLeagueGoal goal, AstroLeagueBall scoredBall)
        {
            if (!IsServer || _finalResultsSent) return;
            if (phase != MatchPhase.Live && phase != MatchPhase.Overtime) return;

            var scorer = _lastStrikers.FirstOrDefault(p => p != null && p.Domain != goal.DefendingDomain);

            scoredBall.DetonateServer();

            if (scorer == null)
            {
                CelebrateThenKickoffAsync(Domains.Blue).Forget();
                return;
            }

            scorer.RoundStats.GoalsScored++; // NetworkVariable - replicates to every peer

            AnnounceGoal_ClientRpc(new FixedString64Bytes(scorer.Name), (int)scorer.Domain);

            bool goldenGoal = phase == MatchPhase.Overtime;
            bool mercy = rule.IsObjectiveReached(gameData, out _);

            if (goldenGoal || mercy)
                FinishMatchAsync(rule.ResolveWinner(gameData)).Forget();
            else
                CelebrateThenKickoffAsync(scorer.Domain).Forget();
        }

        async UniTaskVoid CelebrateThenKickoffAsync(Domains scoringDomain)
        {
            if (matchCts == null) return;
            var token = matchCts.Token;
            bool overtimeKickoff = phase == MatchPhase.Overtime;
            phase = MatchPhase.Celebration;
            matchMonitor.SetClockPaused(true);

            Celebrate_ClientRpc((int)scoringDomain);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(settings.celebrationSeconds),
                    ignoreTimeScale: true, cancellationToken: token);

                if (phase == MatchPhase.Finished) return;
                await RunKickoffAsync(token, overtimeKickoff);
            }
            catch (OperationCanceledException) { /* scene teardown mid-celebration */ }
        }

        async UniTask RunKickoffAsync(CancellationToken token, bool returnToOvertime)
        {
            phase = MatchPhase.Kickoff;
            ball.ResetToCenterServer();
            _lastStrikers.Clear();
            ParkVesselsForKickoff_ClientRpc();

            // Hold the ball frozen for the kickoff count-in window, then go (the shared 3-2-1
            // countdown canvas / GO cue is the existing-system feedback; no bespoke banner here).
            await UniTask.Delay(TimeSpan.FromSeconds(settings.kickoffFreezeSeconds),
                ignoreTimeScale: true, cancellationToken: token);

            if (phase == MatchPhase.Finished) return;
            phase = returnToOvertime ? MatchPhase.Overtime : MatchPhase.Live;

            ball.SetFrozenServer(false);
            matchMonitor.SetClockPaused(false);
            AnnounceKickoffGo_ClientRpc();
        }

        // ── Full time / overtime (server) ────────────────────────────────────

        void HandleClockExpiredServer()
        {
            if (phase == MatchPhase.Finished || _finalResultsSent) return;

            if (IsTiedAcrossActiveDomains() && settings.goldenGoalOvertime)
            {
                matchMonitor.EnterOvertime();
                AnnounceOvertime_ClientRpc();
                EnterOvertimeKickoffAsync().Forget();
                return;
            }

            FinishMatchAsync(rule.ResolveWinner(gameData)).Forget();
        }

        async UniTaskVoid EnterOvertimeKickoffAsync()
        {
            if (matchCts == null) return;
            phase = MatchPhase.Overtime;
            try { await RunKickoffAsync(matchCts.Token, returnToOvertime: true); }
            catch (OperationCanceledException) { /* teardown mid-kickoff */ }
        }

        bool IsTiedAcrossActiveDomains()
        {
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            int best = int.MinValue;
            int bestCount = 0;
            for (int i = 0; i < dc; i++)
            {
                int sum = ScoringMetrics.SumByDomain(gameData, rule.Metric, GameDataSO.ActiveDomains[i]);
                if (sum > best) { best = sum; bestCount = 1; }
                else if (sum == best) bestCount++;
            }
            return bestCount > 1;
        }

        async UniTaskVoid FinishMatchAsync(Domains winner)
        {
            if (phase == MatchPhase.Finished) return;
            phase = MatchPhase.Finished;
            _matchWinner = winner;

            matchMonitor.SetClockPaused(true);
            ball.SetFrozenServer(true);

            foreach (var p in gameData.Players)
            {
                if (p != null && p.IsInitializedAsAI)
                    p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
            }

            AnnounceMatchFinished_ClientRpc((int)winner);

            // Let the winner banner land before handing off to the scoreboard flow.
            if (matchCts != null)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(settings.winnerBannerSeconds),
                        ignoreTimeScale: true, cancellationToken: matchCts.Token);
                }
                catch (OperationCanceledException) { return; }
            }

            matchMonitor.ForceEnd(); // → turn end → OnTurnEndedCustom computes + syncs final scores
        }

        // ── Server-authoritative game end (SkimRace/Joust/Scurry pattern) ──

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;

            var winningDomain = _matchWinner != Domains.Blue ? _matchWinner : rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s.Domain == winningDomain)
                .OrderByDescending(s => s.GoalsScored)
                .FirstOrDefault();
            if (winnerRep == null) return;

            rule.AssignScores(gameData, winningDomain, 0f);
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the match just ended.
        /// HasEndGame=false causes ExecuteServerRoundEnd to call SetupNewRound instead of
        /// ExecuteServerGameEnd - this override prevents the Ready button from reappearing.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var goalsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                goalsArray[i] = statsList[i].GoalsScored;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, goalsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] goalsScored,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[AstroLeague] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.GoalsScored = goalsScored[i];
            }

            // Authoritative winner - written to gameData, consumed by EndGameControllers.
            // OnWinnerCalculated (below) is the "results ready" signal.
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Kickoff parking (every peer parks the vessels it owns) ──────────

        /// <summary>
        /// Vessels replicate owner-authoritatively (ClientNetworkTransform), so a kickoff
        /// teleport must run on the owning peer: each client parks its own human vessel,
        /// the server parks every AI vessel. Slot layout is deterministic (sorted by player
        /// name within each domain) so all peers compute identical lines without extra sync.
        /// </summary>
        [ClientRpc]
        void ParkVesselsForKickoff_ClientRpc() => ParkOwnedVesselsForKickoff();

        /// <summary>
        /// Park every vessel this peer owns on its team's kickoff line, speed ZEROED - kickoffs
        /// (and the on-goal arena reset that precedes them) start from rest. Idempotent, so the
        /// goal-reset park and the kickoff re-park can both run in one celebration window.
        /// </summary>
        void ParkOwnedVesselsForKickoff()
        {
            foreach (var player in gameData.Players)
            {
                if (player?.Vessel == null) continue;

                bool ownedHere = player.IsInitializedAsAI
                    ? NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                    : player.IsLocalUser;
                if (!ownedHere) continue;

                player.SetPoseOfVessel(ComputeKickoffPose(player));
                // Zero the speed on the OWNING peer's transformer directly (motion replicates
                // owner-authoritatively, same as RecoilVessel) - IVessel.SetInitialSpeed routes
                // through a ClientRpc, which a client peer cannot send.
                player.Vessel.VesselStatus?.VesselTransformer?.SetInitialSpeed(0f);
            }
        }

        Pose ComputeKickoffPose(IPlayer player)
        {
            int domainIndex = Array.IndexOf(GameDataSO.ActiveDomains, player.Domain);
            domainIndex = Mathf.Clamp(domainIndex, 0, teamSpawns.Count - 1);
            var anchor = teamSpawns[domainIndex];

            // Deterministic slot within the team: sort domain members by name.
            var teammates = gameData.Players
                .Where(p => p != null && p.Domain == player.Domain)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();
            int slot = Mathf.Max(0, teammates.IndexOf(player));

            // Slots fan out laterally: 0, +1, -1, +2, -2, ... - spacing scales with the arena.
            int offsetSteps = (slot + 1) / 2 * (slot % 2 == 0 ? 1 : -1);
            Vector3 lateral = anchor.right * (offsetSteps * settings.kickoffLateralSpacing * _currentScale);

            Vector3 toCenter = (ball.transform.position - anchor.position).normalized;
            var rotation = toCenter.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toCenter, Vector3.up)
                : anchor.rotation;

            return new Pose(anchor.position + lateral, rotation);
        }

        // ── AI strikers (server) ─────────────────────────────────────────────

        void ArmStrikers()
        {
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                pilot.SetExternalTargetProvider(() => ComputeStrikerTarget(captured));
            }
        }

        // Role assignment, recomputed at most once per frame and shared by every AI's provider
        // callback (AIPilot samples its provider once per Update, so N AI on one domain would
        // otherwise each redo the same sort).
        readonly Dictionary<IPlayer, bool> _aiIsDefender = new();
        readonly List<IPlayer> _roleScratch = new();
        int _rolesFrame = -1;

        /// <summary>
        /// Split each domain's AI into ATTACKERS and DEFENDERS by distance to the ball: the closest
        /// go for it, the farthest drop back. Recomputed per frame from live positions, so the roles
        /// swap naturally as play moves instead of being pinned at kickoff.
        ///
        /// This is the single biggest fix to the old AI: every AI chased the ball, so a domain was
        /// never home and any shot that got past the pack was a goal. A domain with one AI always
        /// attacks (a lone defender would simply never shoot).
        /// </summary>
        void RefreshStrikerRoles()
        {
            if (_rolesFrame == Time.frameCount) return;
            _rolesFrame = Time.frameCount;
            _aiIsDefender.Clear();
            if (ball == null) return;

            Vector3 ballPos = ball.transform.position;
            int domainCount = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);

            for (int d = 0; d < domainCount; d++)
            {
                var domain = GameDataSO.ActiveDomains[d];
                _roleScratch.Clear();
                foreach (var p in gameData.Players)
                    if (p != null && p.IsInitializedAsAI && p.Domain == domain && p.Vessel?.Transform != null)
                        _roleScratch.Add(p);
                if (_roleScratch.Count == 0) continue;

                _roleScratch.Sort((a, b) =>
                    (a.Vessel.Transform.position - ballPos).sqrMagnitude
                    .CompareTo((b.Vessel.Transform.position - ballPos).sqrMagnitude));

                int defenders = Mathf.Clamp(
                    Mathf.FloorToInt(_roleScratch.Count * Mathf.Clamp01(settings.strikerDefenderFraction)),
                    0, _roleScratch.Count - 1);

                for (int i = 0; i < _roleScratch.Count; i++)
                    _aiIsDefender[_roleScratch[i]] = i >= _roleScratch.Count - defenders;
            }
        }

        /// <summary>
        /// Billiard thinking with three additions over the original "run at the ball" striker:
        ///
        /// 1. INTERCEPT - it aims where the ball WILL be after its own travel time, so a moving ball
        ///    is met instead of chased from behind forever.
        /// 2. ROLES - the teammates farthest from the ball hold the line between the ball and their
        ///    own goal (see <see cref="RefreshStrikerRoles"/>) rather than joining the pile-on, and
        ///    a defender abandons the post to clear once the ball reaches its own area.
        /// 3. OWN-GOAL REFUSAL - contact drives the ball roughly along the AI's own approach vector,
        ///    so approaching from a heading that points at its OWN net is refused outright and the
        ///    AI peels off to re-approach. The old striker only checked which side of the ball it
        ///    was on, which let it shepherd the ball into its own goal from a wide angle.
        ///
        /// During kickoffs/celebrations, hold near the team's kickoff line.
        /// </summary>
        Vector3 ComputeStrikerTarget(IPlayer aiPlayer)
        {
            if (aiPlayer?.Vessel == null || ball == null)
                return ball != null ? ball.transform.position : Vector3.zero;

            Vector3 ballPos = ball.transform.position;

            if (ball.IsFrozen || (phase != MatchPhase.Live && phase != MatchPhase.Overtime))
            {
                int idx = Mathf.Clamp(Array.IndexOf(GameDataSO.ActiveDomains, aiPlayer.Domain), 0, teamSpawns.Count - 1);
                return teamSpawns[idx].position;
            }

            var targetGoal = GoalAttackedBy(aiPlayer.Domain);
            if (targetGoal == null) return ballPos;

            // The central shared-goal layout puts BOTH detectors at the arena centre, so "our end"
            // does not exist as a place: there is nothing to fall back and defend, and an own goal
            // is a matter of pass DIRECTION (already handled by aiming past centre), not of
            // approach angle. Roles and the own-goal guard are end-goal concepts only.
            var ownGoal = _appliedCentralGoal ? null : GoalDefendedBy(aiPlayer.Domain);

            Vector3 aiPos = aiPlayer.Vessel.Transform.position;

            // ── 1. Intercept: extrapolate the ball over this AI's own travel time ──
            Vector3 predicted = ballPos;
            if (settings.strikerClosingSpeedEstimate > 0.01f)
            {
                float lead = Mathf.Min(
                    Vector3.Distance(aiPos, ballPos) / settings.strikerClosingSpeedEstimate,
                    Mathf.Max(0f, settings.strikerMaxLeadSeconds));
                predicted = ballPos + ball.Velocity * lead;
            }

            // ── 2. Defenders hold the line, unless the ball is already on top of them ──
            RefreshStrikerRoles();
            bool defending = _aiIsDefender.TryGetValue(aiPlayer, out bool isDef) && isDef;
            if (defending && ownGoal != null)
            {
                float clearRange = settings.defenderClearDistance * _currentScale;
                if (Vector3.Distance(predicted, ownGoal.MouthCenter) > clearRange)
                    return Vector3.Lerp(ownGoal.MouthCenter, predicted,
                        Mathf.Clamp01(settings.defenderStandoffFraction));
                // Ball is in our area - stop holding position and go clear it.
            }

            // ── 3. Attack line ──
            // End goals: aim at the goal mouth. Central shared goal: aim PAST the center along the
            // scoring direction (the goal's inward normal) so contact drives the ball THROUGH the
            // central disk in the team's scoring Z direction - aiming straight at center would own-goal
            // from the wrong side.
            Vector3 aimPoint = _appliedCentralGoal
                ? targetGoal.MouthCenter + targetGoal.InwardNormal * (300f * _currentScale)
                : targetGoal.MouthCenter;
            Vector3 shotDir = (aimPoint - predicted).normalized;
            float approachLead = settings.strikerApproachLead * _currentScale;
            float recover = settings.strikerRecoverDistance * _currentScale;

            bool onAttackSide = Vector3.Dot(predicted - aiPos, shotDir) > 0f;

            // Own-goal refusal: the ball leaves roughly along the direction this AI is travelling
            // through it, so if that direction points at our own net, do NOT make contact.
            bool wouldOwnGoal = false;
            if (ownGoal != null)
            {
                Vector3 driveDir = predicted - aiPos;
                Vector3 toOwnGoal = ownGoal.MouthCenter - predicted;
                if (driveDir.sqrMagnitude > 1e-4f && toOwnGoal.sqrMagnitude > 1e-4f)
                    wouldOwnGoal = Vector3.Angle(driveDir, toOwnGoal) < settings.strikerOwnGoalGuardDegrees;
            }

            if (onAttackSide && !wouldOwnGoal)
                return predicted - shotDir * approachLead;

            // Wrong side of the ball, or lined up on our own net: swing wide around it and come
            // back onto the shot line. The lateral axis is taken off the shot direction, so the
            // detour is always around the ball rather than through it.
            Vector3 side = Vector3.Cross(shotDir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(shotDir, Vector3.forward);
            side.Normalize();
            float sideSign = Mathf.Sign(Vector3.Dot(aiPos - predicted, side));
            if (sideSign == 0f) sideSign = 1f;
            return predicted - shotDir * recover + side * (sideSign * recover * 0.6f);
        }

        AstroLeagueGoal GoalAttackedBy(Domains attackingDomain)
        {
            foreach (var goal in goals)
                if (goal != null && goal.DefendingDomain != attackingDomain)
                    return goal;
            return null;
        }

        AstroLeagueGoal GoalDefendedBy(Domains defendingDomain)
        {
            foreach (var goal in goals)
                if (goal != null && goal.DefendingDomain == defendingDomain)
                    return goal;
            return null;
        }

        // ── Announcer ClientRpcs (play the shared AudioSystem cue on every peer) ──

        [ClientRpc]
        void AnnounceKickoffGo_ClientRpc()
        {
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.SpeedBurst);

            // The replay must never bleed into live play, and a fresh rally must never replay a
            // pre-reset flight: stop the ghost (restores the gameplay camera) and forget the recording.
            if (goalReplay != null)
            {
                goalReplay.Stop();
                goalReplay.ClearRecording();
            }
        }

        [ClientRpc]
        void AnnounceGoal_ClientRpc(FixedString64Bytes scorerName, int scoringDomain) =>
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.ScoreReveal);

        [ClientRpc]
        void Celebrate_ClientRpc(int scoringDomain)
        {
            // Slow-mo beat is solo-session-only: a local timescale change with connected
            // peers desyncs their view of owner-authoritative vessels (see MenuCrystalClickHandler).
            var nm = NetworkManager.Singleton;
            bool solo = nm == null || !nm.IsListening || nm.ConnectedClientsIds.Count <= 1;
            if (solo)
                RunCelebrationSlowMoAsync().Forget();

            // On-goal arena reset, on every peer, behind the goal replay: park the vessels this
            // peer owns with speed zeroed and sweep the field prisms clean (per-peer local copies).
            // The kickoff re-park before GO is idempotent on top of this.
            float resetWindow = settings.celebrationSeconds + settings.kickoffFreezeSeconds;
            if (settings.goalResetsArena)
            {
                ParkOwnedVesselsForKickoff();
                if (matchCts != null)
                    ClearFieldPrismsAsync(matchCts.Token).Forget();
            }

            // The goal replay fills the reset window on the replay camera; it restores the
            // gameplay camera when playback ends (or at kickoff GO, whichever lands first).
            if (settings.goalReplayEnabled && goalReplay != null)
                goalReplay.Play(resetWindow, _currentScale);
        }

        /// <summary>
        /// Local, per-peer: sweep the accumulated FIELD prisms off the pitch with the canonical
        /// animated Damage path - never a raw Destroy (continuity law; the per-frame VFX cap in
        /// PrismEffectsManager paces the burst). Staggered center-out over goalPrismClearSeconds
        /// so the field visibly washes clean behind the goal replay. Skips invulnerable
        /// super-shielded structure (the arena edge lining) and fauna body prisms - the food web
        /// is not part of the pitch reset (no imposed death; fauna die by starvation/predation
        /// only). Prisms are per-peer GameObjects, so each peer clears its own copies - no RPC.
        /// </summary>
        async UniTaskVoid ClearFieldPrismsAsync(CancellationToken token)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || arena == null) return;

            float radius = (arena.Boundary != null ? arena.Boundary.MaxExtent : 400f * _currentScale) * 1.25f;
            Vector3 center = arena.Center;
            index.QuerySphere(center, radius, s_fieldClearBuffer);
            if (s_fieldClearBuffer.Count == 0) return;

            // Center-out wave: the clear reads as washing outward from the pitch center.
            s_fieldClearBuffer.Sort((a, b) =>
            {
                float da = a != null ? (a.transform.position - center).sqrMagnitude : float.MaxValue;
                float db = b != null ? (b.transform.position - center).sqrMagnitude : float.MaxValue;
                return da.CompareTo(db);
            });

            int total = s_fieldClearBuffer.Count;
            float duration = Mathf.Max(0.1f, settings.goalPrismClearSeconds);
            float start = Time.unscaledTime;
            int cleared = 0;

            try
            {
                while (cleared < total)
                {
                    token.ThrowIfCancellationRequested();

                    float progress = Mathf.Clamp01((Time.unscaledTime - start) / duration);
                    int target = Mathf.CeilToInt(progress * total);
                    for (; cleared < target; cleared++)
                    {
                        var prism = s_fieldClearBuffer[cleared];
                        if (prism == null || prism.destroyed) continue;
                        if (prism.prismProperties != null && prism.prismProperties.IsSuperShielded) continue;
                        if (prism is HealthPrism body && body.ResolveOwnerFauna() != null) continue;
                        prism.Damage(Vector3.zero, Domains.Blue, FieldResetAttacker, devastate: true);
                    }

                    if (progress >= 1f) break;
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
            catch (OperationCanceledException) { /* scene teardown mid-sweep */ }
            finally
            {
                s_fieldClearBuffer.Clear();
            }
        }

        async UniTaskVoid RunCelebrationSlowMoAsync()
        {
            if (matchCts == null) return;
            float baseFixedDelta = Time.fixedDeltaTime / Mathf.Max(Time.timeScale, 0.0001f);
            Time.timeScale = settings.celebrationTimeScale;
            Time.fixedDeltaTime = baseFixedDelta * settings.celebrationTimeScale;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(settings.celebrationSeconds),
                    ignoreTimeScale: true, cancellationToken: matchCts.Token);
            }
            catch (OperationCanceledException) { /* teardown mid-celebration */ }
            finally
            {
                // Restore to known constants, not captured values - the ball's hitstop can
                // interleave with this window and a stale capture would re-apply its timescale.
                Time.timeScale = 1f;
                Time.fixedDeltaTime = baseFixedDelta;
            }
        }

        [ClientRpc]
        void AnnounceOvertime_ClientRpc() =>
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.ComebackCharge);

        [ClientRpc]
        void AnnounceMatchFinished_ClientRpc(int winnerDomain)
        {
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.GameEnd);
            // A ghost still on screen would fight the winner banner + end-game cinematic.
            goalReplay?.Stop();
        }
    }
}
