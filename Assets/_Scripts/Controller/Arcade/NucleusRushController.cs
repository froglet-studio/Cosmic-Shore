using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Nucleus Rush ("Brood Rush") - the nucleus-control domain minigame built on the
    /// new cell fundamentals: node control is the per-domain ENVIRONMENT volume INSIDE
    /// the cell's nucleus, everything outside is the voraciously-grazed feeding ground,
    /// and the fauna spawner ticks a fixed 30s wave clock. Every wave the cell births
    /// under a domain's nucleus claim scores that domain one brood (a point); the first
    /// domain to the wave target (default 3 - Tools &gt; Cosmic Shore &gt; End Game
    /// Conditions) wins, so a two-domain match runs ~1.5–2.5 minutes.
    ///
    /// Zero bespoke ecology: the standard Cell + SpawnProfile produce the waves
    /// (SeedFullWaveEveryTick, one fauna species) and raise the SOAP wave event
    /// (CellRuntimeDataSO.OnFaunaWaveSpawned ← RandomLifeSpawner); this controller only
    /// LISTENS. Scoring is server-authoritative: the server's wave event increments a
    /// representative RoundStats' GoalsScored (NetworkVariable - replicates to every
    /// peer; domain sums ride the base controller's domain-sum NetworkVariables), and
    /// NucleusRushWaveTurnMonitor ends the turn when a domain reaches the target.
    /// End-game runs the shared MultiplayerDomainGamesController.SyncFinalResults template.
    /// </summary>
    public class NucleusRushController : MultiplayerDomainGamesController
    {
        [Header("Nucleus Rush")]
        [Tooltip("Drag Event_OnFaunaWaveSpawned.asset - the cell runtime's fauna wave channel this mode scores from.")]
        [SerializeField] ScriptableEventFaunaWave onFaunaWaveSpawned;

        bool _turnActive;

        protected override bool UseGolfRules => false;

        // The scene reload path is required: flora/fauna/crystals and the cell's
        // accumulated trail mass don't fully reset in place (HexRace/CC precedent).
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom → the base SyncFinalResults template;
        // suppress the base turn→round→game flow so we don't get a duplicate InvokeWinnerCalculated.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _turnActive = false;

            onFaunaWaveSpawned.OnRaised += HandleFaunaWave;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
        }

        public override void OnNetworkDespawn()
        {
            onFaunaWaveSpawned.OnRaised -= HandleFaunaWave;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
            base.OnNetworkDespawn();
        }

        // ── Wave clock alignment ──────────────────────────────────────────

        /// <summary>
        /// The cell's spawner starts when its bootstrap crystal registers - during the
        /// ready screen. Realign the fixed 30s wave clock to the GO so the first scored
        /// wave lands ~30s into the race on every peer (fauna are client-local; each
        /// peer realigns its own simulation - StartTurn is raised on all machines).
        /// </summary>
        void HandleTurnStarted()
        {
            _turnActive = true;

            var cells = Cell.ActiveCellsSnapshot;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i]) cells[i].RestartSpawnerForMode();
            }
        }

        void HandleTurnEnded() => _turnActive = false;

        // ── Server-authoritative wave scoring ─────────────────────────────

        /// <summary>
        /// One fauna spawn-cycle tick. The SERVER's simulation is the scoring
        /// authority (every peer raises its own local event; only the server's
        /// counts). A wave scores only while the turn is live and only when its
        /// domain holds a genuine nucleus claim - an unclaimed nucleus births an
        /// ambient wave for the fallback color but awards nobody.
        /// </summary>
        void HandleFaunaWave(FaunaWaveData wave)
        {
            if (!IsServer || !_turnActive || FinalResultsSent) return;
            if (!wave.NucleusControlled || wave.Domain == Domains.Blue) return;

            // Credit the brood to a representative player on the claiming domain -
            // domain SUMS drive the race (ScoringMetrics.SumByDomain over GoalsScored),
            // so which teammate carries the point is presentation only. First roster
            // entry keeps it deterministic (server is the only writer).
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s != null && s.Domain == wave.Domain);
            if (stats == null) return;

            stats.GoalsScored++;

            int sum = ScoringMetrics.SumByDomain(gameData, rule.Metric, wave.Domain);
            AnnounceWaveScored_ClientRpc((int)wave.Domain, sum, gameData.GoalTargetCount);
        }

        [ClientRpc]
        void AnnounceWaveScored_ClientRpc(int domain, int broodSum, int target)
        {
            var d = (Domains)domain;
            GameFeedAPI.Post($"<b>{d}</b> brood hatched - {broodSum}/{target}", d, GameFeedType.Generic);
        }

        // ── Server-authoritative game end (shared SyncFinalResults template) ─────

        /// <summary>
        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
        /// ExecuteServerTurnEnd → SetupNewRound, so FinalResultsSent latches in time
        /// to suppress the Ready button. Score assignment, roster snapshot, and the
        /// canonical results tail are owned by the base SyncFinalResults template.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || FinalResultsSent) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            SyncFinalResults(winningDomain, 0f);
        }

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();

            foreach (var s in gameData.RoundStatsList)
            {
                s.GoalsScored = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
