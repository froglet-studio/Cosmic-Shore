using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Bends - the Dolphin-only debuff duel. Two to four pilots fight in a cactus forest with
    /// no guns at all: the only weapon anyone has is the Dolphin's crystal blast, and the only
    /// thing that scores is catching an OPPOSING pilot in it. A caught pilot takes the blast's
    /// all-element decaying debuff, which is one BEND (10 points); first DOMAIN to the bend target
    /// wins.
    ///
    /// Structurally a sibling of <see cref="RampageController"/> / <see cref="DogFightController"/>
    /// (1 round / 1 turn, HasEndGame=false, server winner detection in OnTurnEndedCustom, snapshot
    /// SyncFinalScores_ClientRpc). What it contributes to the platform is a THIRD answer to "what
    /// is a vessel-vs-vessel hit worth", and one deliberate design position:
    ///
    /// <para><b>1. The mode is the Dolphin's own economy pointed at people.</b> The Dolphin banks
    /// blast energy ONLY by skimming and discharges it ONLY on a crystal
    /// (<c>DOLPHIN_ENERGY_ECONOMY.md</c> §1), so the loop is already: graze the forest to charge,
    /// race a rival to the crystal, then choose where to put the cone. Rampage built an arena
    /// where the answer was "at the thickest forest". This mode changes nothing about the vessel
    /// and changes the answer to "at a pilot" - which is why it needs no new ability, no new
    /// weapon and no new resource. It is the same cone, aimed at the one target Rampage never
    /// paid for.</para>
    ///
    /// <para><b>2. Nothing is destroyed, and that is the point.</b> A bend costs the victim
    /// element LEVELS - their blast gets narrower (Charge), shorter (Space), their crystals come
    /// slower (Mass), their boost weaker (Time) - for four seconds, decaying. So a landed hit does
    /// not remove a player, it makes them worse at the one thing the mode is about, and their
    /// recovery is a real clock you can stack against. Elementals are the platform's single
    /// buff/debuff system, so this mode reaches for that fundamental rather than inventing a
    /// status: the debuff is <c>VesselElementalDebuffByExplosionEffectSO</c>, unchanged, and this
    /// mode's only addition is a sibling effect in the same container that REPORTS the hit.</para>
    ///
    /// <para><b>3. Elemental immunity is a real counter-play, for free.</b> A pilot who is
    /// elementally immune (<c>ResourceSystem.IsElementallyImmune</c>) eats the cone and keeps
    /// their levels - and because the scoring effect is authored
    /// <c>requireDebuffableVictim</c>, it scores the attacker nothing either. The score and the
    /// effect cannot disagree, which is the whole reason that flag exists.</para>
    ///
    /// <para><b>4. The AI keeps the platform's crystal seeking</b> and installs NO
    /// <see cref="AIPilot.SetExternalTargetProvider"/> hook - that override replaces crystal
    /// seeking outright, and in a mode whose weapon is FIRED BY a crystal that would disarm every
    /// AI in the arena (the lesson Rampage recorded after removing exactly such a provider). What
    /// this controller does install is the narrower
    /// <see cref="AIPilot.SetDriftLookTargetProvider"/>: the AI still flies at crystals, and when
    /// it drifts to aim, it aims at the nearest opposing PILOT instead of the densest cluster of
    /// hostile mass. One hook, one behaviour, no steering touched.</para>
    ///
    /// DOLPHIN-ONLY is enforced entirely by the platform machinery, deliberately not
    /// re-implemented here - three independent layers, all reading the single <c>Vessels</c> list
    /// on ArcadeGameBends: <c>GameDataSO.SyncFromArcadeGame</c> clamps the launching machine's
    /// selection; <c>ServerPlayerVesselInitializer.ResolveSpawnVesselType</c> re-clamps
    /// SERVER-SIDE at spawn (the one that catches a client whose owner-write
    /// NetDefaultVesselType still carries the hull it last flew); and
    /// <c>ServerPlayerVesselInitializerWithAI</c> clamps the AI's scene-authored class too.
    /// </summary>
    public class BendsController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag BendsScoringRule.asset - the per-mode scoring strategy. It also owns the " +
                 "point VALUES (a bend is 10, gunnery is 0), which is why the platform can count " +
                 "landed hits everywhere and score them only here.")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The forest cell. Read-only to this controller: it supplies the arena CENTRE an " +
                 "AI falls back to when it has no rival in sight. The forest and the crystals " +
                 "come from the cell's config for the selected intensity.")]
        [SerializeField] Cell arenaCell;

        [Header("Progress feedback")]
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
        [Tooltip("Seconds between AI rival re-selections for the DRIFT AIM. Between samples the " +
                 "AI keeps aiming at the pilot it already picked - the provider is sampled every " +
                 "frame, so the aim point tracks a live position even though the CHOICE is made " +
                 "on a slow cadence. Steering is untouched: the AI still seeks crystals.")]
        [SerializeField, Min(0.25f)] float aiAimRetargetSeconds = 1.25f;

        [Tooltip("How far ahead of its rival the AI aims, as a multiple of that rival's own " +
                 "per-second travel. The blast is a cone with real length, so leading a moving " +
                 "target is what makes an AI's shot connect rather than trail.")]
        [SerializeField, Min(0f)] float aiAimLeadSeconds = 0.35f;

        [Tooltip("Beyond this distance the AI does not bother aiming at a rival - the blast " +
                 "cannot reach, and pointing the nose at an unreachable pilot just stops it " +
                 "clearing forest. Past it the platform default (aim at a mass cluster) resumes, " +
                 "which is also how the AI keeps its own energy topped up.")]
        [SerializeField, Min(1f)] float aiAimMaxRange = 900f;

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

            // The hit latch is static and Time.time keeps running across a scene load, so a fast
            // rematch could otherwise inherit a claimed window and silently eat the first bend of
            // the new match. Cheap to clear, and it runs on every peer because the latch is
            // consulted wherever a blast is simulated - not only on the server.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCombatCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            DisarmAimHooks();
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
                list[i].DebuffHitsLanded = 0;
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
            ArmAimHooks();

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
            // Nobody has bent anyone yet - no leader to announce rather than handing the lead to
            // whoever sorts first on a 0-0 tie-break.
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
                    ? GameToastSituation.BendsHalfBent
                    : GameToastSituation.BendsQuarterBent,
                (Domains)domain, ((Domains)domain).ToString(), points.ToString(), target.ToString());

            // Milestone reached: the alert haptic (the game's third feel, fenced to
            // match-changing events - see Docs/HAPTICS.md). Safe on every peer:
            // HapticController gates on the local player's own haptics setting.
            HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int points, int target)
        {
            GameToastAPI.Post(GameToastSituation.BendsLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), points.ToString(), target.ToString());
        }

        // ── AI aim (server) ──────────────────────────────────────────────────

        /// <summary>
        /// Points every AI Dolphin's DRIFT AIM at the nearest opposing pilot.
        ///
        /// <b>Why this and not a steering provider.</b> The Dolphin fires by collecting a crystal,
        /// so an AI that stops seeking crystals is an AI that never shoots. The platform already
        /// makes it seek them, and already makes it DRIFT once one is lined up - swinging its nose
        /// off its course so the cone comes out somewhere other than straight ahead. All this mode
        /// changes is where that nose ends up: at a rival rather than at the densest cluster of
        /// hostile mass (<c>Cell.GetExplosionTarget</c>, the default, which is right in Rampage
        /// because there the forest IS the score).
        ///
        /// <b>Lead, and a range gate.</b> The aim point runs
        /// <see cref="aiAimLeadSeconds"/> ahead along the rival's own course, because the blast has
        /// real length and a cone put where someone WAS is a miss. Past
        /// <see cref="aiAimMaxRange"/> the provider returns null and the platform default resumes,
        /// which matters more than it looks: aiming at an unreachable pilot would stop the AI
        /// clearing forest, and clearing forest is how it banks the energy for the next shot.
        ///
        /// Server-only, and steering-free: nothing here touches throttle, abilities, or
        /// <c>SetExternalTargetProvider</c>, so it cannot leak into another mode.
        /// </summary>
        void ArmAimHooks()
        {
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                IPlayer rival = null;
                float nextSample = 0f;

                pilot.SetDriftLookTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return null;
                    Vector3 selfPos = selfTf.position;

                    if (Time.time >= nextSample || !IsLiveOpponent(rival, captured))
                    {
                        nextSample = Time.time + aiAimRetargetSeconds;
                        rival = FindNearestOpponent(captured, selfPos);
                    }

                    var rivalTf = rival?.Vessel?.Transform;
                    if (rivalTf == null) return null;   // fall back to the mass cluster

                    Vector3 rivalPos = rivalTf.position;
                    if ((rivalPos - selfPos).sqrMagnitude > aiAimMaxRange * aiAimMaxRange)
                        return null;                    // out of reach - go back to grazing

                    var rivalStatus = rival.Vessel?.VesselStatus;
                    Vector3 lead = rivalStatus != null
                        ? rivalStatus.Course * (rivalStatus.Speed * aiAimLeadSeconds)
                        : Vector3.zero;
                    return rivalPos + lead;
                });
            }
        }

        /// <summary>
        /// Hands every AI back to the platform default. Called on despawn and on replay reset -
        /// the provider closes over this controller's fields and over a <see cref="IPlayer"/>, and
        /// AI players are spawned <c>destroyWithScene: false</c>, so a hook left armed would
        /// outlive the match that installed it.
        /// </summary>
        void DisarmAimHooks()
        {
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;

            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                p.Vessel?.VesselStatus?.AIPilot?.ClearDriftLookTargetProvider();
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
            // Teammates cannot be bent at all - ExplosionImpactor declines own-domain vessels.
            if (candidate.Domain == self.Domain) return false;
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

            // Winning domain (highest point sum, Jade→Ruby→Gold tie-break) delegated to the rule;
            // representative winner-name = the best individual contributor on that domain (legacy
            // display field - victory/defeat attribution uses WinnerDomain).
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
            DisarmAimHooks();
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
            var bendArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                bendArray[i] = statsList[i].DebuffHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, bendArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] bends,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Bends] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: the scoreboard's secondary line is "N pts · M bends",
                // and a client that only replicated the total would show every loser's bend count
                // as 0.
                stat.DebuffHitsLanded = bends[i];
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
            DisarmAimHooks();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;        // the scored metric AND the milestone trigger
                s.DebuffHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
