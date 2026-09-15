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
    /// PeelTheCage (player-facing "Peel the Cage") - the Rhino-only cage-breaking race. Domains race
    /// to smash a LAYERED ORANGE of prism bone; the first to the destruction target has peeled
    /// its way out and wins. Structurally a sibling of <see cref="RampageController"/> (1 round /
    /// 1 turn, HasEndGame=false, server winner detection in OnTurnEndedCustom, snapshot
    /// SyncFinalScores_ClientRpc) - the destruction stat itself auto-increments via
    /// StatsManager.PrismDestroyed, so no per-event listener lives here either.
    ///
    /// The arena's difficulty is authored, not driven from here: intensity picks the
    /// <c>CellConfigDataSO</c> (<c>CellTypeChoiceOptions.IntensityWise</c>) and therefore the
    /// <see cref="SpawnableRibcage"/> variant, which is how one rind becomes four. This
    /// controller adds only two things over Rampage:
    ///
    ///   1. PROGRESS MILESTONES. At a quarter and a half of the win target the leading domain
    ///      crosses a rung, which fires a toast and the alert haptic on every peer. These are
    ///      pure FEEDBACK - they change no game state - and they exist so the match has an
    ///      audible/tactile shape between "go" and "won".
    ///   2. AI CAGE-BREAKERS. AIPilot has no arrive-and-stop behaviour, so the AI needs stations
    ///      OUTSIDE the shell and damage on the transit (see ArmCageBreakers).
    ///
    /// HISTORY - this mode used to run a FAUNA LADDER here: the cell's controlling domain was
    /// pinned to the race leader (<see cref="Cell.SetModeControlOverride"/>) so the penned brood
    /// hatched in the leader's colours and the untouched legacy herbivore diet turned it loose on
    /// every trailing team's mass, with the same two rungs opening the pen and adding a predator.
    /// The fauna were removed from the level on request (2026-08), so that machinery is gone from
    /// this controller - but the PLATFORM capabilities it was built on are deliberately kept
    /// (Cell.SetModeControlOverride / ModePhaseFloor / FaunaReleaseTier / FaunaContainmentRadius,
    /// SpawnProfileSO.InitialFaunaReleaseTier). They are general, documented, and one data change
    /// away from bringing the ladder back. See PEEL_THE_CAGE.md and Docs/ECOSYSTEM.md §22.
    /// </summary>
    public class PeelTheCageController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag PeelTheCageScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The cage cell. Read-only to this controller now: it supplies the arena CENTRE " +
                 "the AI orbits and the density-grid raid target (Cell.GetExplosionTarget). The " +
                 "cage geometry itself comes from the cell's config for the selected intensity.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("Fraction of the win target at which the leading domain crosses the first " +
                 "milestone (toast + alert haptic). A FRACTION, so moving the target moves the " +
                 "milestones with it. Feedback only - no game state changes here.")]
        [SerializeField, Range(0.05f, 0.9f)] float firstMilestoneFraction = 0.25f;

        [Tooltip("Fraction of the win target at which the leading domain crosses the second " +
                 "milestone. Feedback only.")]
        [SerializeField, Range(0.1f, 0.95f)] float secondMilestoneFraction = 0.5f;

        [Tooltip("Seconds between server-side progress samples. Milestones are a coarse state " +
                 "machine, so this does not need to be per-frame.")]
        [SerializeField, Min(0.1f)] float progressSampleSeconds = 0.5f;

        [Header("AI")]
        [Tooltip("Seconds between AI cage-breaker target refreshes - how often each AI Rhino " +
                 "picks a fresh stretch of bone to ram.")]
        [SerializeField, Min(0.25f)] float aiRetargetSeconds = 2f;

        [Tooltip("Cage shell radius the AI aims at. 0 = use SpawnableRibcage.ShellRadius (the " +
                 "value the generator actually builds at). Override only for a resized arena.")]
        [SerializeField, Min(0f)] float aiCageRadiusOverride = 0f;

        // Where the AI's cage stations sit, as a multiple of the shell radius. STRICTLY > 1:
        // AIPilot has no arrive-and-stop behaviour, so any station inside the cage becomes a
        // point the AI orbits from within. Stations live outside; the bars break on the transit.
        const float AiStationStandoff = 1.3f;
        // One strike in N is a raid on live opposing mass instead of the cage.
        const int AiRaidEveryNthStrike = 4;

        // Milestone rungs the leading domain crosses. Feedback only - nothing here changes
        // game state, so a missed or late sample costs a toast, never a rule.
        const int MilestoneNone = 0;
        const int MilestoneFirst = 1;   // firstMilestoneFraction of the win target
        const int MilestoneSecond = 2;  // secondMilestoneFraction

        bool _finalResultsSent;
        Coroutine _progressRoutine;
        int _milestone = MilestoneNone;
        Domains _leader = Domains.Blue;

        // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
        // (see PeelTheCageScoringRuleSO.AssignScores) - lower is better, like SkimRace.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // PeelTheCage handles end-game through OnTurnEndedCustom (server-side winner detection) →
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
            _milestone = MilestoneNone;
            _leader = Domains.Blue;
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            base.OnNetworkDespawn();
        }

        // ── Progress milestones (server samples, every peer gets the feedback) ──

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            ArmCageBreakers();

            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

        void StopProgressSampler()
        {
            if (_progressRoutine == null) return;
            StopCoroutine(_progressRoutine);
            _progressRoutine = null;
        }

        /// <summary>
        /// Server-side sampler: tracks who leads and how far the leader has got, and announces
        /// the crossings. Both are coarse states, so a half-second cadence is ample and costs one
        /// SumByDomain per active domain per sample.
        /// </summary>
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

        void SampleProgress()
        {
            if (!IsServer || rule == null) return;

            int target = gameData.PrismTargetCount;
            if (target <= 0) return; // monitor hasn't resolved the target yet

            // Leading domain by the scoring metric (hostile prisms destroyed), Jade→Ruby→Gold
            // on ties (fixed order, so every machine would agree - though only the server ever
            // computes this).
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

            // Nobody has broken a bar yet - no leader to announce rather than handing the lead
            // to Jade for winning a 0-0 tie-break.
            if (leader == Domains.Blue || best <= 0) return;

            // Rungs ride the LEADER's own progress toward the win target, so they land at a fixed
            // point in the RACE rather than at a point a busy lobby reaches several times faster.
            float progress = best / (float)target;
            int milestone = progress >= secondMilestoneFraction ? MilestoneSecond
                : progress >= firstMilestoneFraction ? MilestoneFirst
                : MilestoneNone;

            bool leaderChanged = leader != _leader;
            bool milestoneChanged = milestone != _milestone;
            if (!leaderChanged && !milestoneChanged) return;

            _leader = leader;

            if (milestoneChanged)
            {
                _milestone = milestone;
                if (milestone > MilestoneNone)
                    AnnounceMilestone_ClientRpc(milestone, (int)leader, best, target);
            }
            else if (leaderChanged && milestone > MilestoneNone)
            {
                // The lead changes hands late in the race - worth calling out.
                AnnounceLeaderChanged_ClientRpc((int)leader, best, target);
            }
        }

        [ClientRpc]
        void AnnounceMilestone_ClientRpc(int milestone, int domain, int sum, int target)
        {
            var d = (Domains)domain;
            GameToastAPI.Post(
                milestone == MilestoneSecond
                    ? GameToastSituation.PeelTheCageHalfPeeled
                    : GameToastSituation.PeelTheCageQuarterPeeled,
                d, d.ToString(), sum.ToString(), target.ToString());

            // Milestone reached: shake the device hard for ~1.2s. This is the game's THIRD haptic
            // feel and the only thing that fires it - added deliberately per Docs/HAPTICS.md ▸
            // "Adding / changing a feel" (dedicated method + extended gate, never the silenced
            // legacy API). It is safe to call on every peer: HapticController gates on the local
            // player's own haptics setting, so each human device buzzes once and nothing else
            // does. Toast copy is unauthored today, so right now this IS the milestone feedback.
            HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeaderChanged_ClientRpc(int domain, int sum, int target)
        {
            var d = (Domains)domain;
            GameToastAPI.Post(GameToastSituation.PeelTheCageLeaderChanged, d,
                d.ToString(), sum.ToString(), target.ToString());
        }

        // ── AI cage-breakers (server) ────────────────────────────────────

        /// <summary>
        /// Drives every AI Rhino as a cage-breaker that works from the OUTSIDE.
        ///
        /// The first version aimed straight at a point ON the shell, which is why the AI lived
        /// inside the cage: a vessel that flies to a point on a sphere does not stop there, it
        /// carries through, and the next shell point is across the middle - so it just rattled
        /// around the interior. The fix is that a strike is TWO waypoints, not one: an APPROACH
        /// point out beyond the shell on the target bar's radial, then a PUNCH point just inside
        /// it on the same radial. The vessel therefore arrives from outside, crosses the bone
        /// roughly perpendicular (which is what breaks bars), exits, and swings out for the next
        /// one. Successive strikes walk a golden-angle spiral so it never re-rams a hole.
        ///
        /// Every few strikes it RAIDS instead - <see cref="Cell.GetExplosionTarget"/>, the densest
        /// mass hostile to its domain, which since the shielded-grid change means opponents'
        /// trails and anything a rival left inside the cage. That is where "sometimes it goes
        /// inside / hits opponent prisms" comes from, and it is a real strategy rather than a
        /// scripted detour: the same density query aggression-1 fauna use.
        ///
        /// Kept deliberately beatable: one waypoint per aiRetargetSeconds (2s), so it is
        /// methodical rather than twitchy, and the raid share is a minority of strikes.
        /// </summary>
        void ArmCageBreakers()
        {
            float shell = aiCageRadiusOverride > 0f ? aiCageRadiusOverride : SpawnableRibcage.ShellRadius;
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            int seat = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                // Phase each AI onto its own arc so a full lobby spreads around the sphere
                // instead of queueing at one rib.
                float phase = seat * Mathf.PI * 2f / 4f;
                int seatIndex = seat;
                seat++;

                int beat = 0;                 // advances one waypoint per sample
                Vector3 cached = centre;
                float nextSample = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    if (Time.time < nextSample) return cached;
                    nextSample = Time.time + aiRetargetSeconds;

                    int strike = beat++;

                    // Every 4th strike is a raid on live opposing mass instead of the cage.
                    // Offset by seat so the AIs don't all raid on the same beat.
                    if (arenaCell != null && (strike + seatIndex) % AiRaidEveryNthStrike == 0)
                    {
                        cached = arenaCell.GetExplosionTarget(captured.Domain);
                        return cached;
                    }

                    // Golden-angle spiral over the sphere: successive strikes are ~137 degrees
                    // apart, so the CHORD between one station and the next passes close to the
                    // centre - a full crossing of the cage, which is what shatters bars. It is
                    // also deterministic and never repeats a spot, so it keeps finding intact bone.
                    float a = phase + strike * 2.39996323f;
                    float y = 1f - 2f * ((strike * 0.37f) % 1f);
                    float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    var dir = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));

                    // ALWAYS outside the shell. This is the whole fix: AIPilot steers at its
                    // target forever and simply flies through on arrival, so a target INSIDE the
                    // cage means an AI that loops around that interior point indefinitely - which
                    // is exactly what "the AI just stays inside" was. Park the station outside and
                    // the loitering happens outside; the damage happens on the transit between
                    // stations, which crosses the bone twice.
                    cached = centre + dir * (shell * AiStationStandoff);
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
            StopProgressSampler();
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
                    CSDebug.LogError($"[PeelTheCage] Client could not match RoundStats for '{sName}'. " +
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
            _milestone = MilestoneNone;
            _leader = Domains.Blue;

            StopProgressSampler();

            foreach (var s in gameData.RoundStatsList)
            {
                s.HostilePrismsDestroyed = 0;   // the scored metric AND the milestone trigger
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
