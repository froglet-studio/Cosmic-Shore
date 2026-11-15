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
    /// Flitstrike - the Honeyguide's CHARGE game: a dogfight with no guns that score. Hold the left trigger to see the
    /// Flit's lane, let go to dart 18 u along it, and every rival hull within the strike radius of the lane takes a
    /// STRIKE (petals only, the fleet's Strike price). Charge widens the strike (8 -> 16 u) and at Charge 5 a Flit that
    /// lands is ready again at once (Relentless); the Ratel's Charge cards bank stronger uses on the same trigger (the
    /// lasso, Blink Drive, the portals, Rosen Bridge, the FTL Chromatool, Monte Cannon), and each of those strikes too.
    /// First DOMAIN to the strike target wins, golf-timed, on <see cref="ScoringMetric.CombatPoints"/> through
    /// <see cref="FlitstrikeScoringRuleSO"/> (a Strike pays, the Peck gun's rounds pay nothing), resolved by
    /// <see cref="FlitstrikeStrikeTurnMonitor"/>. See FLITSTRIKE.md.
    ///
    /// <para><b>It changes nothing about the Honeyguide</b> (the LOCKED vessel rule). Every strike is already COUNTED
    /// in every mode (<c>HoneyguideCrew.StrikeVessel</c> raises a Strike-class combat hit); this mode only adds the rule
    /// that PAYS for it. The arena is Dog Fight's Boneyard, referenced read-only: a wreck-field of cover and canyons,
    /// so a pilot who cannot outgun a rival can still lose them, and a dart is a thing you set up round a corner.</para>
    ///
    /// <para><b>The AI only STEERS.</b> The Honeyguide's crew already pulls its own triggers when an AI flies it (the
    /// lab's <c>wanted8</c>: Flit when a rival is on its lane, input-only), so a second presser would fight it. The
    /// hunters write <see cref="AIPilot.SetExternalTargetProvider"/> and nothing else: an intercept on the nearest
    /// opposing pilot (humans preferred by <see cref="FlitstrikeSettingsSO.aiHumanFocus"/>), the arena centre with no
    /// rival. The crew's own freestyle hunt yields to a match's hook by design (<c>HoneyguideCrew.HuntRuns</c>).</para>
    ///
    /// Structurally <see cref="DustupController"/> (1 round / 1 turn, HasEndGame=false, server winner detection, a
    /// snapshot SyncFinalScores_ClientRpc carrying the breakdown), with the race beats from
    /// <see cref="DomainRaceToasts"/> and one hint. HONEYGUIDE-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class FlitstrikeController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag FlitstrikeSettings.asset - the AI hunters. The strike target lives in EndConditionOverridesSO, " +
                 "resolved by FlitstrikeStrikeTurnMonitor; the strike VALUE lives on the scoring rule.")]
        [SerializeField] FlitstrikeSettingsSO settings;

        [Tooltip("Drag FlitstrikeScoringRule.asset (metric = CombatPoints, a Flit strike = Strike).")]
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
            // otherwise inherit a claimed window and eat the first strike.
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
                list[i].StrikeHitsLanded = 0;
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
            if (_finalResultsSent || gameData == null) return;

            if (rule != null)
            {
                // Quarter, halfway and lead change as toasts: a local poll over the replicated score (128-130).
                _raceToasts ??= new DomainRaceToasts(rule);
                _raceToasts.Tick(gameData);
            }

            if (_hintPosted || !gameData.IsTurnRunning) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.FlitstrikeHint, local.Domain);
        }

        // ── AI (server): steering only ──

        void ArmHunters()
        {
            // The providers below read settings every server frame; without it they would throw inside
            // AIPilot.Update. Fail safe: bots fly the platform's own crystal seeking.
            if (settings == null)
            {
                CSDebug.LogWarning("[FlitstrikeController] no settings asset wired; the AI hunters are not armed.");
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
            // A Flit spares teammates (HoneyguideCrew.RivalStatus declines own-domain victims).
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
            var strikeArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                strikeArray[i] = statsList[i].StrikeHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, strikeArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] strikes,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Flitstrike] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's strikes
                // as 0.
                stat.StrikeHitsLanded = strikes[i];
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
                s.StrikeHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
