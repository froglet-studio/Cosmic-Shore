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
    /// Depth Charge - the Orca's SPACE game: a demolition race through Rampage's forest (its four cells, referenced, not
    /// forked). Every hostile prism the crew destroys counts (<see cref="ScoringMetric.PrismsDestroyed"/>): the gunners'
    /// missiles burst on contact, the bomb seat throws and bursts its bomb in the canopy, the sniper's line breaks
    /// everything hostile on it, and the fast gun chips. The first DOMAIN to the target wins (golf-timed). See
    /// DEPTHCHARGE.md.
    ///
    /// <para>Structurally Harvest's controller (Rampage's shape: 1 round / 1 turn, HasEndGame=false, server winner
    /// detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown). <see cref="DepthChargeScoringRuleSO"/> is
    /// Harvest's rule with its own words. Nothing about the Orca changes: the blasts are its own <c>AOEExplosion</c>s,
    /// which already name its pilot as the attacker.</para>
    ///
    /// <para><b>The field restocks.</b> Rampage's forest grows back (its flora reproduce and its seeders replant), so the
    /// field cannot run dry under three domains - the reason the arena is Rampage's and not a baked one (HARVEST.md's
    /// lesson: a field that never regrows must fit three targets in its smallest hostile pool).</para>
    ///
    /// <para><b>The AI is the crew's own plus a helm.</b> The gunners pick hostile prisms inside 250 u on their own
    /// (<c>OrcaCrewAI.cs</c>); <see cref="OrcaArcadeHelm"/> (goal Raid) only flies the hull to the nearest plant that is
    /// not in its colour.</para>
    ///
    /// ORCA-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class DepthChargeController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag DepthChargeSettings.asset - the AI helm. The prism target lives in EndConditionOverridesSO, " +
                 "resolved by DepthChargePrismTurnMonitor; the results wording on the rule.")]
        [SerializeField] OrcaArcadeSettingsSO settings;

        [Tooltip("Drag DepthChargeScoringRule.asset (metric = PrismsDestroyed, golf).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The forest's cell. Read-only to this controller: its position is the arena CENTRE the helm falls back " +
                 "to with no plant in range.")]
        [SerializeField] Cell arenaCell;

        readonly OrcaArcadeHelm _helm = new();
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

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _helm.Disarm();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only: the setters push through server-write NetworkVariables.</summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) list[i].HostilePrismsDestroyed = 0;
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[DepthChargeController] no settings asset wired; the AI helm is not armed and the " +
                                   "hulls fly the platform's own steering.");
                return;
            }
            _helm.Arm(gameData, settings, OrcaArcadeHelm.Goal.Raid, Centre, 0f);
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
                if (local != null) GameToastAPI.Post(GameToastSituation.DepthChargeHint, local.Domain);
            }

            if (IsServer && _helm.IsArmed) _helm.Tick();
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
            _helm.Disarm();
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
                    CSDebug.LogError($"[DepthCharge] Client could not match RoundStats for '{sName}'. " +
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
            _helm.Disarm();

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
