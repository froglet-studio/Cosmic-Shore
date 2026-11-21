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
    /// Haul - the Thresher's MASS game: a timed steal. Bales of neutral prisms hang in the cell (laid by this controller on
    /// every peer, <see cref="HaulField"/>). Every Thresher spawns in Mass Mode, where its ball STEALS every prism it
    /// touches that is not its colour, SHIELDS every one that is, and lays a trail of its own behind it (THRESHER.md 2b).
    /// The score is the volume a domain has STANDING in its colour when the clock runs out
    /// (<see cref="ScoringMetric.VolumeRemaining"/>): swing your ball through the bales to haul them home, sweep your
    /// hoard to armour it, and swing through a rival's hoard to steal it back. Press A and the ball smashes instead.
    ///
    /// <para><b>The platform keeps the score.</b> A stolen prism is credited to its thief by <c>StatsManager</c> (+ on
    /// steal, - from the victim), so this controller only lays the field, zeroes the counters, feeds the lead toast, steers
    /// the AI and snapshots the whistle.</para>
    ///
    /// <para><b>The arena is a bare nucleus cell, with no food web</b> (Tapestry's reasoning: grazers eat the colour a cell
    /// does not hold, the trailing team's fresh mass). Omni crystals respawn inside the nucleus: each one spins the ball
    /// round the ship for 12 s (the Windmill, THRESHER.md 2c), which in Mass Mode steals everything it sweeps.</para>
    ///
    /// <para><b>The AI only steers.</b> An autopiloted Thresher never presses A, so it stays in Mass Mode (THRESHER.md 2b);
    /// circling a bale at the chain's reach swings the ball through it. Nothing about the Thresher changes (the LOCKED
    /// vessel rule).</para>
    /// </summary>
    public class HaulController : MultiplayerDomainGamesController
    {
        const string FieldOwnerId = "HaulField";

        [Header("Config")]
        [Tooltip("Drag HaulSettings.asset - lead feedback and the AI's haul/raid policy. The field is HaulField; the " +
                 "round length lives in EndConditionOverridesSO.")]
        [SerializeField] HaulSettingsSO settings;

        [Tooltip("Drag HaulScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The scene's cell. Read-only to this controller: its centre places the bales, and its targeting " +
                 "grids answer 'where is the densest opposing mass' for a raiding AI.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("The environment prism every bale prism is laid from (SpawnablePrism.prefab).")]
        [SerializeField] Prism balePrefab;

        [Tooltip("The nucleus radius (u) the bales keep clear of. Authored to the cell's nucleus by the generator.")]
        [SerializeField, Min(0f)] float nucleusRadius = 392f;

        [Tooltip("The membrane radius (u) the bales keep inside. Authored to the cell's membrane by the generator.")]
        [SerializeField, Min(1f)] float membraneRadius = 1200f;

        struct BalePrism
        {
            public Prism Prism;
            public int Bale;
        }

        readonly List<ChumReef.Piece> _pieces = new();
        readonly List<BalePrism> _unclaimed = new();
        Vector3[] _baleCentres = System.Array.Empty<Vector3>();
        int[] _baleUnclaimed = System.Array.Empty<int>();
        Transform _fieldRoot;

        readonly Dictionary<IPlayer, bool> _aiRaiding = new();

        bool _finalResultsSent;
        bool _hintPosted;
        Coroutine _progressRoutine;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;

        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        Vector3 Centre => arenaCell ? arenaCell.transform.position : Vector3.zero;
        int Intensity => gameData != null && gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;
            _leaderDomain = Domains.Blue;

            LayField();
            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            DisarmHaulers();
            ClearField();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Server-only. Every counter the metric and its breakdown read. The live stock is zeroed
        /// at the whistle's opposite end too (<see cref="OnCountdownTimerEnded"/>), so the round
        /// counts only what was woven in it.
        /// </summary>
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

        // ── The field (every peer) ───────────────────────────────────────────

        void LayField()
        {
            ClearField();
            if (!balePrefab)
            {
                CSDebug.LogWarning("[Haul] no balePrefab on the controller - the field is not laid. " +
                                   "Re-run Tools/Build/author_haul_assets.py.");
                return;
            }

            var rung = HaulField.ForIntensity(Intensity);
            HaulField.Lay(Intensity, nucleusRadius, membraneRadius, _pieces);
            _fieldRoot = new GameObject("HaulField").transform;
            _fieldRoot.position = Centre;
            var trail = new Trail();
            var scale = Vector3.one * ChumReef.PrismSize;

            _baleCentres = new Vector3[rung.ShoalCount];
            _baleUnclaimed = new int[rung.ShoalCount];
            for (int b = 0; b < rung.ShoalCount; b++)
                _baleCentres[b] = Centre + ChumReef.ShoalCentre(rung, b, nucleusRadius, membraneRadius);

            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                var prism = PrismTrailBuilder.LayOne(balePrefab,
                    new PrismLay(new SpawnPoint(piece.Position, Quaternion.identity, scale), Domains.Blue,
                                 piece.Shielded ? PrismKind.Shielded : PrismKind.Plain),
                    _fieldRoot, trail, FieldOwnerId, watchForReveal: true);
                if (!prism) continue;
                _unclaimed.Add(new BalePrism { Prism = prism, Bale = piece.Shoal });
                _baleUnclaimed[piece.Shoal]++;
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Haul] intensity {Intensity}: {rung.ShoalCount} bales, {_unclaimed.Count} prisms" +
                (rung.Crusted ? ", crusted" : string.Empty) + ".");
        }

        /// <summary>Still an unclaimed bale prism: alive, not re-issued by the pool, and still no team's.</summary>
        static bool Unclaimed(Prism prism) =>
            prism && !prism.destroyed && prism.Domain == Domains.Blue && prism.ownerID == FieldOwnerId;

        void ClearField()
        {
            for (int i = 0; i < _unclaimed.Count; i++)
            {
                var prism = _unclaimed[i].Prism;
                if (Unclaimed(prism)) prism.Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
            }
            _unclaimed.Clear();
            if (_fieldRoot) Destroy(_fieldRoot.gameObject);
            _fieldRoot = null;
        }

        /// <summary>Counts each bale's unclaimed prisms down (only the AI reads it). Swap-remove keeps it O(unclaimed).
        /// A stolen prism stays in the cell in its thief's colour; it simply leaves this list.</summary>
        void SettleField()
        {
            for (int i = _unclaimed.Count - 1; i >= 0; i--)
            {
                var entry = _unclaimed[i];
                if (Unclaimed(entry.Prism)) continue;
                if (entry.Bale >= 0 && entry.Bale < _baleUnclaimed.Length) _baleUnclaimed[entry.Bale]--;
                int last = _unclaimed.Count - 1;
                _unclaimed[i] = _unclaimed[last];
                _unclaimed.RemoveAt(last);
            }
        }

        int NearestBale(Vector3 from)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int b = 0; b < _baleCentres.Length; b++)
            {
                if (_baleUnclaimed[b] <= 0) continue;
                float d = (_baleCentres[b] - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = b;
            }
            return best;
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded();
            ArmHaulers();

            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

        void Update()
        {
            SettleField();
            if (_hintPosted || _finalResultsSent || gameData == null || !gameData.IsTurnRunning) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.HaulHint, local.Domain);
        }

        // ── Lead feedback (server samples, every peer gets the toast) ─────────

        void StopProgressSampler()
        {
            if (_progressRoutine == null) return;
            StopCoroutine(_progressRoutine);
            _progressRoutine = null;
        }

        IEnumerator ProgressRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, settings ? settings.progressSampleSeconds : 1f));
            while (!_finalResultsSent)
            {
                SampleLead();
                yield return wait;
            }
            _progressRoutine = null;
        }

        void SampleLead()
        {
            if (!IsServer || rule == null || settings == null) return;
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

        // ── AI (server): haul while ahead, raid while behind ───────────────────

        /// <summary>
        /// Every AI Thresher decides, on a slow clock (<see cref="HaulSettingsSO.aiDecisionSeconds"/>), whether to HAUL or
        /// RAID, and only STEERS (<see cref="AIPilot.SetExternalTargetProvider"/>). It never presses A, so its ball stays in
        /// Mass Mode and steals whatever it touches that is not its colour:
        /// <list type="bullet">
        /// <item><b>Haul</b> while its domain is not trailing the best rival by more than its rung of
        /// <see cref="HaulSettingsSO.aiRaidDeficitFraction"/>: the nearest bale with prisms unclaimed (kept until hauled,
        /// re-picked every <see cref="HaulSettingsSO.aiRetargetSeconds"/>), circled at
        /// <see cref="HaulSettingsSO.aiOrbitRadius"/> so the ball on its chain swings through it.</item>
        /// <item><b>Raid</b> while it trails: circling the densest opposing mass (<see cref="Cell.GetExplosionTarget"/>),
        /// where the ball steals the rival's hoard back.</item>
        /// </list>
        /// Difficulty is the seat's AI level, never the intensity. Cleared on the whistle, despawn and replay.
        /// </summary>
        void ArmHaulers()
        {
            if (settings == null)
            {
                CSDebug.LogWarning("[HaulController] no settings asset wired; AI haulers not armed.");
                return;
            }

            Vector3 centre = Centre;
            _aiRaiding.Clear();

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var pilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (pilot == null) continue;

                var captured = p;
                int level = (int)gameData.AIDifficultyFor(p);
                float nextDecision = 0f;
                float retargetAt = 0f;
                int bale = -1;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;
                    if (_finalResultsSent) return centre;

                    float now = Time.time;
                    if (now >= nextDecision)
                    {
                        nextDecision = now + settings.aiDecisionSeconds;
                        _aiRaiding[captured] = ShouldRaid(captured.Domain, level);
                    }
                    bool raid = _aiRaiding.TryGetValue(captured, out var r) && r;
                    Vector3 at = selfTf.position;

                    Vector3 hub;
                    if (raid && arenaCell)
                        hub = arenaCell.GetExplosionTarget(captured.Domain);
                    else
                    {
                        if (bale < 0 || now >= retargetAt || bale >= _baleUnclaimed.Length || _baleUnclaimed[bale] <= 0)
                        {
                            bale = NearestBale(at);
                            retargetAt = now + HaulSettingsSO.Rung(settings.aiRetargetSeconds, level);
                        }
                        if (bale < 0) return centre;
                        hub = _baleCentres[bale];
                    }

                    // Circle the hub at the orbit radius: far off, fly at it; close in, aim along the tangent, so the
                    // ball trailing on its chain swings through what the hull is circling.
                    Vector3 to = hub - at;
                    float orbit = settings.aiOrbitRadius;
                    if (to.sqrMagnitude > orbit * orbit * 4f) return hub;
                    return hub + Vector3.Cross(to.normalized, selfTf.up) * orbit;
                });
            }
        }

        bool ShouldRaid(Domains own, int level)
        {
            if (rule == null) return false;

            var monitor = TurnMonitorRemaining();
            float late = HaulSettingsSO.Rung(settings.aiLateRaidSeconds, level);
            if (late > 0f && monitor >= 0f && monitor <= late) return true;

            float deficit = HaulSettingsSO.Rung(settings.aiRaidDeficitFraction, level);
            if (deficit >= 1f) return false;

            int mine = rule.DomainValue(gameData, own);
            int best = 0;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (d == own) continue;
                best = Mathf.Max(best, rule.DomainValue(gameData, d));
            }
            return best > 0 && mine < best * (1f - deficit);
        }

        /// <summary>Seconds left on the round clock, or -1 when this scene has no timed monitor.</summary>
        float TurnMonitorRemaining()
        {
            // Resolved once and cached - there is exactly one monitor per gameplay scene - and
            // re-resolved only if it dies. Asked on the AI's slow decision clock, never per frame.
            if (!_clock) _clock = FindAnyObjectByType<TimeBasedTurnMonitor>();
            return _clock ? _clock.TimeRemaining : -1f;
        }

        TimeBasedTurnMonitor _clock;

        void DisarmHaulers()
        {
            _aiRaiding.Clear();
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status is Component c && c && c.TryGetComponent(out AIPilot ai)) ai.ClearExternalTargetProvider();
            }
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
            StopProgressSampler();
            DisarmHaulers();
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
            var stolenArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].VolumeRemaining;
                stolenArray[i] = statsList[i].VolumeStolen;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, stolenArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumes,
            float[] stolen,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Haul] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: mass can still be destroyed in the
                // frames after the server decided, and the board must show what was decided.
                stat.VolumeRemaining = volumes[i];
                stat.VolumeStolen = stolen[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

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

            StopProgressSampler();
            DisarmHaulers();

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
