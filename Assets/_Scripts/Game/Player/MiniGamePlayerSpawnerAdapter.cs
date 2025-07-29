using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MiniGamePlayerSpawnerAdapter : MonoBehaviour
    {
        [SerializeField] protected MiniGameDataVariable _miniGameData;

        [SerializeField] private PlayerSpawner _playerSpawner;

        private void OnEnable()
        {
            _miniGameData.OnInitialize += OnInitializeMiniGame;
        }

        private void OnDisable()
        { 
            _miniGameData.OnInitialize -= OnInitializeMiniGame;
        }

        private void OnInitializeMiniGame() => InstantiateAndInitializePlayer();
        
        void InstantiateAndInitializePlayer()
        {
            IPlayer activePlayer = _miniGameData.Value.ActivePlayer;
            
            if (activePlayer != null)
                return;

            // TODO - Selected Player Count will be needed in multiplayer.
            // int noOfPlayersToSpawn = _miniGameData.Value.SelectedPlayerCount.Value;
            
            IPlayer.InitializeData data = new()
            {
                ShipClass = _miniGameData.Value.SelectedShipClass.Value,
                Team = Teams.Jade,  // Defaulted to Jade for now!
                PlayerName =  "HumanJade",  // Defaulted - later need to fetch player name from -> "PlayerDataController.PlayerProfile.DisplayName : PlayerNames[i],"
                PlayerUUID = "HumanJade1",
                AllowSpawning = true,
                EnableAIPilot = false,
            };
            
            IPlayer spawnerPlayer = _playerSpawner.SpawnPlayerAndShip(data);
            _miniGameData.Value.AddPlayer(spawnerPlayer);
        }
    }
}