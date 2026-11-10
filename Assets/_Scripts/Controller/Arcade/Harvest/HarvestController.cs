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
    /// Harvest - the Thresher's SPACE game: a race to destroy hostile prisms in a field of SHEAVES
    /// (<see cref="SpawnableHarvest"/>, the cell's own environment). A sheaf is a core of blocks with stalks standing
    /// out of it: mow the stalks with the chain to bank energy in the ball, then whip the ball into the core, where the
    /// bank goes off as one blast (THRESHER.md §2-2a). First DOMAIN to the target wins, golf-timed. See HARVEST.md.
    ///
    /// <para>Structurally <see cref="RampageController"/> (1 round / 1 turn, HasEndGame=false, server winner detection
    /// in OnTurnEndedCustom, a snapshot SyncFinalScores_ClientRpc carrying the breakdown). The destruction stat
    /// increments itself (StatsManager.PrismDestroyed), so no per-event listener is needed. The omni crystals are the
    /// cell's own (its CrystalManager, in the field's clear centre): a crystal is the Thresher's 12 s windmill.</para>
    ///
    /// <para><b>Nothing about the Thresher changes</b> (the LOCKED vessel rule): the mode's levers are the sheaves,
    /// the crystals and the AI. Every Thresher spawns in Mass Mode, where the ball steals and never destroys; the hint
    /// toast tells a human to press A, and the AI (<see cref="HarvestReaper"/>) presses it once itself.</para>
    ///
    /// THRESHER-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class HarvestController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag HarvestSettings.asset - the AI reapers. The prism target lives in EndConditionOverridesSO, " +
                 "resolved by HarvestPrismTurnMonitor; the results wording on the rule.")]
        [SerializeField] HarvestSettingsSO settings;

        [Tooltip("Drag HarvestScoringRule.asset (metric = PrismsDestroyed, golf).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The field's cell. Read-only to this controller: its position is the arena CENTRE, and its " +
                 "SpawnableHarvest says where the sheaves are.")]
        [SerializeField] Cell arenaCell;

        readonly HarvestReaper _reaper = new();
        readonly List<SpawnableHarvest.Sheaf> _sheaves = new();
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;

        // Golf: winners carry their finish time, losers a remaining-prisms sentinel (RampageScoringRuleSO).
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
        }

        public override void OnNetworkDespawn()
        {
            _reaper.Disarm();
            base.OnNetworkDespawn();
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[HarvestController] no settings asset wired; the AI reapers are not armed, stay in " +
                                   "Mass Mode and fly the platform's own steering.");
                return;
            }
            _reaper.Arm(gameData, settings, Centre, CollectSheaves());
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
                if (local != null) GameToastAPI.Post(GameToastSituation.HarvestHint, local.Domain);
            }

            if (IsServer && _reaper.IsArmed) _reaper.Tick();
        }

        /// <summary>Every sheaf in world space, read off the arena's own environment.</summary>
        List<SpawnableHarvest.Sheaf> CollectSheaves()
        {
            _sheaves.Clear();
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableHarvest>(true) : null;
            if (!env) env = FindAnyObjectByType<SpawnableHarvest>();
            if (!env) return _sheaves;
            env.CollectSheaves(_sheaves);
            float scale = env.transform.lossyScale.x;
            for (int i = 0; i < _sheaves.Count; i++)
            {
                var s = _sheaves[i];
                _sheaves[i] = new SpawnableHarvest.Sheaf(env.transform.TransformPoint(s.Centre), s.CoreRadius * scale,
                                                         s.Reach * scale, s.Domain);
            }
            return _sheaves;
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
                .OrderByDescending(s => s.HostilePrismsDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            _reaper.Disarm();
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
            var prismsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                prismsArray[i] = statsList[i].HostilePrismsDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, prismsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prismsDestroyed,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Harvest] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // prisms as 0.
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
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
            _reaper.Disarm();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
