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
    /// Broadside - the ARENA brawl, and Regatta's fighting twin. Regatta asked what every hull
    /// does with a racing line; this asks what every hull does with a rival in front of it.
    /// Seven hulls loose in Dog Fight's Boneyard, each fighting with the weapon it actually
    /// has, and the first DOMAIN to the point target wins on <c>ScoringMetric.CombatPoints</c>.
    ///
    /// <para><b>The mode's premise is that a hit is priced by its VERB, never by its hull.</b>
    /// Seven kits reach one score by four routes - a round that connects, a contact strike, an
    /// area debuff, a rocket - and <see cref="BroadsideScoringRuleSO"/> prices each by what it
    /// costs the pilot who landed it. No hull is named in the scoring path, so the card can gain
    /// a hull without the rule learning anything.</para>
    ///
    /// <para><b>What it contributed to the platform, and why the mode needed it.</b> A brawl the
    /// whole fleet can enter has to be able to score a BLADE, and the platform could only score
    /// a gun, a rocket and a blast. Measured on the shipped containers, only four of eight hulls
    /// could land a scoreable hit at all:</para>
    /// <list type="bullet">
    /// <item>A new <c>CombatHitClass.Strike</c> - a contact hit, the fourth verb, unranked like
    /// Bullet and Debuff. It is a VERB and not a hull: the Rhino's sword and the Squirrel's
    /// joust share it because both answer "I flew into them".</item>
    /// <item><c>VesselCombatHitBySkimmerEffectSO</c> - the skimmer sibling of the projectile and
    /// explosion reporters, and the one that had to solve AUTHORITY differently: a skimmer
    /// overlap is observed on EVERY peer, so it gates on the machine that owns the striker.</item>
    /// <item>The Urchin's spike container had <c>projectileShipEffects: []</c> - a spike passed
    /// through a rival pilot and did nothing at all, the same empty-container gap Dog Fight
    /// found in the skyburst and The Bends in the Dolphin's cone.</item>
    /// <item><c>CombatHitScoring</c>'s raw tally lost its else-arm, which had been filing a
    /// Rhino's SWORD as a bullet - on a hull with no gun.</item>
    /// </list>
    /// <para>All of it is counted platform-wide and PAID only here, the split Dog Fight
    /// established: a Rhino sword scores in this mode and nowhere else.</para>
    ///
    /// <para><b>The Serpent is deliberately not on the card.</b> It has no anti-vessel verb
    /// authored at all (0/4 abilities), so listing it would seat a pilot who cannot score.
    /// Giving it one is a /vessel job, not a mode's.</para>
    ///
    /// <para><b>The arena is Dog Fight's Boneyard, referenced and read-only</b> - the cell is
    /// per-ARENA, not per-mode (Salvo reuses the same one). It happens to feed every hull's
    /// economy, which is why a mixed fleet can live in it: a Squirrel skims the wreckage for
    /// boost, an Urchin grinds it, a Dolphin skims it for seed energy, a Sparrow's rounds pay
    /// ammo off it, a Rhino's sword has mass to cut and a Scarab forges balls from its crystals.
    /// Its intensity ladder comes with it.</para>
    ///
    /// Structurally a sibling of <see cref="UndertowController"/> and <c>DogFightController</c>:
    /// 1 round / 1 turn, HasEndGame=false, server winner detection, snapshot
    /// SyncFinalScores_ClientRpc, milestone sampler, the hit latch cleared per match.
    /// </summary>
    public class BroadsideController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag BroadsideSettings.asset - feedback and AI. The point target lives in " +
                 "EndConditionOverridesSO, resolved by BroadsidePointTurnMonitor; the point " +
                 "VALUES live on the scoring rule.")]
        [SerializeField] BroadsideSettingsSO settings;

        [Tooltip("Drag BroadsideScoringRule.asset (metric = CombatPoints, priced per verb).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Boneyard cell. Read-only to this controller: it supplies the arena CENTRE " +
                 "an AI falls back to when it has no rival to hunt.")]
        [SerializeField] Cell arenaCell;

        const int MilestoneNone = 0;
        const int MilestoneFirst = 1;
        const int MilestoneSecond = 2;

        bool _finalResultsSent;
        Coroutine _progressRoutine;
        readonly List<Coroutine> _triggerRoutines = new();
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
            // rematch could otherwise inherit a claimed window and eat the first hit of the new
            // match. Cleared on every peer: the latch is consulted wherever a weapon is simulated.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            DisarmTriggers();
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
                list[i].BulletHitsLanded = 0;
                list[i].StrikeHitsLanded = 0;
                list[i].DebuffHitsLanded = 0;
                list[i].MissileHitsLanded = 0;
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
            ArmTriggers();

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
                milestone == MilestoneSecond ? GameToastSituation.BroadsideHalf : GameToastSituation.BroadsideQuarter,
                (Domains)domain, ((Domains)domain).ToString(), points.ToString(), target.ToString());

            // Match-changing event: the alert haptic (Docs/HAPTICS.md). HapticController gates on
            // the local player's own setting, so this is safe on every peer.
            HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int points, int target)
        {
            GameToastAPI.Post(GameToastSituation.BroadsideLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), points.ToString(), target.ToString());
        }

        // ── AI (server) ──

        /// <summary>
        /// <b>Every AI HUNTS through the platform, and only the TRIGGER is per hull.</b>
        ///
        /// <para>Steering is not this controller's business at all: Broadside joins Joust and Dog
        /// Fight in <c>ServerPlayerVesselInitializerWithAI</c>'s seek-players set, so every AI
        /// already chases the live position of a chosen opponent whatever hull it drew. That is
        /// the right shared behaviour here because most of this roster's weapons want exactly
        /// that - a Rhino's sword, a Squirrel's joust and a Manta's bomb are all landed by
        /// ARRIVING, and a Sparrow's AI fires its guns and rockets on its own timer once someone
        /// is in front of it.</para>
        ///
        /// <para>Two hulls need a trigger pulled, because their weapon is a stick gesture that is
        /// inert under autopilot - the Tollway rule (an all-AI domain that cannot play is a
        /// defect):</para>
        /// <list type="bullet">
        /// <item><b>Scarab</b> - <c>ScarabJukeController.TryAutopilotDash</c>, the committed dash
        /// that sweeps the cavitation plate. It refuses while the juke is spent or rolling, so a
        /// bot can never fire faster than a human could.</item>
        /// <item><b>Urchin</b> - a short TAP of the chain-spike trigger through
        /// <c>PerformShipControllerActionsReplicated</c>. It has to be the replicated path: an AI
        /// runs server-only, so a local press would fire spikes on one machine and show them to
        /// nobody. A tap is the aimed shotgun; a long hold would charge the omnidirectional burst
        /// instead, which is why the hold is measured in hundredths.</item>
        /// </list>
        ///
        /// <para><b>Stated honestly:</b> the Dolphin's cone needs a crystal, which its own
        /// platform crystal-seeking finds, so an AI Dolphin fights but opportunistically. No
        /// vessel, ability or throttle behaviour is touched from here beyond those two triggers,
        /// so none of this can leak into another mode.</para>
        /// </summary>
        void ArmTriggers()
        {
            if (settings == null) return;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var vesselTf = p.Vessel?.Transform;
                if (vesselTf == null) continue;

                var captured = p;
                var juke = vesselTf.GetComponent<ScarabJukeController>();
                var handler = vesselTf.GetComponent<R_VesselActionHandler>();
                if (juke == null && handler == null) continue;

                bool isUrchin = captured.Vessel?.VesselStatus?.VesselType == VesselClassType.Urchin;
                if (juke == null && !isUrchin) continue;

                _triggerRoutines.Add(StartCoroutine(TriggerRoutine(captured, juke, isUrchin ? handler : null)));
            }
        }

        /// <summary>
        /// One bot's trigger finger. Sampled on a slow clock rather than per frame, and it exits
        /// the moment the match is decided or the vessel is gone.
        /// </summary>
        IEnumerator TriggerRoutine(IPlayer self, ScarabJukeController juke, R_VesselActionHandler spikes)
        {
            var wait = new WaitForSeconds(Mathf.Max(0.05f, settings.aiFireSampleSeconds));
            IPlayer rival = null;
            float nextRetarget = 0f;

            while (!_finalResultsSent)
            {
                yield return wait;

                var selfTf = self.Vessel?.Transform;
                if (selfTf == null) yield break;

                if (Time.time >= nextRetarget || !IsLiveOpponent(rival, self))
                {
                    nextRetarget = Time.time + settings.aiRetargetSeconds;
                    rival = FindNearestOpponent(self, selfTf.position);
                }

                var rivalTf = rival?.Vessel?.Transform;
                if (rivalTf == null) continue;

                Vector3 toRival = rivalTf.position - selfTf.position;
                float sqr = toRival.sqrMagnitude;

                if (juke != null && sqr <= settings.aiPlateRange * settings.aiPlateRange)
                {
                    juke.TryAutopilotDash(toRival);
                }
                else if (spikes != null && sqr <= settings.aiSpikeRange * settings.aiSpikeRange)
                {
                    // A TAP: press, a beat, release. Replicated, so every peer sees the volley.
                    spikes.PerformShipControllerActionsReplicated(InputEvents.RightStickAction);
                    yield return new WaitForSeconds(settings.aiSpikeTapSeconds);
                    spikes.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
                }
            }
        }

        void DisarmTriggers()
        {
            // The routines exit on _finalResultsSent, but a despawn can beat that - and an Urchin
            // caught mid-tap would otherwise leave its trigger held. Release every one.
            //
            // Stop only the routines THIS controller started. StopAllCoroutines() would reach
            // anything a base class is running too, which is a blunt instrument on a
            // NetworkBehaviour three inheritance levels deep - and the one call site is the end
            // of the match, exactly where a base's end-game flow would be in flight.
            for (int i = 0; i < _triggerRoutines.Count; i++)
                if (_triggerRoutines[i] != null) StopCoroutine(_triggerRoutines[i]);
            _triggerRoutines.Clear();

            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var handler = p.Vessel?.Transform != null
                    ? p.Vessel.Transform.GetComponent<R_VesselActionHandler>()
                    : null;
                handler?.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
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
            // Teammates cannot be hit at all - every scoring path here declines own-domain.
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
            DisarmTriggers();
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
            var bulletArray = new int[count];
            var strikeArray = new int[count];
            var debuffArray = new int[count];
            var missileArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                bulletArray[i] = statsList[i].BulletHitsLanded;
                strikeArray[i] = statsList[i].StrikeHitsLanded;
                debuffArray[i] = statsList[i].DebuffHitsLanded;
                missileArray[i] = statsList[i].MissileHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray,
                bulletArray, strikeArray, debuffArray, missileArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] bullets,
            int[] strikes,
            int[] debuffs,
            int[] missiles,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Broadside] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // ALL FOUR verb counts travel. The scoreboard's secondary line is the only place
                // a mixed-fleet brawl says what a pilot actually DID, and a client that only
                // replicated the total would show every loser's breakdown as "no hits".
                stat.BulletHitsLanded = bullets[i];
                stat.StrikeHitsLanded = strikes[i];
                stat.DebuffHitsLanded = debuffs[i];
                stat.MissileHitsLanded = missiles[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }
    }
}
