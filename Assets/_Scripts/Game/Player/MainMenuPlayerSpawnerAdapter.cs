using System;
using UnityEngine;

namespace CosmicShore.Game.Player
{
    public class MainMenuPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        private void Start() => InitializeGame();

        void InitializeGame()
        {
            _gameData.InitializeGame();
            AddSpawnPosesToGameData();
            SpawnDefaultPlayersAndAddToGameData();
            _gameData.SetPlayersActive();
            _gameData.InvokeMiniGameRoundStarted();
            _gameData.InvokeTurnStarted();
        }
    }
}