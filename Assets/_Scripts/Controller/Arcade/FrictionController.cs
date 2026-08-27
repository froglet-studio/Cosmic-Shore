using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction: Skim-Race-style crystal collection under Joust-style hunter pressure.
    /// Reach the per-intensity crystal target before time runs out or every human player
    /// is eliminated by the Rhino hunters. Modeled directly on HexRaceController for the
    /// track/seed/replay plumbing, with hunter-elimination and storm-hazard handling added.
    /// </summary>
    public class FrictionController : MultiplayerDomainGamesController
    {
        [Header("Course")]
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] int baseNumberOfSegments = 10;
        [SerializeField] int baseStraightLineLength = 400;
        [SerializeField] bool scaleNumberOfSegmentsWithIntensity = true;
        [SerializeField] bool scaleLengthWithIntensity = true;

        [Header("Seed")]
        [SerializeField] int seed = 0;

        [Header("Storm (Intensity 3-4)")]
        [Tooltip("URP fog/color-grading Volume — weight ramps up from Intensity 2 to 4. Purely visual.")]
        [SerializeField] Volume stormVolume;
        [SerializeField] int stormRampStartIntensity = 2;
        [SerializeField] int stormRampFullIntensity = 4;

        [Header("Lightning (Intensity 4)")]
        [SerializeField] FrictionLightningStrikeController lightningController;
        [SerializeField] int lightningMinIntensity = 4;

        [Header("Lives")]
        [SerializeField] int startingLives = 3;

        [Header("Rewards")]
        [Tooltip("Auto-unlocked in the local Hangar when the local player wins at max intensity.")]
        [SerializeField] SO_Vessel rhinoUnlockReward;
        [SerializeField] int rhinoUnlockAtIntensity = 4;

        [Header("Turn Monitors")]
        [Tooltip("Shared with TurnMonitorController — reused here to check *why* the turn ended, since all three Friction monitors OR-trigger the same OnTurnEndedCustom callback.")]
        [SerializeField] AllHumansEliminatedTurnMonitor allHumansEliminatedMonitor;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        bool _matchEnded;
        bool _reachedTarget;
        FrictionOutcome _outcome = FrictionOutcome.TimeExpired;
        bool _trackSpawned;
        CancellationTokenSource _seedPollCts;
        readonly NetworkVariable<int> _netTrackSeed = new(0);

        /// <summary>
        /// How the run finished. Server-authoritative, replicated to every client by
        /// <see cref="SyncFinalResults_ClientRpc"/>. Read by FrictionEndGameController to
        /// pick the VICTORY / DEFEAT / no-winner headline.
        /// </summary>
        public FrictionOutcome Outcome => _outcome;

        /// <summary>True only when a player actually reached the crystal target.</summary>
        public bool ReachedTarget => _reachedTarget;

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // Friction handles end-game through OnTurnEndedCustom (server-side outcome detection) →
        // SyncFinalResults_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeMiniGameEnd from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _matchEnded = false;
            _reachedTarget = false;
            _outcome = FrictionOutcome.TimeExpired;

            if (segmentSpawner) segmentSpawner.ExternalResetControl = true;

            _netTrackSeed.OnValueChanged += OnTrackSeedChanged;

            if (IsServer)
            {
                SpawnTrackEarly().Forget();
            }
            else if (_netTrackSeed.Value != 0)
            {
                SpawnTrackLocally(_netTrackSeed.Value);
            }
            else
            {
                StartSeedPoll();
            }

            ApplyStormVisuals();
        }

        public override void OnNetworkDespawn()
        {
            CancelSeedPoll();
            _netTrackSeed.OnValueChanged -= OnTrackSeedChanged;
            lightningController?.Deactivate();
            base.OnNetworkDespawn();
        }

        // ── Track spawning (mirrors HexRaceController) ────────────────────

        void OnTrackSeedChanged(int previousValue, int newValue)
        {
            if (newValue != 0)
                SpawnTrackLocally(newValue);
        }

        void StartSeedPoll()
        {
            CancelSeedPoll();
            _seedPollCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            WaitForTrackSeed(_seedPollCts.Token).Forget();
        }

        void CancelSeedPoll()
        {
            _seedPollCts?.Cancel();
            _seedPollCts?.Dispose();
            _seedPollCts = null;
        }

        async UniTaskVoid WaitForTrackSeed(CancellationToken ct)
        {
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    await UniTask.Delay(100, DelayType.UnscaledDeltaTime, cancellationToken: ct);

                    if (_trackSpawned) return;

                    if (_netTrackSeed.Value != 0)
                    {
                        SpawnTrackLocally(_netTrackSeed.Value);
                        return;
                    }
                }

                CSDebug.LogWarning("[FrictionController] Client poll: timed out after 5s waiting for track seed.");
            }
            catch (System.OperationCanceledException)
            {
                // Network despawn or object destroyed — expected
            }
        }

        async UniTaskVoid SpawnTrackEarly()
        {
            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime);
            if (!IsServer || _trackSpawned) return;

            int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
            _netTrackSeed.Value = generatedSeed;
            SpawnTrack_ClientRpc(generatedSeed);
        }

        [ClientRpc]
        void SpawnTrack_ClientRpc(int trackSeed) => SpawnTrackLocally(trackSeed);

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            if (_netTrackSeed.Value == 0)
            {
                int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
                _netTrackSeed.Value = generatedSeed;
            }
            SpawnTrack_ClientRpc(_netTrackSeed.Value);

            // Reset lives/elimination for every human player at the start of the run.
            foreach (var stats in gameData.RoundStatsList)
            {
                stats.Lives = startingLives;
                stats.IsEliminated = false;
            }

            if (lightningController && Intensity >= lightningMinIntensity)
                lightningController.Activate();

            base.OnCountdownTimerEnded();
        }

        void SpawnTrackLocally(int trackSeed)
        {
            if (_trackSpawned || !segmentSpawner) return;

            segmentSpawner.Seed = trackSeed;
            segmentSpawner.NumberOfSegments = scaleNumberOfSegmentsWithIntensity
                ? baseNumberOfSegments * Intensity
                : baseNumberOfSegments;
            segmentSpawner.StraightLineLength = scaleLengthWithIntensity
                ? baseStraightLineLength / Intensity
                : baseStraightLineLength;
            segmentSpawner.Initialize();
            _trackSpawned = true;
        }

        void ApplyStormVisuals()
        {
            if (!stormVolume) return;
            stormVolume.weight = Mathf.InverseLerp(stormRampStartIntensity, stormRampFullIntensity, Intensity);
        }

        // ── Server-authoritative outcome detection ─────────────────────────

        /// <summary>
        /// Friction combines three OR-triggered TurnMonitors (crystal target, time limit,
        /// all-humans-eliminated) into one callback, so unlike HexRace/Joust (single
        /// monitor each) we must re-check which condition is actually true here.
        /// Priority: reaching the crystal target wins outright, even if time/elimination
        /// technically also became true on the same frame.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _matchEnded) return;

            int target = ResolveCrystalTarget();
            var winner = gameData.RoundStatsList.FirstOrDefault(s => s.CrystalsCollected >= target);

            if (winner != null)
            {
                HandleWin(winner, target);
                return;
            }

            // Re-use the same monitor the TurnMonitorController already runs, rather than
            // duplicating its human-vs-AI RoundStats query here.
            bool allEliminated = allHumansEliminatedMonitor && allHumansEliminatedMonitor.CheckForEndOfTurn();
            HandleLoss(target, allEliminated);
        }

        int ResolveCrystalTarget() => gameData.CrystalTargetCount > 0 ? gameData.CrystalTargetCount : 10;

        void HandleWin(IRoundStats winner, int target)
        {
            _matchEnded = true;
            _reachedTarget = true;
            Domains winningDomain = winner.Domain;
            float finishTime = gameData.LocalRoundStats?.Score ?? 0f;

            foreach (var stats in gameData.RoundStatsList)
            {
                stats.Score = stats.Domain == winningDomain
                    ? finishTime
                    : 10000f + Mathf.Max(0, target - stats.CrystalsCollected);
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            SyncFinalResultsSnapshot(winner.Name, winningDomain, FrictionOutcome.TargetReached);
        }

        void HandleLoss(int target, bool allEliminated)
        {
            _matchEnded = true;
            CSDebug.Log($"[FrictionController] Run ended without reaching target={target}. Cause={(allEliminated ? "all humans eliminated" : "time expired")}");

            // No one reached the target — score everyone by crystals remaining (golf: lower
            // is better), whether the cause was the clock or total elimination.
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = 10000f + Mathf.Max(0, target - stats.CrystalsCollected);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            // No winner: Domains.Blue is the project's "no team" sentinel, so end-game screens
            // (and anything else reading WinnerDomain) correctly report that nobody won rather
            // than crowning whoever happened to rank first. Ranking is unaffected — Scoreboard
            // derives placement from RoundStatsList/DomainStatsList, not from WinnerName.
            SyncFinalResultsSnapshot("", Domains.Blue,
                allEliminated ? FrictionOutcome.AllHumansEliminated : FrictionOutcome.TimeExpired);
        }

        protected override void SetupNewRound()
        {
            if (_matchEnded) return;
            base.SetupNewRound();
        }

        void SyncFinalResultsSnapshot(string winnerName, Domains winnerDomain, FrictionOutcome outcome)
        {
            var list = gameData.RoundStatsList;
            int count = list.Count;

            var names = new FixedString64Bytes[count];
            var scores = new float[count];
            var domains = new int[count];
            var crystals = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(list[i].Name);
                scores[i] = list[i].Score;
                domains[i] = (int)list[i].Domain;
                crystals[i] = list[i].CrystalsCollected;
            }

            SyncFinalResults_ClientRpc(names, scores, domains, crystals,
                new FixedString64Bytes(winnerName), (int)winnerDomain, (int)outcome);
        }

        [ClientRpc]
        void SyncFinalResults_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] crystalsCollected,
            FixedString64Bytes winnerName,
            int winnerDomain,
            int outcome)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == n);
                if (stat == null) continue;

                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CrystalsCollected = crystalsCollected[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;
            _outcome = (FrictionOutcome)outcome;
            _reachedTarget = _outcome == FrictionOutcome.TargetReached;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            TryAwardRhinoUnlock();

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        /// <summary>
        /// Auto-unlocks Rhino in the local player's Hangar the first time they win a
        /// Friction match at the configured max intensity. Simplification: this checks
        /// "won at max intensity" rather than tracking completion of all 4 intensities
        /// across separate matches, since no cross-match progress tracker exists for
        /// Friction (or any other mode) yet.
        /// </summary>
        void TryAwardRhinoUnlock()
        {
            // _reachedTarget (not just "WinnerName is set") gates this — a loss still
            // populates WinnerName/WinnerDomain with the best-scoring player so the
            // Scoreboard can rank everyone, but that's not an actual win.
            if (!_reachedTarget || !rhinoUnlockReward || Intensity < rhinoUnlockAtIntensity) return;

            var localName = gameData.LocalPlayer?.Name;
            if (string.IsNullOrEmpty(localName) || localName != gameData.WinnerName) return;

            if (rhinoUnlockReward.IsLocked)
                VesselUnlockSystem.UnlockVessel(rhinoUnlockReward);
        }
    }
}
