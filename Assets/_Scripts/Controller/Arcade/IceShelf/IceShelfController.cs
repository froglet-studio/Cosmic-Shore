using System.Collections;
using System.Collections.Generic;
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
    /// Ice Shelf - the Orca's MASS game: a timed hold. The score is the mass standing in your colour when the clock runs
    /// out (<see cref="ScoringMetric.VolumeRemaining"/>): the helm's trail, the fast gun's pen strokes, and the helm's
    /// DANGER SHIELD - 144 super-shielded prisms carried round the hull, then dropped where they stand as danger prisms
    /// that linger 20 s. A shield dropped in the last half minute is still standing at the whistle, so the closing seconds
    /// are where the shelf is built. Rival gunners break your mass; wild herds graze it outside the nucleus. See
    /// ICESHELF.md.
    ///
    /// <para><b>Nothing about the Orca changes</b> (the LOCKED vessel rule). The arena is Glasshouse's four cells,
    /// referenced: the standard cell with a nucleus, its tameless grazing herds, and nothing planted wild.</para>
    ///
    /// <para><b>What this controller adds is the MEASUREMENT.</b> Every <see cref="OrcaArcadeSettingsSO.sampleSeconds"/>
    /// the server counts the mass each pilot actually has standing (<see cref="IceShelfCensus"/>) and sets their
    /// VolumeRemaining to it: an expired shield or a recycled pen stroke goes back to its pool without a destroyed event,
    /// so the running stat alone would keep every shield ever raised.</para>
    ///
    /// <para><b>The AI is the crew's own plus a helm.</b> The gunners shoot rival mass and hulls on their own
    /// (<c>OrcaCrewAI.cs</c>); <see cref="OrcaArcadeHelm"/> (goal Shelf) flies each AI hull round a loop of its own, spread
    /// round the nucleus, so its trail closes a ring.</para>
    ///
    /// Structurally Glasshouse's controller (timed, points mode, UseGolfRules = false, an
    /// <see cref="IceShelfTimeTurnMonitor"/> as the scene's only monitor, a snapshot ClientRpc carrying the breakdown).
    /// ORCA-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class IceShelfController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag IceShelfSettings.asset - the census sample, the lead feedback and the AI helm. The round " +
                 "length lives in EndConditionOverridesSO, resolved by IceShelfTimeTurnMonitor.")]
        [SerializeField] OrcaArcadeSettingsSO settings;

        [Tooltip("Drag IceShelfScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The shelf's cell. Read-only to this controller: its centre and nucleus place the AI helms' loops.")]
        [SerializeField] Cell arenaCell;

        readonly OrcaArcadeHelm _helm = new();
        readonly IceShelfCensus _census = new();
        readonly List<string> _names = new();

        Coroutine _sampler;
        bool _finalResultsSent;
        bool _hintPosted;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;
        float[] _snapshotShelf;
        string[] _snapshotNames;

        protected override bool UseGolfRules => false;
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
            _leaderDomain = Domains.Blue;

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopSampler();
            _helm.Disarm();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only. Every counter the metric and its breakdown read, and the census with them.</summary>
        void ZeroCounters()
        {
            _census.Clear();
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

            if (settings == null)
            {
                CSDebug.LogWarning("[IceShelfController] no settings asset wired; the mass is not measured and the " +
                                   "AI hulls fly the platform's own steering.");
                return;
            }

            float nucleus = arenaCell ? arenaCell.NucleusWorldRadius : 0f;
            _helm.Arm(gameData, settings, OrcaArcadeHelm.Goal.Shelf, Centre, nucleus > 0f ? nucleus : 400f);

            StopSampler();
            _sampler = StartCoroutine(SampleRoutine());
        }

        void Update()
        {
            if (_finalResultsSent || gameData == null || !gameData.IsTurnRunning) return;

            if (!_hintPosted)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.IceShelfHint, local.Domain);
            }

            if (IsServer && _helm.IsArmed) _helm.Tick();
        }

        // ── The census (server) ──────────────────────────────────────────────

        void StopSampler()
        {
            if (_sampler == null) return;
            StopCoroutine(_sampler);
            _sampler = null;
        }

        IEnumerator SampleRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, settings.sampleSeconds));
            while (!_finalResultsSent)
            {
                Census();
                SampleLead();
                yield return wait;
            }
            _sampler = null;
        }

        /// <summary>
        /// SERVER. Counts the mass every pilot has standing and sets their VolumeRemaining to it
        /// (<see cref="IceShelfCensus"/>: the running stat alone would keep every expired shield).
        /// </summary>
        void Census()
        {
            if (!IsServer) return;
            var list = gameData.RoundStatsList;
            if (list == null) return;

            _names.Clear();
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) _names.Add(list[i].Name);
            _census.Measure(_names);

            for (int i = 0; i < list.Count; i++)
            {
                var stats = list[i];
                if (stats == null) continue;
                float standing = _census.Volume(stats.Name);
                if (!Mathf.Approximately(stats.VolumeRemaining, standing)) stats.VolumeRemaining = standing;
            }
        }

        void SampleLead()
        {
            if (!IsServer || rule == null) return;
            if (Time.time - gameData.TurnStartTime < settings.leadAnnounceAfterSeconds) return;

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue || leader == _leaderDomain) return;

            int volume = rule.DomainValue(gameData, leader);
            if (volume <= 0) return;

            bool first = _leaderDomain == Domains.Blue;
            _leaderDomain = leader;
            if (first) return;   // the first leader after the opening is not a CHANGE
            if (Time.time - _lastLeadAnnounce < settings.leadAnnounceMinGapSeconds) return;

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

            // The whistle's census: one last measurement, so the score is what stood at the end, not a second ago.
            Census();

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
            var volumeArray = new float[count];
            var shelfArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].VolumeRemaining;
                shelfArray[i] = _census.Shelf(statsList[i].Name);
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, shelfArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumes,
            float[] shelves,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            _snapshotNames = new string[names.Length];
            _snapshotShelf = shelves;
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                _snapshotNames[i] = sName;
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[IceShelf] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: a shield can still expire or a round still land in the
                // frames after the server decided, and the board must show what was decided.
                stat.VolumeRemaining = volumes[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            // The breakdown travels too: only the server measured, so the shelf column comes from the snapshot.
            gameData.SetResults(rule is GlasshouseScoringRuleSO shelfRule
                ? shelfRule.BuildResults(gameData, SnapshotShelf)
                : rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        float SnapshotShelf(string pilot)
        {
            if (_snapshotNames == null || _snapshotShelf == null) return 0f;
            for (int i = 0; i < _snapshotNames.Length && i < _snapshotShelf.Length; i++)
                if (_snapshotNames[i] == pilot) return _snapshotShelf[i];
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
            _snapshotShelf = null;

            StopSampler();
            _helm.Disarm();
            _census.Clear();

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
