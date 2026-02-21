using CosmicShore.App.Profile;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MiniGamePlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        [SerializeField] private bool _spawnAIAtStart = false;

        private void OnEnable()
        {
            _gameData.OnInitializeGame += InitializeGame;
            AddSpawnPosesToGameData();
        }

        private void Start()
        {
            if (_spawnAIAtStart)
                SpawnDefaultPlayersAndAddToGameData();
        }

        private void OnDisable()
        {
            _gameData.OnInitializeGame -= InitializeGame;
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

            var profileService = FindAnyObjectByType<PlayerDataService>();
            if (profileService != null && profileService.IsInitialized && profileService.CurrentProfile != null)
            {
                displayName = profileService.CurrentProfile.displayName;
                avatarId = profileService.CurrentProfile.avatarId;
            }
            else if (!string.IsNullOrEmpty(_gameData.LocalPlayerDisplayName))
            {
                displayName = _gameData.LocalPlayerDisplayName;
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