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
    /// Thornbush - the Shrike's MASS game: a timed nest-building match in Jackstraw's heap of sticks (its cells,
    /// referenced read-only). Dock on a stick, press Mass and the Shrike leaves a NEST: a cup of super-shielded blocks
    /// crowned with a missile plant (SHRIKE.md §3). The score is the mass standing in your colour when the clock runs out
    /// (<see cref="ScoringMetric.VolumeRemaining"/>): your nests and your trail. A rival's missiles crack shields and
    /// break what is under them; with Void Pierce (Space 5) their lance breaks nest blocks outright. See THORNBUSH.md.
    ///
    /// <para><b>Nothing about the Shrike changes</b> (the LOCKED vessel rule). The nest, its size (Mass), its cap of three
    /// and its missile plant are the Shrike's own kit. The mode's levers are the heap (Jackstraw's cells), the crystals
    /// per intensity (the scene's CrystalManager) and the AI builders.</para>
    ///
    /// <para><b>This controller credits nothing.</b> A nest block is laid in the pilot's name, so <c>StatsManager</c>
    /// counts it into their VolumeRemaining like the trail, and out again when it breaks. The controller zeroes the stat
    /// at the start, arms the AI, calls lead changes and decides the winner at the whistle.</para>
    ///
    /// Structurally Glasshouse's controller without the garden (timed, points mode, UseGolfRules = false, a
    /// <see cref="ThornbushTimeTurnMonitor"/> as the scene's only monitor, a snapshot ClientRpc carrying the breakdown).
    /// SHRIKE-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class ThornbushController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag ThornbushSettings.asset - the lead feedback and the AI builders. The round length lives in " +
                 "EndConditionOverridesSO, resolved by ThornbushTimeTurnMonitor.")]
        [SerializeField] ThornbushSettingsSO settings;

        [Tooltip("Drag ThornbushScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The heap's cell. Read-only to this controller: its centre is the heap's, where the AI builders pick " +
                 "their perches.")]
        [SerializeField] Cell arenaCell;

        readonly ThornbushBuilder _builder = new();
        Coroutine _sampler;
        bool _finalResultsSent;
        bool _hintPosted;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;

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
            _builder.Disarm();
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
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[ThornbushController] no settings asset wired; the AI builders are not armed and " +
                                   "fly the platform's own steering.");
                return;
            }
            _builder.Arm(gameData, settings, Centre, HeapRadius());
            StopSampler();
            _sampler = StartCoroutine(SampleRoutine());
        }

        /// <summary>The heap's radius at the selected intensity - the MAP, which the controller may read; the AI's skill
        /// is its seat's difficulty.</summary>
        float HeapRadius()
        {
            var radii = settings.heapRadiusByIntensity;
            if (radii == null || radii.Length == 0) return 470f;
            int intensity = gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;
            return radii[Mathf.Clamp(intensity - 1, 0, radii.Length - 1)];
        }

        void Update()
        {
            if (_finalResultsSent || gameData == null || !gameData.IsTurnRunning) return;

            if (!_hintPosted)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.ThornbushHint, local.Domain);
            }

            if (IsServer && _builder.IsArmed) _builder.Tick();
        }

        // ── Lead feedback (server) ──

        void StopSampler()
        {
            if (_sampler == null) return;
            StopCoroutine(_sampler);
            _sampler = null;
        }

        IEnumerator SampleRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, settings.leadSampleSeconds));
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

        // ── Server-authoritative game end at time-out ──

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
            _builder.Disarm();
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
            var blockArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].VolumeRemaining;
                blockArray[i] = statsList[i].PrismsRemaining;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, blockArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumes,
            int[] blocks,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Thornbush] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: a missile can still land in the frames after the server
                // decided, and the board must show what was decided. The breakdown travels too.
                stat.VolumeRemaining = volumes[i];
                stat.PrismsRemaining = blocks[i];
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
            _leaderDomain = Domains.Blue;
            _lastLeadAnnounce = float.NegativeInfinity;

            StopSampler();
            _builder.Disarm();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.VolumeRemaining = 0f;
                s.PrismsRemaining = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
