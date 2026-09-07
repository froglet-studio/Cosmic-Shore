namespace CosmicShore.Gameplay
{
    public class VolumeTestPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        private void Start()
        {
            _gameData.InitializeGame();
            AddSpawnPosesToGameData();
            SpawnDefaultPlayersAndAddToGameData();
            _gameData.SetPlayersActive();
        }
    }
}