using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.App.Systems;
using CosmicShore.Core;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.SOAP
{
    /// <summary>
    /// Every MiniGame in the project should use the same asset of this SO.
    /// It connects MiniGameBase with StatsManager, TurnMonitor and others.
    /// </summary>
    [CreateAssetMenu(fileName = "scriptable_variable_" + nameof(MiniGameDataSO), menuName = "ScriptableObjects/DataContainers/"+ nameof(MiniGameDataSO))]
    public class MiniGameDataSO : ScriptableObject
    {
        // TODO - need two more events OnMiniGameStart and OnMiniGameEnd
        
        public event Action OnMiniGameInitialize;
        public event Action OnAllPlayersSpawned;
        public event Action OnMiniGameStart;
        public event Action OnMiniGameEndConditionsMet;
        public event Action OnMiniGameTurnEnd;
        public event Action OnMiniGameEnd;
        public event Action OnWinnerCalculated;
        
        public GameModes GameMode;
        public ShipClassTypeVariable SelectedShipClass;
        public IntVariable SelectedPlayerCount;
        public IntVariable SelectedIntensity;
        public List<IPlayer> Players = new();
        // public List<int> RemainingPlayers = new();
        public Transform[] PlayerOrigins;
        public IPlayer ActivePlayer => Players.Count > 0 && Players[activePlayerId] != null ? Players[activePlayerId] : null;
        int activePlayerId = 0;
        
        public List<IRoundStats> RoundStatsList = new ();
        public Dictionary<int, CellStats> CellStatsList = new();
        
        public float TurnStartTime;

        public bool IsRunning { get; private set; }

        public void InvokeMiniGameInitialize()
        {
            PauseSystem.TogglePauseGame(false);
            OnMiniGameInitialize?.Invoke();
        }

        public void InvokeMiniGameStart()
        {
            IsRunning = true;
            TurnStartTime = Time.time;
            ActivateAllPlayers();
            OnMiniGameStart?.Invoke();
        }
        public void InvokeMiniGameTurnConditionsMet() => OnMiniGameEndConditionsMet?.Invoke();

        public void InvokeMiniGameTurnEnd()
        {
            IsRunning = false;
            OnMiniGameTurnEnd?.Invoke();
        }

        public void InvokeMiniGameEnd() => OnMiniGameEnd?.Invoke();
        
        public void InvokeWinnerCalculated() => OnWinnerCalculated?.Invoke();

        public (Teams, float) GetControllingTeamStatsBasedOnVolumeRemaining()
        {
            var sortedList = GetSortedListInDecendingOrderBasedOnVolumeRemaining();
            return  (sortedList[0].Team, sortedList[0].VolumeRemaining);
        }

        public List<IRoundStats> GetSortedListInDecendingOrderBasedOnVolumeRemaining() =>
            RoundStatsList
                .OrderByDescending(r => r.VolumeRemaining)
                .ToList();

        public bool TryGetActivePlayerStats(out IPlayer player, out IRoundStats roundStats)
        {
            player = null;
            roundStats = null;

            if (Players == null || Players.Count == 0 || RoundStatsList == null || RoundStatsList.Count == 0)
            {
                Debug.LogError("This should never happen!");
                return false;
            }

            var p = Players[activePlayerId];
            player = p;
            roundStats = RoundStatsList.FirstOrDefault(stats => stats.Name == p.Name);

            return true;
        }

        public bool TryGetTeamRemainingVolume(Teams team, out float volume)
        {
            volume = 0f;
            foreach (var roundStats in RoundStatsList.Where(roundStats => roundStats.Team == team))
            {
                volume = roundStats.VolumeRemaining;
                return true;
            }
            return false;
        }
        
        public bool TryGetRoundStats(Teams team, out IRoundStats roundStats)
        {
            roundStats = null;
            
            foreach (var score in RoundStatsList.Where(score => score.Team == team))
            {
                roundStats = score;
                return true;
            }

            Debug.LogError("This should never happen! Every Score data need to have a local player!");
            return false;
        }
        
        public bool TryGetRoundStats(string playerName, out IRoundStats roundStats)
        {
            roundStats = null;
            
            foreach (var score in RoundStatsList.Where(score => score.Name == playerName))
            {
                roundStats = score;
                return true;
            }

            Debug.LogError("This should never happen! Every Score data need to have a local player!");
            return false;
        }
        
        public float GetTotalVolume() => RoundStatsList.Sum(stats => stats.VolumeRemaining);
        
        public Vector4 GetTeamVolumes()
        {
            var greenVolume = TryGetRoundStats(Teams.Jade, out var gStats) ? gStats.VolumeRemaining : 0f;
            var redVolume = TryGetRoundStats(Teams.Ruby, out var rStats) ? rStats.VolumeRemaining : 0f;
            var blueVolume = TryGetRoundStats(Teams.Blue, out var bStats) ? bStats.VolumeRemaining : 0f;
            var goldVolume = TryGetRoundStats(Teams.Gold, out var yStats) ? yStats.VolumeRemaining : 0f;
            return new Vector4(greenVolume, redVolume, blueVolume, goldVolume);
        }
        
        public void AddPlayer(IPlayer p)
        {
            if (Players.Any(player => player.Name == p.Name))
                return;
            
            if (RoundStatsList.Any(score => score.Name == p.Name))
                return;
            
            Players.Add(p);
            var roundStats = new RoundStats()       // For Networking, we need NetworkRoundStats
            {
                Name = p.Name,
                Team = p.Team,
            };
            RoundStatsList.Add(roundStats);
            
            p.ToggleStationaryMode(true);
            
            // RemainingPlayers.Add(Players.Count-1);
            // localPlayer ??= p;
        }

        public void ActivateAllPlayers()
        {
            foreach (var player in Players)
            {
                player.Ship.ShipStatus.ResourceSystem.Reset();
                player.Ship.ShipStatus.ShipTransformer.ResetShipTransformer();
                player.ToggleStationaryMode(false);
                player.ToggleInputStatus(false);
                // ActivePlayer.Ship.SetResourceLevels(ResourceCollection);
            }
        }
        
        public void DeactivateAllPlayers()
        {
            foreach (var player in Players)
            {
                player.ToggleStationaryMode(true);
                player.ToggleInputStatus(true);
            }
        }
        
        public void InvokeAllPlayersSpawned() => OnAllPlayersSpawned?.Invoke();
        
        public void ResetData()
        {
            GameMode = GameModes.Random;
            Players.Clear();
            RoundStatsList.Clear();
            // RemainingPlayers.Clear();
            PlayerOrigins = Array.Empty<Transform>();
            activePlayerId = 0;
            GameMode = GameModes.Random;
            SelectedShipClass.Value = ShipClassType.Random;
            SelectedPlayerCount.Value = 1;
            SelectedIntensity.Value = 1;
            TurnStartTime = 0f;
        }
        
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
            localPlayer.Ship.Teleport(activePlayerOrigin);
            localPlayer.Ship.ShipStatus.ShipTransformer.ResetShipTransformer();
            // ActivePlayer.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
            localPlayer.Ship.ShipStatus.ResourceSystem.Reset();
            // ActivePlayer.Ship.SetResourceLevels(ResourceCollection);

            // CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.ShipStatus.FollowTarget);
            
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
            // ActivePlayer.Ship.ShipStatus.TrailSpawner.ForceStartSpawningTrail();
        }


        public void SetupForNextTurn()
        {
            LocalPlayer.InputController.InputStatus.Paused = false;
            LocalPlayer.Ship.ShipStatus.TrailSpawner.ForceStartSpawningTrail();
            LocalPlayer.Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
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
