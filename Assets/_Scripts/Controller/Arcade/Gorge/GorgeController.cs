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
    /// Gorge - the Jewel Wasp's SPACE game: a timed feast on Chum's reef. Shoals of neutral prisms hang in the cell (laid
    /// by this controller on every peer, <see cref="ChumReef"/>, the same reef Chum lays). Sting a creature, ride it to a
    /// shoal and hold RT: its suction pulls the nearest prisms into its mouth (Space widens the reach; at Space 5 it pulls
    /// from every side). Every shield is food to a possessed creature, so the crusted rungs' shells are eaten too. The
    /// score is the prisms you take off the board (<see cref="ScoringMetric.PrismsDestroyed"/>) when the clock runs out.
    /// See GORGE.md.
    ///
    /// <para><b>Nothing about the wasp changes</b> (the LOCKED vessel rule). Sting, ride, suction and the omni crystal's
    /// predator are the wasp's own (JEWEL_WASP.md). The mode's levers are the reef per intensity, the cells' herds (the
    /// creatures there are to ride, and they graze the reef too), the scene's crystals and the AI gluttons.</para>
    ///
    /// <para><b>Who is credited.</b> A possessed creature's suction eats as its rider (<c>Fauna.PossessedSuction</c>
    /// passes the wasp's pilot name), so <c>StatsManager</c> credits the bite on the machine that SIMULATES that pilot:
    /// the server for itself and its AIs. The creatures are simulated on the server, so a remote human's host feeds
    /// there, where that pilot is not simulated, and nobody would credit it. The server therefore credits a reef prism a
    /// creature took in a REMOTE pilot's name itself (<see cref="CreditBite"/>), and books every pilot's meals for the
    /// breakdown (<see cref="ChumReef.EatenBook"/>).</para>
    ///
    /// Structurally Chum's controller (timed, points mode, UseGolfRules = false, a <see cref="GorgeTimeTurnMonitor"/> as
    /// the scene's only monitor, a snapshot ClientRpc carrying the breakdown). JEWEL-WASP-ONLY is enforced by the arcade
    /// card's Vessels list.
    /// </summary>
    public class GorgeController : MultiplayerDomainGamesController, GorgeGlutton.IReefView
    {
        const string ReefOwnerId = "GorgeReef";

        [Header("Config")]
        [Tooltip("Drag GorgeSettings.asset - the lead feedback and the AI gluttons. The round length lives in " +
                 "EndConditionOverridesSO, resolved by GorgeTimeTurnMonitor.")]
        [SerializeField] GorgeSettingsSO settings;

        [Tooltip("Drag GorgeScoringRule.asset (metric = PrismsDestroyed, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The reef's cell: its centre places the shoals, and its live fauna are the creatures the AI chooses from.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("The environment prism every reef prism is laid from (SpawnablePrism.prefab).")]
        [SerializeField] Prism reefPrefab;

        [Tooltip("The nucleus radius (u) the shoals keep clear of. Authored to the cell's nucleus by the generator.")]
        [SerializeField, Min(0f)] float nucleusRadius = 392f;

        [Tooltip("The membrane radius (u) the shoals keep inside. Authored to the cell's membrane by the generator.")]
        [SerializeField, Min(1f)] float membraneRadius = 1200f;

        struct ReefPrism
        {
            public Prism Prism;
            public Vector3 Position;
            public int Shoal;
        }

        readonly GorgeGlutton _forager = new();
        readonly ChumReef.EatenBook _book = new();
        readonly List<ChumReef.Piece> _pieces = new();
        readonly List<ReefPrism> _standing = new();
        Vector3[] _shoalCentres = System.Array.Empty<Vector3>();
        int[] _shoalStanding = System.Array.Empty<int>();
        Transform _reefRoot;

        Coroutine _sampler;
        bool _finalResultsSent;
        bool _hintPosted;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;
        string[] _snapshotNames;
        int[] _snapshotEaten;

        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        Vector3 Centre => arenaCell ? arenaCell.transform.position : Vector3.zero;
        int Intensity => gameData != null && gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;

        // ── IReefView (the AI) ───────────────────────────────────────────────

        public int ShoalCount => _shoalCentres.Length;
        public Vector3 ShoalCentre(int shoal) => _shoalCentres[shoal];
        public int Standing(int shoal) => shoal >= 0 && shoal < _shoalStanding.Length ? _shoalStanding[shoal] : 0;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;
            _leaderDomain = Domains.Blue;

            LayReef();
            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopSampler();
            _forager.Disarm();
            ClearReef();
            base.OnNetworkDespawn();
        }

        // ── The reef (every peer) ────────────────────────────────────────────

        void LayReef()
        {
            ClearReef();
            if (!reefPrefab)
            {
                CSDebug.LogWarning("[Gorge] no reefPrefab on the controller - the reef is not laid. " +
                                   "Re-run Tools/Build/author_gorge_assets.py.");
                return;
            }

            var rung = ChumReef.ForIntensity(Intensity);
            ChumReef.Lay(rung, nucleusRadius, membraneRadius, _pieces);
            _reefRoot = new GameObject("GorgeReef").transform;
            _reefRoot.position = Centre;
            var trail = new Trail();
            var scale = Vector3.one * ChumReef.PrismSize;

            _shoalCentres = new Vector3[rung.ShoalCount];
            _shoalStanding = new int[rung.ShoalCount];
            for (int s = 0; s < rung.ShoalCount; s++)
                _shoalCentres[s] = Centre + ChumReef.ShoalCentre(rung, s, nucleusRadius, membraneRadius);

            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                var prism = PrismTrailBuilder.LayOne(reefPrefab,
                    new PrismLay(new SpawnPoint(piece.Position, Quaternion.identity, scale), Domains.Blue,
                                 piece.Shielded ? PrismKind.Shielded : PrismKind.Plain),
                    _reefRoot, trail, ReefOwnerId, watchForReveal: true);
                if (!prism) continue;
                _standing.Add(new ReefPrism { Prism = prism, Position = Centre + piece.Position, Shoal = piece.Shoal });
                _shoalStanding[piece.Shoal]++;
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Gorge] intensity {Intensity}: {rung.ShoalCount} shoals, {_standing.Count} reef prisms" +
                (rung.Crusted ? ", crusted" : string.Empty) + ".");
        }

        /// <summary>Still a prism of the reef: alive and not re-issued (a destroyed prism goes back to the pool and is
        /// handed to somebody else, with another owner).</summary>
        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == ReefOwnerId;

        void ClearReef()
        {
            for (int i = 0; i < _standing.Count; i++)
            {
                var prism = _standing[i].Prism;
                if (Holds(prism)) prism.Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
            }
            _standing.Clear();
            if (_reefRoot) Destroy(_reefRoot.gameObject);
            _reefRoot = null;
        }

        /// <summary>
        /// Every peer counts the shoals down (the AI and nothing else reads it); the SERVER also books and credits each
        /// reef prism a creature ate (<see cref="CreditBite"/>). Swap-remove keeps the pass O(standing).
        /// </summary>
        void SettleReef()
        {
            for (int i = _standing.Count - 1; i >= 0; i--)
            {
                var entry = _standing[i];
                if (Holds(entry.Prism)) continue;

                if (IsServer && gameData != null && gameData.IsTurnRunning && !_finalResultsSent
                    && entry.Prism && entry.Prism.DestroyedByCreature)
                    CreditBite(entry.Prism);

                if (entry.Shoal >= 0 && entry.Shoal < _shoalStanding.Length) _shoalStanding[entry.Shoal]--;
                int last = _standing.Count - 1;
                _standing[i] = _standing[last];
                _standing.RemoveAt(last);
            }
        }

        /// <summary>SERVER. A reef prism a creature took names its eater (<see cref="Prism.DestroyerName"/>): a possessed
        /// creature's rider, or a wild one's nobody. The meal is booked for the breakdown, and credited here only when
        /// the rider is a pilot the server does not simulate - StatsManager already credited every other one.</summary>
        void CreditBite(Prism prism)
        {
            string pilot = prism.DestroyerName;
            if (string.IsNullOrEmpty(pilot)) return;
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s != null && s.Name == pilot);
            if (stats == null) return;   // a wild creature's meal: nobody's
            _book.Credit(pilot);
            if (!SimulatedHere(pilot)) stats.HostilePrismsDestroyed += 1;
        }

        /// <summary>Does the server simulate the named pilot (its own player or an AI)? Those bites StatsManager credits.</summary>
        bool SimulatedHere(string pilot)
        {
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.Name != pilot) continue;
                if (p.IsInitializedAsAI) return true;
                var local = gameData.LocalPlayer;
                return local != null && local.Name == pilot;
            }
            return true;
        }

        /// <summary>Server-only. Every counter the metric and its breakdown read.</summary>
        void ZeroCounters()
        {
            _book.Clear();
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].HostilePrismsDestroyed = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[GorgeController] no settings asset wired; the AI gluttons are not armed and fly the " +
                                   "wasp's own steering.");
                return;
            }

            _forager.Arm(gameData, settings, this, arenaCell);

            StopSampler();
            _sampler = StartCoroutine(SampleRoutine());
        }

        void Update()
        {
            SettleReef();
            if (_finalResultsSent || gameData == null || !gameData.IsTurnRunning) return;

            if (!_hintPosted)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.GorgeHint, local.Domain);
            }

            if (IsServer && _forager.IsArmed) _forager.Tick();
        }

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

            int prisms = rule.DomainValue(gameData, leader);
            if (prisms <= 0) return;

            bool first = _leaderDomain == Domains.Blue;
            _leaderDomain = leader;
            if (first) return;   // the first leader after the opening is not a CHANGE
            if (Time.time - _lastLeadAnnounce < settings.leadAnnounceMinGapSeconds) return;

            _lastLeadAnnounce = Time.time;
            AnnounceLeadChanged_ClientRpc((int)leader, prisms);
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int prisms)
        {
            GameToastAPI.Post(GameToastSituation.DomainRaceLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), prisms.ToString());
        }

        // ── Server-authoritative game end at time-out ────────────────────────

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
            if (rule == null) return;

            // The whistle's last bites count.
            SettleReef();

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
            StopSampler();
            _forager.Disarm();
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
            var prismArray = new int[count];
            var eatenArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                prismArray[i] = statsList[i].HostilePrismsDestroyed;
                eatenArray[i] = _book.Eaten(statsList[i].Name);
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, prismArray, eatenArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prisms,
            int[] eaten,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            _snapshotNames = new string[names.Length];
            _snapshotEaten = eaten;
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                _snapshotNames[i] = sName;
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Gorge] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: a ridden creature can still land a bite in the frames
                // after the server decided, and the board must show what was decided.
                stat.HostilePrismsDestroyed = prisms[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule is GorgeScoringRuleSO gorge
                ? gorge.BuildResults(gameData, SnapshotEaten)
                : rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        int SnapshotEaten(string pilot)
        {
            if (_snapshotNames == null || _snapshotEaten == null) return 0;
            for (int i = 0; i < _snapshotNames.Length && i < _snapshotEaten.Length; i++)
                if (_snapshotNames[i] == pilot) return _snapshotEaten[i];
            return 0;
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
            _snapshotEaten = null;

            StopSampler();
            _forager.Disarm();
            _book.Clear();

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
