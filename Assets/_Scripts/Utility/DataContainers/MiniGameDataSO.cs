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
    [CreateAssetMenu(
        fileName = "scriptable_variable_" + nameof(MiniGameDataSO),
        menuName = "ScriptableObjects/DataContainers/" + nameof(MiniGameDataSO))]
    public class MiniGameDataSO : ScriptableObject
    {
        // Events
        public event Action OnMiniGameInitialize;
        public event Action OnAllPlayersSpawned;
        public event Action OnMiniGameStart;
        public event Action OnMiniGameEndConditionsMet;
        public event Action OnMiniGameTurnEnd;
        public event Action OnMiniGameEnd;
        public event Action OnWinnerCalculated;

        // Config / State
        public GameModes GameMode;
        public ShipClassTypeVariable SelectedShipClass;
        public IntVariable SelectedPlayerCount;
        public IntVariable SelectedIntensity;

        public List<IPlayer> Players = new();
        public Transform[] PlayerOrigins;

        public List<IRoundStats> RoundStatsList = new();
        public Dictionary<int, CellStats> CellStatsList = new();

        public float TurnStartTime;
        public bool IsRunning { get; private set; }

        int _activePlayerId;

        public IPlayer ActivePlayer =>
            (_activePlayerId >= 0 && _activePlayerId < Players.Count) ? Players[_activePlayerId] : null;

        // -----------------------------------------------------------------------------------------
        // Initialization / Lifecycle

        public void InvokeMiniGameInitialize()
        {
            PauseSystem.TogglePauseGame(false);

            Players.Clear();
            RoundStatsList.Clear();
            PlayerOrigins = Array.Empty<Transform>();
            _activePlayerId = 0;
            TurnStartTime = 0f;

            OnMiniGameInitialize?.Invoke();
        }

        public void InvokeMiniGameStart()
        {
            IsRunning = true;
            TurnStartTime = Time.time;

            SetPlayersActive(active: true);
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
        public void InvokeAllPlayersSpawned() => OnAllPlayersSpawned?.Invoke();

        public void ResetData()
        {
            GameMode = GameModes.Random;
            Players.Clear();
            RoundStatsList.Clear();
            PlayerOrigins = Array.Empty<Transform>();
            _activePlayerId = 0;

            SelectedShipClass.Value = ShipClassType.Random;
            SelectedPlayerCount.Value = 1;
            SelectedIntensity.Value = 1;
            TurnStartTime = 0f;
        }

        // -----------------------------------------------------------------------------------------
        // Queries / Scores

        public (Teams Team, float Volume) GetControllingTeamStatsBasedOnVolumeRemaining()
        {
            var top = RoundStatsList
                .OrderByDescending(rs => rs.VolumeRemaining)
                .FirstOrDefault();

            return top is null ? (Teams.Jade, 0f) : (top.Team, top.VolumeRemaining);
        }

        // Keep existing (misspelled) method to avoid breaking callers
        public List<IRoundStats> GetSortedListInDecendingOrderBasedOnVolumeRemaining() =>
            RoundStatsList.OrderByDescending(r => r.VolumeRemaining).ToList();

        // Correctly spelled alias (use this in new code)
        public List<IRoundStats> GetSortedListInDescendingOrderBasedOnVolumeRemaining() =>
            GetSortedListInDecendingOrderBasedOnVolumeRemaining();

        public bool TryGetActivePlayerStats(out IPlayer player, out IRoundStats roundStats)
        {
            player = ActivePlayer;
            roundStats = player != null ? FindByName(player.Name) : null;
            return player != null && roundStats != null;
        }

        public bool TryGetTeamRemainingVolume(Teams team, out float volume)
        {
            volume = VolumeOf(team);
            return FindByTeam(team) != null;
        }

        public bool TryGetRoundStats(Teams team, out IRoundStats roundStats)
        {
            roundStats = FindByTeam(team);
            return roundStats != null;
        }

        public bool TryGetRoundStats(string playerName, out IRoundStats roundStats)
        {
            roundStats = FindByName(playerName);
            return roundStats != null;
        }

        public float GetTotalVolume() => RoundStatsList.Sum(stats => stats.VolumeRemaining);

        public Vector4 GetTeamVolumes()
        {
            float jade = VolumeOf(Teams.Jade);
            float ruby = VolumeOf(Teams.Ruby);
            float blue = VolumeOf(Teams.Blue);
            float gold = VolumeOf(Teams.Gold);
            return new Vector4(jade, ruby, blue, gold);
        }

        // -----------------------------------------------------------------------------------------
        // Mutation

        public void AddPlayer(IPlayer p)
        {
            if (p == null) return;

            // Avoid duplicates by Name
            if (Players.Any(player => player.Name == p.Name)) return;
            if (RoundStatsList.Any(rs => rs.Name == p.Name)) return;

            Players.Add(p);

            // For Networking, replace with NetworkRoundStats as needed
            RoundStatsList.Add(new RoundStats
            {
                Name = p.Name,
                Team = p.Team
            });

            // Keep players stationary until game starts
            p.ToggleStationaryMode(true);
        }

        public void ActivateAllPlayers()  => SetPlayersActive(active: true);
        public void DeactivateAllPlayers() => SetPlayersActive(active: false);

        // -----------------------------------------------------------------------------------------
        // Helpers (private)

        IRoundStats FindByTeam(Teams team) =>
            RoundStatsList.FirstOrDefault(rs => rs.Team == team);

        IRoundStats FindByName(string name) =>
            RoundStatsList.FirstOrDefault(rs => rs.Name == name);

        float VolumeOf(Teams team) =>
            FindByTeam(team)?.VolumeRemaining ?? 0f;

        void SetPlayersActive(bool active)
        {
            foreach (var player in Players)
            {
                if (player?.Ship?.ShipStatus == null) continue;

                if (active)
                {
                    // Reset ship state when activating for a new run
                    player.Ship.ShipStatus.ResourceSystem.Reset();
                    player.Ship.ShipStatus.ShipTransformer.ResetShipTransformer();
                }

                // Stationary/input flags invert relative to "active"
                player.ToggleStationaryMode(!active);
                player.ToggleInputStatus(!active);
            }
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
