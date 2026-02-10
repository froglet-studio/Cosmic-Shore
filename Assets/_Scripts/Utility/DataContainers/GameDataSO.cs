using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Models.Enums;
using Obvious.Soap;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using IPlayer = CosmicShore.Game.IPlayer;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Every MiniGame in the project should use the same asset of this SO.
    /// It connects MiniGameBase with GameManager, StatsManager, TurnMonitor, Aracade, MultiplayerSetup and others.
    /// </summary>
    [CreateAssetMenu(
        fileName = "scriptable_variable_" + nameof(GameDataSO),
        menuName = "ScriptableObjects/DataContainers/" + nameof(GameDataSO))]
    public class GameDataSO : ScriptableObject
    {
        // Events - Maybe later it will be better to change all Actions to ScriptableEvent of SOAP 
        public event Action OnLaunchGameScene;
        public event Action OnSessionStarted;
        public event Action OnInitializeGame;
        public ScriptableEventNoParam OnMiniGameRoundStarted;
        public event Action OnClientReady;
        public ScriptableEventNoParam OnMiniGameTurnStarted;
        public ScriptableEventNoParam OnMiniGameTurnEnd;
        // DTFC
        public ScriptableEventNoParam OnMiniGameRoundEnd;
        public event Action OnMiniGameEnd;
        public event Action OnWinnerCalculated;
        
        public ScriptableEventNoParam OnResetForReplay;

        [Header("UI Flow")]
        public ScriptableEventNoParam OnShowGameEndScreen;

        public void InvokeShowGameEndScreen() => OnShowGameEndScreen?.Raise();
        // Local player config / state
        public VesselClassTypeVariable selectedVesselClass;
        public IntVariable VesselClassSelectedIndex;
        public IntVariable SelectedPlayerCount;
        public IntVariable SelectedIntensity;
        public SO_Captain PlayerCaptain;
        public ResourceCollection ResourceCollection;
        public ThemeManagerDataContainerSO ThemeManagerData;
        
        
        // Game Config / State
        public string SceneName;
        public GameModes GameMode;
        public string LocalPlayerDisplayName;
        public bool IsDailyChallenge;
        public bool IsTraining;
        public bool IsMission;
        public bool IsMultiplayerMode;
        public List<IPlayer> Players = new();
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
        
        // -----------------------------------------------------------------------------------------
        // Initialization / Lifecycle

        
        public void InitializeGame()
        {
            ResetRuntimeData();
            InvokeInitializeGame();
        }

        public void SetupForMultiplayer()
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

            InvokeTurnStarted();
        }
        
        public void InvokeGameLaunch() => OnLaunchGameScene?.Invoke();
        public void InvokeSessionStarted() => OnSessionStarted?.Invoke();
        public void InvokeInitializeGame() => OnInitializeGame?.Invoke();
        public void InvokeClientReady() => OnClientReady?.Invoke();
        public void InvokeMiniGameRoundStarted() => OnMiniGameRoundStarted?.Raise();
        public void InvokeTurnStarted() => OnMiniGameTurnStarted?.Raise();

        public void InvokeGameTurnConditionsMet()
        {
            IsTurnRunning = false;
            OnMiniGameTurnEnd?.Raise();   
        }
        public void InvokeMiniGameRoundEnd() => OnMiniGameRoundEnd?.Raise();
        public void InvokeMiniGameEnd() => OnMiniGameEnd?.Invoke();
        public void InvokeWinnerCalculated() => OnWinnerCalculated?.Invoke();

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
            RoundStatsList.Clear();
            DomainStatsList.Clear();
            TurnStartTime = 0f;
            RoundsPlayed = 0;
            TurnsTakenThisRound = 0;
            _playerSpawnPoseList.Clear();
        }

        void ResetRuntimeDataForReplay()
        {
            TurnStartTime = 0f;
            RoundsPlayed = 0;
            TurnsTakenThisRound = 0;
            _playerSpawnPoseList.Clear();
        }

        public void ResetStatsDataForReplay()
        {
            if (RoundStatsList == null || RoundStatsList.Count == 0)
            {
                Debug.LogError("Cannot Replay game mode, no round stats data found!");
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
            selectedVesselClass.Value = VesselClassType.Manta;
            VesselClassSelectedIndex.Value = 1;
            SelectedPlayerCount.Value = 1;
            SelectedIntensity.Value = 1;
            
            ResetRuntimeData();
            
            DomainAssigner.Initialize();
        }

        public void AddPlayer(IPlayer p)
        {
            if (p == null) 
                return;

            // Avoid duplicates by Name
            if (Players.Any(player => player.Name == p.Name)) 
                return;
            
            if (RoundStatsList.Any(rs => rs.Name == p.Name)) 
                return;

            Players.Add(p);
            
            RoundStatsList.Add(p.RoundStats);
            if (p.IsLocalUser)
            {
                LocalPlayer = p;
                LocalRoundStats = p.RoundStats;
            }
            
            p.ResetForPlay();
            
            if (!NetworkManager.Singleton || NetworkManager.Singleton.IsServer)
                p.SetPoseOfVessel(GetRandomSpawnPose());
        }
        
        public void SortRoundStats(bool golfRules)
        {
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

            return top is null ? (Domains.Jade, 0f) : (Team: top.Domain, top.VolumeRemaining);
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
                Debug.LogError("No round stats found to calculate winner!");
                return false;
            }

            if (!TryGetLocalPlayerStats(out IPlayer _, out roundStats))
            {
                Debug.LogError("No round stats of active player found!");
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
                Debug.LogError("[ServerPlayerVesselInitializer] PlayerSpawnPoints array not set or empty.");
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
                Debug.LogError("[ServerPlayerVesselInitializer] PlayerSpawnPoints array not set or empty.");
                return;
            }

            _playerSpawnPoseList?.Clear();
            _playerSpawnPoseList = new List<Pose>(SpawnPoses.ToList());
        }
        
        Pose GetRandomSpawnPose()
        {
            if (_playerSpawnPoseList == null || _playerSpawnPoseList.Count == 0)
            {
                _playerSpawnPoseList = new List<Pose>(SpawnPoses.Length);
                _playerSpawnPoseList = SpawnPoses.ToList();
            }
            
            int index = UnityEngine.Random.Range(0, _playerSpawnPoseList.Count);
            var spawnPoint = _playerSpawnPoseList[index];
            _playerSpawnPoseList.RemoveAt(index);
            return spawnPoint;
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