using System;
using CosmicShore.Core;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MiniGamePlayerSpawnerAdapter : MonoBehaviour
    {
        [SerializeField] MiniGameDataSO _miniGameData;
        
        [SerializeField] PlayerSpawner _playerSpawner;
        
        [SerializeField] bool _spawnAIAtStart;

        private void OnEnable()
        {
            _miniGameData.OnMiniGameInitialize += OnInitializeMiniGame;
        }

        private void Start()
        {
            InstantiateAndInitializeAI();
        }

        private void OnDisable()
        { 
            _miniGameData.OnMiniGameInitialize -= OnInitializeMiniGame;
        }

        private void OnInitializeMiniGame()
        {
            InstantiateAndInitializePlayer();
            InstantiateAndInitializeAI();
            
            _miniGameData.InvokeAllPlayersSpawned();
        }

        void InstantiateAndInitializeAI()
        {
            int initializeDataCount = _playerSpawner.InitializeDatas.Length;
            for (int i = 0; i < initializeDataCount; i++)
            {
                var data =  _playerSpawner.InitializeDatas[i];
                if (!data.AllowSpawning)
                    continue;
                
                IPlayer spawnerAI = _playerSpawner.SpawnPlayerAndShip(data);
                _miniGameData.AddPlayer(spawnerAI);
            }
        }
        
        void InstantiateAndInitializePlayer()
        {
            // TODO - Selected Player Count will be needed in multiplayer.
            // int noOfPlayersToSpawn = _miniGameData.Value.SelectedPlayerCount.Value;
            
            IPlayer.InitializeData data = new()
            {
                ShipClass = _miniGameData.SelectedShipClass.Value,
                Team = Teams.Jade,  // Defaulted to Jade for now!
                PlayerName =  "HumanJade",  // Defaulted - later need to fetch player name from -> "PlayerDataController.PlayerProfile.DisplayName : PlayerNames[i],"
                PlayerUUID = "HumanJade1",
                AllowSpawning = true,
                EnableAIPilot = false,
            };
            
            IPlayer spawnerPlayer = _playerSpawner.SpawnPlayerAndShip(data);
            _miniGameData.AddPlayer(spawnerPlayer);
        }
    }
}