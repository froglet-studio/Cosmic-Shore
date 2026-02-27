using CosmicShore.UI;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class MiniGamePlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        [Inject] private PlayerDataService playerDataService;
        [SerializeField] private bool _spawnAIAtStart = false;

        private void OnEnable() => TrySubscribeAndSetup();

        private void Start()
        {
            TrySubscribeAndSetup();
            if (_spawnAIAtStart)
                SpawnDefaultPlayersAndAddToGameData();
        }

        private void OnDisable()
        {
            if (_gameData != null)
                _gameData.OnInitializeGame.OnRaised -= InitializeGame;
        }

        private void TrySubscribeAndSetup()
        {
            if (_gameData == null) return;
            _gameData.OnInitializeGame.OnRaised -= InitializeGame;
            _gameData.OnInitializeGame.OnRaised += InitializeGame;
            AddSpawnPosesToGameData();
        }

        void InitializeGame()
        {
            SpawnCustomPlayerAndAddToGameData(InitializePlayerData());
            SpawnDefaultPlayersAndAddToGameData();
        }

        private IPlayer.InitializeData InitializePlayerData()
        {
            // Resolve display name and avatar from PlayerDataService if available
            string displayName = "HumanJade";
            int avatarId = 0;

            if (playerDataService != null && playerDataService.IsInitialized && playerDataService.CurrentProfile != null)
            {
                displayName = playerDataService.CurrentProfile.displayName;
                avatarId = playerDataService.CurrentProfile.avatarId;
            }
            else
            {
                if (!string.IsNullOrEmpty(_gameData.LocalPlayerDisplayName))
                    displayName = _gameData.LocalPlayerDisplayName;
                if (_gameData.LocalPlayerAvatarId != 0)
                    avatarId = _gameData.LocalPlayerAvatarId;
            }

            return new IPlayer.InitializeData
            {
                vesselClass    = _gameData.selectedVesselClass.Value,
                domain         = Domains.Jade,
                PlayerName     = displayName,
                AvatarId       = avatarId,
                AllowSpawning  = true,
                IsAI           = false,
            };
        }
    }
}