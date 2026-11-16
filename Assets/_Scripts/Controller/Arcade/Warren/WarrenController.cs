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
    /// Warren - the Meerkat's MASS game: a timed burrow war in the bare Barren cell. The score is the mass standing in
    /// your colour when the clock runs out (<see cref="ScoringMetric.VolumeRemaining"/>, counted by <c>StatsManager</c>
    /// as in every mode): your trail, and the EXIT PORTAL every soliton you form rings its way out with - six of your
    /// prisms on a hexagon, bigger with Mass (<c>MeerkatExitPortal</c>, MEERKAT.md §10.4). The only other mass in the
    /// cell is your rivals', so the raid is the Meerkat's own Space: a soliton formed beside a rival's mass SWALLOWS it
    /// (<c>MeerkatSwallow</c>) and fires it out of the way out, and each fired prism that runs into another breaks both.
    /// See WARREN.md.
    ///
    /// <para><b>Nothing about the Meerkat changes</b> (the LOCKED vessel rule). The mode's levers are the cell (bare on
    /// purpose, Tapestry's reasoning: nothing to hide behind and nothing to score but pilots' mass), the scene's omni
    /// crystals per intensity, the card's per-intensity starting elements (I3 starts at Mass 5, Danger Portal; I4 at
    /// Mass 5 and Space 5, Steal), and the AI burrowers (<see cref="MeerkatArcadeSteer"/>: steering only).</para>
    ///
    /// Structurally Glasshouse's controller without the garden (timed, points mode, UseGolfRules = false, a
    /// <see cref="WarrenTimeTurnMonitor"/> as the scene's only monitor, the lead sampled for the lead-change toast, a
    /// snapshot ClientRpc carrying the breakdown). MEERKAT-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class WarrenController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag WarrenSettings.asset - the lead feedback and the AI burrowers. The round length lives in " +
                 "EndConditionOverridesSO, resolved by WarrenTimeTurnMonitor.")]
        [SerializeField] WarrenSettingsSO settings;

        [Tooltip("Drag WarrenScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The bare cell (Barren). Read-only to this controller: its centre, nucleus and membrane bound where the " +
                 "AI burrowers lay.")]
        [SerializeField] Cell arenaCell;

        readonly MeerkatArcadeSteer _steer = new();

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
            _steer.Disarm();
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

            if (settings == null)
            {
                CSDebug.LogWarning("[WarrenController] no settings asset wired; the lead is not sampled and the AI " +
                                   "burrowers fly the platform's own steering.");
                return;
            }

            _steer.Arm(gameData, (int)GameModes.Warren, Centre, PickBurrow,
                s => MeerkatArcadeSteer.Rung(settings.aiRetargetSeconds, s.Level));

            StopSampler();
            _sampler = StartCoroutine(SampleRoutine());
        }

        /// <summary>
        /// Where a burrower goes next. By the rung's chance, a RAID: just behind the nearest rival, where their freshest
        /// trail is, so the next soliton forms beside it and swallows it. Otherwise an omni crystal within reach (a level
        /// of Mass makes every portal bigger, of Space every soliton), or else a fresh point in open space between the
        /// nucleus and the membrane, to lay trail and portals where nobody has raided yet. Steering only.
        /// </summary>
        Vector3 PickBurrow(MeerkatArcadeSteer.Seat seat, Transform self)
        {
            int level = seat.Level;
            float error = MeerkatArcadeSteer.Rung(settings.aiAimError, level);
            var rival = _steer.NearestRival(seat, self.position, MeerkatArcadeSteer.Rung(settings.aiRaidRange, level));
            if (rival != null && seat.Rng.NextDouble() < MeerkatArcadeSteer.Rung(settings.aiRaidChance, level))
                return MeerkatArcadeSteer.Jitter(seat,
                    MeerkatArcadeSteer.Led(rival, -MeerkatArcadeSteer.Rung(settings.aiRaidBehindSeconds, level)), error);

            if (MeerkatArcadeSteer.NearestOmniCrystal(self.position) is Vector3 crystal &&
                Vector3.Distance(crystal, self.position) <= settings.aiCrystalReach)
                return crystal;

            float nucleus = arenaCell ? arenaCell.NucleusWorldRadius : 0f;
            float membrane = arenaCell ? arenaCell.MembraneRadius : 1200f;
            float inner = nucleus + settings.aiLayClearance;
            float outer = Mathf.Max(inner + 1f, membrane * settings.aiLayOuterFraction);
            var dir = new Vector3((float)(seat.Rng.NextDouble() * 2 - 1), (float)(seat.Rng.NextDouble() * 2 - 1),
                                  (float)(seat.Rng.NextDouble() * 2 - 1));
            if (dir.sqrMagnitude < 1e-6f) dir = self.forward;
            return Centre + dir.normalized * Mathf.Lerp(inner, outer, (float)seat.Rng.NextDouble());
        }

        void Update()
        {
            if (_finalResultsSent || gameData == null || !gameData.IsTurnRunning) return;

            if (!_hintPosted)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.WarrenHint, local.Domain);
            }

            if (IsServer && _steer.IsArmed) _steer.Tick();
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
            _steer.Disarm();
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
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Warren] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: a fired prism can still break something in the frames
                // after the server decided, and the board must show what was decided. The breakdown travels too.
                stat.VolumeRemaining = volumes[i];
                stat.PrismsRemaining = prisms[i];
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

        // ── Replay ───────────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _hintPosted = false;
            _leaderDomain = Domains.Blue;
            _lastLeadAnnounce = float.NegativeInfinity;

            StopSampler();
            _steer.Disarm();

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
