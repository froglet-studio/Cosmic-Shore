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
    /// Astro League match director — hypersea soccer on the multiplayer domain-games stack.
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
        [Tooltip("Drag AstroLeagueScoringRule.asset — the per-mode scoring strategy (winner, scores, results).")]
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

        // Intensity scaling: base (authored) positions captured at spawn, multiplied by the scale.
        readonly List<Vector3> _baseGoalLocalPos = new();
        readonly List<Vector3> _baseSpawnLocalPos = new();
        float _currentScale = 1f;

        // The runtime-generated nucleus cage mesh (polytope/cylinder courts) — owned here and destroyed
        // on despawn so it doesn't leak across matches/replays (the scene-reload replay re-generates it).
        Mesh _generatedNucleusMesh;

        // Match config (intensity scale, court shape, goal target) replicated via NetworkVariables rather
        // than a one-shot ClientRpc, so a client that spawns AFTER the server set them still builds the
        // arena (a one-shot RPC missed late joiners — the "not all players see the arena" bug). Every
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

        // End-game runs through OnTurnEndedCustom → SyncFinalScores_ClientRpc (HexRace/Joust/
        // CrystalCapture pattern); suppress the base turn→round→game flow so we don't get a
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

        /// <summary>Arena/ball/layout scale: 1x at intensity 1, ramping to intensityScaleAtMax at the top.</summary>
        float ScaleForIntensity()
        {
            int maxLevel = Mathf.Max(2, settings.maxIntensityLevel);
            int intensity = gameData.SelectedIntensity != null
                ? Mathf.Clamp(gameData.SelectedIntensity.Value, 1, maxLevel)
                : 1;
            float t = (intensity - 1f) / (maxLevel - 1f);
            return Mathf.Lerp(1f, Mathf.Max(1f, settings.intensityScaleAtMax), t);
        }

        /// <summary>Court geometry for the selected intensity — one ricochet test shape per level.</summary>
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
                if (shape == AstroLeagueBoundaryShape.Sphere)
                {
                    cell.SetNucleusWorldRadius(arena.BoundaryRadius);
                }
                else
                {
                    // Polytope + cylinder courts get a generated cage mesh that matches their walls; a
                    // degenerate shape with no mesh falls back to a sphere sized to the boundary.
                    Mesh cage = arena.Boundary.BuildVisualMesh();
                    if (cage != null)
                    {
                        if (_generatedNucleusMesh != null) Destroy(_generatedNucleusMesh); // free a prior rebuild
                        _generatedNucleusMesh = cage;
                        cell.SetNucleusMesh(cage);
                    }
                    else
                    {
                        cell.SetNucleusWorldRadius(arena.Boundary.MaxExtent);
                    }
                }
            }

            Vector3 arenaCenter = arena != null ? arena.Center : transform.position;

            // Ball spawn: only the central shared goal overrides it — off-center in the goal's plane
            // (along X) so the ball doesn't start sitting inside the central goal. Non-central modes keep
            // the ball's authored center spawn.
            if (ball != null && centralGoal)
                ball.SetSpawnPosition(arenaCenter + Vector3.right * (settings.centralBallSpawnOffset * _currentScale));

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
                        goals[i].Configure(ball, arenaCenter, _currentScale, normal, passThrough: true);
                    }
                    else
                    {
                        if (i < _baseGoalLocalPos.Count)
                            goals[i].transform.localPosition = _baseGoalLocalPos[i] * _currentScale;
                        // Wire the ball + arena center + scale AFTER repositioning so the goal's inward
                        // normal and mouth radius are computed from its final scaled world position.
                        goals[i].Configure(ball, arenaCenter, _currentScale);
                    }
                }

            if (teamSpawns != null)
                for (int i = 0; i < teamSpawns.Count; i++)
                {
                    if (teamSpawns[i] == null || i >= _baseSpawnLocalPos.Count) continue;
                    teamSpawns[i].localPosition = _baseSpawnLocalPos[i] * _currentScale;
                }
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

            // Recoil the striker away from the ball so it bounces back a bit — extra anti-clip
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
        /// OWNING peer — the ClientRpc resolves the vessel by NetworkObjectId and only the owner
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
        /// recent striker NOT on the defending domain — an own-goal hands the point to the
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

            scorer.RoundStats.GoalsScored++; // NetworkVariable — replicates to every peer

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

        // ── Server-authoritative game end (HexRace/Joust/CrystalCapture pattern) ──

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
        /// ExecuteServerGameEnd — this override prevents the Ready button from reappearing.
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

            // Authoritative winner — written to gameData, consumed by EndGameControllers.
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
        void ParkVesselsForKickoff_ClientRpc()
        {
            foreach (var player in gameData.Players)
            {
                if (player?.Vessel == null) continue;

                bool ownedHere = player.IsInitializedAsAI
                    ? NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                    : player.IsLocalUser;
                if (!ownedHere) continue;

                player.SetPoseOfVessel(ComputeKickoffPose(player));
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

            // Slots fan out laterally: 0, +1, -1, +2, -2, ... — spacing scales with the arena.
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

        /// <summary>
        /// Billiard thinking: approach the ball from the own-goal side so contact drives it
        /// toward the enemy goal; swing wide to recover when caught on the wrong side of the
        /// play. During kickoffs/celebrations, hold near the team's kickoff line.
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

            // End goals: aim at the goal mouth. Central shared goal: aim PAST the center along the
            // scoring direction (the goal's inward normal) so contact drives the ball THROUGH the
            // central disk in the team's scoring Z direction — aiming straight at center would own-goal
            // from the wrong side.
            Vector3 aimPoint = _appliedCentralGoal
                ? targetGoal.MouthCenter + targetGoal.InwardNormal * (300f * _currentScale)
                : targetGoal.MouthCenter;
            Vector3 shotDir = (aimPoint - ballPos).normalized;
            Vector3 approachPoint = ballPos - shotDir * settings.strikerApproachLead;

            Vector3 aiPos = aiPlayer.Vessel.Transform.position;
            bool onAttackSide = Vector3.Dot(ballPos - aiPos, shotDir) > 0f;
            if (onAttackSide)
                return approachPoint;

            // Wrong side of the ball: swing wide around it back toward our half.
            Vector3 side = Vector3.Cross(shotDir, Vector3.up).normalized;
            float sideSign = Mathf.Sign(Vector3.Dot(aiPos - ballPos, side));
            if (sideSign == 0f) sideSign = 1f;
            return ballPos - shotDir * settings.strikerRecoverDistance
                          + side * (sideSign * settings.strikerRecoverDistance * 0.6f);
        }

        AstroLeagueGoal GoalAttackedBy(Domains attackingDomain)
        {
            foreach (var goal in goals)
                if (goal != null && goal.DefendingDomain != attackingDomain)
                    return goal;
            return null;
        }

        // ── Announcer ClientRpcs (play the shared AudioSystem cue on every peer) ──

        [ClientRpc]
        void AnnounceKickoffGo_ClientRpc() =>
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.SpeedBurst);

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
                // Restore to known constants, not captured values — the ball's hitstop can
                // interleave with this window and a stale capture would re-apply its timescale.
                Time.timeScale = 1f;
                Time.fixedDeltaTime = baseFixedDelta;
            }
        }

        [ClientRpc]
        void AnnounceOvertime_ClientRpc() =>
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.ComebackCharge);

        [ClientRpc]
        void AnnounceMatchFinished_ClientRpc(int winnerDomain) =>
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.GameEnd);
    }
}
