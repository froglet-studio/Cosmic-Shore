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
    ///      and opens the grazer swarm; 50% floors it at Frenzy and adds the predator.
    ///      Aggression bands, steering, danger immunity and speed all come from the existing
    ///      CellPhase → CellAggressionLevel mapping.
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

        [Tooltip("Fraction of the win target at which the pen opens: the brood pours out in the " +
                 "leader's colour and the cell is floored at Restless. A FRACTION because the " +
                 "race and the trigger are the same axis again (destruction) - move the target " +
                 "and the whole ladder moves with it.")]
        [SerializeField, Range(0.05f, 0.9f)] float broodReleaseFraction = 0.25f;

        [Tooltip("Fraction of the win target at which the predator joins and the cell is " +
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

        // Where the AI's cage stations sit, as a multiple of the shell radius. STRICTLY > 1:
        // AIPilot has no arrive-and-stop behaviour, so any station inside the cage becomes a
        // point the AI orbits from within. Stations live outside; the bars break on the transit.
        const float AiStationStandoff = 1.3f;
        // One strike in N is a raid on live opposing mass instead of the cage.
        const int AiRaidEveryNthStrike = 4;

        // The ladder's three stages. Each maps to (species release tier, containment, phase
        // floor) in ApplyStage - one place, so the three levers can never disagree.
        //   Caged  - the brood is penned in the cage, visible through the bars. It eats the
        //            trail of anything that comes IN and cannot touch the match outside.
        //   Loosed - 25% of the target: containment lifted, cell floored at Restless. The swarm
        //            pours out wearing the leader's colours and hunts the trailing teams' mass.
        //   Pack   - 50%: the predator species joins, cell floored at Frenzy.
        const int StageCaged = 0;
        const int StageLoosed = 1;
        const int StagePack = 2;

        // Species release tiers authored on the fauna configs (grazer 0, predator 1).
        const int SpeciesBrood = 0;
        const int SpeciesPack = 1;

        bool _finalResultsSent;
        Coroutine _ladderRoutine;
        int _stage = StageCaged;
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

            // Pen the brood on every peer before anything can tick: fauna are client-local, so
            // each machine must contain its own cage until the ladder opens it. (The cell also
            // seeds its release tier from the spawn profile at config-assign time, which is what
            // makes the START state independent of this call winning the race - see
            // SpawnProfileSO.InitialFaunaReleaseTier.)
            ApplyStage(StageCaged);
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

            // Leading domain by the scoring metric (hostile prisms destroyed), Jade→Ruby→Gold
            // on ties (fixed order, so every machine would agree - though only the server ever
            // computes this). The brood hatches in this colour and the legacy herbivore diet
            // then points it at every trailing team's mass.
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

            // The rungs ride the LEADER's own progress toward the win target: the race and the
            // trigger are one axis, so "the leader is a quarter of the way out" is the signal.
            float progress = best / (float)target;
            int stage = progress >= packReleaseFraction ? StagePack
                : progress >= broodReleaseFraction ? StageLoosed
                : StageCaged;

            bool leaderChanged = leader != _leader;
            bool stageChanged = stage != _stage;
            if (!leaderChanged && !stageChanged) return;

            _leader = leader;
            PublishLeader_ClientRpc((int)leader);

            if (stageChanged)
            {
                _stage = stage;
                PublishRelease_ClientRpc(stage, (int)leader, best, target);
            }
            else if (leaderChanged && stage > StageCaged)
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
        void PublishRelease_ClientRpc(int stage, int domain, int sum, int target)
        {
            ApplyStage(stage);

            var d = (Domains)domain;
            if (stage == StageLoosed)
                GameToastAPI.Post(GameToastSituation.RibcageBroodReleased, d,
                    d.ToString(), sum.ToString(), target.ToString());
            else if (stage == StagePack)
                GameToastAPI.Post(GameToastSituation.RibcagePackReleased, d,
                    d.ToString(), sum.ToString(), target.ToString());

            // Rung reached: shake the device hard for ~1.2s. This is the game's THIRD haptic feel
            // and the only thing that fires it - added deliberately per Docs/HAPTICS.md ▸ "Adding
            // / changing a feel" (dedicated method + extended gate, never the silenced legacy
            // API). It is safe to call on every peer: HapticController gates on the local
            // player's own haptics setting, so each human device buzzes once and nothing else
            // does. Toast copy is unauthored today, so right now this IS the release feedback.
            if (stage > StageCaged) HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeaderChanged_ClientRpc(int domain, int sum, int target)
        {
            var d = (Domains)domain;
            GameToastAPI.Post(GameToastSituation.RibcageLeaderChanged, d,
                d.ToString(), sum.ToString(), target.ToString());
        }

        /// <summary>
        /// Applies a ladder STAGE to the local cell - the one place the three levers are set
        /// together, so they can never disagree:
        ///   • which species may seed   (Cell.FaunaReleaseTier vs FaunaConfigurationSO.ReleaseTier)
        ///   • whether the brood is penned (Cell.FaunaContainmentRadius)
        ///   • how hard the cell runs   (Cell.ModePhaseFloor → CellAggressionLevel)
        /// Runs on every peer because fauna are client-local.
        /// </summary>
        void ApplyStage(int stage)
        {
            if (!arenaCell)
            {
                // Fail loud: a silent return here leaves the cell at its authored stage and the
                // ladder simply never advances, which reads in play as "the fauna reward never
                // arrived" with nothing in the log to explain it.
                CSDebug.LogError("[Ribcage] arenaCell is not wired on RibcageController - the fauna " +
                                 "ladder cannot publish. Assign the scene's Cell in the inspector.");
                return;
            }

            switch (stage)
            {
                case StagePack:
                    arenaCell.FaunaReleaseTier = SpeciesPack;      // predator joins the grazers
                    arenaCell.FaunaContainmentRadius = 0f;
                    arenaCell.ContainmentIntruderFrenzy = false;
                    arenaCell.ModePhaseFloor = CellPhase.Frenzy;   // any-colour steering, no friendly avoidance, danger-immune
                    break;

                case StageLoosed:
                    arenaCell.FaunaReleaseTier = SpeciesBrood;
                    arenaCell.FaunaContainmentRadius = 0f;         // the pen is open - the swarm pours out
                    arenaCell.ContainmentIntruderFrenzy = false;
                    arenaCell.ModePhaseFloor = CellPhase.Restless; // hunt the opposing-colour centroid = the trailing teams
                    break;

                default: // StageCaged
                    arenaCell.FaunaReleaseTier = SpeciesBrood;     // the brood exists from the start...
                    // ...penned INSIDE the shell. ContainmentRadius, not ShellRadius: the pen sits
                    // just inside the bone so the cage's own prisms - including the unshielded
                    // DANGER traps - are outside it and can never be eaten or read as an intruder.
                    arenaCell.FaunaContainmentRadius = SpawnableRibcage.ContainmentRadius;
                    // Fly in and the whole pen goes berserk (Frenzy) until you and your mass leave.
                    arenaCell.ContainmentIntruderFrenzy = true;
                    arenaCell.ModePhaseFloor = null;               // otherwise Calm: they idle at the core
                    break;
            }

            // Realign the fauna spawn clock to the RELEASE moment. Without this the stage
            // advances mid-period and the reinforcement wave can take a full BaseFaunaSpawnTime
            // to appear, which reads as the reward simply not arriving. Not done for the caged
            // stage: that one is the cell's own bootstrap and must not restart its clock.
            // The profile authors no flora, so the "restart re-runs the initial flora batch"
            // caveat on RestartSpawnerForMode does not apply here.
            if (stage > StageCaged) arenaCell.RestartSpawnerForMode();
        }

        void ReleaseCellOverrides()
        {
            if (!arenaCell) return;
            arenaCell.SetModeControlOverride(null);
            arenaCell.ModePhaseFloor = null;
            arenaCell.FaunaContainmentRadius = 0f;
            arenaCell.ContainmentIntruderFrenzy = false;
            arenaCell.FaunaReleaseTier = int.MaxValue;
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
            _stage = StageCaged;
            _leader = Domains.Blue;

            StopLadder();
            ApplyStage(StageCaged);
            if (arenaCell) arenaCell.SetModeControlOverride(null);

            foreach (var s in gameData.RoundStatsList)
            {
                s.HostilePrismsDestroyed = 0;   // the scored metric AND the ladder trigger
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
