using UnityEngine;

namespace CosmicShore.Game
{
    public class MainMenuPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        private void Start() => InitializeGame();

        protected override void InitializeGame()
        {
            SpawnAndAddAIPlayers();
            RaiseAllPlayersSpawned();
        }
    }
}