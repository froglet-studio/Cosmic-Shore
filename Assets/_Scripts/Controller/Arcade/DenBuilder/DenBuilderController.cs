using System.Collections;
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
    /// Den Builder - the Goby &amp; Pistol's MASS game: a timed build in Glasshouse's grazed cell. The Goby darts and
    /// perches, laying the Pistol's islands; the Pistol rides them Tiny Wings style, earns ENERGY for every trail block it
    /// flies over instead of riding, and at each perch rests and builds a DEN from it (the Mass ability, Dens; Fortress
    /// at Mass 5). The score is the mass standing in your colour when the clock runs out
    /// (<see cref="ScoringMetric.VolumeRemaining"/>): dens, perches and the islands' trail. See DEN_BUILDER.md.
    ///
    /// <para><b>Nothing about the pair changes</b> (the LOCKED vessel rule). The mode's levers are the cell's: Glasshouse's
    /// herds graze whatever stands outside the NUCLEUS, whatever its colour, so a den built in the open is food and one
    /// built in the nucleus is safe - a choice of where to perch, the arena's, not the vessel's. Rival rings and the
    /// rivals' undertow take dens too. Glasshouse's species is planted only from an Angler's seed, so the cell grows no
    /// garden here.</para>
    ///
    /// <para><b>No mode AI.</b> An AI pair is the seat's own crew (<c>GobyPistolDuo</c>): its Goby lays islands, its
    /// Pistol jumps them and builds at every perch, which is the game.</para>
    ///
    /// <para><b>What this controller adds over the platform is only the lead change toast</b>: a timed mode has no
    /// target, so <see cref="DomainRaceToasts"/> has no quarter or halfway, and the lead is sampled here (Glasshouse's
    /// sampler). The metric is counted platform-wide by <c>StatsManager</c>.</para>
    ///
    /// Structurally Glasshouse's controller without the garden (timed, points mode, UseGolfRules = false, a
    /// <see cref="DenBuilderTimeTurnMonitor"/> as the scene's only monitor, a snapshot ClientRpc carrying the breakdown).
    /// GOBY-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class DenBuilderController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag DenBuilderScoringRule.asset (metric = VolumeRemaining, timed, highest wins). The round length " +
                 "lives in EndConditionOverridesSO, resolved by DenBuilderTimeTurnMonitor.")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Lead feedback")]
        [Tooltip("Seconds between lead samples on the server.")]
        [Min(0.1f)] [SerializeField] float leadSampleSeconds = 1f;

        [Tooltip("No lead change is called before this many seconds of the round: the first dens decide nothing.")]
        [Min(0f)] [SerializeField] float leadAnnounceAfterSeconds = 20f;

        [Tooltip("At least this many seconds between two lead-change toasts.")]
        [Min(0f)] [SerializeField] float leadAnnounceMinGapSeconds = 8f;

        Coroutine _sampler;
        bool _finalResultsSent;
        bool _hintPosted;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;
        string[] _snapshotNames;
        int[] _snapshotPrisms;

        protected override bool UseGolfRules => false;
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
            _leaderDomain = Domains.Blue;

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopSampler();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only. Every counter the metric and its breakdown read.</summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].VolumeRemaining = 0f;
                list[i].PrismsRemaining = 0;
                list[i].VolumeStolen = 0f;
                list[i].PrismStolen = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            StopSampler();
            _sampler = StartCoroutine(SampleRoutine());
        }

        void Update()
        {
            if (_finalResultsSent || gameData == null || !gameData.IsTurnRunning || _hintPosted) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.DenBuilderHint, local.Domain);
        }

        // ── The lead (server) ────────────────────────────────────────────────

        void StopSampler()
        {
            if (_sampler == null) return;
            StopCoroutine(_sampler);
            _sampler = null;
        }

        IEnumerator SampleRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, leadSampleSeconds));
            while (!_finalResultsSent)
            {
                SampleLead();
                yield return wait;
            }
            _sampler = null;
        }

        void SampleLead()
        {
            if (!IsServer || rule == null) return;
            if (Time.time - gameData.TurnStartTime < leadAnnounceAfterSeconds) return;

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue || leader == _leaderDomain) return;

            int volume = rule.DomainValue(gameData, leader);
            if (volume <= 0) return;

            bool first = _leaderDomain == Domains.Blue;
            _leaderDomain = leader;
            if (first) return;   // the first leader after the opening is not a CHANGE
            if (Time.time - _lastLeadAnnounce < leadAnnounceMinGapSeconds) return;

            _lastLeadAnnounce = Time.time;
            AnnounceLeadChanged_ClientRpc((int)leader, volume);
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int volume)
        {
            GameToastAPI.Post(GameToastSituation.DomainRaceLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), volume.ToString());
        }

        // ── Server-authoritative game end at time-out ────────────────────────

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
                .OrderByDescending(s => s.VolumeRemaining)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopSampler();
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
            var volumeArray = new float[count];
            var prismArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].VolumeRemaining;
                prismArray[i] = statsList[i].PrismsRemaining;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, prismArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumes,
            int[] prisms,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            _snapshotNames = new string[names.Length];
            _snapshotPrisms = prisms;
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                _snapshotNames[i] = sName;
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[DenBuilder] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: a grazer or a ring can still take a block in the frames
                // after the server decided, and the board must show what was decided.
                stat.VolumeRemaining = volumes[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            // The breakdown column is each pilot's standing prism count from the same snapshot.
            gameData.SetResults(rule is GlasshouseScoringRuleSO standing
                ? standing.BuildResults(gameData, SnapshotPrisms)
                : rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        float SnapshotPrisms(string pilot)
        {
            if (_snapshotNames == null || _snapshotPrisms == null) return 0f;
            for (int i = 0; i < _snapshotNames.Length && i < _snapshotPrisms.Length; i++)
                if (_snapshotNames[i] == pilot) return _snapshotPrisms[i];
            return 0f;
        }

        // ── Replay ───────────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _hintPosted = false;
            _leaderDomain = Domains.Blue;
            _lastLeadAnnounce = float.NegativeInfinity;
            _snapshotNames = null;
            _snapshotPrisms = null;

            StopSampler();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.VolumeRemaining = 0f;
                s.PrismsRemaining = 0;
                s.VolumeStolen = 0f;
                s.PrismStolen = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
