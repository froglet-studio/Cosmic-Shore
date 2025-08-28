using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MainMenuPlayerSpawnerAdapter : MonoBehaviour
    {
        [SerializeField] MiniGameDataSO _mainMenuGameData;
        
        [SerializeField] PlayerSpawner _playerSpawner;

        private void Start()
        {
            OnInitializeGame();
        }

        private void OnInitializeGame()
        {
            InstantiateAndInitializeAI();
            _mainMenuGameData.InvokeAllPlayersSpawned();
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
                _mainMenuGameData.AddPlayer(spawnerAI);
            }
        }
    }
}