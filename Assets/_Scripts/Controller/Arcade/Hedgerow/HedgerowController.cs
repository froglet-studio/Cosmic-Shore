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
    /// Hedgerow - the Serpent's MASS game: a timed steal. Hedges of neutral prisms - straight rows of the Serpent trail's
    /// own bricks - stand round the core (laid by this controller on every peer, <see cref="HedgerowField"/>). Stop dead
    /// (the stance) just short of one, flying across it, and your latest trail prism becomes a held seed: a wall grows in
    /// the plane across your heading, stealing loose mass from its edge, so it creeps along the hedge and every brick it
    /// takes turns your colour (SERPENT_SEED_WALL.md). It steals a rival's trail and bricks the same way. The score is the
    /// volume a domain has STANDING in its colour when the clock runs out (<see cref="ScoringMetric.VolumeRemaining"/>):
    /// your trail, your walls and everything they ate.
    ///
    /// <para><b>The platform keeps the score.</b> A stolen prism is credited to its thief by <c>StatsManager</c> (+ on
    /// steal, - from the victim), so this controller only lays the field, zeroes the counters, feeds the lead toast, arms
    /// the AI and snapshots the whistle.</para>
    ///
    /// <para><b>The arena is a bare nucleus cell, with no food web</b> (Tapestry's reasoning: grazers eat the colour a cell
    /// does not hold). Omni crystals respawn inside the nucleus: each one re-shields every active wall the Serpent owns
    /// (SERPENT_SEED_WALL.md, "Crystals re-shield every active wall"), so a wall a rival would eat back is armoured.</para>
    ///
    /// <para><b>The AI presses only the stop toggle</b> (<see cref="HedgerowWarden"/>). Nothing about the Serpent changes
    /// (the LOCKED vessel rule).</para>
    /// </summary>
    public class HedgerowController : MultiplayerDomainGamesController, HedgerowWarden.IFieldView
    {
        const string FieldOwnerId = "HedgerowField";

        [Header("Config")]
        [Tooltip("Drag HedgerowSettings.asset - lead feedback and the AI warden's policy. The field is HedgerowField; " +
                 "the round length lives in EndConditionOverridesSO.")]
        [SerializeField] HedgerowSettingsSO settings;

        [Tooltip("Drag HedgerowScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The scene's cell. Read-only to this controller: its centre places the hedges, and its targeting " +
                 "grids answer 'where is the densest opposing mass' for a raiding AI.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("The environment prism every hedge prism is laid from (SpawnablePrism.prefab).")]
        [SerializeField] Prism hedgePrefab;

        [Tooltip("The nucleus radius (u) the hedges keep clear of. Authored to the cell's nucleus by the generator.")]
        [SerializeField, Min(0f)] float nucleusRadius = 392f;

        [Tooltip("The membrane radius (u) the hedges keep inside. Authored to the cell's membrane by the generator.")]
        [SerializeField, Min(1f)] float membraneRadius = 1200f;

        struct HedgePrism
        {
            public Prism Prism;
            public int Row;
        }

        readonly List<HedgerowField.Piece> _pieces = new();
        readonly List<HedgePrism> _unclaimed = new();
        Vector3[] _rowMidpoints = System.Array.Empty<Vector3>();
        Vector3[] _rowApproaches = System.Array.Empty<Vector3>();
        int[] _rowUnclaimed = System.Array.Empty<int>();
        Transform _fieldRoot;

        readonly HedgerowWarden _wardens = new();

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

        // ── What the warden reads (HedgerowWarden.IFieldView) ────────────────

        int HedgerowWarden.IFieldView.RowCount => _rowMidpoints.Length;
        Vector3 HedgerowWarden.IFieldView.RowMidpoint(int row) => _rowMidpoints[row];
        Vector3 HedgerowWarden.IFieldView.RowApproach(int row) => _rowApproaches[row];
        int HedgerowWarden.IFieldView.Unclaimed(int row) => row >= 0 && row < _rowUnclaimed.Length ? _rowUnclaimed[row] : 0;

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
            _wardens.Disarm();
            ClearField();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Server-only. Every counter the metric and its breakdown read. The live stock is zeroed
        /// at the whistle's opposite end too (<see cref="OnCountdownTimerEnded"/>), so the round
        /// counts only what was walled in it.
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
            if (!hedgePrefab)
            {
                CSDebug.LogWarning("[Hedgerow] no hedgePrefab on the controller - the field is not laid. " +
                                   "Re-run Tools/Build/author_hedgerow_assets.py.");
                return;
            }

            var rung = HedgerowField.ForIntensity(Intensity);
            HedgerowField.Lay(rung, nucleusRadius, membraneRadius, _pieces);
            _fieldRoot = new GameObject("HedgerowField").transform;
            _fieldRoot.position = Centre;
            var trail = new Trail();

            _rowMidpoints = new Vector3[rung.RowCount];
            _rowApproaches = new Vector3[rung.RowCount];
            _rowUnclaimed = new int[rung.RowCount];
            for (int r = 0; r < rung.RowCount; r++)
            {
                _rowMidpoints[r] = Centre + HedgerowField.RowMidpoint(rung, r, nucleusRadius, membraneRadius);
                _rowApproaches[r] = HedgerowField.Approach(rung, r);
            }

            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                var prism = PrismTrailBuilder.LayOne(hedgePrefab,
                    new PrismLay(new SpawnPoint(Centre + piece.Position, piece.Rotation, HedgerowField.BrickScale),
                                 Domains.Blue, PrismKind.Plain),
                    _fieldRoot, trail, FieldOwnerId, watchForReveal: true);
                if (!prism) continue;
                _unclaimed.Add(new HedgePrism { Prism = prism, Row = piece.Row });
                _rowUnclaimed[piece.Row]++;
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Hedgerow] intensity {Intensity}: {rung.RowCount} hedges, {_unclaimed.Count} prisms.");
        }

        /// <summary>Still an unclaimed hedge prism: alive, not re-issued by the pool, and still no team's.</summary>
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

        /// <summary>Counts each hedge's unclaimed prisms down (only the AI reads it). Swap-remove keeps it O(unclaimed).
        /// A stolen prism stays in the cell as a brick in its thief's wall; it simply leaves this list.</summary>
        void SettleField()
        {
            for (int i = _unclaimed.Count - 1; i >= 0; i--)
            {
                var entry = _unclaimed[i];
                if (Unclaimed(entry.Prism)) continue;
                if (entry.Row >= 0 && entry.Row < _rowUnclaimed.Length) _rowUnclaimed[entry.Row]--;
                int last = _unclaimed.Count - 1;
                _unclaimed[i] = _unclaimed[last];
                _unclaimed.RemoveAt(last);
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded();
            ArmWardens();

            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

        void Update()
        {
            SettleField();
            if (IsServer && !_finalResultsSent) _wardens.Tick();
            if (_hintPosted || _finalResultsSent || gameData == null || !gameData.IsTurnRunning) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.HedgerowHint, local.Domain);
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

        // ── AI (server): wall a hedge while ahead, raid while behind ─────────

        /// <summary>
        /// Arms every AI Serpent as a <see cref="HedgerowWarden"/>: it runs in across the nearest hedge with prisms left,
        /// stops dead short of it and holds while the wall eats along it - or, while its domain trails (or late in the
        /// round), stops beside the densest rival mass (<see cref="Cell.GetExplosionTarget"/>) so its wall eats the
        /// rival's trail. Difficulty is the seat's AI level, never the intensity. Cleared on the whistle, despawn and replay.
        /// </summary>
        void ArmWardens()
        {
            if (settings == null)
            {
                CSDebug.LogWarning("[HedgerowController] no settings asset wired; AI wardens not armed.");
                return;
            }
            _wardens.Arm(gameData, settings, this, ShouldRaid,
                d => arenaCell ? arenaCell.GetExplosionTarget(d) : (Vector3?)null);
        }

        bool ShouldRaid(Domains own, int level)
        {
            if (rule == null) return false;

            var monitor = TurnMonitorRemaining();
            float late = HedgerowSettingsSO.Rung(settings.aiLateRaidSeconds, level);
            if (late > 0f && monitor >= 0f && monitor <= late) return true;

            float deficit = HedgerowSettingsSO.Rung(settings.aiRaidDeficitFraction, level);
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
            _wardens.Disarm();
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
                    CSDebug.LogError($"[Hedgerow] Client could not match RoundStats for '{sName}'. " +
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
            _wardens.Disarm();

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
