using System;
using System.Collections.Generic;
using System.Linq;
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
        public event Action OnInitialize;
        public event Action OnAllPlayersSpawned;
        
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
        public float TurnStartTime;

        public void InvokeInitialize()
        {
            TurnStartTime = Time.time;
            OnInitialize?.Invoke();
        }

        public bool TryGetActivePlayerStats(out IPlayer player, out IRoundStats roundStats)
        {
            player = null;
            roundStats = null;

            if (Players == null || Players.Count == 0)
            {
                Debug.LogError("This should never happen!");
                return false;
            }

            if (RoundStatsList == null || RoundStatsList.Count == 0)
            {
                Debug.LogError("This should never happen!");
                return false;
            }

            var p = Players[activePlayerId];
            player = p;
            roundStats = RoundStatsList.FirstOrDefault(stats => stats.Name == p.Name);

            return true;
        }

        public bool TryGetRoundStats(Teams team, out IRoundStats roundStats)
        {
            roundStats = null;
            
            foreach (var score in RoundStatsList)
            {
                if (score.Team != team)
                    continue;
                roundStats = score;
                return true;
            }

            Debug.LogError("This should never happen! Every Score data need to have a local player!");
            return false;
        }
        
        public bool TryGetRoundStats(string playerName, out IRoundStats roundStats)
        {
            roundStats = null;
            
            foreach (var score in RoundStatsList)
            {
                if (score.Name != playerName)
                    continue;
                roundStats = score;
                return true;
            }

            Debug.LogError("This should never happen! Every Score data need to have a local player!");
            return false;
        }

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
        
        public void AddPlayer(IPlayer p, IRoundStats  stats)
        {
            if (Players.Any(player => player.Name == p.Name))
                return;
            
            if (RoundStatsList.Any(score => score.Name == p.Name))
                return;
            
            Players.Add(p);
            stats.Name = p.Name;
            stats.Team = p.Team;
            RoundStatsList.Add(stats);
            
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
