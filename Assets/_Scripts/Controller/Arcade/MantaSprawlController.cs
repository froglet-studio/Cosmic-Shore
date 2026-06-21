using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manta "Sprawl" — a time-boxed territory land-grab. Every domain soars through open
    /// space laying mass (prisms); when the clock runs out, the domain that has bloomed the
    /// most territory (summed <c>BlocksCreated</c>) wins. Points, not golf: each player's
    /// Score IS their blocks created.
    ///
    /// Mirrors <see cref="MultiplayerCrystalCaptureController"/>'s server-authoritative
    /// end-game flow, but keys off the Blocks metric instead of Crystals and ends on a
    /// <see cref="NetworkTimeBasedTurnMonitor"/> rather than an objective count. Block counts
    /// are recorded server-side by <c>StatsManager</c> (the server's local sim spawns every
    /// vessel's trail), so no per-prism RPC sync is needed — the base controller already
    /// replicates each domain's metric sum to clients for the HUD.
    ///
    /// "Mass is the spine" — see Docs/ECOSYSTEM_MASTERPLAN.md. Sprawl is the territory genre
    /// expressed directly through conserved mass: you win by creating the most of it.
    /// </summary>
    public class MantaSprawlController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag MantaSprawlScoringRule.asset — points scoring keyed on BlocksCreated.")]
        [SerializeField] ScoringRuleSO rule;

        private bool _finalResultsSent;

        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;

        // Sprawl handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection, mirroring HexRace/Joust/CrystalCapture. Called from
        /// SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound, so
        /// _finalResultsSent is set in time to suppress the Ready button. The turn was ended
        /// by the time monitor, so there is always a winning domain (the highest block sum).
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;

            // Winning domain = highest block sum (Jade → Ruby → Gold tie-break) via the rule.
            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            // Representative winner-name = best individual contributor on that domain (legacy
            // display field — victory/defeat attribution uses WinnerDomain).
            var winnerRep = gameData.RoundStatsList
                .Where(s => s.Domain == winningDomain)
                .OrderByDescending(s => s.BlocksCreated)
                .FirstOrDefault();
            if (winnerRep == null) return;

            // Per-player Score = blocks created (the rule owns this); domain aggregation in
            // CalculateDomainStats determines team standing.
            rule.AssignScores(gameData, winningDomain, 0f);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the game just ended. HasEndGame=false
        /// causes ExecuteServerRoundEnd to call SetupNewRound instead of ExecuteServerGameEnd —
        /// this override prevents the Ready button from reappearing.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ───────────────────────────────────────────────────

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var blocksArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                blocksArray[i] = statsList[i].BlocksCreated;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, blocksArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] blocksCreated,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[MantaSprawl] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.BlocksCreated = blocksCreated[i];
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

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;

            foreach (var s in gameData.RoundStatsList)
            {
                s.BlocksCreated = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
