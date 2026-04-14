using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using Obvious.Soap;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
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
        /// Number of AI players to backfill in multiplayer when not enough
        /// human players are present.
        /// A value of 0 means no AI backfill (all human or solo-mode AI logic applies).
        /// </summary>
        public int RequestedAIBackfillCount;

        /// <summary>
        /// Number of teams configured by the host (1-3).
        /// 1 = all players on same team, 2 = Jade + Ruby, 3 = Jade + Ruby + Gold.
        /// Used by BuildTeamCounts() to limit available teams and by AI spawning
        /// to assign AI to the correct teams.
        /// Default is 3 for backward compatibility.
        /// </summary>
        public int RequestedTeamCount = 3;

        /// <summary>
        /// Whether the current game uses golf-style scoring (lower = better).
        /// Set by <see cref="SortRoundStats"/> during end-game flow.
        /// </summary>
        public bool IsGolfRules { get; private set; }

        /// <summary>
        /// Server-authoritative winner name, written by game controllers in their
        /// SyncFinalScores_ClientRpc. Read by EndGameControllers after OnWinnerCalculated fires.
        /// Reset automatically in <see cref="ResetRuntimeData"/> and <see cref="ResetRuntimeDataForReplay"/>.
        /// </summary>
        [NonSerialized] public string WinnerName = "";

        /// <summary>
        /// The resolved crystal collection target for the current session.
        /// Written by <see cref="NetworkCrystalCollisionTurnMonitor"/> in StartMonitor (server),
        /// synced to clients via NetworkVariable.OnValueChanged.
        /// Read by game controllers for scoring calculations.
        /// Reset automatically in <see cref="ResetRuntimeData"/> and <see cref="ResetRuntimeDataForReplay"/>.
        /// </summary>
        [NonSerialized] public int CrystalTargetCount;

        /// <summary>
        /// Syncs essential game identity fields from an <see cref="SO_ArcadeGame"/> asset.
        /// Must be called before <see cref="InvokeGameLaunch"/> so that SceneLoader
        /// and ServerPlayerVesselInitializerWithAI see correct values.
        /// </summary>
        public void SyncFromArcadeGame(SO_ArcadeGame game)
        {
            if (game == null)
            {
                Debug.LogError("<color=#FF0000>[GameDataSO] SyncFromArcadeGame — game is NULL!</color>");
                return;
            }

            SceneName = game.SceneName;
            GameMode = game.Mode;
            IsMultiplayerMode = game.IsMultiplayer;
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

            Debug.Log($"<color=#FFD700>[GameDataSO] ConfigurePlayerCounts — total={totalDesired}, humans={humanCount}, AI={aiBackfill}</color>");
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

        /// <summary>
        /// Set by SceneLoader before loading Menu_Main from a game scene.
        /// Prevents the game scene's ServerPlayerVesselInitializer from calling
        /// NetworkManager.Shutdown() on despawn — the network must stay alive
        /// for Menu_Main's vessel spawning pipeline.
        /// Cleared by MainMenuController.Start().
        /// </summary>
        [NonSerialized] public bool IsReturnToMenuTransition;
        
        // -----------------------------------------------------------------------------------------
        // Initialization / Lifecycle

        
        public void InitializeGame()
        {
            InvokeInitializeGame();
        }

        public void DestroyPlayerAndVessel()
        {
            // Ensure the domain pool is fresh for the new session so every
            // player gets a unique domain.  Without this, leftover state from
            // a previous session could cause duplicate or swapped domains.
            DomainAssigner.Initialize();

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
            OnMiniGameTurnEnd?.Raise();
        }
        
        public void InvokeMiniGameRoundEnd() => OnMiniGameRoundEnd?.Raise();
        public void InvokeMiniGameEnd() => OnMiniGameEnd?.Raise();
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
            CrystalTargetCount = 0;
            // Note: RequestedAIBackfillCount and RequestedTeamCount are intentionally
            // NOT reset here. They are pre-launch config values set by
            // ArcadeGameConfigureModal and must survive the ResetRuntimeData() call
            // in SceneLoader.LoadSceneAsync() so the game scene can read them.
            // They are reset in ResetAllData() instead.
        }

        void ResetRuntimeDataForReplay()
        {
            IsTurnRunning = false;
            TurnStartTime = 0f;
            RoundsPlayed = 0;
            TurnsTakenThisRound = 0;
            _playerSpawnPoseList.Clear();
            WinnerName = "";
            CrystalTargetCount = 0;
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
            ActiveSession = null;
            selectedVesselClass.Value = VesselClassType.Manta;
            VesselClassSelectedIndex.Value = 1;
            SelectedPlayerCount.Value = 1;
            SelectedIntensity.Value = 1;
            RequestedAIBackfillCount = 0;

            IsReplayReload = false;
            // Note: IsReturnToMenuTransition is NOT cleared here because ResetAllData()
            // may run before the game scene's OnNetworkDespawn fires. The flag is cleared
            // by MainMenuController.Start() after the menu scene finishes loading.

            ResetRuntimeData();
            DestroyPlayerAndVessel();
            DomainAssigner.Initialize();
        }

        public void AddPlayer(IPlayer p)
        {
            if (p == null)
                return;

            // Avoid duplicates by Name
            if (Players.All(player => player.Name != p.Name))
                Players.Add(p);

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

            OnPlayerAdded?.Invoke(p.Name, p.RoundStats?.Domain ?? Domains.Unassigned);
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
                    CSDebug.LogError("[GameDataSO] SpawnPoses is null or empty — returning default pose at origin.");
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
        // Team Balancing

        /// <summary>
        /// All available team domains in order. Index 0 = 1st team, etc.
        /// </summary>
        public static readonly Domains[] TeamDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        /// <summary>
        /// Counts how many players are on each team, limited to RequestedTeamCount.
        /// Used by AI spawning to assign AI to the team with the fewest players.
        /// Players on domains outside the active team set are counted on the first team.
        /// </summary>
        public Dictionary<Domains, int> BuildTeamCounts()
        {
            int teamCount = Mathf.Clamp(RequestedTeamCount, 1, TeamDomains.Length);
            var counts = new Dictionary<Domains, int>();

            for (int i = 0; i < teamCount; i++)
                counts[TeamDomains[i]] = 0;

            foreach (var p in Players)
            {
                if (p is not Player player) continue;

                var domain = player.NetDomain.Value;
                if (counts.ContainsKey(domain))
                {
                    counts[domain]++;
                }
                else
                {
                    // Player has a domain outside the active set — count on first team
                    counts[TeamDomains[0]]++;
                }
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