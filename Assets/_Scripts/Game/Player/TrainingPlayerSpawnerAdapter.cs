using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Spawns AI-only players for automated training.
    /// Unlike MiniGamePlayerSpawnerAdapter, this does NOT spawn a human player.
    /// Configure _initializeDatas in the inspector with AI entries
    /// (IsAI=true, AllowSpawning=true) for each pilot you want racing.
    /// </summary>
    public class TrainingPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        void OnEnable()
        {
            _gameData.OnInitializeGame += InitializeGame;
            AddSpawnPosesToGameData();
        }

        void OnDisable()
        {
            _gameData.OnInitializeGame -= InitializeGame;
        }

        void InitializeGame()
        {
            SpawnDefaultPlayersAndAddToGameData();
        }
    }
}
