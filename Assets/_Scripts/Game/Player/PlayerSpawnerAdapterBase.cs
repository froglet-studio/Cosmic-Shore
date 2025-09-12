using System;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class PlayerSpawnerAdapterBase : MonoBehaviour
    {
        [SerializeField] protected MiniGameDataSO _gameData;
        [SerializeField] protected PlayerSpawner _playerSpawner;

        [SerializeField] 
        protected IPlayer.InitializeData[] _initializeDatas;
        
        private void OnApplicationQuit()
        {
            _gameData.ResetData();
        }

        /// <summary>Spawn all AI entries allowed by the spawner and add them to game data.</summary>
        protected void SpawnAndAddAIPlayers()
        {
            if (_initializeDatas == null) return;

            var initializeDataCount = _initializeDatas.Length;
            for (int i = 0; i < initializeDataCount; i++)
            {
                var data = _initializeDatas[i];
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