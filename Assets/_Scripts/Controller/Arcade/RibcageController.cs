using System.Collections;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Ribcage - the Rhino-only cage-breaking race. Domains race to smash a shielded prism
    /// sphere; the first to the destruction target breaks out and wins. Structurally a sibling
    /// of <see cref="RampageController"/> (1 round / 1 turn, HasEndGame=false, server winner
    /// detection in OnTurnEndedCustom, snapshot SyncFinalScores_ClientRpc) - the destruction
    /// stat itself auto-increments via StatsManager.PrismDestroyed, so no per-event listener
    /// lives here either.
    ///
    /// What this controller adds over Rampage is the FAUNA LADDER, and the point of its design
    /// is that it contains no fauna targeting code at all. It only publishes two facts to the
    /// arena cell and lets the existing ecology draw every consequence:
    ///
    ///   1. WHO CONTROLS THE CELL = who leads the race (<see cref="Cell.SetModeControlOverride"/>).
    ///      Fauna already spawn in exactly one colour - the cell's controlling colour (the locked
    ///      no-domain-asymmetry invariant) - and herbivores in a nucleus-less cell already eat
    ///      opposing-domain mass. So the brood hatches wearing the leader's colours and hunts
    ///      every trailing team's trails, and when the lead changes hands the override recolours
    ///      the live swarm and its diet flips with it. There is no "target the loser" code
    ///      anywhere; the diet rule was always this.
    ///   2. HOW HARD the cell is running (<see cref="Cell.ModePhaseFloor"/> +
    ///      <see cref="Cell.FaunaReleaseTier"/>). 25% of the target floors the cell at Restless
    ///      and opens tier 0 (the grazer swarm); 50% floors it at Frenzy and opens tier 1 (the
    ///      predator). Aggression bands, steering, danger immunity and speed all come from the
    ///      existing CellPhase → CellAggressionLevel mapping.
    ///
    /// Neither publication removes a prism or starts a clock, so the conserved-mass law is
    /// untouched: the cage falls only to vessel abilities, and holding fauna PRODUCTION closed
    /// until the ladder opens it is the explicitly-allowed "don't create mass" lever, never a
    /// culler. See RIBCAGE.md and Docs/ECOSYSTEM.md.
    /// </summary>
    public class RibcageController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag RibcageScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The cage cell. The controller publishes race leadership and the escalation " +
                 "ladder onto it; the cell's own ecology does the rest. Must be a cell with NO " +
                 "NucleusPrefab - a nucleus control zone switches herbivores to the spatial " +
                 "'eat anything outside the nucleus' diet, which would point the swarm at every " +
                 "team including the leader's and break the whole hook.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("Fraction of the cage target at which the grazer swarm is released for the " +
                 "leader and the cell is floored at Restless.")]
        [SerializeField, Range(0.05f, 0.9f)] float broodReleaseFraction = 0.25f;

        [Tooltip("Fraction of the cage target at which the predator joins and the cell is " +
                 "floored at Frenzy.")]
        [SerializeField, Range(0.1f, 0.95f)] float packReleaseFraction = 0.5f;

        [Tooltip("Seconds between server-side leadership/escalation samples. The ladder is a " +
                 "coarse state machine, so this does not need to be per-frame.")]
        [SerializeField, Min(0.1f)] float ladderSampleSeconds = 0.5f;

        [Header("AI")]
        [Tooltip("Seconds between AI cage-breaker target refreshes - how often each AI Rhino " +
                 "picks a fresh stretch of bone to ram.")]
        [SerializeField, Min(0.25f)] float aiRetargetSeconds = 2f;

        [Tooltip("Cage shell radius the AI aims at. 0 = use SpawnableRibcage.ShellRadius (the " +
                 "value the generator actually builds at). Override only for a resized arena.")]
        [SerializeField, Min(0f)] float aiCageRadiusOverride = 0f;

        // Release tiers published to the cell. -1 = nothing released (the cage is sealed).
        const int TierSealed = -1;
        const int TierBrood = 0;
        const int TierPack = 1;

        bool _finalResultsSent;
        Coroutine _ladderRoutine;
        int _releaseTier = TierSealed;
        Domains _leader = Domains.Blue;

        // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
        // (see RibcageScoringRuleSO.AssignScores) - lower is better, like HexRace.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // Ribcage handles end-game through OnTurnEndedCustom (server-side winner detection) →
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

            // Seal the cage on every peer before anything can tick: fauna are client-local, so
            // each machine must hold its own brood closed until the ladder opens it.
            ApplyRelease(TierSealed);
        }

        public override void OnNetworkDespawn()
        {
            StopLadder();
            ReleaseCellOverrides();
            base.OnNetworkDespawn();
        }

        // ── The ladder (server publishes; the cell's ecology reacts) ──────

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            ArmCageBreakers();

            StopLadder();
            _ladderRoutine = StartCoroutine(LadderRoutine());
        }

        void StopLadder()
        {
            if (_ladderRoutine == null) return;
            StopCoroutine(_ladderRoutine);
            _ladderRoutine = null;
        }

        /// <summary>
        /// Server-side sampler: publishes who leads (the cell's controlling domain) and how far
        /// the leader has got (the escalation tier). Both are coarse states, so a half-second
        /// cadence is ample and costs one SumByDomain per active domain per sample.
        /// </summary>
        IEnumerator LadderRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, ladderSampleSeconds));

            while (!_finalResultsSent)
            {
                SampleLadder();
                yield return wait;
            }

            _ladderRoutine = null;
        }

        void SampleLadder()
        {
            if (!IsServer || rule == null) return;

            int target = gameData.PrismTargetCount;
            if (target <= 0) return; // monitor hasn't resolved the target yet

            // Leading domain by destruction sum, Jade→Ruby→Gold on ties (fixed order, so every
            // machine would agree - though only the server ever computes this).
            var leader = Domains.Blue;
            int best = 0;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                int sum = ScoringMetrics.SumByDomain(gameData, rule.Metric, d);
                if (sum > best)
                {
                    best = sum;
                    leader = d;
                }
            }

            // Nobody has broken a bar yet - leave the cell unclaimed rather than handing the
            // brood to Jade for winning a 0-0 tie-break.
            if (leader == Domains.Blue || best <= 0) return;

            float progress = best / (float)target;
            int tier = progress >= packReleaseFraction ? TierPack
                : progress >= broodReleaseFraction ? TierBrood
                : TierSealed;

            bool leaderChanged = leader != _leader;
            bool tierChanged = tier != _releaseTier;
            if (!leaderChanged && !tierChanged) return;

            _leader = leader;
            PublishLeader_ClientRpc((int)leader);

            if (tierChanged)
            {
                _releaseTier = tier;
                PublishRelease_ClientRpc(tier, (int)leader, best, target);
            }
            else if (leaderChanged && tier > TierSealed)
            {
                // The swarm changes hands mid-match: worth announcing, since every trailing
                // team's trails just became the menu.
                AnnounceLeaderChanged_ClientRpc((int)leader, best, target);
            }
        }

        /// <summary>
        /// Pins the cell's controlling domain on EVERY peer. The server's own pin would
        /// replicate through CellNetworkSync anyway, but fauna are client-local and a client's
        /// swarm should change colour on the same event rather than on the next 0.5s mirror.
        /// </summary>
        [ClientRpc]
        void PublishLeader_ClientRpc(int domain)
        {
            if (arenaCell) arenaCell.SetModeControlOverride((Domains)domain);
        }

        [ClientRpc]
        void PublishRelease_ClientRpc(int tier, int domain, int sum, int target)
        {
            ApplyRelease(tier);

            var d = (Domains)domain;
            if (tier == TierBrood)
                GameToastAPI.Post(GameToastSituation.RibcageBroodReleased, d,
                    d.ToString(), sum.ToString(), target.ToString());
            else if (tier == TierPack)
                GameToastAPI.Post(GameToastSituation.RibcagePackReleased, d,
                    d.ToString(), sum.ToString(), target.ToString());
        }

        [ClientRpc]
        void AnnounceLeaderChanged_ClientRpc(int domain, int sum, int target)
        {
            var d = (Domains)domain;
            GameToastAPI.Post(GameToastSituation.RibcageLeaderChanged, d,
                d.ToString(), sum.ToString(), target.ToString());
        }

        /// <summary>
        /// Applies an escalation tier to the local cell: which species may seed, and the phase
        /// the cell is held at (which IS the fauna aggression band - see CellAggressionLevel).
        /// Runs on every peer because fauna are client-local.
        /// </summary>
        void ApplyRelease(int tier)
        {
            if (!arenaCell) return;

            arenaCell.FaunaReleaseTier = tier;
            arenaCell.ModePhaseFloor = tier switch
            {
                TierPack => CellPhase.Frenzy,     // any-colour steering, no friendly avoidance, danger-immune
                TierBrood => CellPhase.Restless,  // hunt the opposing-colour centroid = the trailing teams
                _ => null,                        // sealed: the volume ladder alone (an emptying cage is Calm)
            };

            // Realign the fauna spawn clock to the RELEASE moment. Without this the gate opens
            // mid-period and the brood can take up to a full BaseFaunaSpawnTime to appear, which
            // reads as the reward simply not arriving. The profile authors no flora, so the
            // "restart re-runs the initial flora batch" caveat on RestartSpawnerForMode does not
            // apply here.
            if (tier > TierSealed) arenaCell.RestartSpawnerForMode();
        }

        void ReleaseCellOverrides()
        {
            if (!arenaCell) return;
            arenaCell.SetModeControlOverride(null);
            arenaCell.ModePhaseFloor = null;
            arenaCell.FaunaReleaseTier = int.MaxValue;
        }

        // ── AI cage-breakers (server) ────────────────────────────────────

        /// <summary>
        /// Points every AI Rhino at a stretch of the cage shell and refreshes it periodically so
        /// each one rams through fresh bone instead of orbiting one hole.
        ///
        /// Deliberately NOT <see cref="Cell.GetExplosionTarget"/> (Rampage's density-grid mass
        /// hunt): the cage is shielded, and shielded mass is kept out of the targeting grids
        /// precisely so nothing is steered onto mass it cannot eat - so the grids here hold only
        /// player trails and would send every AI chasing vessels instead of breaking out. The
        /// shell is an analytic sphere, so aiming at it needs no query at all.
        /// </summary>
        void ArmCageBreakers()
        {
            float radius = aiCageRadiusOverride > 0f ? aiCageRadiusOverride : SpawnableRibcage.ShellRadius;
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            int seat = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                // Phase each AI onto its own arc of the cage so a full lobby spreads around the
                // sphere rather than queueing at one rib.
                float phase = seat++ * Mathf.PI * 2f / 4f;
                int step = 0;
                Vector3 cached = centre;
                float nextSample = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    if (Time.time >= nextSample)
                    {
                        nextSample = Time.time + aiRetargetSeconds;

                        // Walk the shell on a golden-angle spiral: successive targets are far
                        // apart, deterministic, and never repeat a spot, so the AI keeps finding
                        // intact bone. Ramming THROUGH the aimed point is what breaks the bars.
                        float a = phase + step * 2.39996323f;
                        float y = 1f - 2f * ((step * 0.37f) % 1f);
                        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                        step++;

                        cached = centre + new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)) * radius;
                    }
                    return cached;
                });
            }
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection, mirroring the Crystal Capture / Rampage pattern.
        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound, so
        /// _finalResultsSent is set in time to suppress the Ready button.
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
                .OrderByDescending(s => s.HostilePrismsDestroyed)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopLadder();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the game just ended. HasEndGame=false
        /// causes ExecuteServerRoundEnd to call SetupNewRound instead of ExecuteServerGameEnd -
        /// this override prevents the Ready button from appearing.
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
            var prismsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                prismsArray[i] = statsList[i].HostilePrismsDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, prismsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prismsDestroyed,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Ribcage] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
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
            _releaseTier = TierSealed;
            _leader = Domains.Blue;

            StopLadder();
            ApplyRelease(TierSealed);
            if (arenaCell) arenaCell.SetModeControlOverride(null);

            foreach (var s in gameData.RoundStatsList)
            {
                s.HostilePrismsDestroyed = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
