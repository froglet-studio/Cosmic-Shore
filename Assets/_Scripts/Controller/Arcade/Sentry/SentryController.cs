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
    /// Sentry - the Meerkat's CHARGE game: a guard war round a crystal heart, inside nested prism shells cut by gates
    /// (Corona's arena, referenced read-only: <see cref="SpawnableCorona"/>). Turn on Sentinel Mode (Y) and every
    /// soliton you form leaves a SENTINEL standing at its way in that zaps any rival hull in its guard; every ZAP - a
    /// Bullet-class combat hit the Meerkat already lands in every mode - is one point. The first DOMAIN to the target
    /// wins (golf-timed). See SENTRY.md.
    ///
    /// <para>Structurally Kelpwood's controller (1 round / 1 turn, HasEndGame=false, server winner detection, a
    /// snapshot SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per match). It changes nothing
    /// about the Meerkat: a zap is already a Bullet-class hit (<c>MeerkatSolitonKit.ZapVessel</c>), and
    /// <see cref="SentryScoringRuleSO"/> pays it.</para>
    ///
    /// <para><b>The arena is Corona's</b>: every pilot comes in through the gates for the heart's omni crystals, so
    /// the gates and the heart are where a guard pays. A soliton's tube phases the hull, so a tunnel is also the way
    /// through a shell without a gate. This controller lays nothing: it arms the AI wardens
    /// (<see cref="MeerkatArcadeSteer"/>) and runs the race.</para>
    ///
    /// MEERKAT-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class SentryController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag SentrySettings.asset - the AI wardens. The zap target lives in EndConditionOverridesSO, " +
                 "resolved by SentryZapTurnMonitor; the zap VALUE on the rule.")]
        [SerializeField] SentrySettingsSO settings;

        [Tooltip("Drag SentryScoringRule.asset (metric = CombatPoints, a zap = Bullet).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The shells' cell (Corona's). Read-only to this controller: its position is the crystal HEART.")]
        [SerializeField] Cell arenaCell;

        readonly MeerkatArcadeSteer _steer = new();
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        Vector3 Centre => arenaCell ? arenaCell.transform.position : Vector3.zero;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;

            // The hit latch is static and Time.time keeps running across a scene load, so a fast rematch could
            // otherwise inherit a claimed window and eat the first zap.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _steer.Disarm();
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
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[SentryController] no settings asset wired; the AI wardens are not armed and " +
                                   "fly the platform's own steering.");
                return;
            }
            _steer.Arm(gameData, (int)GameModes.Sentry, Centre, PickGuardPost,
                s => MeerkatArcadeSteer.Rung(settings.aiRetargetSeconds, s.Level));
        }

        /// <summary>
        /// Where a warden goes next: ahead of the nearest rival (its soliton forms where it is, so the sentinel stands
        /// on the rival's line), or - by the rung's chance, or with nobody in range - into the crystal heart, where
        /// every pilot comes. Steering only: the hull's own autopilot forms the solitons and turns Sentinel Mode on.
        /// </summary>
        Vector3 PickGuardPost(MeerkatArcadeSteer.Seat seat, Transform self)
        {
            int level = seat.Level;
            var rival = _steer.NearestRival(seat, self.position, MeerkatArcadeSteer.Rung(settings.aiHuntRange, level),
                settings.aiHumanFocus);
            bool holdHeart = rival == null ||
                             seat.Rng.NextDouble() < MeerkatArcadeSteer.Rung(settings.aiHoldHeartChance, level);
            Vector3 pick = holdHeart
                ? MeerkatArcadeSteer.Jitter(seat, Centre, settings.aiHeartAimRadius)
                : MeerkatArcadeSteer.Led(rival, MeerkatArcadeSteer.Rung(settings.aiLeadSeconds, level));
            return MeerkatArcadeSteer.Jitter(seat, pick, MeerkatArcadeSteer.Rung(settings.aiAimError, level));
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
                if (local != null) GameToastAPI.Post(GameToastSituation.SentryHint, local.Domain);
            }

            if (IsServer && _steer.IsArmed) _steer.Tick();
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
            _steer.Disarm();
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
            var zapArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                zapArray[i] = statsList[i].BulletHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, zapArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] zaps,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Sentry] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // zaps as 0.
                stat.BulletHitsLanded = zaps[i];
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

            _steer.Disarm();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;
                s.BulletHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
