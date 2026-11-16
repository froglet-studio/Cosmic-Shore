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
    /// Riptide - the Goby &amp; Pistol's SPACE game: Shatterplane's field of neutral plates. The Goby darts its islands
    /// through the plates; the Pistol's UNDERTOW (the Space ability) pulls their loose tiles into its plane, and its vortex
    /// rings gather them and smash them as they collapse. First DOMAIN to the hostile-prism target wins, golf-timed, on
    /// Rampage's metric (<see cref="ScoringMetric.PrismsDestroyed"/> through <see cref="RiptideScoringRuleSO"/>, resolved
    /// by <see cref="RiptidePrismTurnMonitor"/>). See RIPTIDE.md.
    ///
    /// <para><b>Nothing about the pair changes</b> (the LOCKED vessel rule). The plates are the arena's lever: neutral
    /// plain prisms the undertow may move (super-shielded ribs and danger it never moves, and at I4 they brace and screen
    /// the plates), standing still in the cell for a Goby to lay its islands past. Shatterplane's own crystal field and
    /// nucleus sizing are that mode's controller's, not the cell's, so they are not here: the crystals are the scene's
    /// CrystalManager's.</para>
    ///
    /// <para><b>No mode AI.</b> An AI pair is the seat's own crew (<c>GobyPistolDuo</c>): its Goby routes each island
    /// through the most breakable rival mass (<c>AiPlanRoute</c>, the path map's own score) and its Pistol snaps at what
    /// the undertow brings in, which is the game.</para>
    ///
    /// Structurally Shatterplane's prism race without the field (1 round / 1 turn, HasEndGame=false, server winner
    /// detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown). GOBY-ONLY is enforced by the arcade
    /// card's Vessels list.
    /// </summary>
    public class RiptideController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag RiptideScoringRule.asset (metric = PrismsDestroyed, Rampage's rule). The target lives in " +
                 "EndConditionOverridesSO, resolved by RiptidePrismTurnMonitor.")]
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
                list[i].HostilePrismsDestroyed = 0;
                list[i].HostileVolumeDestroyed = 0f;
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
            if (local != null) GameToastAPI.Post(GameToastSituation.RiptideHint, local.Domain);
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
            var volumeArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                prismsArray[i] = statsList[i].HostilePrismsDestroyed;
                volumeArray[i] = statsList[i].HostileVolumeDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, prismsArray, volumeArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prismsDestroyed,
            float[] volumeDestroyed,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Riptide] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The metric and its breakdown travel as the server decided them: a ring still collapsing on a
                // client after the whistle must not move the board.
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
                stat.HostileVolumeDestroyed = volumeDestroyed[i];
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

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.HostileVolumeDestroyed = 0f;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
