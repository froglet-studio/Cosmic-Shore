using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fake Artist (GameModes.FakeArtist = 39) - the free-for-all social-deduction
    /// painting party game built on the Connect-the-Dots painting toy (3-12 players,
    /// each on their own team, distinguished by 12 brush identities: 6 colors x
    /// normal/shielded trails - see <see cref="FakeArtistBrushes"/>).
    ///
    /// Each round (one turn of the base flow): the server generates a parametric
    /// variation of a preset artwork (never the same twice), deals every player their
    /// strokes via TARGETED ClientRpcs (per-client secrets - the only sanctioned
    /// pattern), and picks the fake artist(s) - one, or two in large groups - who alone
    /// learn the SUBJECT but receive only each stroke's start+end dots (no ring guides).
    /// Player count scales the strokes-per-player (so small groups still draw a
    /// recognizable picture), the number of fake artists, and the fake-artist reward
    /// (see <see cref="FakeArtistConfigSO"/>). Everyone draws simultaneously; then everyone
    /// but the fake artists answers two questions (what is it? who faked it?) while a
    /// gallery camera frames the shared creation; the server tallies
    /// (<see cref="FakeArtistScorer"/>, values on FakeArtistConfig.asset), reveals, and the
    /// next round begins on a fresh canvas near the players - finished artworks accumulate
    /// as a conserved-mass gallery (no culling; scene reload on replay is the only sink).
    /// First player to the win target (default 8, Tools &gt; Cosmic Shore &gt; End Game
    /// Conditions) takes the gallery.
    ///
    /// Server-authoritative throughout: role/deal/vote/tally state lives in plain
    /// server fields; clients only ever see their own secrets. Points ride
    /// RoundStats.GoalsScored (server-write NetworkVariable) so the shared HUD shows
    /// live totals; Score lands at game end via the HexRace SyncFinalScores pattern.
    /// </summary>
    public class FakeArtistController : MultiplayerDomainGamesController
    {
        [Header("Fake Artist")]
        [Tooltip("Drag FakeArtistScoringRule.asset - per-player rule (win check, results).")]
        [SerializeField] ScoringRuleSO rule;
        [Tooltip("Drag FakeArtistConfig.asset - all round timing, point values, and AI tuning.")]
        [SerializeField] FakeArtistConfigSO config;

        const float RoleCardSeconds = 7f;

        // The mode's design floor (SO_ArcadeGame.MinPlayersAllowed) - the baseline the
        // player-count reward scaling measures "extra players" from.
        const int ModeMinPlayers = 3;

        enum RoundPhase { Idle, Drawing, Voting, Revealing, Resolved }

        // ── Server round state (never replicated wholesale - secrets stay here) ──
        RoundPhase _phase = RoundPhase.Idle;
        bool _finalResultsSent;
        System.Random _serverRng;
        readonly List<string> _roster = new();
        readonly Dictionary<string, int> _points = new();
        readonly Dictionary<string, int> _imposterCounts = new();
        readonly Dictionary<string, int> _strokesDone = new();
        readonly Dictionary<string, int> _strokesDealt = new(); // actual strokes each player got this round
        readonly Dictionary<string, FakeArtistScorer.Answers> _votes = new();
        readonly HashSet<string> _imposterNames = new(); // 1-2 fake artists this round
        int _imposterReward;                              // scaled by player count this round
        PaintingPreset _preset;
        int _seed;
        int _strokeCount;
        int _correctSubjectChoice;
        List<PaintingPreset> _subjectChoices = new();
        Vector3 _artOrigin;
        Quaternion _artRotation = Quaternion.identity;
        Vector3 _artCenter;    // world center of the round's canvas (for the gallery camera)
        float _artRadius;
        float _drawDeadline;
        float _voteDeadline;
        float _revealEndTime;
        Vector3[][][] _dealtDots; // [rosterIndex][strokeIndex][dot]
        readonly List<AIPilot> _armedPilots = new();

        // ── Brush table (server assigns; every peer mirrors + applies visuals) ──
        readonly Dictionary<string, int> _brushSlots = new();
        readonly Dictionary<VesselPrismController, Action<Prism>> _spawnHooks = new();

        // ── Local (per-peer) presentation state ──
        FakeArtistVotePanel _panel;
        FakeArtistStrokeGuide _guide;
        FakeArtistRevealGhost _ghost;
        FakeArtistGalleryCam _galleryCam;
        bool _localIsImposter;
        int _localRosterIndex = -1;
        ToyContext _toyContext;

        /// <summary>Read by FakeArtistTurnMonitor: the round is fully resolved (votes tallied, reveal shown).</summary>
        public bool IsRoundResolved => _phase == RoundPhase.Resolved;

        /// <summary>Server-side phase text the turn monitor mirrors to every HUD.</summary>
        public string PhaseDisplayText => _phase switch
        {
            RoundPhase.Drawing => Mathf.Max(0, Mathf.CeilToInt(_drawDeadline - Time.time)).ToString(),
            RoundPhase.Voting => "VOTE",
            RoundPhase.Revealing => "REVEAL",
            _ => string.Empty,
        };

        protected override bool UseGolfRules => false;

        // Painted prisms are conserved mass; the gallery only leaves with the scene.
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom → SyncFinalScores_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            // numberOfRounds stays int.MaxValue: rounds repeat until a player reaches
            // the point target (checked in OnTurnEndedCustom).
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _phase = RoundPhase.Idle;

            // Mint + register the fire/lime synthetic brush sets on THIS peer before
            // any NetDomain replicates a synthetic value.
            FakeArtistBrushes.EnsureSyntheticSetsRegistered(gameData.ThemeManagerData);
            _toyContext = new ToyContext { GameData = gameData, IsFreestyleActive = null };

            _panel = FakeArtistVotePanel.Create();
            _panel.VoteSubmitted += HandleLocalVoteSubmitted;

            gameData.OnPlayerPairInitialized.OnRaised += HandlePlayerPairInitialized;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStartedAllPeers;

            if (IsServer)
            {
                _serverRng = new System.Random(unchecked((Environment.TickCount * 397) ^ GetInstanceID()));
                gameData.OnPlayerAdded += HandlePlayerAdded;
            }
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnPlayerPairInitialized.OnRaised -= HandlePlayerPairInitialized;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStartedAllPeers;
            if (IsServer)
                gameData.OnPlayerAdded -= HandlePlayerAdded;

            if (_panel != null)
                _panel.VoteSubmitted -= HandleLocalVoteSubmitted;

            StopGalleryCam();
            UnhookAllSpawners();
            ClearAIProviders();
            base.OnNetworkDespawn();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Brush identities
        // ══════════════════════════════════════════════════════════════════

        void HandlePlayerAdded(string playerName, Domains domain)
        {
            if (!IsServer || string.IsNullOrEmpty(playerName)) return;
            AssignBrush(playerName);
        }

        void AssignBrush(string playerName)
        {
            if (!_brushSlots.ContainsKey(playerName))
            {
                var used = new HashSet<int>(_brushSlots.Values);
                int slot = 0;
                while (used.Contains(slot) && slot < FakeArtistBrushes.MaxBrushes - 1) slot++;
                _brushSlots[playerName] = slot;
            }

            ApplyBrushDomainOnServer(playerName);
            BroadcastBrushTable();
        }

        /// <summary>Server-authoritative paint-domain write (bypasses the client RPC's active-domain validation by design).</summary>
        void ApplyBrushDomainOnServer(string playerName)
        {
            if (!_brushSlots.TryGetValue(playerName, out int slot)) return;
            var brush = FakeArtistBrushes.ForSlot(slot);

            var player = gameData.Players.OfType<Player>()
                .FirstOrDefault(p => p != null && p.Name == playerName);
            if (player != null && player.IsSpawned && player.NetDomain.Value != brush.PaintDomain)
                player.NetDomain.Value = brush.PaintDomain;
        }

        void BroadcastBrushTable()
        {
            int count = _brushSlots.Count;
            var names = new FixedString64Bytes[count];
            var slots = new int[count];
            int i = 0;
            foreach (var kvp in _brushSlots)
            {
                names[i] = new FixedString64Bytes(kvp.Key);
                slots[i] = kvp.Value;
                i++;
            }
            SyncBrushTable_ClientRpc(names, slots);
        }

        [ClientRpc]
        void SyncBrushTable_ClientRpc(FixedString64Bytes[] names, int[] slots)
        {
            for (int i = 0; i < names.Length; i++)
                _brushSlots[names[i].ToString()] = slots[i];
            ApplyBrushVisualsLocally();
        }

        void HandlePlayerPairInitialized(ulong playerNetObjId) => ApplyBrushVisualsLocally();

        /// <summary>
        /// Applies each rostered player's brush on THIS peer: shielded-at-birth trail
        /// flag + per-spawn integrity hook (pool-flag repair) on the local simulation of
        /// their vessel, and manual ribbon colors for the synthetic paint colors the
        /// shared theme lookup can't resolve. Idempotent - safe to re-run on every
        /// table sync / pair init.
        /// </summary>
        void ApplyBrushVisualsLocally()
        {
            foreach (var player in gameData.Players)
            {
                if (player == null || string.IsNullOrEmpty(player.Name)) continue;
                if (!_brushSlots.TryGetValue(player.Name, out int slot)) continue;

                var brush = FakeArtistBrushes.ForSlot(slot);
                var vessel = player.Vessel;
                var status = vessel?.VesselStatus;
                if (status == null || (status is UnityEngine.Object o && !o)) continue;

                var spawner = status.VesselPrismController;
                if (spawner == null) continue;

                spawner.SetTrailShielded(brush.Shielded);

                if (!_spawnHooks.ContainsKey(spawner))
                {
                    bool shielded = brush.Shielded;
                    Action<Prism> hook = prism => FakeArtistBrushes.EnforceBrushOnSpawnedPrism(prism, shielded);
                    spawner.OnBlockSpawned += hook;
                    _spawnHooks[spawner] = hook;
                }

                if ((brush.PaintDomain == FakeArtistBrushes.FireDomain || brush.PaintDomain == FakeArtistBrushes.LimeDomain)
                    && FakeArtistBrushes.TryGetTrailColors(gameData.ThemeManagerData, brush.PaintDomain, out var hi, out var core))
                {
                    vessel.SetTrailColors(hi, core);
                }
            }
        }

        void UnhookAllSpawners()
        {
            foreach (var kvp in _spawnHooks)
            {
                if (kvp.Key != null)
                    kvp.Key.OnBlockSpawned -= kvp.Value;
            }
            _spawnHooks.Clear();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Round setup (server, at countdown end)
        // ══════════════════════════════════════════════════════════════════

        protected override void OnCountdownTimerEnded()
        {
            // Deal BEFORE the base start RPC: RPCs on one NetworkObject are ordered, so
            // every client holds its strokes/role when the turn-start RPC lands.
            if (IsServer)
                SetupRoundAndDeal();

            base.OnCountdownTimerEnded();

            if (IsServer)
                ArmAIPainters();
        }

        void SetupRoundAndDeal()
        {
            // Stable round roster (server order is authoritative; all index-addressed
            // payloads carry the name array, so clients never rely on local order).
            _roster.Clear();
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null || string.IsNullOrEmpty(stats.Name)) continue;
                if (_roster.Contains(stats.Name)) continue;
                _roster.Add(stats.Name);
                if (!_points.ContainsKey(stats.Name)) _points[stats.Name] = stats.GoalsScored;
                if (!_imposterCounts.ContainsKey(stats.Name)) _imposterCounts[stats.Name] = 0;
            }
            if (_roster.Count == 0)
            {
                CSDebug.LogError("[FakeArtist] SetupRoundAndDeal with empty roster - aborting round setup.");
                return;
            }

            // Brushes: make sure every rostered player has one and wears it.
            foreach (var name in _roster)
                if (!_brushSlots.ContainsKey(name))
                    AssignBrush(name);
            foreach (var name in _roster)
                ApplyBrushDomainOnServer(name);

            // Artwork: random preset + fresh seed = a parametric variation nobody has
            // painted before. Strokes-per-player scales UP in small groups so the total
            // stays above config.MinTotalStrokes - enough to still tell what it is.
            int strokesPerPlayer = config.StrokesPerPlayerFor(_roster.Count);
            _preset = FakeArtistArtworkBuilder.SubjectCatalog[_serverRng.Next(FakeArtistArtworkBuilder.SubjectCatalog.Length)];
            _seed = _serverRng.Next();
            _strokeCount = _roster.Count * strokesPerPlayer;
            var strokes = FakeArtistArtworkBuilder.BuildStrokes(_preset, config.ArtworkSize, _seed, _strokeCount);
            var bundles = FakeArtistArtworkBuilder.Deal(strokes, _roster.Count, strokesPerPlayer);
            FakeArtistArtworkBuilder.AnchorForRound(gameData.RoundsPlayed, config.ArtworkSize, out _artOrigin, out _artRotation);

            // World center + radius of this canvas, for the vote-time gallery camera.
            var localBounds = PaintingPresetLibrary.ComputeBounds(strokes);
            _artCenter = _artOrigin + _artRotation * localBounds.center;
            _artRadius = Mathf.Max(60f, localBounds.extents.magnitude);

            // Fake artists: 1, or 2 in large groups (config.SecondImposterAtPlayers). Pick
            // the least-often-imposter each time (fair rotation), ties broken by server RNG.
            int imposterCount = Mathf.Min(config.ImposterCountFor(_roster.Count), _roster.Count - 1);
            _imposterReward = config.ImposterRewardFor(_roster.Count, ModeMinPlayers);
            _imposterNames.Clear();
            for (int k = 0; k < imposterCount; k++)
            {
                var available = _roster.Where(n => !_imposterNames.Contains(n)).ToList();
                if (available.Count == 0) break;
                int minCount = available.Min(n => _imposterCounts[n]);
                var candidates = available.Where(n => _imposterCounts[n] == minCount).ToList();
                var pick = candidates[_serverRng.Next(candidates.Count)];
                _imposterNames.Add(pick);
                _imposterCounts[pick]++;
            }

            // Subject options (correct + decoys, shuffled deterministically).
            _subjectChoices = FakeArtistArtworkBuilder.BuildSubjectChoices(_preset, _seed, config.SubjectChoiceCount);
            _correctSubjectChoice = _subjectChoices.IndexOf(_preset);

            // Per-player world-space dots; a fake artist's strokes degrade to start+end.
            _dealtDots = new Vector3[_roster.Count][][];
            _strokesDone.Clear();
            _strokesDealt.Clear();
            _votes.Clear();
            for (int p = 0; p < _roster.Count; p++)
            {
                bool isImposter = _imposterNames.Contains(_roster[p]);
                var bundle = bundles[p];
                _dealtDots[p] = new Vector3[bundle.Count][];
                for (int s = 0; s < bundle.Count; s++)
                {
                    var localDots = FakeArtistArtworkBuilder.BuildDots(bundle[s].points, config.DotReach);
                    var world = new List<Vector3>(localDots.Count);
                    foreach (var d in localDots)
                        world.Add(_artOrigin + _artRotation * d);
                    if (isImposter && world.Count > 2)
                        world = new List<Vector3> { world[0], world[^1] };
                    _dealtDots[p][s] = world.ToArray();
                }
                _strokesDone[_roster[p]] = 0;
                // Actual strokes dealt - usually StrokesPerPlayer, but a preset with too
                // little total path can under-deliver; AllPaintersFinished keys off this
                // so a short-changed player doesn't stall the phase until the timer.
                _strokesDealt[_roster[p]] = bundle.Count;
            }

            _phase = RoundPhase.Drawing;
            _drawDeadline = Time.time + config.DrawSeconds;

            DealToHumans();
            AnnounceRound_ClientRpc(gameData.RoundsPlayed + 1);
        }

        void DealToHumans()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            foreach (var client in nm.ConnectedClientsList)
            {
                var player = FindHumanPlayerByClientId(client.ClientId);
                if (player == null) continue;

                int rosterIndex = _roster.IndexOf(player.Name);
                if (rosterIndex < 0) continue;

                bool isImposter = _imposterNames.Contains(player.Name);
                FlattenDots(_dealtDots[rosterIndex], out var dots, out var counts);

                var brushName = _brushSlots.TryGetValue(player.Name, out int slot)
                    ? FakeArtistBrushes.DisplayName(FakeArtistBrushes.ForSlot(slot))
                    : "?";

                var target = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                };
                DealRound_ClientRpc(
                    rosterIndex, dots, counts, config.DotReach, isImposter, _imposterNames.Count,
                    new FixedString64Bytes(isImposter ? FakeArtistArtworkBuilder.SubjectName(_preset) : string.Empty),
                    new FixedString64Bytes(brushName),
                    target);
            }
        }

        static void FlattenDots(Vector3[][] strokeDots, out Vector3[] dots, out int[] counts)
        {
            counts = new int[strokeDots.Length];
            int total = 0;
            for (int s = 0; s < strokeDots.Length; s++)
            {
                counts[s] = strokeDots[s].Length;
                total += strokeDots[s].Length;
            }
            dots = new Vector3[total];
            int i = 0;
            for (int s = 0; s < strokeDots.Length; s++)
                for (int d = 0; d < strokeDots[s].Length; d++)
                    dots[i++] = strokeDots[s][d];
        }

        Player FindHumanPlayerByClientId(ulong clientId)
        {
            // AI shares the host's OwnerClientId - never resolve a sender/target to an AI.
            return gameData.Players.OfType<Player>()
                .FirstOrDefault(p => p != null && p.IsSpawned
                                     && p.OwnerClientNetId == clientId
                                     && !p.NetIsAI.Value);
        }

        [ClientRpc]
        void AnnounceRound_ClientRpc(int roundNumber)
        {
            GameFeedAPI.Post($"Round {roundNumber} - brushes up!", Domains.Blue, GameFeedType.Generic);
        }

        [ClientRpc]
        void DealRound_ClientRpc(int rosterIndex, Vector3[] dots, int[] dotCounts, float reach,
            bool isImposter, int imposterCount, FixedString64Bytes subject, FixedString64Bytes brushName,
            ClientRpcParams rpcParams = default)
        {
            // Fresh round presentation: the old guide dies here (releasing the pen), the
            // new one takes it over in the same frame; the vote-time gallery camera (if any)
            // hands back to normal gameplay flight.
            StopGalleryCam();
            if (_guide != null)
                Destroy(_guide.gameObject);
            _ghost?.FadeOutAndDestroy();
            _ghost = null;

            _localIsImposter = isImposter;
            _localRosterIndex = rosterIndex;

            var strokeDots = new List<Vector3[]>(dotCounts.Length);
            int cursor = 0;
            for (int s = 0; s < dotCounts.Length; s++)
            {
                var arr = new Vector3[dotCounts[s]];
                Array.Copy(dots, cursor, arr, 0, dotCounts[s]);
                cursor += dotCounts[s];
                strokeDots.Add(arr);
            }

            var paintDomain = gameData.LocalPlayer?.Domain ?? Domains.Blue;
            var accent = FakeArtistBrushes.UIColor(gameData.ThemeManagerData, paintDomain);
            var prismMat = ToyFactory.DomainPrismMaterial(_toyContext, paintDomain);

            var guideGO = new GameObject("FakeArtistGuide");
            _guide = guideGO.AddComponent<FakeArtistStrokeGuide>();
            _guide.Configure(gameData, strokeDots, reach, isImposter, accent, prismMat);
            _guide.StrokeCompleted += HandleLocalStrokeCompleted;
            _guide.Begin();

            _panel.ShowRoleCard(isImposter, imposterCount, subject.ToString(), brushName.ToString(), RoleCardSeconds);
        }

        void HandleLocalStrokeCompleted(int strokeIndex) => ReportStrokeComplete_ServerRpc();

        [ServerRpc(RequireOwnership = false)]
        void ReportStrokeComplete_ServerRpc(ServerRpcParams rpcParams = default)
        {
            if (_phase != RoundPhase.Drawing) return;
            var player = FindHumanPlayerByClientId(rpcParams.Receive.SenderClientId);
            if (player == null || !_strokesDone.ContainsKey(player.Name)) return;
            int dealt = _strokesDealt.TryGetValue(player.Name, out var d) ? d : config.StrokesPerPlayer;
            _strokesDone[player.Name] = Mathf.Min(_strokesDone[player.Name] + 1, dealt);
        }

        void HandleTurnStartedAllPeers()
        {
            // New round is live on this peer - fold away the previous reveal ghost.
            _ghost?.FadeOutAndDestroy();
            _ghost = null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Server phase machine
        // ══════════════════════════════════════════════════════════════════

        void Update()
        {
            if (!IsServer || _finalResultsSent) return;

            switch (_phase)
            {
                case RoundPhase.Drawing:
                    if (AllPaintersFinished() || Time.time >= _drawDeadline)
                        BeginVoting();
                    break;
                case RoundPhase.Voting:
                    if (AllVotesIn() || Time.time >= _voteDeadline)
                        ResolveRound();
                    break;
                case RoundPhase.Revealing:
                    if (Time.time >= _revealEndTime)
                        _phase = RoundPhase.Resolved; // FakeArtistTurnMonitor ends the turn
                    break;
            }
        }

        bool AllPaintersFinished()
        {
            foreach (var name in _roster)
            {
                int dealt = _strokesDealt.TryGetValue(name, out var d) ? d : config.StrokesPerPlayer;
                if (!_strokesDone.TryGetValue(name, out int done) || done < dealt)
                    return false;
            }
            return true;
        }

        bool AllVotesIn() => _votes.Count >= _roster.Count - _imposterNames.Count; // everyone but the fake artists

        void BeginVoting()
        {
            _phase = RoundPhase.Voting;
            _voteDeadline = Time.time + config.VoteSeconds;

            // AI: stop the painters first (an armed provider would pen-down again at its
            // next dot latch), then pens up (replicates via n_TrailPenUp) and ballots
            // cast silently - revealed only at the tally.
            ClearAIProviders();
            HoldAIPens();
            InjectAIVotes();

            var subjects = new FixedString64Bytes[_subjectChoices.Count];
            for (int i = 0; i < _subjectChoices.Count; i++)
                subjects[i] = new FixedString64Bytes(FakeArtistArtworkBuilder.SubjectName(_subjectChoices[i]));

            var names = new FixedString64Bytes[_roster.Count];
            for (int i = 0; i < _roster.Count; i++)
                names[i] = new FixedString64Bytes(_roster[i]);

            BeginVote_ClientRpc(subjects, names, config.VoteSeconds, _artCenter, _artRadius);
        }

        [ClientRpc]
        void BeginVote_ClientRpc(FixedString64Bytes[] subjectOptions, FixedString64Bytes[] rosterNames,
            float voteSeconds, Vector3 artCenter, float artRadius)
        {
            _guide?.EndDrawing();
            gameData.LocalPlayer?.InputController?.SetPause(true);

            // Frame the shared creation so everyone studies it while answering, instead of
            // staring at wherever their own vessel stopped.
            if (config.GalleryCameraDuringVote)
            {
                StopGalleryCam();
                _galleryCam = FakeArtistGalleryCam.Begin(artCenter, artRadius);
            }

            if (_localIsImposter)
            {
                _panel.ShowWaiting(voteSeconds);
                return;
            }

            var subjects = new List<string>(subjectOptions.Length);
            foreach (var s in subjectOptions) subjects.Add(s.ToString());
            var names = new List<string>(rosterNames.Length);
            foreach (var n in rosterNames) names.Add(n.ToString());

            int selfIndex = _localRosterIndex >= 0
                ? _localRosterIndex
                : names.IndexOf(gameData.LocalPlayer?.Name ?? string.Empty);

            _panel.ShowVote(subjects, names, selfIndex, voteSeconds);
        }

        void HandleLocalVoteSubmitted(int subjectChoice, int accusedIndex) =>
            SubmitVote_ServerRpc(subjectChoice, accusedIndex);

        [ServerRpc(RequireOwnership = false)]
        void SubmitVote_ServerRpc(int subjectChoice, int accusedIndex, ServerRpcParams rpcParams = default)
        {
            if (_phase != RoundPhase.Voting) return;

            var player = FindHumanPlayerByClientId(rpcParams.Receive.SenderClientId);
            if (player == null) return;
            string name = player.Name;
            if (_imposterNames.Contains(name) || !_roster.Contains(name) || _votes.ContainsKey(name)) return;

            if (accusedIndex < 0 || accusedIndex >= _roster.Count || _roster[accusedIndex] == name)
                accusedIndex = -1;
            if (subjectChoice < 0 || subjectChoice >= _subjectChoices.Count)
                subjectChoice = -1;

            _votes[name] = new FakeArtistScorer.Answers(subjectChoice, accusedIndex);
        }

        void InjectAIVotes()
        {
            var imposterIndices = _imposterNames.Select(n => _roster.IndexOf(n)).Where(idx => idx >= 0).ToList();

            for (int i = 0; i < _roster.Count; i++)
            {
                string name = _roster[i];
                if (_imposterNames.Contains(name)) continue;

                var player = gameData.Players.FirstOrDefault(p => p != null && p.Name == name);
                if (player == null || !player.IsInitializedAsAI) continue;

                int subject;
                if (_serverRng.NextDouble() < config.AICorrectSubjectChance)
                {
                    subject = _correctSubjectChoice;
                }
                else
                {
                    subject = _serverRng.Next(_subjectChoices.Count - 1);
                    if (subject >= _correctSubjectChoice) subject++;
                }

                int accused;
                if (imposterIndices.Count > 0 && _serverRng.NextDouble() < config.AICorrectImposterChance)
                {
                    // Accuse one of the actual fake artists.
                    accused = imposterIndices[_serverRng.Next(imposterIndices.Count)];
                }
                else
                {
                    // A "wrong" accusation targets anyone but self and the fake artists. In a
                    // 3-player round no such target exists - the accusation lands on a fake
                    // artist (the only other player).
                    var wrongTargets = new List<int>();
                    for (int c = 0; c < _roster.Count; c++)
                    {
                        if (c != i && !_imposterNames.Contains(_roster[c]))
                            wrongTargets.Add(c);
                    }
                    accused = wrongTargets.Count > 0
                        ? wrongTargets[_serverRng.Next(wrongTargets.Count)]
                        : (imposterIndices.Count > 0 ? imposterIndices[_serverRng.Next(imposterIndices.Count)] : -1);
                }

                _votes[name] = new FakeArtistScorer.Answers(subject, accused);
            }
        }

        void ResolveRound()
        {
            _phase = RoundPhase.Revealing;
            _revealEndTime = Time.time + config.RevealSeconds;

            var imposterIndices = _imposterNames.Select(n => _roster.IndexOf(n)).Where(idx => idx >= 0).ToList();
            var answersByIndex = new Dictionary<int, FakeArtistScorer.Answers>();
            foreach (var kvp in _votes)
            {
                int idx = _roster.IndexOf(kvp.Key);
                if (idx >= 0) answersByIndex[idx] = kvp.Value;
            }

            var deltas = FakeArtistScorer.ScoreRound(
                _roster.Count, imposterIndices, _correctSubjectChoice, answersByIndex,
                new FakeArtistScorer.Config(
                    config.CorrectSubjectPoints, config.CorrectImposterPoints,
                    config.GuessedPenalty, _imposterReward));

            var names = new FixedString64Bytes[_roster.Count];
            var deltaArr = new int[_roster.Count];
            var totals = new int[_roster.Count];
            for (int i = 0; i < _roster.Count; i++)
            {
                string name = _roster[i];
                _points[name] = (_points.TryGetValue(name, out int cur) ? cur : 0) + deltas[i];

                var stats = gameData.RoundStatsList.FirstOrDefault(s => s != null && s.Name == name);
                if (stats != null)
                    stats.GoalsScored = _points[name]; // server-write NV → live HUD on every peer

                names[i] = new FixedString64Bytes(name);
                deltaArr[i] = deltas[i];
                totals[i] = _points[name];
            }

            var imposterList = string.Join(" & ", _imposterNames);
            RevealRound_ClientRpc(
                new FixedString64Bytes(FakeArtistArtworkBuilder.SubjectName(_preset)),
                new FixedString64Bytes(imposterList),
                (int)_preset, config.ArtworkSize, _seed, _strokeCount,
                _artOrigin, _artRotation,
                names, deltaArr, totals);
        }

        [ClientRpc]
        void RevealRound_ClientRpc(FixedString64Bytes subject, FixedString64Bytes imposterNames,
            int preset, float artworkSize, int seed, int strokeCount,
            Vector3 origin, Quaternion rotation,
            FixedString64Bytes[] names, int[] deltas, int[] totals)
        {
            _guide?.EndDrawing();

            var lines = new List<string>(names.Length);
            for (int i = 0; i < names.Length; i++)
                lines.Add($"{names[i]}  {(deltas[i] >= 0 ? "+" : "")}{deltas[i]}   ({totals[i]})");

            string imposters = imposterNames.ToString();
            _panel.ShowReveal(subject.ToString(), imposters, lines, config.RevealSeconds);

            // The full blueprint blooms over the painted prisms - regenerated locally
            // from (preset, size, seed); no geometry crossed the wire before the vote.
            // The gallery camera stays up so players watch the answer overlay the creation.
            _ghost?.FadeOutAndDestroy();
            _ghost = FakeArtistRevealGhost.Spawn(
                (PaintingPreset)preset, artworkSize, seed, strokeCount, origin, rotation, _toyContext);

            GameFeedAPI.Post(
                $"The fake artist{(imposters.Contains("&") ? "s were" : " was")} <b>{imposters}</b>! The subject: <b>{subject}</b>",
                Domains.Blue, GameFeedType.Generic);
        }

        // ══════════════════════════════════════════════════════════════════
        //  AI painters
        // ══════════════════════════════════════════════════════════════════

        void ArmAIPainters()
        {
            ClearAIProviders();

            for (int rosterIndex = 0; rosterIndex < _roster.Count; rosterIndex++)
            {
                string name = _roster[rosterIndex];
                var player = gameData.Players.FirstOrDefault(p => p != null && p.Name == name);
                if (player == null || !player.IsInitializedAsAI) continue;

                var status = player.Vessel?.VesselStatus;
                var pilot = status?.AIPilot;
                var pen = status?.VesselPrismController;
                var vesselTransform = player.Vessel?.Transform;
                if (pilot == null || pen == null || vesselTransform == null) continue;

                var strokes = _dealtDots[rosterIndex];
                float latch = Mathf.Max(18f, config.DotReach * 1.8f);
                float latchSq = latch * latch;
                int stroke = 0, dot = 0;
                bool done = false;

                pen.SetSpawnerPaused(true); // pen-up until the first stroke's start dot

                pilot.SetExternalTargetProvider(() =>
                {
                    if (done || strokes.Length == 0)
                        return strokes.Length > 0 ? strokes[^1][^1] : vesselTransform.position;

                    var target = strokes[stroke][dot];
                    if ((vesselTransform.position - target).sqrMagnitude <= latchSq)
                    {
                        if (dot == 0)
                            pen.SetSpawnerPaused(false); // pen-down at the stroke start

                        dot++;
                        if (dot >= strokes[stroke].Length)
                        {
                            pen.SetSpawnerPaused(true);  // pen-up between strokes
                            int aiDealt = _strokesDealt.TryGetValue(name, out var dealtCount)
                                ? dealtCount : config.StrokesPerPlayer;
                            _strokesDone[name] = Mathf.Min(
                                (_strokesDone.TryGetValue(name, out int v) ? v : 0) + 1, aiDealt);
                            stroke++;
                            dot = 0;
                            if (stroke >= strokes.Length)
                                done = true;
                        }
                        if (!done)
                            target = strokes[Mathf.Min(stroke, strokes.Length - 1)][dot];
                    }
                    return target;
                });

                _armedPilots.Add(pilot);
            }
        }

        void HoldAIPens()
        {
            foreach (var player in gameData.Players)
            {
                if (player == null || !player.IsInitializedAsAI) continue;
                player.Vessel?.VesselStatus?.VesselPrismController?.SetSpawnerPaused(true);
            }
        }

        void ClearAIProviders()
        {
            foreach (var pilot in _armedPilots)
            {
                if (pilot != null)
                    pilot.ClearExternalTargetProvider();
            }
            _armedPilots.Clear();
        }

        /// <summary>Restore the normal gameplay camera if the gallery camera was up (idempotent).</summary>
        void StopGalleryCam()
        {
            if (_galleryCam != null)
            {
                _galleryCam.Stop();
                _galleryCam = null;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Game end (HexRace/NucleusRush SyncFinalScores pattern)
        // ══════════════════════════════════════════════════════════════════

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (!rule.IsObjectiveReached(gameData, out _)) return; // nobody at the target yet - next round

            var winner = FakeArtistScoringRuleSO.BestPlayer(gameData);
            if (winner == null) return;

            rule.AssignScores(gameData, winner.Domain, 0f);
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            ClearAIProviders();
            SyncFinalScoresSnapshot(winner.Name, winner.Domain);
        }

        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;

            if (IsServer)
            {
                _phase = RoundPhase.Idle;
                ClearAIProviders();
            }

            base.SetupNewRound();
        }

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var pointsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].GoalsScored;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[FakeArtist] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.GoalsScored = points[i];
            }

            _panel?.Hide();
            // Hand the camera back before EndGameSequencer runs its own end-game flourish.
            StopGalleryCam();

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay (scene reload path is primary; in-place kept for parity) ──

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _phase = RoundPhase.Idle;
            _points.Clear();
            _imposterCounts.Clear();
            _imposterNames.Clear();
            StopGalleryCam();

            foreach (var s in gameData.RoundStatsList)
            {
                s.GoalsScored = 0;
                s.Score = 0f;
            }
        }
    }
}
