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
    /// Snap - the Goby &amp; Pistol's CHARGE game: a hunt through Kelpwood's ball of kelp. The Goby locks a rival (its
    /// Charge card, Lock); while that rival is on the Pistol's screen, a vortex ring the Pistol fires (Snap) homes on
    /// them, and every RING HIT (Vortex Sting) is one point. The first DOMAIN to the target wins (golf-timed). See SNAP.md.
    ///
    /// <para><b>Nothing about the pair changes</b> (the LOCKED vessel rule, Shombith 2026-10-31). A ring hit is already a
    /// scored combat hit (<c>GobyPistolDuo.ScoreRingHit</c>, MissileDirect class) and <see cref="SnapScoringRuleSO"/>
    /// pays it. The kelp is the arena's lever: its strands are the perches a dart stops on, and the neutral mass the Goby
    /// AI routes the Pistol's islands through.</para>
    ///
    /// <para><b>No mode AI.</b> An AI pair is the seat's own crew (<c>GobyPistolDuo</c>'s Goby AI and Pistol AI, on the
    /// machine that simulates the hull): it locks the nearest rival and snaps when the lock is on screen, which IS this
    /// game, so this controller arms nothing (Rampage's reason). A second presser would fight the crew.</para>
    ///
    /// Structurally Kelpwood's controller without the hunter (1 round / 1 turn, HasEndGame=false, server winner
    /// detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per match).
    /// GOBY-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class SnapController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag SnapScoringRule.asset (metric = CombatPoints, a ring hit = MissileDirect). The target lives in " +
                 "EndConditionOverridesSO, resolved by SnapHitTurnMonitor.")]
        [SerializeField] ScoringRuleSO rule;

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
            // otherwise inherit a claimed window and eat the first ring hit.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
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

            if (gameData == null || !gameData.IsTurnRunning || _hintPosted) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.SnapHint, local.Domain);
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
            var hitArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                hitArray[i] = statsList[i].MissileHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, hitArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] hits,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Snap] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // ring hits as 0.
                stat.MissileHitsLanded = hits[i];
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
