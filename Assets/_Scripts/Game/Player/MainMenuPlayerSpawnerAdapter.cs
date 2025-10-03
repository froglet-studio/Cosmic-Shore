using UnityEngine;

namespace CosmicShore.Game
{
    public class MainMenuPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        private void Start() => InitializeGame();

        void InitializeGame()
        {
            SpawnAIPlayersAndAddToGameData();
            _gameData.SetPlayersActive(true);
            _gameData.InvokeGameStarted();
        }
    }
}