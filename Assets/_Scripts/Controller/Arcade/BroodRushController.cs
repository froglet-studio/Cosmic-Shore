using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Collections;
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
    /// BroodRushWaveTurnMonitor ends the turn when a domain reaches the target.
    /// End-game runs the SkimRace/Joust/Scurry SyncFinalScores pattern.
    /// </summary>
    public class BroodRushController : MultiplayerDomainGamesController
    {
        [Header("Nucleus Rush")]
        [Tooltip("Drag BroodRushScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
        [SerializeField] ScoringRuleSO rule;
        [Tooltip("Drag Event_OnFaunaWaveSpawned.asset - the cell runtime's fauna wave channel this mode scores from.")]
        [SerializeField] ScriptableEventFaunaWave onFaunaWaveSpawned;

        bool _finalResultsSent;
        bool _turnActive;

        protected override bool UseGolfRules => false;

        // The scene reload path is required: flora/fauna/crystals and the cell's
        // accumulated trail mass don't fully reset in place (SkimRace/CC precedent).
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom → SyncFinalScores_ClientRpc; suppress
        // the base turn→round→game flow so we don't get a duplicate InvokeWinnerCalculated.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
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
            if (!IsServer || !_turnActive || _finalResultsSent) return;
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
            GameToastAPI.Post(GameToastSituation.BroodWaveScored, d,
                d.ToString(), broodSum.ToString(), target.ToString());
        }

        // ── Server-authoritative game end (SkimRace/Joust/CC pattern) ─────

        /// <summary>
        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
        /// ExecuteServerTurnEnd → SetupNewRound, so _finalResultsSent is set in time
        /// to suppress the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;

            var winningDomain = rule.ResolveWinner(gameData);
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

            // The second way to the number: what every pilot still owns, measured from the field, beside the books
            // the match kept (Garrett, 2026-11-18). A difference names a pilot whose books miss an event.
            gameData.VolumeLedger.CountLive(PrismSpatialIndex.Instance,
                gameData.RoundStatsList.Where(s => s != null).Select(s => s.Name).ToList());
            ReportBooksGaps();

            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the game just ended
        /// (HasEndGame=false routes ExecuteServerRoundEnd back here otherwise).
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        /// <summary>
        /// The bug signal (Garrett, 2026-11-18: "a way for us to be sure we are reporting match victory and personal
        /// scores accurately"): a pilot whose books (VC less what they lost) are not what the field holds, or a
        /// two-team match whose difference of team totals is not the difference of volume left, is a warning naming
        /// the numbers. A pilot-level gap is an event the books missed; a team-level gap with balanced pilots is
        /// volume that moved by something no pilot did (fauna, a pilot destroying their own team's, shrinking).
        /// </summary>
        void ReportBooksGaps()
        {
            var books = gameData.VolumeLedger;
            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null || !books.TryGet(s.Name, out var line) || !line.HasCount) continue;
                if (!VolumeLedger.Balances(line.Remaining, line.Counted))
                    CSDebug.LogWarning($"[BroodRush] Volume books for '{s.Name}' say {line.Remaining:0.#} left " +
                                       $"(VC {line.Created:0.#} - lost {line.Lost:0.#}) but the field holds {line.Counted:0.#}: " +
                                       "an event is missing from the books (VolumeLedger).");
            }

            var teams = gameData.RoundStatsList.Where(s => s != null).Select(s => s.Domain).Distinct().ToList();
            if (teams.Count != 2) return;
            var a = books.Sum(gameData.RoundStatsList.Where(s => s != null && s.Domain == teams[0]).Select(s => s.Name));
            var b = books.Sum(gameData.RoundStatsList.Where(s => s != null && s.Domain == teams[1]).Select(s => s.Name));
            float totals = a.Total - b.Total, left = a.Counted - b.Counted;
            if (!VolumeLedger.Balances(totals, left))
                CSDebug.LogWarning($"[BroodRush] {teams[0]} - {teams[1]}: team totals differ by {totals:0.#}, volume left " +
                                   $"by {left:0.#}. {teams[0]} lost {a.Lost:0.#} and {teams[1]} took {b.Reduced:0.#} of it; " +
                                   $"{teams[1]} lost {b.Lost:0.#} and {teams[0]} took {a.Reduced:0.#} (VolumeLedger).");
        }

        const int BooksStride = 5;

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var broodsArray = new int[count];
            // The volume books, five numbers a pilot (VC, VD, lost, super-shields, counted): the server
            // kept them, so every client's scoreboard shows the server's.
            var booksArray = new float[count * BooksStride];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                broodsArray[i] = statsList[i].GoalsScored;
                var line = gameData.VolumeLedger.Get(statsList[i].Name);
                int o = i * BooksStride;
                booksArray[o] = line.Created;
                booksArray[o + 1] = line.Reduced;
                booksArray[o + 2] = line.Lost;
                booksArray[o + 3] = line.SuperShieldsBroken;
                booksArray[o + 4] = line.HasCount ? line.Counted : float.NaN;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, broodsArray, booksArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] broods,
            float[] books,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[BroodRush] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.GoalsScored = broods[i];

                int o = i * BooksStride;
                if (books != null && o + BooksStride <= books.Length)
                {
                    float counted = books[o + 4];
                    gameData.VolumeLedger.Set(sName, new VolumeLedger.Line
                    {
                        Created = books[o],
                        Reduced = books[o + 1],
                        Lost = books[o + 2],
                        SuperShieldsBroken = Mathf.RoundToInt(books[o + 3]),
                        Counted = float.IsNaN(counted) ? 0f : counted,
                        HasCount = !float.IsNaN(counted),
                    });
                }
            }

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
                s.GoalsScored = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
