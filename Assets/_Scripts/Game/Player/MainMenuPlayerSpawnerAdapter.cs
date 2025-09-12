using UnityEngine;

namespace CosmicShore.Game
{
    public class MainMenuPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        private void Start() => InitializeGame();

        void InitializeGame()
        {
            SpawnAndAddAIPlayers();
            RaiseAllPlayersSpawned();
        }
    }
}