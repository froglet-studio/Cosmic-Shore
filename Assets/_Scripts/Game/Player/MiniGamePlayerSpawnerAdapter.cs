using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MiniGamePlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        [SerializeField] private bool _spawnAIAtStart = false;

        private void OnEnable()
        {
            _gameData.OnMiniGameInitialize += InitializeGame;
        }

        private void Start()
        {
            if (_spawnAIAtStart)
                SpawnAndAddAIPlayers();
        }

        private void OnDisable()
        {
            _gameData.OnMiniGameInitialize -= InitializeGame;
        }

        protected override void InitializeGame()
        {
            SpawnAndAddHumanPlayer();
            SpawnAndAddAIPlayers();
            RaiseAllPlayersSpawned();
        }

        private void SpawnAndAddHumanPlayer()
        {
            // TODO: Wire player name/uuid/teams from your profile/select flow.
            var data = new IPlayer.InitializeData
            {
                ShipClass      = _gameData.SelectedShipClass.Value,
                Team           = Teams.Jade,         // Default for now
                PlayerName     = "HumanJade",        // Placeholder
                PlayerUUID     = "HumanJade1",       // Placeholder
                AllowSpawning  = true,
                EnableAIPilot  = false,
            };

            SpawnAndAddPlayer(data);
        }
    }
}