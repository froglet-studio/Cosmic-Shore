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
    /// Shockfront - the Stoat's CHARGE game: a duel in Dog Fight's Boneyard. The Stoat's Charge ability is the RIPPLE
    /// (<see cref="StoatRippleExecutor"/>): whenever its pair annihilates - the orbit's pair closing after a slingshot -
    /// the meeting point throws a space-time ripple whose front strips a petal of every element from each rival it
    /// sweeps over. Every rival stripped is a Debuff-class combat hit and one point. The first DOMAIN to the target
    /// wins (golf-timed). See SHOCKFRONT.md.
    ///
    /// <para>Structurally Corona's controller (1 round / 1 turn, HasEndGame=false, server winner detection, a snapshot
    /// SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per match, the shared race toasts and a
    /// one-line hint). It changes nothing about the Stoat: a ripple strip is already a scored hit (since 2026-11-15, the
    /// one-to-one rule of SILENCE.md), and <see cref="ShockfrontScoringRuleSO"/> pays it.</para>
    ///
    /// <para><b>The AI only steers.</b> The Stoat's own autopilot slings whenever its target is 30 deg or more off the
    /// nose (<c>StoatSlingExecutor.AutopilotSling</c>, the replicated press), so pointing it at a lead point on the
    /// nearest rival is enough: it slings round toward them, and the pair it slings out of closes where it was laid,
    /// beside the rival. <see cref="AIPilot.SetExternalTargetProvider"/> and nothing else - the generator fails if this
    /// controller presses an input.</para>
    ///
    /// STOAT-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class ShockfrontController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag ShockfrontSettings.asset - the AI hunters. The ripple target lives in EndConditionOverridesSO, " +
                 "resolved by ShockfrontRippleTurnMonitor; the hit VALUE on the rule.")]
        [SerializeField] ShockfrontSettingsSO settings;

        [Tooltip("Drag ShockfrontScoringRule.asset (metric = CombatPoints, a ripple hit = Debuff).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Boneyard cell. Read-only to this controller: it supplies the arena CENTRE an AI falls back to " +
                 "when it has no rival.")]
        [SerializeField] Cell arenaCell;

        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;
        bool _huntersArmed;

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
            _hintPosted = false;

            // The hit latch is static and Time.time keeps running across a scene load, so a fast rematch could
            // otherwise inherit a claimed window and eat the first ripple.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
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
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            ArmHunters();
        }

        void Update()
        {
            if (_finalResultsSent) return;

            if (rule != null)
            {
                // Quarter, halfway and lead change as toasts: a local poll over the replicated score (128-130).
                _raceToasts ??= new DomainRaceToasts(rule);
                _raceToasts.Tick(gameData);
            }

            if (gameData == null || !gameData.IsTurnRunning) return;

            if (!_hintPosted)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.ShockfrontHint, local.Domain);
            }
        }

        // ── AI (server) ──

        /// <summary>
        /// Every AI Stoat HUNTS: its steering runs at a lead point on the nearest opposing pilot (humans preferred by
        /// <see cref="ShockfrontSettingsSO.aiHumanFocus"/>). With no rival it flies to the arena centre. The slings are
        /// the hull's own autopilot's; this only says where to go.
        /// </summary>
        void ArmHunters()
        {
            // The providers below read settings every server frame; without it they would throw inside AIPilot.Update.
            // Fail safe: bots fly the platform's own crystal seeking.
            if (settings == null)
            {
                CSDebug.LogWarning("[ShockfrontController] no settings asset wired; the AI hunters are not armed and " +
                                   "fly the platform's own steering.");
                return;
            }

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
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

                    Vector3 selfPos = selfTf.position;
                    if (Time.time >= nextSample || !IsLiveOpponent(rival, captured))
                    {
                        nextSample = Time.time + settings.aiRetargetSeconds;
                        rival = FindNearestOpponent(captured, selfPos);
                    }

                    var rivalTf = rival?.Vessel?.Transform;
                    if (rivalTf == null) return centre;

                    var rivalStatus = rival.Vessel?.VesselStatus;
                    Vector3 rivalVelocity = rivalStatus != null ? rivalStatus.Course * rivalStatus.Speed : Vector3.zero;
                    return rivalTf.position + rivalVelocity * settings.aiInterceptLeadSeconds;
                });
            }
            _huntersArmed = true;
        }

        void DisarmHunters()
        {
            if (!_huntersArmed) return;
            _huntersArmed = false;
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
            // Teammates are never stripped: StoatRippleMath.Strips declines own-domain victims.
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
            var rippleArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                rippleArray[i] = statsList[i].DebuffHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, rippleArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] ripples,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Shockfront] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // ripple hits as 0.
                stat.DebuffHitsLanded = ripples[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;

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
            _hintPosted = false;
            _raceToasts = null;

            DisarmHunters();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;
                s.DebuffHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
