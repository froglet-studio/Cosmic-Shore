using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using Obvious.Soap;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Core;
using IPlayer = CosmicShore.Gameplay.IPlayer;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Every MiniGame in the project should use the same asset of this SO.
    /// It connects MiniGameBase with SceneLoader, StatsManager, TurnMonitor, Arcade, MultiplayerSetup and others.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DataContainer_" + nameof(GameDataSO),
        menuName = "ScriptableObjects/Data Containers/" + nameof(GameDataSO))]
    public class GameDataSO : ScriptableObject
    {
        // Events - Maybe later it will be better to change all Actions to ScriptableEvent of SOAP
        public ScriptableEventNoParam OnLaunchGame;
        public ScriptableEventBool OnSceneTransition;
        public ScriptableEventNoParam OnSessionStarted;
        public ScriptableEventNoParam OnInitializeGame;
        public ScriptableEventNoParam OnMiniGameRoundStarted;
        public ScriptableEventNoParam OnClientReady;
        public ScriptableEventNoParam OnMiniGameTurnStarted;
        public ScriptableEventNoParam OnMiniGameTurnEnd;
        public ScriptableEventNoParam OnMiniGameRoundEnd;
        public ScriptableEventNoParam OnMiniGameEnd;
        public ScriptableEventNoParam OnWinnerCalculated;
        public ScriptableEventNoParam OnResetForReplay;
        public ScriptableEventNoParam OnSessionEnded;
        public ScriptableEventUlong OnPlayerNetworkSpawnedUlong;
        public ScriptableEventNoParam OnVesselNetworkSpawned;
        public ScriptableEventUlong OnPlayerPairInitialized;
        public event Action<string, Domains> OnPlayerAdded;

        /// <summary>
        /// Raised on every peer when the server-synced per-domain metric sums change (see
        /// <see cref="SetDomainMetricSum"/>). The in-game multiplayer HUD listens so its domain boxes
        /// display the SERVER's authoritative sums verbatim instead of each client re-summing
        /// per-player stats (which can freeze for a client's own player when its own RoundStats
        /// replication lags).
        /// </summary>
        public event Action OnDomainMetricSumsChanged;

        [Header("UI Flow")]
        public ScriptableEventNoParam OnShowGameEndScreen;
        
        // Local player config / state
        public VesselClassTypeVariable selectedVesselClass;
        public IntVariable VesselClassSelectedIndex;
        public IntVariable SelectedPlayerCount;
        public IntVariable SelectedIntensity;
        public ResourceCollection ResourceCollection;
        public ThemeManagerDataContainerSO ThemeManagerData;
        
        
        // Game Config / State
        public string SceneName;
        public GameModes GameMode;
        public string LocalPlayerDisplayName;
        public int LocalPlayerAvatarId;
        public bool IsDailyChallenge;
        public bool IsTraining;
        public bool IsMission;
        public bool IsMultiplayerMode;

        /// <summary>
        /// True while a Tournament session is in progress (set by
        /// <see cref="CosmicShore.Gameplay.TournamentController"/> at tournament start,
        /// cleared on tournament end / exit). A peer of the other game-context flags
        /// above. Read by the <see cref="CosmicShore.UI.Scoreboard"/> to swap the
        /// per-game lobby buttons for the tournament Continue flow, and synced to
        /// clients via <see cref="CosmicShore.Gameplay.MultiplayerMiniGameControllerBase"/>'s
        /// config RPC so every peer in a minigame scene knows it is inside a tournament.
        /// Like the other context flags it is NOT reset by <see cref="ResetRuntimeData"/>
        /// (it must survive the per-game scene loads); it is cleared in
        /// <see cref="ResetAllData"/> as a quit/session-end safety net.
        /// </summary>
        public bool IsTournamentMode;

        /// <summary>
        /// Number of AI players to backfill in multiplayer when not enough
        /// human players are present.
        /// A value of 0 means no AI backfill (all human or solo-mode AI logic applies).
        /// </summary>
        public int RequestedAIBackfillCount;

        /// <summary>
        /// Levels of ALL FOUR elements a trailing player/team gains per unit of score deficit
        /// behind first place - this game's comeback strength, authored on SO_ArcadeGame and
        /// synced from the launch pipeline (host) / config RPC (clients). Read every tick by
        /// the required ElementalComebackSystem; the comeback layer never lifts an element
        /// above level 10. 0 = comeback disabled for this game.
        /// </summary>
        public float ComebackRatePerScoreDeficit = 1f;

        /// <summary>
        /// Number of domains configured by the host (1-3).
        /// 1 = Jade only, 2 = Jade + Ruby, 3 = Jade + Ruby + Gold.
        /// Used by AI spawning to assign AI to the correct domains and by
        /// <see cref="IsActiveDomain"/> to validate domain picks server-side.
        /// </summary>
        [FormerlySerializedAs("RequestedTeamCount")]
        public int RequestedDomainCount = 3;

        /// <summary>
        /// Whether the current game uses golf-style scoring (lower = better).
        /// Set by <see cref="SortRoundStats"/> during end-game flow.
        /// </summary>
        public bool IsGolfRules { get; private set; }

        // ── Server-synced per-domain metric sums (in-game HUD) ───────────────────
        // MultiplayerDomainGamesController computes ScoringMetrics.SumByDomain for each active domain
        // on the server and replicates it; SetDomainMetricSum runs on every peer from the
        // NetworkVariable callback. Indexed by ActiveDomains order (Jade, Ruby, Gold).
        readonly int[] _domainMetricSums = new int[3];

        /// <summary>Server-synced summed metric (crystals / jousts) for an active domain; 0 if not active.</summary>
        public int GetDomainMetricSum(Domains domain)
        {
            int i = System.Array.IndexOf(ActiveDomains, domain);
            return (i >= 0 && i < _domainMetricSums.Length) ? _domainMetricSums[i] : 0;
        }

        /// <summary>
        /// Writes one synced domain sum (called on every peer from the controller's NetworkVariable
        /// callback) and raises <see cref="OnDomainMetricSumsChanged"/> when it actually changes.
        /// </summary>
        public void SetDomainMetricSum(Domains domain, int value)
        {
            int i = System.Array.IndexOf(ActiveDomains, domain);
            if (i < 0 || i >= _domainMetricSums.Length) return;
            if (_domainMetricSums[i] == value) return;
            _domainMetricSums[i] = value;
            OnDomainMetricSumsChanged?.Invoke();
        }

        /// <summary>
        /// Server-authoritative winner name, written by game controllers in their
        /// SyncFinalScores_ClientRpc. Read by EndGameControllers after OnWinnerCalculated fires.
        /// Reset automatically in <see cref="ResetRuntimeData"/> and <see cref="ResetRuntimeDataForReplay"/>.
        /// </summary>
        [NonSerialized] public string WinnerName = "";

        /// <summary>
        /// Server-authoritative winning team. Set alongside <see cref="WinnerName"/> by
        /// game controllers in their final-score ClientRpc. In team/co-op modes, any
        /// player whose Domain equals this value is on the winning team.
        /// Reset automatically in <see cref="ResetRuntimeData"/> and <see cref="ResetRuntimeDataForReplay"/>.
        /// </summary>
        [NonSerialized] public Domains WinnerDomain = Domains.Blue;

        /// <summary>
        /// Single source of truth for the final ranked results (per-player, sorted, with
        /// 1-based <see cref="ScoreResult.Rank"/>). Produced once per game end by the mode:
        /// server-side in networked modes (and assembled identically on each client from the
        /// already-synced score arrays), or locally in single-player. Every end-game surface
        /// - scoreboard banner + cards, end-game cinematic, crystal reward - reads this.
        /// <see cref="WinnerName"/>/<see cref="WinnerDomain"/> are a thin derived view over
        /// <c>Results[0]</c> (see <see cref="SetResults"/>). Empty until the mode computes
        /// results. Reset in <see cref="ResetRuntimeData"/> and <see cref="ResetRuntimeDataForReplay"/>.
        /// See Docs/ScoringSystem/REFACTOR.md R10.
        /// </summary>
        [NonSerialized] public List<ScoreResult> Results = new();

        /// <summary>
        /// Sets <see cref="Results"/> (reusing the same list instance so existing references
        /// stay valid) and derives <see cref="WinnerName"/>/<see cref="WinnerDomain"/> from
        /// the top row ONLY when the mode has not already written them. The domain modes set
        /// the authoritative winner (highest DOMAIN metric sum, via their final-score ClientRpc)
        /// BEFORE calling this - deriving over it from <c>Results[0]</c> (the best INDIVIDUAL)
        /// mis-credited team games whenever the top individual sat on a losing domain (e.g. a
        /// 2v2 Crystal Capture where a losing-team player tied the top score - the losing team
        /// saw VICTORY). Call once per game end on the server and on each client.
        /// </summary>
        public void SetResults(IEnumerable<ScoreResult> results)
        {
            Results.Clear();
            if (results != null)
                Results.AddRange(results);

            if (Results.Count > 0)
            {
                if (string.IsNullOrEmpty(WinnerName))
                    WinnerName = Results[0].Name;
                if (WinnerDomain == Domains.Blue)
                    WinnerDomain = Results[0].Domain;
            }
        }

        /// <summary>
        /// The resolved crystal collection target for the current session.
        /// Written by <see cref="NetworkCrystalCollisionTurnMonitor"/> in StartMonitor (server),
        /// synced to clients via NetworkVariable.OnValueChanged.
        /// Read by game controllers for scoring calculations.
        /// Reset automatically in <see cref="ResetRuntimeData"/> and <see cref="ResetRuntimeDataForReplay"/>.
        /// </summary>
        [NonSerialized] public int CrystalTargetCount;

        /// <summary>
        /// The resolved joust-collision target for the current session - the per-domain
        /// joust sum that ends a Joust turn. Published by <see cref="JoustCollisionTurnMonitor"/>
        /// in StartMonitor on every peer (a scene constant, identical across clients). Read by
        /// the Joust controller to format the "N Jousts Left" loser line into
        /// <see cref="ScoreResult.ScoreText"/>. Reset in <see cref="ResetRuntimeData"/> and
        /// <see cref="ResetRuntimeDataForReplay"/>.
        /// </summary>
        [NonSerialized] public int JoustTargetCount;

        /// <summary>
        /// The resolved goal target for the current Astro League session - the per-domain
        /// goal sum that triggers the mercy-rule finish. Published by
        /// <see cref="AstroLeagueController"/> in OnNetworkSpawn on the server and synced to
        /// clients via its match-config ClientRpc (a settings constant, identical across
        /// clients). Read by <see cref="AstroLeagueScoringRuleSO"/> for the end condition and
        /// the "remaining" readout. Reset in <see cref="ResetRuntimeData"/> and
        /// <see cref="ResetRuntimeDataForReplay"/>.
        /// </summary>
        [NonSerialized] public int GoalTargetCount;

        /// <summary>
        /// The resolved hostile-prism destruction target for the current Rampage session -
        /// the per-domain <see cref="IRoundStats.HostilePrismsDestroyed"/> sum that ends the
        /// turn. Published by <see cref="RampagePrismTurnMonitor"/> in StartMonitor (server),
        /// synced to clients via NetworkVariable.OnValueChanged. Read by
        /// <see cref="CosmicShore.Gameplay.RampageScoringRuleSO"/> for the end condition and
        /// the "remaining" readout. Reset in <see cref="ResetRuntimeData"/> and
        /// <see cref="ResetRuntimeDataForReplay"/>.
        /// </summary>
        [NonSerialized] public int PrismTargetCount;

        /// <summary>
        /// The active scoring strategy for the current mode, published by the mode's controller
        /// in OnNetworkSpawn (drag the matching <see cref="CosmicShore.Gameplay.ScoringRuleSO"/>
        /// asset onto the controller). Read by the network turn monitors for the end condition
        /// and the "remaining" readout (and, in later commits, the scoreboard + end-game
        /// cinematic). Transient - re-published on every (re)spawn, so it is intentionally NOT
        /// cleared by the reset methods.
        /// </summary>
        [NonSerialized] public ScoringRuleSO ScoringRule;

        /// <summary>
        /// Syncs essential game identity fields from an <see cref="SO_ArcadeGame"/> asset.
        /// Must be called before <see cref="InvokeGameLaunch"/> so that SceneLoader
        /// and ServerPlayerVesselInitializerWithAI see correct values.
        /// </summary>
        public void SyncFromArcadeGame(SO_ArcadeGame game)
        {
            if (game == null)
            {
                Debug.LogError("<color=#FF0000>[GameDataSO] SyncFromArcadeGame - game is NULL!</color>");
                return;
            }

            SceneName = game.SceneName;
            GameMode = game.Mode;
            IsMultiplayerMode = game.IsMultiplayer;
            ComebackRatePerScoreDeficit = game.ComebackRatePerScoreDeficit;
        }

        /// <summary>
        /// Single source of truth for player count configuration at game launch.
        /// Computes and stores both SelectedPlayerCount and RequestedAIBackfillCount atomically.
        /// Minimum player counts are enforced upstream by SO_ArcadeGame.MinPlayersAllowed via the UI.
        /// </summary>
        /// <param name="totalDesired">Total players the user selected (human + AI)</param>
        /// <param name="humanCount">Number of human players in the party</param>
        public void ConfigurePlayerCounts(int totalDesired, int humanCount)
        {
            int aiBackfill = Mathf.Max(0, totalDesired - humanCount);

            SelectedPlayerCount.Value = totalDesired;
            RequestedAIBackfillCount = aiBackfill;

            Debug.Log($"<color=#FFD700>[GameDataSO] ConfigurePlayerCounts - total={totalDesired}, humans={humanCount}, AI={aiBackfill}</color>");
        }


        public List<IPlayer> Players = new();
        public List<IVessel> Vessels = new();
        public List<IRoundStats> RoundStatsList = new();
        public List<DomainStats> DomainStatsList = new();
        public HashSet<Transform> SlowedShipTransforms = new();
        public float TurnStartTime;
        public bool IsTurnRunning { get; private set; }
        public Pose[] SpawnPoses { get; private set; }
        List<Pose> _playerSpawnPoseList = new ();
        public IPlayer LocalPlayer { get; private set; }
        public IRoundStats LocalRoundStats { get; private set; }
        public ISession ActiveSession { get; set; }
        public int TurnsTakenThisRound { get; set; }
        public int RoundsPlayed { get; set; }

        /// <summary>
        /// Set by MultiplayerMiniGameControllerBase before a scene-reload replay.
        /// Used to control fade-in timing after the reload completes.
        /// </summary>
        [NonSerialized] public bool IsReplayReload;
        
        // -----------------------------------------------------------------------------------------
        // Initialization / Lifecycle

        
        public void InitializeGame()
        {
            InvokeInitializeGame();
        }

        public void DestroyPlayerAndVessel()
        {
            if (Players == null || Players.Count == 0)
                return;

            for (int i = Players.Count - 1; i >= 0; i--)
            {
                Players[i].Vessel?.DestroyVessel();
                Players[i].DestroyPlayer();
            }

            Players.Clear();
        }

        public void StartTurn()
        {
            IsTurnRunning = true;
            TurnStartTime = Time.time;

            // Control has just been handed to the player (countdown ended, players activated).
            // This is the start point for flight_time_seconds.
            FlightClock.OnTurnStarted();

            InvokeTurnStarted();
        }

        public void InvokeGameLaunch() => OnLaunchGame?.Raise();
        public void InvokeSceneTransition(bool param) => OnSceneTransition?.Raise(param);
        public void InvokeSessionStarted() => OnSessionStarted?.Raise();
        public void InvokeInitializeGame() => OnInitializeGame?.Raise();
        public void InvokeClientReady() => OnClientReady?.Raise();
        public void InvokeMiniGameRoundStarted() => OnMiniGameRoundStarted?.Raise();
        public void InvokeTurnStarted() => OnMiniGameTurnStarted?.Raise();

        public void InvokeGameTurnConditionsMet()
        {
            IsTurnRunning = false;
            FlightClock.OnTurnEnded();
            OnMiniGameTurnEnd?.Raise();
        }
        
        public void InvokeMiniGameRoundEnd() => OnMiniGameRoundEnd?.Raise();
        /// <summary>
        /// Ends the game. The flight clock is settled BEFORE the SOAP raise so every
        /// subscriber - analytics, stats reporters, the profile - reads the same final
        /// FlightClock.LastGameSeconds regardless of subscription order.
        /// </summary>
        public void InvokeMiniGameEnd()
        {
            FlightClock.EndGame();
            OnMiniGameEnd?.Raise();
        }
        public void InvokeWinnerCalculated() => OnWinnerCalculated?.Raise();
        public void InvokeOnSessionEnded() => OnSessionEnded?.Raise();
        public void InvokeShowGameEndScreen() => OnShowGameEndScreen?.Raise();

        public void InvokePlayerNetworkSpawned(ulong ownerClientId) => OnPlayerNetworkSpawnedUlong.Raise(ownerClientId);
        public void InvokeVesselNetworkSpawned() => OnVesselNetworkSpawned.Raise();
        public void InvokePlayerPairInitialized(ulong playerNetObjId) => OnPlayerPairInitialized?.Raise(playerNetObjId);

        public void ResetForReplay()
        {
            ResetStatsDataForReplay();
            ResetPlayers();
            ResetRuntimeDataForReplay();
            OnResetForReplay?.Raise();
        }

        public void ResetRuntimeData()
        {
            IsTurnRunning = false;

            // Scene teardown / mid-game exit: abandon the flight clock without publishing a
            // time, so an abandoned game cannot leak its seconds into the next one.
            FlightClock.AbortGame();
            Players.Clear();
            Vessels.Clear();
            SlowedShipTransforms.Clear();
            RoundStatsList.Clear();
            DomainStatsList.Clear();
            TurnStartTime = 0f;
            RoundsPlayed = 0;
            TurnsTakenThisRound = 0;
            _playerSpawnPoseList.Clear();
            LocalPlayer = null;
            LocalRoundStats = null;
            WinnerName = "";
            WinnerDomain = Domains.Blue;
            Results.Clear();
            CrystalTargetCount = 0;
            JoustTargetCount = 0;
            GoalTargetCount = 0;
            PrismTargetCount = 0;
            System.Array.Clear(_domainMetricSums, 0, _domainMetricSums.Length);
            // Note: RequestedAIBackfillCount and RequestedDomainCount are intentionally
            // NOT reset here. They are pre-launch config values set by
            // ArcadeGameConfigureModal and must survive the ResetRuntimeData() call
            // in SceneLoader.LoadSceneAsync() so the game scene can read them.
            // They are reset in ResetAllData() instead.
        }

        /// <summary>
        /// Narrower reset used by the party-join transition (NetworkTransitionService.
        /// ClearStaleReferences) after the client's local NetworkManager shuts down.
        /// The shutdown despawned the client's own Player/Vessel NetworkObjects, but
        /// the lists keep managed references to the destroyed components - including
        /// the menu session's RoundStats entries (the menu populates RoundStatsList
        /// via AddPlayer exactly like a game does). A destroyed RoundStats keeps its
        /// managed Name, so it wins AddPlayer's name-keyed dedup and SHADOWS the live
        /// component for every name-keyed consumer on this client (ready-feed color,
        /// final-score sync, per-domain sums) - frozen at the pre-party state (Jade).
        /// Clear the roster lists; the party session's pair-init re-adds live entries.
        /// Round/turn counters are NOT touched (no game round was entered).
        /// </summary>
        public void ResetRuntimeDataForPartyJoin()
        {
            Players.Clear();
            Vessels.Clear();
            RoundStatsList.Clear();
            DomainStatsList.Clear();
            LocalPlayer = null;
            LocalRoundStats = null;
        }

        void ResetRuntimeDataForReplay()
        {
            IsTurnRunning = false;
            FlightClock.AbortGame();
            TurnStartTime = 0f;
            RoundsPlayed = 0;
            TurnsTakenThisRound = 0;
            _playerSpawnPoseList.Clear();
            WinnerName = "";
            WinnerDomain = Domains.Blue;
            Results.Clear();
            CrystalTargetCount = 0;
            JoustTargetCount = 0;
            GoalTargetCount = 0;
            PrismTargetCount = 0;
            System.Array.Clear(_domainMetricSums, 0, _domainMetricSums.Length);
        }

        public void ResetStatsDataForReplay()
        {
            if (RoundStatsList == null || RoundStatsList.Count == 0)
            {
                CSDebug.LogError("Cannot Replay game mode, no round stats data found!");
                return;
            }
            
            for (int i = 0, count = RoundStatsList.Count; i < count ; i++)
            {
                RoundStatsList[i].Cleanup();
            }
        }
        
        public void ResetAllData()
        {
            GameMode = GameModes.Random;
            IsMultiplayerMode = false;
            // ActiveSession is intentionally NOT reset here. Under the "Always
            // InParty" model the field is the single source of truth for the
            // live UGS Relay session reference (shared with HCS via
            // PartySessionService); nulling it would orphan the live Relay.
            // The locked invariant: ActiveSession is only nulled by intentional
            // leave paths (PartySessionService.LeaveAsync / OnTransportFailure
            // after Delete/Leave). See Docs/PartySystem/ARCHITECTURE.md (Locked design).
            selectedVesselClass.Value = VesselClassType.Manta;
            VesselClassSelectedIndex.Value = 1;
            SelectedPlayerCount.Value = 1;
            SelectedIntensity.Value = 1;
            RequestedAIBackfillCount = 0;
            RequestedDomainCount = 3;
            IsTournamentMode = false;

            IsReplayReload = false;

            ResetRuntimeData();
            DestroyPlayerAndVessel();
        }

        public void AddPlayer(IPlayer p)
        {
            if (p == null)
                return;

            PruneDestroyedRosterEntries();

            // Avoid duplicates by Name
            if (Players.All(player => player.Name != p.Name))
                Players.Add(p);

            // A same-name RoundStats that is NOT this player's live component is a
            // stale shadow from a previous session (e.g. the pre-party solo Player,
            // despawned by the invite-accept NetworkManager shutdown). Name-keyed
            // consumers - ready-feed color, final-score sync, per-domain sums - must
            // resolve the LIVE component, so the shadow is replaced. Unity defers
            // Destroy to end of frame, so this also covers shadows the destroyed-
            // object prune above cannot see yet.
            for (int i = RoundStatsList.Count - 1; i >= 0; i--)
            {
                var rs = RoundStatsList[i];
                if (rs != null && rs.Name == p.Name && !ReferenceEquals(rs, p.RoundStats))
                {
                    CSDebug.LogWarning($"[GameDataSO] Replacing stale RoundStats entry for '{p.Name}' with the live component.");
                    RoundStatsList.RemoveAt(i);
                }
            }

            if (RoundStatsList.All(rs => rs.Name != p.Name))
                RoundStatsList.Add(p.RoundStats);

            if (p.IsLocalUser)
            {
                LocalPlayer = p;
                LocalRoundStats = p.RoundStats;
            }

            p.ResetForPlay();

            if (!NetworkManager.Singleton || NetworkManager.Singleton.IsServer)
                p.SetPoseOfVessel(GetRandomSpawnPose());

            OnPlayerAdded?.Invoke(p.Name, p.RoundStats?.Domain ?? Domains.Blue);
        }
        
        public void SortRoundStats(bool golfRules)
        {
            IsGolfRules = golfRules;
            if (golfRules)
                RoundStatsList.Sort((score1, score2) => score1.Score.CompareTo(score2.Score));
            else
                RoundStatsList.Sort((score1, score2) => score2.Score.CompareTo(score1.Score));
        }

        public void SortDomainStats(bool golfRules)
        {
            if (golfRules)
                DomainStatsList.Sort((score1, score2) => score1.Score.CompareTo(score2.Score));
            else
                DomainStatsList.Sort((score1, score2) => score2.Score.CompareTo(score1.Score));
        }

        public void CalculateDomainStats(bool golfRules)
        {
            if (DomainStatsList == null)
                DomainStatsList = new List<DomainStats>();
            else
                DomainStatsList.Clear();

            if (RoundStatsList == null || RoundStatsList.Count == 0)
                return;

            // Sum per-domain
            var totals = new Dictionary<Domains, float>();

            foreach (var roundStat in RoundStatsList)
            {
                if (totals.TryGetValue(roundStat.Domain, out var current))
                    totals[roundStat.Domain] = current + roundStat.Score;
                else
                    totals.Add(roundStat.Domain, roundStat.Score);
            }

            // Convert to DomainStatsList
            foreach (var kvp in totals)
            {
                var score = kvp.Value;
                
                DomainStatsList.Add(new DomainStats
                {
                    Domain = kvp.Key,
                    Score = score,
                });
            }

            SortDomainStats(golfRules);
        }

        public bool IsLocalDomain(Domains domain) =>
            LocalPlayer != null && domain == LocalPlayer.Domain;

        public bool IsLocalDomainWinner(out DomainStats stats)
        {
            stats = default;
            foreach (var stat in DomainStatsList.Where(stat => stat.Domain == LocalPlayer.Domain))
            {
                stats = stat;
            }
            return stats.Domain == LocalPlayer.Domain;
        }

        // ----- Domain aggregation helpers (per-domain sums for team-based scoring) -----

        public int SumCrystalsCollectedByDomain(Domains domain)
        {
            int sum = 0;
            for (int i = 0, count = RoundStatsList.Count; i < count; i++)
            {
                var s = RoundStatsList[i];
                if (s != null && s.Domain == domain) sum += s.CrystalsCollected;
            }
            return sum;
        }
        
        public void SetPlayersActive()
        {
            foreach (var player in Players)
                player.StartPlayer();
        }

        public void SetNonOwnerPlayersActiveInNewClient()
        {
            foreach (var player in Players.Where(player => !player.IsMultiplayerOwner))
                player.StartPlayer();
        }

        public void SetNewPlayerActive(string playerName)
        {
            foreach (var player in Players.Where(player => player.Name.Equals(playerName)))
                player.StartPlayer();
        }

        public void ResetPlayers()
        {
            foreach (var player in Players)
            {
                player.ResetForPlay();
                
                if (!NetworkManager.Singleton || NetworkManager.Singleton.IsServer)
                    player.SetPoseOfVessel(GetRandomSpawnPose());
            }
        }
        
        /// <summary>
        /// Drops roster entries whose underlying UnityEngine.Object has been destroyed
        /// (e.g. the client's pre-party Player after the invite-accept NetworkManager
        /// shutdown). Destroyed components keep their managed state - Name still
        /// matches - so without pruning they shadow live entries in the name-keyed
        /// dedup and lookups. Plain C# stats (edit-mode test fakes) are not
        /// UnityEngine.Objects and pass through untouched.
        /// </summary>
        void PruneDestroyedRosterEntries()
        {
            for (int i = Players.Count - 1; i >= 0; i--)
                if (Players[i] is UnityEngine.Object obj && !obj)
                    Players.RemoveAt(i);

            for (int i = RoundStatsList.Count - 1; i >= 0; i--)
                if (RoundStatsList[i] is UnityEngine.Object obj && !obj)
                    RoundStatsList.RemoveAt(i);
        }

        /// <summary>
        /// Remove a player (by display name) from Players & RoundStatsList and fix LocalPlayer if needed.
        /// </summary>
        public bool RemovePlayerData(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return false;

            // Remove from Players
            int removedPlayers = Players.RemoveAll(p => p != null && p.Name == playerName);

            // Remove from RoundStats
            int removedStats = RoundStatsList.RemoveAll(rs => rs != null && rs.Name == playerName);

            // Fix LocalPlayer if it was the removed one
            if (LocalPlayer != null && LocalPlayer.Name == playerName)
                LocalPlayer = Players.Count > 0 ? Players[0] : null;

            // Optional: also stop their vessel spawning if any dangling reference exists (defensive)
            // No-op here because Players list holds the references.

            return (removedPlayers + removedStats) > 0;
        }
        
        public void SwapVessels()
        {
            var player0 = Players[0];
            var player1 = Players[1];
            
            var vessel0 = player0.Vessel;
            var vessel1 = player1.Vessel;
            
            player0.ChangeVessel(vessel1);
            player1.ChangeVessel(vessel0);
            
            vessel0.ChangePlayer(player1);
            vessel1.ChangePlayer(player0);
        }
        
        // -----------------------------------------------------------------------------------------
        // Queries / Scores

        public (Domains Team, float Volume) GetControllingTeamStatsBasedOnVolumeRemaining()
        {
            var top = RoundStatsList
                .OrderByDescending(rs => rs.VolumeRemaining)
                .FirstOrDefault();

            return top is null ? (Domains.Jade, 0f) : (top.Domain, top.VolumeRemaining);
        }
        
        public List<IRoundStats> GetSortedListInDecendingOrderBasedOnVolumeRemaining() =>
            RoundStatsList.OrderByDescending(r => r.VolumeRemaining).ToList();

        public bool TryGetLocalPlayerStats(out IPlayer player, out IRoundStats roundStats)
        {
            player = LocalPlayer;
            roundStats = player != null ? FindByName(player.Name) : null;
            return player != null && roundStats != null;
        }

        /// <summary>
        /// Can be true or false
        /// </summary>
        /// <returns>
        /// true if roundstats found, false other wise
        /// </returns>
        public bool TryGetRoundStats(string playerName, out IRoundStats roundStats)
        {
            roundStats = FindByName(playerName);
            return roundStats != null;
        }

        public float GetTotalVolume() => RoundStatsList.Sum(stats => stats.VolumeRemaining);

        public Vector4 GetTeamVolumes()
        {
            float jade = VolumeOf(Domains.Jade);
            float ruby = VolumeOf(Domains.Ruby);
            float blue = VolumeOf(Domains.Blue);
            float gold = VolumeOf(Domains.Gold);
            return new Vector4(jade, ruby, blue, gold);
        }

        public bool TryGetWinner(out IRoundStats roundStats, out bool won)
        {
            roundStats = null;
            won = false;
            if (RoundStatsList is null || RoundStatsList.Count == 0)
            {
                CSDebug.LogError("No round stats found to calculate winner!");
                return false;
            }

            if (!TryGetLocalPlayerStats(out IPlayer _, out roundStats))
            {
                CSDebug.LogError("No round stats of active player found!");
                return false;   
            }
            
            if (roundStats.Name == RoundStatsList[0].Name)
                won = true;

            return true;
        }
        
        public void SetSpawnPositions(Transform[] spawnTransforms)
        {
            if (spawnTransforms == null)
            {
                CSDebug.LogError("[ServerPlayerVesselInitializer] PlayerSpawnPoints array not set or empty.");
                return;
            }
            
            SpawnPoses = new Pose[spawnTransforms.Length];
            for (int i = 0, count = spawnTransforms.Length; i < count; i++)
            {
                SpawnPoses[i] = new Pose
                {
                    position = spawnTransforms[i].position,
                    rotation = spawnTransforms[i].rotation
                };
            }
            
            if (SpawnPoses == null || SpawnPoses.Length == 0)
            {
                CSDebug.LogError("[ServerPlayerVesselInitializer] PlayerSpawnPoints array not set or empty.");
                return;
            }

            _playerSpawnPoseList?.Clear();
            _playerSpawnPoseList = new List<Pose>(SpawnPoses.ToList());
        }
        
        public bool TryGetPlayerByOwnerClientId(ulong clientId, out IPlayer player)
        {
            player = null;
            foreach (var p in Players)
            {
                if (p is UnityEngine.Object obj && !obj) continue;
                if (p.OwnerClientNetId == clientId)
                {
                    player = p;
                    return true;
                }
            }

            CSDebug.LogError($"No player found {clientId}");
            return false;
        }

        public bool TryGetPlayerByNetworkObjectId(ulong playerId, out IPlayer player)
        {
            player = null;
            foreach (var p in Players)
            {
                if (p is UnityEngine.Object obj && !obj) continue;
                if (p.PlayerNetId == playerId)
                {
                    player = p;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetVesselByNetworkObjectId(ulong netVesselId, out IVessel vessel)
        {
            vessel = null;
            foreach (var v in Vessels)
            {
                if (v is UnityEngine.Object obj && !obj) continue;
                if (v.VesselNetId == netVesselId)
                {
                    vessel = v;
                    return true;
                }
            }

            return false;
        }
        
        Pose GetRandomSpawnPose()
        {
            if (_playerSpawnPoseList == null || _playerSpawnPoseList.Count == 0)
            {
                if (SpawnPoses == null || SpawnPoses.Length == 0)
                {
                    CSDebug.LogError("[GameDataSO] SpawnPoses is null or empty - returning default pose at origin.");
                    return new Pose(Vector3.zero, Quaternion.identity);
                }
                _playerSpawnPoseList = new List<Pose>(SpawnPoses.Length);
                _playerSpawnPoseList = SpawnPoses.ToList();
            }

            int index = UnityEngine.Random.Range(0, _playerSpawnPoseList.Count);
            var spawnPoint = _playerSpawnPoseList[index];
            _playerSpawnPoseList.RemoveAt(index);
            return spawnPoint;
        }
        
        // -----------------------------------------------------------------------------------------
        // Domain Balancing

        /// <summary>
        /// All playable domains in priority order. The active set for a session
        /// is the contiguous slice <c>ActiveDomains[0..DC-1]</c> where DC is
        /// <see cref="RequestedDomainCount"/>: DC=1 → Jade only, DC=2 → Jade+Ruby,
        /// DC=3 → Jade+Ruby+Gold. Tie-break order for AI placement is the same.
        /// Blue is intentionally absent - it's the "no team / neutral" sentinel
        /// and never a playable assignment.
        /// </summary>
        public static readonly Domains[] ActiveDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        /// <summary>
        /// True iff <paramref name="d"/> is one of the first <paramref name="dc"/>
        /// entries of <see cref="ActiveDomains"/>. Single source of truth used by
        /// the modal (tile dimming), Player RPC (domain pick validation), and
        /// AI placement (active-set membership).
        /// </summary>
        public static bool IsActiveDomain(Domains d, int dc)
        {
            int clampedDc = Mathf.Clamp(dc, 1, ActiveDomains.Length);
            for (int i = 0; i < clampedDc; i++)
                if (ActiveDomains[i] == d) return true;
            return false;
        }

        /// <summary>
        /// Counts non-AI players per active domain. Returns a fresh dictionary
        /// keyed by every entry in <paramref name="activeDomains"/>, with zero
        /// entries for domains no human has picked. Humans whose
        /// <see cref="Player.NetDomain"/> is outside the active set are not
        /// counted (caller handles them via NormalizeUnassignedHumans).
        /// </summary>
        public static Dictionary<Domains, int> BuildHumanCounts(
            IEnumerable<Player> humans,
            IList<Domains> activeDomains)
        {
            var counts = new Dictionary<Domains, int>(activeDomains.Count);
            foreach (var d in activeDomains) counts[d] = 0;
            foreach (var h in humans)
            {
                if (h == null || h.NetIsAI.Value) continue;
                var d = h.NetDomain.Value;
                if (counts.ContainsKey(d)) counts[d]++;
            }
            return counts;
        }

        // -----------------------------------------------------------------------------------------
        // Helpers (private)

        IRoundStats FindByTeam(Domains domain) =>
            RoundStatsList.FirstOrDefault(rs => rs.Domain == domain);

        IRoundStats FindByName(string name) =>
            RoundStatsList.FirstOrDefault(rs => rs.Name == name);

        float VolumeOf(Domains domain) =>
            FindByTeam(domain)?.VolumeRemaining ?? 0f;
    }
}