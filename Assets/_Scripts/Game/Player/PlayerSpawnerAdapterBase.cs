using System;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class PlayerSpawnerAdapterBase : MonoBehaviour
    {
        [SerializeField] protected MiniGameDataSO _gameData;
        [SerializeField] protected PlayerSpawner _playerSpawner;

        private void OnApplicationQuit()
        {
            _gameData.ResetData();
        }

        /// <summary>Runs when you want to kick off spawning for this context.</summary>
        protected abstract void InitializeGame();

        /// <summary>Spawn all AI entries allowed by the spawner and add them to game data.</summary>
        protected void SpawnAndAddAIPlayers()
        {
            if (_playerSpawner?.InitializeDatas == null) return;

            var initializeDataCount = _playerSpawner.InitializeDatas.Length;
            for (int i = 0; i < initializeDataCount; i++)
            {
                var data = _playerSpawner.InitializeDatas[i];
                if (!data.AllowSpawning) continue;

                var player = _playerSpawner.SpawnPlayerAndShip(data);
                _gameData.AddPlayer(player);
            }
        }

        /// <summary>Spawn one player from InitializeData and add to game data.</summary>
        protected void SpawnAndAddPlayer(IPlayer.InitializeData data)
        {
            if (!data.AllowSpawning) return;
            var player = _playerSpawner.SpawnPlayerAndShip(data);
            _gameData.AddPlayer(player);
        }

        /// <summary>Signal that all players are spawned.</summary>
        protected void RaiseAllPlayersSpawned() => _gameData.InvokeAllPlayersSpawned();
    }
}