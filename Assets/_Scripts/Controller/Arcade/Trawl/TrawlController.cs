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
    /// Trawl - the Jacks' SPACE game: a race to destroy hostile prisms with the net. While the jacks net, everything that
    /// crosses the mouth between the two fish is caught and DRAGGED (up to the net's cap), and a dragged prism that runs
    /// into another destroys both, scored to the net (JACKS.md §1.1, the prism-motion fundamental). First DOMAIN to the
    /// target wins, golf-timed. See TRAWL.md.
    ///
    /// <para>Structurally <see cref="HarvestController"/> (1 round / 1 turn, HasEndGame=false, server winner detection in
    /// OnTurnEndedCustom, a snapshot SyncFinalScores_ClientRpc carrying the breakdown), in Harvest's field: sheaves of
    /// blocks, loose chaff and bales that never regrow. The destruction stat increments itself
    /// (StatsManager.PrismDestroyed), so no per-event listener is needed. Harvest's team Space crystals are the
    /// Thresher's (its blast) and are not laid here; the omni crystals are the scene's CrystalManager's, and on the Jacks
    /// an omni crystal is a worm colony that eats prisms and creatures.</para>
    ///
    /// <para><b>Nothing about the Jacks changes</b> (the LOCKED vessel rule): the mode's levers are the field and the
    /// AI (<see cref="TrawlSkipper"/>, which keeps the AI netting and steers it through the densest hostile mass).</para>
    ///
    /// JACKS-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class TrawlController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag TrawlSettings.asset - the AI skippers. The prism target lives in EndConditionOverridesSO, " +
                 "resolved by TrawlPrismTurnMonitor; the results wording on the rule.")]
        [SerializeField] TrawlSettingsSO settings;

        [Tooltip("Drag TrawlScoringRule.asset (metric = PrismsDestroyed, golf).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The field's cell (Harvest's configs). Read-only to this controller: its position is the arena CENTRE, " +
                 "and its density grid is where the AI skippers trawl.")]
        [SerializeField] Cell arenaCell;

        readonly TrawlSkipper _skipper = new();
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
            _skipper.Disarm();
            base.OnNetworkDespawn();
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[TrawlController] no settings asset wired; the AI skippers are not armed and keep " +
                                   "the Jacks' own herd-and-net rhythm.");
                return;
            }
            _skipper.Arm(gameData, settings, arenaCell, Centre);
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
                if (local != null) GameToastAPI.Post(GameToastSituation.TrawlHint, local.Domain);
            }

            if (IsServer && _skipper.IsArmed) _skipper.Tick();
        }

        /// <summary>Every sheaf in world space, read off the arena's own environment.</summary>
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
            _skipper.Disarm();
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
                    CSDebug.LogError($"[Trawl] Client could not match RoundStats for '{sName}'. " +
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
            _skipper.Disarm();

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
