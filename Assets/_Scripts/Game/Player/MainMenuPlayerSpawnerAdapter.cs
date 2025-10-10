using UnityEngine;

namespace CosmicShore.Game
{
    public class MainMenuPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        private void Start() => InitializeGame();

        void InitializeGame()
        {
            AddSpawnPosesToGameData();
            SpawnAIPlayersAndAddToGameData();
            _gameData.SetPlayersActive();
            _gameData.InvokeGameStarted();
        }
    }
}