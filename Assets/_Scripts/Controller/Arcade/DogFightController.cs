using System.Collections;
using System.Collections.Generic;
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
    /// Dog Fight - the Sparrow-only gun duel. Two to four pilots hunt each other through the
    /// Boneyard; a bullet hit banks 1 point, a missile hit (direct strike or caught in the
    /// blast) banks 50, and the first DOMAIN to the point target wins.
    ///
    /// Structurally a sibling of <see cref="RampageController"/> / <see cref="RibcageController"/>
    /// (1 round / 1 turn, HasEndGame=false, server winner detection in OnTurnEndedCustom,
    /// snapshot SyncFinalScores_ClientRpc), with three deliberate differences:
    ///
    ///   1. THE SCORE COMES FROM GUNNERY. The metric is <see cref="IRoundStats.CombatPoints"/>,
    ///      fed by the platform combat-hit path (the two combat-hit impact effects →
    ///      <c>GameDataSO.OnCombatHitLanded</c> → <c>StatsManager.CombatHitLanded</c> →
    ///      <c>CombatHitScoring.Credit</c>), so like Rampage and Ribcage there is no per-event
    ///      listener here at all. The only thing that scores is landing a shot on an OPPOSING
    ///      pilot: not prisms, not crystals, not wildlife.
    ///   2. THE ARENA IS COVER, NOT THE OBJECTIVE. Ribcage's bone IS the score and Wildlife
    ///      Liberation's cages ARE the walls; the Boneyard is neither. Shooting it is worth
    ///      nothing - it exists to break sightlines, so the fight is a series of close
    ///      encounters rather than one long open-space joust.
    ///   3. AI FLIES LEAD PURSUIT, NOT RAMMING. <see cref="ArmDogfighters"/> aims each AI at
    ///      where its quarry is GOING and breaks off on the overshoot; see that method for why
    ///      chasing the exact position makes a worse opponent, not a better one.
    ///
    /// SPARROW-ONLY is enforced entirely by the platform machinery Wildlife Liberation put in
    /// place and is deliberately not re-implemented here - three independent layers, all reading
    /// the single <c>Vessels</c> list on ArcadeGameDogFight:
    ///   (a) <c>GameDataSO.SyncFromArcadeGame</c> clamps the launching machine's selection;
    ///   (b) <c>ServerPlayerVesselInitializer.ResolveSpawnVesselType</c> re-clamps SERVER-SIDE at
    ///       spawn, which is the one that catches a client whose owner-write NetDefaultVesselType
    ///       still carries the hull it last flew;
    ///   (c) <c>ServerPlayerVesselInitializerWithAI</c> clamps the AI's scene-authored class too.
    /// </summary>
    public class DogFightController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag DogFightScoringRule.asset - the per-mode scoring strategy. It also owns " +
                 "the point VALUES (bullet 1 / missile 50), which is why the platform can count " +
                 "landed hits everywhere and score them only here.")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Boneyard cell. Read-only to this controller: it supplies the arena CENTRE " +
                 "an AI falls back to when it has no quarry. The wreckage itself comes from the " +
                 "cell's config for the selected intensity (CellTypeChoiceOptions.IntensityWise).")]
        [SerializeField] Cell arenaCell;

        [Tooltip("Fraction of the point target at which the LEADING DOMAIN crosses the first " +
                 "milestone (toast + alert haptic). A FRACTION, so moving the target moves the " +
                 "milestones with it. Feedback only - no game state changes here.")]
        [SerializeField, Range(0.05f, 0.9f)] float firstMilestoneFraction = 0.25f;

        [Tooltip("Fraction of the point target at which the leading domain crosses the second " +
                 "milestone. Feedback only.")]
        [SerializeField, Range(0.1f, 0.95f)] float secondMilestoneFraction = 0.5f;

        [Tooltip("Seconds between server-side progress samples. Milestones are a coarse state " +
                 "machine, so this does not need to be per-frame.")]
        [SerializeField, Min(0.1f)] float progressSampleSeconds = 0.5f;

        [Header("AI")]
        [Tooltip("Seconds between AI quarry re-selections. Between samples the AI keeps flying " +
                 "lead pursuit on the pilot it already picked, so this is 'how long before it " +
                 "reconsiders', not its steering rate.")]
        [SerializeField, Min(0.25f)] float aiRetargetSeconds = 1.5f;

        [Tooltip("How far ahead of its quarry an AI aims, as a multiple of the quarry's own " +
                 "per-second travel. Lead pursuit: aim where the target is GOING. 0 = pure " +
                 "pursuit (tail-chasing, and easy to shake).")]
        [SerializeField, Min(0f)] float aiLeadSeconds = 0.6f;

        [Tooltip("Inside this distance the AI stops aiming AT its quarry and aims THROUGH it, " +
                 "so it overshoots and comes back around instead of grinding hull-to-hull. This " +
                 "is what makes an AI read as a dogfighter rather than a battering ram.")]
        [SerializeField, Min(1f)] float aiBreakOffDistance = 120f;

        // Milestone rungs the leading domain crosses. Feedback only - nothing here changes game
        // state, so a missed or late sample costs a toast, never a rule.
        const int MilestoneNone = 0;
        const int MilestoneFirst = 1;
        const int MilestoneSecond = 2;

        bool _finalResultsSent;
        Coroutine _progressRoutine;
        int _milestone = MilestoneNone;
        Domains _leaderDomain = Domains.Blue;

        // Golf: the winning domain's pilots carry their finish time, everyone else a
        // DnfThreshold+remaining sentinel - lower is better, like every race here.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base turn→round→game flow so there is no duplicate.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _milestone = MilestoneNone;
            _leaderDomain = Domains.Blue;

            // The hit latch is static and Time.time keeps running across a scene load, so a
            // fast rematch could otherwise inherit a claimed window and silently eat the first
            // hit of the new match. Cheap to clear, and it runs on every peer because the latch
            // is consulted wherever a shot is simulated - not only on the server.
            VesselCombatHitLatch.Clear();

            // Belt-and-braces against the Ribcage regression where players started a match on a
            // non-zero score. The authoritative reset is
            // ServerPlayerVesselInitializer.PrepareForNewScene (unconditional, once per player,
            // on the processing path) - this is a second, cheap sweep at the one moment every
            // peer agrees the match has not started, because RoundStats lives on the PERSISTENT
            // Player object and a stat that survives is worth zeroing twice rather than never.
            if (IsServer) ZeroCombatCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Zeroes the scored stat and its breakdown on every roster entry. Server-only: the
        /// setters push through server-write NetworkVariables and replication clears every
        /// client's mirror, so a client zeroing its own would just be overwritten (and would
        /// desync until the next delta).
        /// </summary>
        void ZeroCombatCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].CombatPoints = 0;
                list[i].BulletHitsLanded = 0;
                list[i].MissileHitsLanded = 0;
            }
        }

        // ── Progress milestones (server samples, every peer gets the feedback) ──

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            // The last moment before anyone can score. A player who joined late, or whose
            // PrepareForNewScene landed before their RoundStats had replicated its name, is on
            // the roster by now - so this is the sweep that actually guarantees "everyone starts
            // at 0" in a real lobby.
            ZeroCombatCounters();

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            ArmDogfighters();

            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

        void StopProgressSampler()
        {
            if (_progressRoutine == null) return;
            StopCoroutine(_progressRoutine);
            _progressRoutine = null;
        }

        IEnumerator ProgressRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, progressSampleSeconds));
            while (!_finalResultsSent)
            {
                SampleProgress();
                yield return wait;
            }
            _progressRoutine = null;
        }

        /// <summary>
        /// Server-side sampler: which domain is leading and how far along it is. Both are coarse
        /// states, so a half-second cadence is ample and costs one roster scan per domain.
        /// </summary>
        void SampleProgress()
        {
            if (!IsServer || rule == null) return;

            int target = gameData.CombatPointTargetCount;
            if (target <= 0) return; // monitor hasn't resolved the target yet

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue) return;

            int leaderPoints = ScoringMetrics.SumByDomain(gameData, rule.Metric, leader);
            // Nobody has landed anything yet - no leader to announce rather than handing the
            // lead to whoever sorts first on a 0-0 tie-break.
            if (leaderPoints <= 0) return;

            float progress = leaderPoints / (float)target;
            int milestone = progress >= secondMilestoneFraction ? MilestoneSecond
                : progress >= firstMilestoneFraction ? MilestoneFirst
                : MilestoneNone;

            bool leaderChanged = leader != _leaderDomain;
            bool milestoneChanged = milestone != _milestone;
            if (!leaderChanged && !milestoneChanged) return;

            _leaderDomain = leader;

            if (milestoneChanged)
            {
                _milestone = milestone;
                if (milestone > MilestoneNone)
                    AnnounceMilestone_ClientRpc(milestone, (int)leader, leaderPoints, target);
            }
            else if (leaderChanged && milestone > MilestoneNone)
            {
                // The lead changes hands late in the fight - worth calling out.
                AnnounceLeadChanged_ClientRpc((int)leader, leaderPoints, target);
            }
        }

        [ClientRpc]
        void AnnounceMilestone_ClientRpc(int milestone, int domain, int points, int target)
        {
            GameToastAPI.Post(
                milestone == MilestoneSecond
                    ? GameToastSituation.DogFightHalfDown
                    : GameToastSituation.DogFightQuarterDown,
                (Domains)domain, ((Domains)domain).ToString(), points.ToString(), target.ToString());

            // Milestone reached: the alert haptic (the game's third feel, fenced to
            // match-changing events - see Docs/HAPTICS.md). Safe on every peer:
            // HapticController gates on the local player's own haptics setting.
            HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int points, int target)
        {
            GameToastAPI.Post(GameToastSituation.DogFightLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), points.ToString(), target.ToString());
        }

        // ── AI dogfighters (server) ──────────────────────────────────────────

        /// <summary>
        /// Drives every AI Sparrow as a dogfighter rather than a missile.
        ///
        /// The AI's guns need no wiring here: the Sparrow prefab's <c>AIPilot</c> already runs
        /// FullAuto and SkyBurst on their own cooldowns, so an AI that has an opponent in front
        /// of it is already shooting. What this method decides is what "in front of it" means.
        ///
        /// <b>Lead pursuit, not pure pursuit.</b> <c>AIPilot</c> has no arrive-and-stop
        /// behaviour - it steers at its target forever and flies through on arrival - so an AI
        /// aimed at an opponent's CURRENT position permanently trails them and only ever fires
        /// at where they were. Aiming <see cref="aiLeadSeconds"/> ahead along the quarry's own
        /// course puts the nose where the target is going, which is both how a real intercept
        /// works and what makes the AI's shots connect.
        ///
        /// <b>Break off on the merge.</b> Inside <see cref="aiBreakOffDistance"/> the aim point
        /// flips to a spot BEYOND the quarry, so the AI commits to an overshoot and comes back
        /// around instead of grinding hull-to-hull. Without it, "steer at the enemy forever"
        /// degenerates into a ramming contest that neither pilot can shoot their way out of -
        /// the same class of mistake as Wildlife Liberation's AI orbiting a cage wall, and the
        /// exact inverse of Rampage, where ramming IS the scoring verb.
        ///
        /// Quarry selection re-runs every <see cref="aiRetargetSeconds"/> and simply takes the
        /// nearest live opponent, so a pilot who flies into a brawl gets picked up by whoever is
        /// closest rather than every AI in the arena converging on one victim. Between samples
        /// the AI keeps flying lead pursuit on the pilot it already picked - the provider is
        /// sampled every frame, so the aim point tracks a live position even though the CHOICE
        /// is re-made on a slow cadence.
        /// </summary>
        void ArmDogfighters()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                IPlayer quarry = null;
                float nextSample = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    var self = captured.Vessel?.Transform;
                    if (self == null) return centre;

                    if (Time.time >= nextSample || !IsLiveOpponent(quarry, captured))
                    {
                        nextSample = Time.time + aiRetargetSeconds;
                        quarry = FindNearestOpponent(captured, self.position);
                    }

                    var quarryStatus = quarry?.Vessel?.VesselStatus;
                    var quarryTf = quarry?.Vessel?.Transform;
                    // No live opponent (a 1v1 mid-respawn, everyone on our own domain) - loiter
                    // toward the arena centre rather than holding a stale or zero target.
                    if (quarryTf == null) return centre;

                    Vector3 quarryPos = quarryTf.position;
                    Vector3 lead = quarryStatus != null
                        ? quarryStatus.Course * (quarryStatus.Speed * aiLeadSeconds)
                        : Vector3.zero;
                    Vector3 aimPoint = quarryPos + lead;

                    Vector3 toQuarry = quarryPos - self.position;
                    if (toQuarry.sqrMagnitude <= aiBreakOffDistance * aiBreakOffDistance)
                    {
                        // Merged: aim THROUGH them and out the far side. Direction of travel is
                        // preserved, so the AI keeps its energy through the pass.
                        Vector3 through = toQuarry.sqrMagnitude > 1e-4f
                            ? toQuarry.normalized
                            : self.forward;
                        aimPoint = quarryPos + through * aiBreakOffDistance;
                    }

                    return aimPoint;
                });
            }
        }

        /// <summary>The nearest player on a different domain with a live vessel.</summary>
        IPlayer FindNearestOpponent(IPlayer self, Vector3 from)
        {
            IPlayer best = null;
            float bestSqr = float.MaxValue;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveOpponent(candidate, self)) continue;

                float sqr = (candidate.Vessel.Transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = candidate;
            }
            return best;
        }

        static bool IsLiveOpponent(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;  // teammates cannot be shot at all
            return candidate.Vessel?.Transform != null;
        }

        // ── Server-authoritative game end ────────────────────────────────────

        /// <summary>
        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
        /// ExecuteServerTurnEnd → SetupNewRound, so _finalResultsSent is set in time to suppress
        /// the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
            if (rule == null) return;

            // Winning domain (highest point sum, Jade→Ruby→Gold tie-break) delegated to the
            // rule; representative winner-name = the best individual contributor on that domain
            // (legacy display field - victory/defeat attribution uses WinnerDomain).
            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s != null && s.Domain == winningDomain)
                .OrderByDescending(s => s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopProgressSampler();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the fight just ended. HasEndGame=false
        /// causes ExecuteServerRoundEnd to call SetupNewRound instead of ExecuteServerGameEnd -
        /// this override prevents the Ready button from reappearing.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ───────────────────────────────────────────────────────

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var pointsArray = new int[count];
            var bulletArray = new int[count];
            var missileArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                bulletArray[i] = statsList[i].BulletHitsLanded;
                missileArray[i] = statsList[i].MissileHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray,
                bulletArray, missileArray, new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] bulletHits,
            int[] missileHits,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[DogFight] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: the scoreboard's secondary line is "N pts, X
                // bullets, Y rockets", and a client that only replicated the total would show
                // every loser's breakdown as 0-0.
                stat.BulletHitsLanded = bulletHits[i];
                stat.MissileHitsLanded = missileHits[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ───────────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _milestone = MilestoneNone;
            _leaderDomain = Domains.Blue;

            StopProgressSampler();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;        // the scored metric AND the milestone trigger
                s.BulletHitsLanded = 0;
                s.MissileHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
