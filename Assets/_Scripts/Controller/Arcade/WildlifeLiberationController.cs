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
    /// Wildlife Liberation - the Sparrow-only hunt. Three concentric cages at 1050 / 600 / 200
    /// pen three tiers of wildlife (a huge swarm of small creatures outside, much bigger ones in
    /// the middle room, the biggest and toughest in the core). Break in and shoot; the first
    /// PLAYER to the kill target wins.
    ///
    /// Structurally a sibling of <see cref="RampageController"/> / <see cref="RibcageController"/>
    /// (1 round / 1 turn, HasEndGame=false, server winner detection in OnTurnEndedCustom,
    /// snapshot SyncFinalScores_ClientRpc), with three deliberate differences:
    ///
    ///   1. THE WINNER IS A PLAYER, NOT A DOMAIN. Everything else in this family races domain
    ///      sums; this one is a free-for-all, and <see cref="WildlifeLiberationScoringRuleSO"/>
    ///      owns that distinction. The domain sums are still synced and still shown on the HUD
    ///      (the base class's SyncDomainSumsRoutine, untouched) as a secondary "how is my colour
    ///      doing" readout. See WILDLIFE_LIBERATION.md.
    ///   2. THE SCORE COMES FROM THE ECOLOGY. The stat is
    ///      <see cref="IRoundStats.LifeformsKilled"/>, fed by the platform kill path
    ///      (<c>Fauna.OnBodyPrismExploded</c> → sealed <c>Die</c> → <c>CellRuntimeDataSO.OnFaunaKilled</c>
    ///      → <c>StatsManager</c>), so like Rampage and Ribcage there is no per-event listener
    ///      here at all. Only PLAYER-attributed kills count - the swarm starving or a shark
    ///      eating a tadpole credits nobody.
    ///   3. AI HUNTERS WORK THE ROOMS. <see cref="ArmHunters"/> walks each AI Sparrow through the
    ///      three rooms rather than orbiting one; see that method for why the waypoints must sit
    ///      INSIDE a room and not on a wall.
    ///
    /// SPARROW-ONLY is enforced entirely by platform machinery and deliberately not re-implemented
    /// here - three independent layers, because in Ribcage one was not enough and a client walked
    /// in flying a Dolphin:
    ///   (a) <c>GameDataSO.SyncFromArcadeGame</c> clamps the launching machine's selection;
    ///   (b) <c>ServerPlayerVesselInitializer.ResolveSpawnVesselType</c> re-clamps SERVER-SIDE at
    ///       spawn, which is the one that catches a client whose owner-write NetDefaultVesselType
    ///       still carries the hull it last flew;
    ///   (c) <c>ServerPlayerVesselInitializerWithAI</c> clamps the AI's scene-authored class too,
    ///       so a mis-authored aiInitializeDatas cannot field an illegal opponent.
    /// All three read the single <c>Vessels</c> list on ArcadeGameWildlifeLiberation.
    /// </summary>
    public class WildlifeLiberationController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag WildlifeLiberationScoringRule.asset - the per-mode scoring strategy. " +
                 "This one resolves a winning PLAYER rather than a winning domain.")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The jail cell. Read-only to this controller: it supplies the arena CENTRE the " +
                 "AI hunters work around. The cages themselves come from the cell's config for " +
                 "the selected intensity (CellTypeChoiceOptions.IntensityWise).")]
        [SerializeField] Cell arenaCell;

        [Tooltip("Fraction of the kill target at which the LEADING HUNTER crosses the first " +
                 "milestone (toast + alert haptic). A FRACTION, so moving the target moves the " +
                 "milestones with it. Feedback only - no game state changes here.")]
        [SerializeField, Range(0.05f, 0.9f)] float firstMilestoneFraction = 0.25f;

        [Tooltip("Fraction of the kill target at which the leading hunter crosses the second " +
                 "milestone. Feedback only.")]
        [SerializeField, Range(0.1f, 0.95f)] float secondMilestoneFraction = 0.5f;

        [Tooltip("Seconds between server-side progress samples. Milestones are a coarse state " +
                 "machine, so this does not need to be per-frame.")]
        [SerializeField, Min(0.1f)] float progressSampleSeconds = 0.5f;

        [Header("AI")]
        [Tooltip("Seconds between AI hunter waypoint refreshes - how long an AI Sparrow works " +
                 "one patch of a room before moving on.")]
        [SerializeField, Min(0.25f)] float aiRetargetSeconds = 2.5f;

        // How long an AI works one ROOM before descending to the next. Long enough that it
        // actually hunts what is in there rather than commuting between cages forever.
        const int AiWaypointsPerRoom = 4;

        // One AI waypoint in N is a hunt on live mass hostile to its domain rather than a
        // patrol point - Cell.GetExplosionTarget, the same density query aggression-1 fauna use.
        // In this arena the densest hostile mass is usually a creature swarm, so this is what
        // makes an AI Sparrow visibly go FOR the wildlife instead of sweeping empty space.
        const int AiHuntEveryNthWaypoint = 2;

        // Milestone rungs the leading hunter crosses. Feedback only - nothing here changes game
        // state, so a missed or late sample costs a toast, never a rule.
        const int MilestoneNone = 0;
        const int MilestoneFirst = 1;
        const int MilestoneSecond = 2;

        bool _finalResultsSent;
        Coroutine _progressRoutine;
        int _milestone = MilestoneNone;
        string _leaderName = string.Empty;

        // Golf: the winning hunter carries their finish time, everyone else a
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
            _leaderName = string.Empty;

            // Belt-and-braces against the Ribcage regression where players started a match on a
            // non-zero score. The authoritative reset is
            // ServerPlayerVesselInitializer.PrepareForNewScene (unconditional, once per player,
            // on the processing path) - this is a second, cheap sweep at the one moment every
            // peer agrees the match has not started, because RoundStats lives on the PERSISTENT
            // Player object and a stat that survives is worth zeroing twice rather than never.
            if (IsServer) ZeroKillCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Zeroes the scored stat on every roster entry. Server-only: the setter pushes through
        /// the server-write NetworkVariable and replication clears every client's mirror, so a
        /// client zeroing its own would just be overwritten (and would desync until the next
        /// delta).
        /// </summary>
        void ZeroKillCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null)
                    list[i].LifeformsKilled = 0;
        }

        // ── Progress milestones (server samples, every peer gets the feedback) ──

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            // The last moment before anyone can score. A player who joined late, or whose
            // PrepareForNewScene landed before their RoundStats had replicated its name, is on
            // the roster by now - so this is the sweep that actually guarantees "everyone starts
            // at 0" in a real lobby.
            ZeroKillCounters();

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            ArmHunters();

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
        /// Server-side sampler: who is leading the hunt and how far along they are. Both are
        /// coarse states, so a half-second cadence is ample and costs one roster scan.
        /// </summary>
        void SampleProgress()
        {
            if (!IsServer || rule is not WildlifeLiberationScoringRuleSO hunt) return;

            int target = gameData.LifeformTargetCount;
            if (target <= 0) return; // monitor hasn't resolved the target yet

            var leader = hunt.ResolveWinningHunter(gameData);
            // Nobody has killed anything yet - no leader to announce rather than handing the
            // lead to whoever sorts first on a 0-0 tie-break.
            if (leader == null || leader.LifeformsKilled <= 0) return;

            float progress = leader.LifeformsKilled / (float)target;
            int milestone = progress >= secondMilestoneFraction ? MilestoneSecond
                : progress >= firstMilestoneFraction ? MilestoneFirst
                : MilestoneNone;

            bool leaderChanged = leader.Name != _leaderName;
            bool milestoneChanged = milestone != _milestone;
            if (!leaderChanged && !milestoneChanged) return;

            _leaderName = leader.Name;

            if (milestoneChanged)
            {
                _milestone = milestone;
                if (milestone > MilestoneNone)
                    AnnounceMilestone_ClientRpc(milestone, (int)leader.Domain,
                        new FixedString64Bytes(leader.Name), leader.LifeformsKilled, target);
            }
            else if (leaderChanged && milestone > MilestoneNone)
            {
                // The lead changes hands late in the hunt - worth calling out.
                AnnounceLeadChanged_ClientRpc((int)leader.Domain,
                    new FixedString64Bytes(leader.Name), leader.LifeformsKilled, target);
            }
        }

        [ClientRpc]
        void AnnounceMilestone_ClientRpc(int milestone, int domain, FixedString64Bytes hunter, int kills, int target)
        {
            GameToastAPI.Post(
                milestone == MilestoneSecond
                    ? GameToastSituation.WildlifeHuntHalf
                    : GameToastSituation.WildlifeHuntQuarter,
                (Domains)domain, hunter.ToString(), kills.ToString(), target.ToString());

            // Milestone reached: the alert haptic (the game's third feel, fenced to
            // match-changing events - see Docs/HAPTICS.md). Safe on every peer: HapticController
            // gates on the local player's own haptics setting.
            HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, FixedString64Bytes hunter, int kills, int target)
        {
            GameToastAPI.Post(GameToastSituation.WildlifeLeadChanged, (Domains)domain,
                hunter.ToString(), kills.ToString(), target.ToString());
        }

        // ── AI hunters (server) ──────────────────────────────────────────────

        /// <summary>
        /// Drives every AI Sparrow as a hunter that works the ROOMS.
        ///
        /// The waypoint rule here is the exact inverse of Ribcage's, and for the same underlying
        /// reason. <c>AIPilot</c> has no arrive-and-stop behaviour - it steers at its target
        /// forever and flies through on arrival - so a target's placement decides where the AI
        /// LIVES. Ribcage wants its AI outside the bone (damage happens on the transit), so its
        /// stations sit beyond the shell. Here the prey is INSIDE the rooms, so a station on a
        /// wall would make the AI orbit the wall and never hunt: every waypoint is placed at the
        /// MIDDLE of a room's radial band, where the creatures actually are.
        ///
        /// Each AI works one room for <see cref="AiWaypointsPerRoom"/> waypoints and then steps
        /// inward, cycling outer → middle → core → OPEN WATER → outer (four rooms, including the
        /// stocked water outside the cages), so a full lobby is spread through the whole arena
        /// rather than queueing in one swarm. Waypoints walk a golden-angle spiral, so
        /// successive ones are ~137° apart and it keeps finding fresh, un-hunted wildlife.
        ///
        /// Every other waypoint is a HUNT on <see cref="Cell.GetExplosionTarget"/> - the densest
        /// mass hostile to the AI's domain. In an arena whose mass is mostly creature bodies that
        /// resolves to a swarm, so the AI visibly goes for the wildlife rather than sweeping
        /// empty space. It is the same density query the ecology already runs; no bespoke
        /// targeting exists here.
        ///
        /// Kept deliberately beatable: one waypoint per aiRetargetSeconds (2.5s), so an AI hunter
        /// is methodical rather than twitchy.
        /// </summary>
        void ArmHunters()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            int seat = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                // Phase each AI onto its own arc AND its own starting room, so four AIs do not
                // all begin in the outer swarm.
                float phase = seat * Mathf.PI * 2f / 4f;
                int startRoom = seat % SpawnableWildlifeCage.RoomCount;
                int seatIndex = seat;
                seat++;

                int beat = 0;
                Vector3 cached = centre;
                float nextSample = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    if (Time.time < nextSample) return cached;
                    nextSample = Time.time + aiRetargetSeconds;

                    int waypoint = beat++;

                    // Hunt beat: go at the densest hostile mass (usually a swarm). Offset by seat
                    // so the AIs don't all switch to hunting on the same beat.
                    if (arenaCell != null && (waypoint + seatIndex) % AiHuntEveryNthWaypoint == 0)
                    {
                        cached = arenaCell.GetExplosionTarget(captured.Domain);
                        return cached;
                    }

                    // Patrol beat: the middle of the current room's band, on a golden-angle
                    // spiral. INSIDE the room by construction - see the summary.
                    int room = (startRoom + waypoint / AiWaypointsPerRoom) % SpawnableWildlifeCage.RoomCount;
                    float mid = 0.5f * (SpawnableWildlifeCage.BandInner(room) + SpawnableWildlifeCage.BandOuter(room));

                    float a = phase + waypoint * 2.39996323f;
                    float y = 1f - 2f * ((waypoint * 0.37f) % 1f);
                    float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    var dir = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));

                    cached = centre + dir * mid;
                    return cached;
                });
            }
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
            if (rule is not WildlifeLiberationScoringRuleSO hunt) return;

            var champion = hunt.ResolveWinningHunter(gameData);
            if (champion == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            // The rule gives the finish time to the winning PLAYER; the domain argument only
            // colours the banner.
            hunt.AssignScores(gameData, champion.Domain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopProgressSampler();
            SyncFinalScoresSnapshot(champion.Name, champion.Domain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the hunt just ended. HasEndGame=false
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
            var killsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                killsArray[i] = statsList[i].LifeformsKilled;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, killsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] kills,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[WildlifeLiberation] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.LifeformsKilled = kills[i];
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
            _leaderName = string.Empty;

            StopProgressSampler();

            foreach (var s in gameData.RoundStatsList)
            {
                s.LifeformsKilled = 0;   // the scored metric AND the milestone trigger
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
