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
    /// Kelpwood - the Angler's CHARGE game: a hunt through a 0G ball of kelp (<see cref="SpawnableKelpwood"/>, the
    /// cell's own environment). Sweep the eraser onto a rival to LOCK them: one creature leaves your school and hunts
    /// them, and every BITE is one point. The first DOMAIN to the target wins (golf-timed). See KELPWOOD.md.
    ///
    /// <para>Structurally Jackstraw's controller (1 round / 1 turn, HasEndGame=false, server winner detection, a
    /// snapshot SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per match). It changes nothing
    /// about the Angler: a bite is already a Strike-class combat hit, and <see cref="KelpwoodScoringRuleSO"/> pays it.</para>
    ///
    /// <para><b>The ammunition is the cell's.</b> The herbivores the Angler tames graze the kelp (the spawn profile),
    /// and the omni crystals - one creature each - are the scene's CrystalManager's, per intensity. This controller lays
    /// nothing: it arms the AI hunters and runs the race.</para>
    ///
    /// ANGLER-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class KelpwoodController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag KelpwoodSettings.asset - the AI hunters. The bite target lives in EndConditionOverridesSO, " +
                 "resolved by KelpwoodBiteTurnMonitor; the bite VALUE on the rule.")]
        [SerializeField] KelpwoodSettingsSO settings;

        [Tooltip("Drag KelpwoodScoringRule.asset (metric = CombatPoints, a bite = Strike).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The kelp's cell. Read-only to this controller: its position is the arena CENTRE, and its " +
                 "SpawnableKelpwood says how far the strands reach.")]
        [SerializeField] Cell arenaCell;

        readonly KelpwoodHunter _hunter = new();
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
            // otherwise inherit a claimed window and eat the first bite.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _hunter.Disarm();
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

            if (settings == null)
            {
                CSDebug.LogWarning("[KelpwoodController] no settings asset wired; the AI hunters are not armed and " +
                                   "fly the platform's own steering.");
                return;
            }
            _hunter.Arm(gameData, settings, Centre, KelpRadius());
        }

        /// <summary>How far the strands reach, in world units, read off the arena's own environment.</summary>
        float KelpRadius()
        {
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableKelpwood>(true) : null;
            if (!env) env = FindAnyObjectByType<SpawnableKelpwood>();
            return env ? env.KelpRadius * env.transform.lossyScale.x : 320f;
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
                if (local != null) GameToastAPI.Post(GameToastSituation.KelpwoodHint, local.Domain);
            }

            if (IsServer && _hunter.IsArmed) _hunter.Tick();
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
            _hunter.Disarm();
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
            var biteArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                biteArray[i] = statsList[i].StrikeHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, biteArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] bites,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Kelpwood] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // bites as 0.
                stat.StrikeHitsLanded = bites[i];
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

            _hunter.Disarm();
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
