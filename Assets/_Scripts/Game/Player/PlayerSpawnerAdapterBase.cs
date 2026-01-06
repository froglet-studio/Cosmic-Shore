using System;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class PlayerSpawnerAdapterBase : MonoBehaviour
    {
        [SerializeField] protected GameDataSO _gameData;
        [SerializeField] protected PlayerSpawner _playerSpawner;

        [SerializeField] 
        protected IPlayer.InitializeData[] _initializeDatas;
        
        [SerializeField] private Transform[] _spawnTransforms;
        
        protected void AddSpawnPosesToGameData() =>
            _gameData.SetSpawnPositions(_spawnTransforms);
        
        /// <summary>Spawn all AI entries allowed by the spawner and add them to game data.</summary>
        protected void SpawnDefaultPlayersAndAddToGameData()
        {
            if (_initializeDatas == null) return;

            var initializeDataCount = _initializeDatas.Length;
            for (int i = 0; i < initializeDataCount; i++)
            {
                var data = _initializeDatas[i];
                if (!data.AllowSpawning) continue;

                SpawnCustomPlayerAndAddToGameData(data);
            }
        }

        /// <summary>Spawn one player from InitializeData and add to game data.</summary>
        protected void SpawnCustomPlayerAndAddToGameData(IPlayer.InitializeData data)
        {
            var player = _playerSpawner.SpawnPlayerAndShip(data);
            _gameData.AddPlayer(player);
        }
    }
}