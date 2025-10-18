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
using UnityEngine.Serialization;
using IPlayer = CosmicShore.Game.IPlayer;

namespace CosmicShore.SOAP
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
        // Events
        public event Action OnLaunchGameScene;
        public event Action OnSessionStarted;
        public event Action OnInitializeGame;
        public event Action OnClientReady;
        public event Action OnTurnStarted;
        public ScriptableEventNoParam OnMiniGameTurnEnd;
        public event Action OnMiniGameEnd;
        public event Action OnWinnerCalculated;
        
        public ScriptableEventNoParam OnResetForReplay;

        
        // Local player config / state
        public VesselClassTypeVariable selectedVesselClass;
        public IntVariable VesselClassSelectedIndex;
        public IntVariable SelectedPlayerCount;
        public IntVariable SelectedIntensity;
        public SO_Captain PlayerCaptain;
        public ResourceCollection ResourceCollection;
        
        
        // Game Config / State
        public string SceneName;
        public GameModes GameMode;
        public bool IsDailyChallenge;
        public bool IsTraining;
        public bool IsMission;
        public bool IsMultiplayerMode;
        public List<IPlayer> Players = new();
        public List<IRoundStats> RoundStatsList = new();
        public Dictionary<int, CellStats> CellStatsList = new();
        public HashSet<Transform> SlowedShipTransforms = new();
        public float TurnStartTime;
        public bool IsRunning { get; private set; }
        public Pose[] SpawnPoses { get; private set; }
        List<Pose> _playerSpawnPoseList = new ();
        public IPlayer ActivePlayer { get; private set; }
        public ISession ActiveSession { get; set; }
        
        // -----------------------------------------------------------------------------------------
        // Initialization / Lifecycle

        public void InvokeGameLaunch() => OnLaunchGameScene?.Invoke();
        
        public void InitializeGame()
        {
            ResetRuntimeData();
            OnInitializeGame?.Invoke();
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
            IsRunning = true;
            TurnStartTime = Time.time;

            InvokeTurnStarted();
        }
        
        public void InvokeSessionStarted() => OnSessionStarted?.Invoke();
        public void InvokeTurnStarted() => OnTurnStarted?.Invoke();
        public void InvokeGameTurnConditionsMet() => OnMiniGameTurnEnd?.Raise();
        public void InvokeMiniGameEnd() => OnMiniGameEnd?.Invoke();
        public void InvokeWinnerCalculated() => OnWinnerCalculated?.Invoke();
        public void InvokeClientReady() => OnClientReady?.Invoke();

        public void ResetRuntimeData()
        {
            Players.Clear();
            RoundStatsList.Clear();
            TurnStartTime = 0f;
        }

        public void ResetDataForReplay()
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
            
            TurnStartTime = 0f;
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

        public bool TryGetActivePlayerStats(out IPlayer player, out IRoundStats roundStats)
        {
            player = ActivePlayer;
            roundStats = player != null ? FindByName(player.Name) : null;
            return player != null && roundStats != null;
        }

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
        
        public bool TryGetWinnerForMultiplayer(out IRoundStats roundStats, out bool won)
        {
            roundStats = null;
            won = false;
            if (RoundStatsList is null || RoundStatsList.Count == 0)
            {
                Debug.LogError("No round stats found to calculate winner!");
                return false;
            }

            if (!TryGetActivePlayerStats(out IPlayer _, out roundStats))
            {
                Debug.LogError("No round stats of active player found!");
                return false;   
            }

            if (roundStats.Name == RoundStatsList[0].Name)
                won = true;

            return true;
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

            if (!TryGetActivePlayerStats(out IPlayer _, out roundStats))
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
        
        public void AddPlayer(IPlayer p)
        {
            if (p == null) return;

            // Avoid duplicates by Name
            if (Players.Any(player => player.Name == p.Name)) return;
            if (RoundStatsList.Any(rs => rs.Name == p.Name)) return;

            Players.Add(p);
            if (Players.Count == 1)
                ActivePlayer = p;

            // For Networking, replace with NetworkRoundStats as needed, adding it to the Player Prefab
            RoundStatsList.Add(new RoundStats
            {
                Name = p.Name,
                Domain = p.Domain
            });
            
            p.ResetForPlay();
        }

        public void AddPlayerInMultiplayer(IPlayer p, IRoundStats roundStats)
        {
            if (p == null) 
                return;

            // Avoid duplicates by Name
            if (Players.Any(player => player.Name == p.Name)) 
                return;
            
            if (RoundStatsList.Any(rs => rs.Name == p.Name)) 
                return;

            Players.Add(p);
            if (p.IsNetworkOwner)
                ActivePlayer = p;
            
            // For Networking, replace with NetworkRoundStats as needed, adding it to the Player Prefab
            RoundStatsList.Add(roundStats);
            
            p.ResetForPlay();
        }
        
        public void SortRoundStats(bool golfRules)
        {
            if (golfRules)
                RoundStatsList.Sort((score1, score2) => score1.Score.CompareTo(score2.Score));
            else
                RoundStatsList.Sort((score1, score2) => score2.Score.CompareTo(score1.Score));
        }
        
        public void SetPlayersActive()
        {
            foreach (var player in Players)
            {
                var vesselStatus = player?.Vessel?.VesselStatus;

                if (vesselStatus == null)
                {
                    Debug.LogError("No vessel status found for player.! This should never happen!");
                    return;
                }
                
                player.ToggleStationaryMode(false);
                player.ToggleInputPause(player.IsInitializedAsAI);
                player.ToggleAutoPilot(player.IsInitializedAsAI);
                player.ToggleActive(true);
                vesselStatus.VesselPrismController.StartSpawn();
            }
        }
        
        public void SetPlayersActiveForMultiplayer()
        {
            foreach (var player in Players)
            {
                var vesselStatus = player?.Vessel?.VesselStatus;

                if (vesselStatus == null)
                {
                    Debug.LogError("No vessel status found for player.! This should never happen!");
                    return;
                }

                if (!vesselStatus.IsStationary)
                    continue;
                
                bool isOwner = player.IsNetworkOwner;
                player.ToggleStationaryMode(false);
                player.ToggleInputPause(!isOwner);
                player.ToggleActive(true);
                vesselStatus.VesselPrismController.StartSpawn();
            }
        }

        public void ResetPlayers()
        {
            foreach (var player in Players)
            {
                var vesselStatus = player?.Vessel?.VesselStatus;

                if (vesselStatus == null)
                {
                    Debug.LogError("No vessel status found for player.! This should never happen!");
                    return;
                }
                
                player.ResetForPlay();
                
                if (IsMultiplayerMode && !player.IsNetworkOwner)
                    continue;
                player.SetPoseOfVessel(GetRandomSpawnPose());
            }
        }
        
        /// <summary>
        /// Remove a player (by display name) from Players & RoundStatsList and fix ActivePlayer if needed.
        /// </summary>
        public bool RemovePlayerData(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return false;

            // Remove from Players
            int removedPlayers = Players.RemoveAll(p => p != null && p.Name == playerName);

            // Remove from RoundStats
            int removedStats = RoundStatsList.RemoveAll(rs => rs != null && rs.Name == playerName);

            // Fix ActivePlayer if it was the removed one
            if (ActivePlayer != null && ActivePlayer.Name == playerName)
                ActivePlayer = Players.Count > 0 ? Players[0] : null;

            // Optional: also stop their vessel spawning if any dangling reference exists (defensive)
            // No-op here because Players list holds the references.

            return (removedPlayers + removedStats) > 0;
        }
        
        // ----------------------------
        // Spawn point picker
        // ----------------------------
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
        
        // TODO - Need to rewrite the following method.
        /*
        public bool TryAdvanceActivePlayer(out IPlayer activePlayer)
        {
            activePlayer = null;
            if (RemainingPlayers.Count == 0)
            {
                Debug.LogError($"No remaining player found to set as active player!");
                return false;
            }

            activePlayerId = 0; // (ActivePlayerId + 1) % RemainingPlayers.Count; 
            localPlayer = Players[0]; // Players[RemainingPlayers[ActivePlayerId]];
            Transform activePlayerOrigin =  PlayerOrigins[activePlayerId];
            
            localPlayer.Transform.SetPositionAndRotation(activePlayerOrigin.position, activePlayerOrigin.rotation);
            localPlayer.InputController.InputStatus.Paused = true;
            localPlayer.Vessel.Teleport(activePlayerOrigin);
            localPlayer.Vessel.VesselStatus.VesselTransformer.ResetTransformer();
            // ActivePlayer.Vessel.VesselStatus.TrailSpawner.PauseTrailSpawner();
            localPlayer.Vessel.VesselStatus.ResourceSystem.Reset();
            // ActivePlayer.Vessel.SetResourceLevels(ResourceCollection);

            // CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Vessel.VesselStatus.CameraFollowTarget);
            
            foreach (var player in Players)
            {
                // Debug.Log($"PlayerUUID: {player.PlayerUUID}");
                player.ToggleGameObject(player.PlayerUUID == localPlayer.PlayerUUID);
            }
            
            activePlayer = localPlayer;
            return true;
        }

        
        public void PlayActivePlayer()
        {
            LocalPlayer.ToggleStationaryMode(false);
            LocalPlayer.InputController.InputStatus.Paused = false;
            // ActivePlayer.Vessel.VesselStatus.TrailSpawner.ForceStartSpawningTrail();
        }


        public void SetupForNextTurn()
        {
            LocalPlayer.InputController.InputStatus.Paused = false;
            LocalPlayer.Vessel.VesselStatus.TrailSpawner.ForceStartSpawningTrail();
            LocalPlayer.Vessel.VesselStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
        }

        public void EliminateActive()
        {
            RemainingPlayers.RemoveAt(ActivePlayerId);
            ActivePlayerId--;

            if (ActivePlayerId < 0 && RemainingPlayers.Count > 0)
                ActivePlayerId = RemainingPlayers.Count - 1;

            if (RemainingPlayers.Count > 0)
                LocalPlayer = Players[RemainingPlayers[ActivePlayerId]];
        }
        */
    }
}
