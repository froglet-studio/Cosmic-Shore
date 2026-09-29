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
    /// Dustup - the Butterfly's CHARGE game, and a duel between the two slowest-turning hulls on
    /// any card. The Butterfly carries no gun. Its one weapon is the Scale Dust - a capsule that
    /// hangs BELOW the hull while it is in Dust mode (right trigger) - and CHARGE is the dust's
    /// BITE (<c>VesselElementalDebuffBySkimmerEffectSO.biteScale</c>, 0.5x at rest to 2x at level
    /// 10, doubled again at Charge 5 "Monarch"). So a rival is hit by flying OVER them: every
    /// opposing pilot the dust passes through takes the all-element bite and pays one DUSTING.
    /// First DOMAIN to the point target wins. Fought in Dog Fight's Boneyard - a wreck-field of
    /// cover and canyons, which is exactly what a duel between two hulls that cannot turn needs:
    /// somewhere to lose a pursuer and somewhere to wait above.
    ///
    /// Structurally a sibling of <see cref="UndertowController"/> (1 round / 1 turn,
    /// HasEndGame=false, server winner detection, snapshot SyncFinalScores_ClientRpc, milestone
    /// sampler, the hit latch cleared per match). It changes nothing outside the mode:
    ///
    /// <para><b>The scoring report already existed.</b> The dust container has carried a
    /// Strike-class <c>VesselCombatHitBySkimmerEffectSO</c> since the hull shipped, so every
    /// dusting has been COUNTED in every mode. <see cref="DustupScoringRuleSO"/> is the one rule
    /// that PAYS for it.</para>
    ///
    /// <para><b>The AI has to be told to switch modes.</b> A Butterfly spawns in Mass mode and
    /// an autopilot presses nothing, so without <see cref="ButterflyAutopilotModeDriver"/> every
    /// AI would fly the match with its only weapon switched off. The hunters hold Dust mode for
    /// the whole turn and steer to a point ABOVE their rival (<see
    /// cref="DustupSettingsSO.aiDustHover"/>), because a hull flown AT a rival meets them with
    /// its body and misses with its dust.</para>
    ///
    /// BUTTERFLY-ONLY is enforced entirely by the arcade card's Vessels list - no mode-local
    /// vessel check.
    /// </summary>
    public class DustupController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag DustupSettings.asset - feedback and AI. The point target lives in " +
                 "EndConditionOverridesSO, resolved by DustupPointTurnMonitor; the point VALUE " +
                 "lives on the scoring rule.")]
        [SerializeField] DustupSettingsSO settings;

        [Tooltip("Drag DustupScoringRule.asset (metric = CombatPoints, a dusting = Strike).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Boneyard cell. Read-only to this controller: it supplies the arena CENTRE an " +
                 "AI falls back to when it has no rival.")]
        [SerializeField] Cell arenaCell;

        const int MilestoneNone = 0;
        const int MilestoneFirst = 1;
        const int MilestoneSecond = 2;

        readonly ButterflyAutopilotModeDriver _modes = new();

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
            // rematch could otherwise inherit a claimed window and eat the first dusting.
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
                list[i].StrikeHitsLanded = 0;
            }
        }

        // ── Progress milestones (server samples, every peer gets the feedback) ──

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

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
                milestone == MilestoneSecond ? GameToastSituation.DustupHalf : GameToastSituation.DustupQuarter,
                (Domains)domain, ((Domains)domain).ToString(), points.ToString(), target.ToString());

            // Match-changing event: the alert haptic (Docs/HAPTICS.md). HapticController gates on
            // the local player's own setting, so this is safe on every peer.
            HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int points, int target)
        {
            GameToastAPI.Post(GameToastSituation.DustupLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), points.ToString(), target.ToString());
        }

        // ── AI (server) ──

        /// <summary>
        /// Every AI Butterfly HUNTS: it switches into Dust mode and holds it, and its steering
        /// runs at a point ABOVE an intercept on the nearest opposing pilot (humans preferred by
        /// <see cref="DustupSettingsSO.aiHumanFocus"/>) - above along its OWN up axis, since the
        /// capsule hangs along its own down. With no rival it flies to the arena centre.
        ///
        /// Steering is <see cref="AIPilot.SetExternalTargetProvider"/> and nothing else, so it
        /// cannot leak into another mode; the mode switch is the replicated press
        /// (<see cref="ButterflyAutopilotModeDriver"/>), driven from inside the provider because
        /// the provider is already a per-frame server callback owned by exactly this AI.
        /// </summary>
        void ArmHunters()
        {
            // The providers below read settings every server frame; without it they would throw
            // inside AIPilot.Update. Fail safe: bots fly the platform's own crystal seeking.
            if (settings == null)
            {
                CSDebug.LogWarning($"[DustupController] no settings asset wired; AI mode hooks not armed.");
                return;
            }

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            _modes.RetrySeconds = settings ? settings.aiModeRetrySeconds : 1f;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                IPlayer rival = null;
                float nextSample = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;

                    // The only weapon - hold it on for the whole turn.
                    if (!_finalResultsSent) _modes.Drive(captured, wantDust: true);

                    Vector3 selfPos = selfTf.position;
                    if (Time.time >= nextSample || !IsLiveOpponent(rival, captured))
                    {
                        nextSample = Time.time + settings.aiRetargetSeconds;
                        rival = FindNearestOpponent(captured, selfPos);
                    }

                    var rivalTf = rival?.Vessel?.Transform;
                    if (rivalTf == null) return centre;

                    var rivalStatus = rival.Vessel?.VesselStatus;
                    Vector3 rivalVelocity = rivalStatus != null
                        ? rivalStatus.Course * rivalStatus.Speed
                        : Vector3.zero;
                    return rivalTf.position
                           + rivalVelocity * settings.aiInterceptLeadSeconds
                           + selfTf.up * settings.aiDustHover;
                });
            }
        }

        void DisarmHunters()
        {
            _modes.ReleaseAll();
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
            // Teammates cannot be dusted at all - the combat-hit reporter declines own-domain victims.
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
            var dustArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                dustArray[i] = statsList[i].StrikeHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, dustArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] dustings,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Dustup] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show
                // every loser's dustings as 0.
                stat.StrikeHitsLanded = dustings[i];
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
                s.StrikeHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
