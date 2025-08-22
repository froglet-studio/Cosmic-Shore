using System;
using System.Collections.Generic;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.SOAP
{
    [System.Serializable]
    public class MiniGameData
    {
        public GameModes GameMode;
        public ShipClassTypeVariable SelectedShipClass;
        public IntVariable SelectedPlayerCount;
        public IntVariable SelectedIntensity;
        public int HighScore;
        public List<IPlayer> Players = new();
        public List<int> RemainingPlayers = new();
        public Transform[] PlayerOrigins;

        public int ActivePlayerId { get; private set; } = -1;
        public IPlayer ActivePlayer { get; private set; }

        public void ResetData()
        {
            GameMode = GameModes.Random;
            HighScore = 0;
            Players.Clear();
            RemainingPlayers.Clear();
            PlayerOrigins = Array.Empty<Transform>();
            ActivePlayerId = 0;
            ActivePlayer = null;
        }
        
        public void AddPlayer(IPlayer p)
        {
            Players.Add(p);
            // RemainingPlayers.Add(Players.Count-1);
            ActivePlayer ??= p;
        }

        public bool TryAdvanceActivePlayer(out IPlayer activePlayer)
        {
            activePlayer = null;
            /*if (RemainingPlayers.Count == 0)
            {
                Debug.LogError($"No remaining player found to set as active player!");
                return false; 
            }*/

            ActivePlayerId = 0; // (ActivePlayerId + 1) % RemainingPlayers.Count; 
            ActivePlayer = Players[0]; // Players[RemainingPlayers[ActivePlayerId]];
            Transform activePlayerOrigin =  PlayerOrigins[ActivePlayerId];
            
            ActivePlayer.Transform.SetPositionAndRotation(activePlayerOrigin.position, activePlayerOrigin.rotation);
            ActivePlayer.InputController.InputStatus.Paused = true;
            ActivePlayer.Ship.Teleport(activePlayerOrigin);
            ActivePlayer.Ship.ShipStatus.ShipTransformer.ResetShipTransformer();
            // ActivePlayer.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
            ActivePlayer.Ship.ShipStatus.ResourceSystem.Reset();
            // ActivePlayer.Ship.SetResourceLevels(ResourceCollection);

            // CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.ShipStatus.FollowTarget);
            
            foreach (var player in Players)
            {
                // Debug.Log($"PlayerUUID: {player.PlayerUUID}");
                player.ToggleGameObject(player.PlayerUUID == ActivePlayer.PlayerUUID);
            }
            
            activePlayer = ActivePlayer;
            return true;
        }

        public void PlayActivePlayer()
        {
            ActivePlayer.ToggleStationaryMode(false);
            ActivePlayer.InputController.InputStatus.Paused = false;
            // ActivePlayer.Ship.ShipStatus.TrailSpawner.ForceStartSpawningTrail();
        }

        public void SetupForNextTurn()
        {
            Debug.Log($"Player {ActivePlayer.PlayerUUID + 1} Get Ready! {Time.time}");
            ActivePlayer.InputController.InputStatus.Paused = false;
            ActivePlayer.Ship.ShipStatus.TrailSpawner.ForceStartSpawningTrail();
            ActivePlayer.Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
        }
        
        public void EliminateActive()
        { 
            RemainingPlayers.RemoveAt(ActivePlayerId); 
            ActivePlayerId--;
            
            if (ActivePlayerId < 0 && RemainingPlayers.Count > 0) 
                ActivePlayerId = RemainingPlayers.Count - 1;
            
            if (RemainingPlayers.Count > 0) 
                ActivePlayer = Players[RemainingPlayers[ActivePlayerId]];
        }
    }
}
