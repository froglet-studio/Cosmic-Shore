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
    /// Undertow - the Scarab-only cavitation duel, and The Bends for the hull whose blast is a
    /// sideways PLATE rather than a cone. Two to four Scarabs hunt each other through Wildlife
    /// Liberation's caged arena with no guns and no balls worth chasing: the juke dash's
    /// cavitation plate - a cylinder sweeping out from the hull and MIRRORED behind it, so it
    /// drags whatever is behind you forward through you (SCARAB.md §3.9) - is the only weapon,
    /// and two things it does score. A rival caught in it takes the plate's all-element decaying
    /// debuff (one BEND, 3 points); a creature whose heart it reaches dies the Squirrel's joust
    /// (one KILL, 1 point). First DOMAIN to the point target wins.
    ///
    /// Structurally a sibling of <see cref="BendsController"/> (1 round / 1 turn, HasEndGame=false,
    /// server winner detection, snapshot SyncFinalScores_ClientRpc, milestone sampler, the hit
    /// latch cleared per match). What it contributes to the platform is small and lands outside
    /// the mode, exactly as The Bends' did:
    ///
    /// <para><b>1. The plate SCORES its debuff, and KILLS through a heart.</b> The Scarab's
    /// cavitation container already carried the debuff (a pilot in the plate has been bent in
    /// every mode since the hull shipped) and NO scoring report and NO lifeform-crystal effect.
    /// Both are added: a <c>VesselCombatHitByExplosionEffectSO</c> stamped Debuff-class with
    /// <c>requireDebuffableVictim</c> (the score follows the effect, The Bends' rule) and an
    /// <c>ExplosionWitherLifeformByCrystalEffectSO</c> (the Sparrow warhead's creature kill, on a
    /// plate). Both land platform-wide - the plate now kills wildlife in Scramble and Tollway too,
    /// where it was already shredding their bodies - and only this mode's rule PAYS for either.</para>
    ///
    /// <para><b>2. An AI Scarab can dash.</b> The juke is stick-driven and inert under autopilot,
    /// so an AI could never fire the plate and an all-AI domain would have been an opponent that
    /// cannot play. <c>ScarabJukeController.TryAutopilotDash</c> runs a committed dash through the
    /// ordinary fire path on the simulating machine, and the plate rides it.</para>
    ///
    /// <para><b>The arena is Wildlife Liberation's, referenced and read-only.</b> The mode wants
    /// what that cell already authors: a very heavy swarm of small creatures, bigger ones, and
    /// the biggest and toughest, roaming one arena-wide band through three concentric cages
    /// that the plate tears through - the cell is per-ARENA, not per-mode (Salvo in the
    /// Boneyard). Its intensity ladder comes with it.</para>
    ///
    /// SCARAB-ONLY is enforced entirely by the arcade card's Vessels list, read by the three
    /// platform layers - no mode-local vessel check.
    /// </summary>
    public class UndertowController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag UndertowSettings.asset - feedback and AI. The point target lives in " +
                 "EndConditionOverridesSO, resolved by UndertowPointTurnMonitor; the point " +
                 "VALUES live on the scoring rule.")]
        [SerializeField] UndertowSettingsSO settings;

        [Tooltip("Drag UndertowScoringRule.asset (metric = CombatPoints, folded with kills).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The caged cell. Read-only to this controller: it supplies the arena CENTRE an " +
                 "AI falls back to and the densest-hostile-mass query the AI dashes at when no " +
                 "rival is near.")]
        [SerializeField] Cell arenaCell;

        const int MilestoneNone = 0;
        const int MilestoneFirst = 1;
        const int MilestoneSecond = 2;

        bool _finalResultsSent;
        Coroutine _progressRoutine;
        int _milestone = MilestoneNone;
        Domains _leaderDomain = Domains.Blue;

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;
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

            // The hit latch is static and Time.time keeps running across a scene load, so a fast
            // rematch could otherwise inherit a claimed window and eat the first bend of the new
            // match. Cleared on every peer: the latch is consulted wherever a plate is simulated.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            DisarmHunters();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only: the setters push through server-write NetworkVariables.</summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].CombatPoints = 0;
                list[i].DebuffHitsLanded = 0;
                list[i].LifeformsKilled = 0;
            }
        }

        // ── Progress milestones (server samples, every peer gets the feedback) ──

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            // The last moment before anyone can score: a late joiner is on the roster by now, so
            // this is the sweep that actually guarantees "everyone starts at 0" in a real lobby.
            ZeroCounters();

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
            var wait = new WaitForSeconds(Mathf.Max(0.1f, settings ? settings.progressSampleSeconds : 0.5f));
            while (!_finalResultsSent)
            {
                SampleProgress();
                yield return wait;
            }
            _progressRoutine = null;
        }

        void SampleProgress()
        {
            if (!IsServer || rule == null || settings == null) return;

            int target = gameData.CombatPointTargetCount;
            if (target <= 0) return;

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue) return;

            int leaderPoints = rule.DomainValue(gameData, leader);
            if (leaderPoints <= 0) return;

            float progress = leaderPoints / (float)target;
            int milestone = progress >= settings.secondMilestoneFraction ? MilestoneSecond
                : progress >= settings.firstMilestoneFraction ? MilestoneFirst
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
                AnnounceLeadChanged_ClientRpc((int)leader, leaderPoints, target);
            }
        }

        [ClientRpc]
        void AnnounceMilestone_ClientRpc(int milestone, int domain, int points, int target)
        {
            GameToastAPI.Post(
                milestone == MilestoneSecond ? GameToastSituation.UndertowHalf : GameToastSituation.UndertowQuarter,
                (Domains)domain, ((Domains)domain).ToString(), points.ToString(), target.ToString());

            // Match-changing event: the alert haptic (Docs/HAPTICS.md). HapticController gates on
            // the local player's own setting, so this is safe on every peer.
            HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int points, int target)
        {
            GameToastAPI.Post(GameToastSituation.UndertowLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), points.ToString(), target.ToString());
        }

        // ── AI (server) ──

        /// <summary>
        /// Every AI Scarab HUNTS: its steering runs at an intercept point ahead of the nearest
        /// opposing pilot (humans preferred by <see cref="UndertowSettingsSO.aiHumanFocus"/>),
        /// and on a slow sample clock it asks its juke for a committed dash whenever that rival
        /// is inside the plate's reach - or, with no rival that close, whenever the densest
        /// hostile mass (the cages, the wildlife) is. The dash direction is toward the target,
        /// projected off the course inside the juke as a pilot's push is; the plate is mirrored,
        /// so which side the target is on does not matter. With no rival at all the AI flies to
        /// the arena centre, where the innermost cage and its wildlife are.
        ///
        /// Steering is <see cref="AIPilot.SetExternalTargetProvider"/> and nothing else, so it
        /// cannot leak into another mode; the dash is
        /// <see cref="ScarabJukeController.TryAutopilotDash"/>, which refuses while the juke is
        /// spent or rolling, so the AI can never fire faster than a human could.
        /// </summary>
        void ArmHunters()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                var juke = captured.Vessel?.Transform != null
                    ? captured.Vessel.Transform.GetComponent<ScarabJukeController>()
                    : null;
                IPlayer rival = null;
                float nextSample = 0f;
                float nextDash = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;
                    Vector3 selfPos = selfTf.position;

                    if (Time.time >= nextSample || !IsLiveOpponent(rival, captured))
                    {
                        nextSample = Time.time + settings.aiRetargetSeconds;
                        rival = FindNearestOpponent(captured, selfPos);
                    }

                    var rivalTf = rival?.Vessel?.Transform;
                    Vector3? rivalPos = rivalTf != null ? rivalTf.position : null;

                    // THE PLATE. A rival inside reach out-bids everything; failing that, the
                    // densest hostile mass - which in this arena is mostly the wildlife's own
                    // bodies and the cage bars they roam through.
                    if (juke != null && Time.time >= nextDash)
                    {
                        nextDash = Time.time + settings.aiDashSampleSeconds;
                        if (rivalPos.HasValue
                            && (rivalPos.Value - selfPos).sqrMagnitude <= settings.aiDashRange * settings.aiDashRange)
                        {
                            juke.TryAutopilotDash(rivalPos.Value - selfPos);
                        }
                        else if (arenaCell)
                        {
                            Vector3 mass = arenaCell.GetExplosionTarget(captured.Domain);
                            if ((mass - selfPos).sqrMagnitude <= settings.aiWildlifeDashRange * settings.aiWildlifeDashRange)
                                juke.TryAutopilotDash(mass - selfPos);
                        }
                    }

                    if (!rivalPos.HasValue) return centre;

                    var rivalStatus = rival.Vessel?.VesselStatus;
                    Vector3 rivalVelocity = rivalStatus != null
                        ? rivalStatus.Course * rivalStatus.Speed
                        : Vector3.zero;
                    return rivalPos.Value + rivalVelocity * settings.aiInterceptLeadSeconds;
                });
            }
        }

        void DisarmHunters()
        {
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
            }
        }

        IPlayer FindNearestOpponent(IPlayer self, Vector3 from)
        {
            IPlayer best = null;
            float bestScore = float.MaxValue;
            float humanFocusSqr = settings.aiHumanFocus * settings.aiHumanFocus;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveOpponent(candidate, self)) continue;

                float score = (candidate.Vessel.Transform.position - from).sqrMagnitude;
                if (!candidate.IsInitializedAsAI) score /= humanFocusSqr;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        static bool IsLiveOpponent(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            // Teammates cannot be bent at all - ExplosionImpactor declines own-domain vessels.
            if (candidate.Domain == self.Domain) return false;
            return candidate.Vessel?.Transform != null;
        }

        // ── Server-authoritative game end ──

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
            if (rule == null) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s != null && s.Domain == winningDomain)
                .OrderByDescending(s => s.CombatPoints + s.LifeformsKilled)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopProgressSampler();
            DisarmHunters();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

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
            var pointsArray = new int[count];
            var bendArray = new int[count];
            var killArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                bendArray[i] = statsList[i].DebuffHitsLanded;
                killArray[i] = statsList[i].LifeformsKilled;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, bendArray, killArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] bends,
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
                    CSDebug.LogError($"[Undertow] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: the scoreboard's secondary line is "N bends · M
                // kills", and a client that only replicated the total would show every loser's
                // breakdown as 0.
                stat.DebuffHitsLanded = bends[i];
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

        // ── Replay ──

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _milestone = MilestoneNone;
            _leaderDomain = Domains.Blue;

            StopProgressSampler();
            DisarmHunters();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;
                s.DebuffHitsLanded = 0;
                s.LifeformsKilled = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
