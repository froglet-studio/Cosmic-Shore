using CosmicShore.App.Profile;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MiniGamePlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        [SerializeField] private bool _spawnAIAtStart = false;

        private void OnEnable()
        {
            _gameData.OnInitializeGame.OnRaised += InitializeGame;
            AddSpawnPosesToGameData();
        }

        private void Start()
        {
            if (_spawnAIAtStart)
                SpawnDefaultPlayersAndAddToGameData();
        }

        private void OnDisable()
        {
            _gameData.OnInitializeGame.OnRaised -= InitializeGame;
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

            var profileService = PlayerDataService.Instance;
            if (profileService != null && profileService.IsInitialized && profileService.CurrentProfile != null)
            {
                displayName = profileService.CurrentProfile.displayName;
                avatarId = profileService.CurrentProfile.avatarId;
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