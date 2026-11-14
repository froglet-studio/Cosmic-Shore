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
    /// Glasshouse - the Angler's MASS game: a timed garden. Take an omni crystal (a creature and a SEED), let go of the
    /// lure and the seed is planted in your colour; hold the lure over your plants and its light grows them three times
    /// as fast. The score is the mass standing in your colour when the clock runs out
    /// (<see cref="ScoringMetric.VolumeRemaining"/>): your trail, as every mode counts it, plus the garden grown from your
    /// seeds. Wild herds graze every garden outside the nucleus, and a rival's paint sends their school at yours. See
    /// GLASSHOUSE.md.
    ///
    /// <para><b>Nothing about the Angler changes</b> (the LOCKED vessel rule). The seed, the planting, the growth and
    /// the taming are all the Angler's own Mass ability (ANGLER.md §1d, §1g). The mode's levers are the cell (its
    /// species - planted only from seeds, its herds, its plant cap and prism scale, one spawn profile per intensity),
    /// the scene's omni crystals per intensity, and the AI gardeners.</para>
    ///
    /// <para><b>What this controller adds is the GARDEN's half of the score.</b> <c>StatsManager</c> already keeps
    /// each pilot's VolumeRemaining as the trail they hold standing. The garden is environment mass (no roster owner),
    /// so nothing credits it; on the server, every <see cref="GlasshouseSettingsSO.gardenSampleSeconds"/> this measures
    /// each domain's plants (<see cref="GlasshouseGarden.Measure"/>) and moves each pilot's VolumeRemaining by the change
    /// in their share (<see cref="GlasshouseGarden.ShareBook"/>). Plants are simulated per peer
    /// (<c>FloraNetworkSync</c> replicates where they stand, not their shape), so the server's garden is the one that
    /// scores, as the server's nucleus is in Brood Rush.</para>
    ///
    /// Structurally Tapestry's controller (timed, points mode, UseGolfRules = false, a
    /// <see cref="GlasshouseTimeTurnMonitor"/> as the scene's only monitor, a snapshot ClientRpc carrying the breakdown).
    /// ANGLER-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class GlasshouseController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag GlasshouseSettings.asset - the garden sample, the lead feedback and the AI gardeners. The round " +
                 "length lives in EndConditionOverridesSO, resolved by GlasshouseTimeTurnMonitor.")]
        [SerializeField] GlasshouseSettingsSO settings;

        [Tooltip("Drag GlasshouseScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The garden's cell. Read-only to this controller: its nucleus says which plants count (those outside " +
                 "it), and its centre, nucleus and membrane place the AI gardeners' beds.")]
        [SerializeField] Cell arenaCell;

        readonly GlasshouseGardener _gardener = new();
        readonly GlasshouseGarden.ShareBook _book = new();
        readonly float[] _garden = new float[GlasshouseGarden.DomainSlots];
        readonly int[] _pilots = new int[GlasshouseGarden.DomainSlots];

        Coroutine _sampler;
        bool _finalResultsSent;
        bool _hintPosted;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;
        float[] _snapshotGarden;
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
            _gardener.Disarm();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only. Every counter the metric and its breakdown read, and the garden credits with them.</summary>
        void ZeroCounters()
        {
            _book.Clear();
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
                CSDebug.LogWarning("[GlasshouseController] no settings asset wired; the garden is not measured and the " +
                                   "AI gardeners fly the platform's own steering.");
                return;
            }

            float nucleus = arenaCell ? arenaCell.NucleusWorldRadius : 0f;
            float membrane = arenaCell ? arenaCell.MembraneRadius : 1200f;
            _gardener.Arm(gameData, settings, Centre, nucleus, membrane);

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
                if (local != null) GameToastAPI.Post(GameToastSituation.GlasshouseHint, local.Domain);
            }

            if (IsServer && _gardener.IsArmed) _gardener.Tick();
        }

        // ── The garden (server) ──────────────────────────────────────────────

        void StopSampler()
        {
            if (_sampler == null) return;
            StopCoroutine(_sampler);
            _sampler = null;
        }

        IEnumerator SampleRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, settings.gardenSampleSeconds));
            while (!_finalResultsSent)
            {
                SettleGarden();
                SampleLead();
                yield return wait;
            }
            _sampler = null;
        }

        /// <summary>
        /// SERVER. Measures every domain's garden and moves each pilot's VolumeRemaining by the change in their share
        /// since the last measurement, so the stat stays "trail standing + garden standing".
        /// </summary>
        void SettleGarden()
        {
            if (!IsServer) return;
            var list = gameData.RoundStatsList;
            if (list == null) return;

            GlasshouseGarden.Measure(FloraHeartRegistry.Live, arenaCell, _garden);

            for (int i = 0; i < _pilots.Length; i++) _pilots[i] = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int slot = list[i] != null ? GlasshouseGarden.Slot(list[i].Domain) : -1;
                if (slot >= 0) _pilots[slot]++;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var stats = list[i];
                if (stats == null) continue;
                int slot = GlasshouseGarden.Slot(stats.Domain);
                float share = slot >= 0 ? GlasshouseGarden.Share(_garden[slot], _pilots[slot]) : 0f;
                float delta = _book.Settle(stats.Name, share);
                if (!Mathf.Approximately(delta, 0f)) stats.VolumeRemaining += delta;
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

            // The whistle's garden: one last measurement, so the score is what stood at the end, not a second ago.
            if (settings != null) SettleGarden();

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
            _gardener.Disarm();
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
            var gardenArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].VolumeRemaining;
                gardenArray[i] = _book.Credited(statsList[i].Name);
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, gardenArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumes,
            float[] gardens,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            _snapshotNames = new string[names.Length];
            _snapshotGarden = gardens;
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                _snapshotNames[i] = sName;
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Glasshouse] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: a grazer can still take a mouthful in the frames after
                // the server decided, and the board must show what was decided.
                stat.VolumeRemaining = volumes[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            // The breakdown travels too: a client measured its own garden, not the server's, so the grown column comes
            // from the snapshot.
            gameData.SetResults(rule is GlasshouseScoringRuleSO glasshouse
                ? glasshouse.BuildResults(gameData, SnapshotGarden)
                : rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        float SnapshotGarden(string pilot)
        {
            if (_snapshotNames == null || _snapshotGarden == null) return 0f;
            for (int i = 0; i < _snapshotNames.Length && i < _snapshotGarden.Length; i++)
                if (_snapshotNames[i] == pilot) return _snapshotGarden[i];
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
            _snapshotGarden = null;

            StopSampler();
            _gardener.Disarm();
            _book.Clear();

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
