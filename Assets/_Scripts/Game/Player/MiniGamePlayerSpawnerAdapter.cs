using UnityEngine;

namespace CosmicShore.Game
{
    public class MiniGamePlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        [SerializeField] private bool _spawnAIAtStart = false;

        private void OnEnable()
        {
            _gameData.OnInitializeGame += InitializeGame;
            AddSpawnPosesToGameData();
        }

        private void Start()
        {
            if (_spawnAIAtStart)
                SpawnDefaultPlayersAndAddToGameData();
        }

        private void OnDisable()
        {
            _gameData.OnInitializeGame -= InitializeGame;
        }

        void InitializeGame()
        {
            SpawnCustomPlayerAndAddToGameData(InitializePlayerData());
            SpawnDefaultPlayersAndAddToGameData();
        }

        private IPlayer.InitializeData InitializePlayerData()
        {
            // TODO: Wire player name/uuid/teams from your profile/select flow.
            return new IPlayer.InitializeData
            {
                vesselClass      = _gameData.selectedVesselClass.Value,
                domain           = Domains.Jade,         // Default for now
                PlayerName     = "HumanJade",        // Placeholder
                // PlayerUUID     = "HumanJade1",       // Placeholder
                AllowSpawning  = true,
                IsAI  = false,
            };
        }
    }
}