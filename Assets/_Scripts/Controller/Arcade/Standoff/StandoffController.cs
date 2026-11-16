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
    /// Standoff - the Grizzly's CHARGE game: a field of prism POSTS facing an open centre (<see cref="SpawnableStandoff"/>,
    /// the cell's own environment). A trigger bomb that touches a rival hull freezes on it; the next pull and release
    /// blows it. Every rival a blast of yours catches is one point. Dig In behind a post to aim and recharge. The first
    /// DOMAIN to the target wins (golf-timed). See STANDOFF.md.
    ///
    /// <para>Structurally Corona's controller (1 round / 1 turn, HasEndGame=false, server winner detection, a
    /// snapshot SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per match). It changes nothing
    /// about the Grizzly: a blast catching a rival is already a MissileBlast-class combat hit, and
    /// <see cref="StandoffScoringRuleSO"/> pays it.</para>
    ///
    /// <para><b>The posts and the crystals are the cell's.</b> The posts are the cell's environment and the omni
    /// crystals in the open centre are the scene's CrystalManager's, per intensity. This controller lays nothing: it
    /// arms the AI gunners and runs the race.</para>
    ///
    /// GRIZZLY-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class StandoffController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag StandoffSettings.asset - the AI gunners. The blast target lives in EndConditionOverridesSO, " +
                 "resolved by StandoffBlastTurnMonitor; the blast VALUE on the rule.")]
        [SerializeField] StandoffSettingsSO settings;

        [Tooltip("Drag StandoffScoringRule.asset (metric = CombatPoints, a blast hit = MissileBlast).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The posts' cell. Read-only to this controller: its SpawnableStandoff is where the AI gunners " +
                 "take cover.")]
        [SerializeField] Cell arenaCell;

        readonly StandoffGunner _gunner = new();
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;

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
            // otherwise inherit a claimed window and eat the first blast.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _gunner.Disarm();
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
                list[i].MissileHitsLanded = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[StandoffController] no settings asset wired; the AI gunners are not armed and " +
                                   "fly the platform's own steering.");
                return;
            }
            var arena = arenaCell ? arenaCell.GetComponentInChildren<SpawnableStandoff>(true) : null;
            if (!arena) arena = FindAnyObjectByType<SpawnableStandoff>();
            if (!arena)
            {
                CSDebug.LogWarning("[StandoffController] no SpawnableStandoff in the scene; the AI gunners are not armed.");
                return;
            }
            _gunner.Arm(gameData, settings, arena);
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
                if (local != null) GameToastAPI.Post(GameToastSituation.StandoffHint, local.Domain);
            }

            if (IsServer && _gunner.IsArmed) _gunner.Tick();
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
            _gunner.Disarm();
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
            var blastArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                blastArray[i] = statsList[i].MissileHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, blastArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] blasts,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Standoff] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // blasts as 0.
                stat.MissileHitsLanded = blasts[i];
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

            _gunner.Disarm();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;
                s.MissileHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
