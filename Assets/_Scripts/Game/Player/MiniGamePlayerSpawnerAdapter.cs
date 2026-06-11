using System.Collections.Generic;
using CosmicShore.App.Profile;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MiniGamePlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        private const int TotalPlayerSlots = 4;

        [SerializeField] private bool _spawnAIAtStart = false;

        [Header("AI Configuration")]
        [Tooltip("Optional AI profile list for assigning unique names to AI players.")]
        [SerializeField] private SO_AIProfileList _aiProfileList;

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
            SpawnAIPlayersToFillSlots();
        }

        private void SpawnAIPlayersToFillSlots()
        {
            int humanCount = Mathf.Clamp(_gameData.SelectedPlayerCount.Value, 1, TotalPlayerSlots);
            int aiCount = TotalPlayerSlots - humanCount;
            if (aiCount <= 0) return;

            var aiDomains = GetAIDomains(humanCount, aiCount);
            var profiles = _aiProfileList != null ? _aiProfileList.PickRandom(aiCount) : null;

            for (int i = 0; i < aiCount; i++)
            {
                var data = new IPlayer.InitializeData
                {
                    vesselClass   = _gameData.selectedVesselClass.Value,
                    domain        = aiDomains[i],
                    PlayerName    = profiles != null && i < profiles.Count ? profiles[i].Name : $"AI_{i + 1}",
                    AvatarId      = 0,
                    IsAI          = true,
                    AllowSpawning = true,
                };
                SpawnCustomPlayerAndAddToGameData(data);
            }
        }

        /// <summary>
        /// Determines AI domain assignments to always form balanced 2v2 teams.
        /// 1 human:  human(Jade) + AI(Jade) vs AI(Ruby) + AI(Ruby)
        /// 2 humans: human(Jade) + human(Jade) vs AI(Ruby) + AI(Ruby)
        /// 3 humans: human(Jade) + human(Jade) vs human(Ruby) + AI(Ruby)
        /// </summary>
        private List<Domains> GetAIDomains(int humanCount, int aiCount)
        {
            var domains = new List<Domains>(aiCount);

            switch (humanCount)
            {
                case 1:
                    // 1 AI teammate on Jade, 2 AI opponents on Ruby
                    domains.Add(Domains.Jade);
                    domains.Add(Domains.Ruby);
                    domains.Add(Domains.Ruby);
                    break;

                case 2:
                    // Both humans on Jade (same team), both AI on Ruby
                    domains.Add(Domains.Ruby);
                    domains.Add(Domains.Ruby);
                    break;

                case 3:
                    // 3rd player is on Ruby; AI joins their team
                    domains.Add(Domains.Ruby);
                    break;
            }

            return domains;
        }

        private IPlayer.InitializeData InitializePlayerData()
        {
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
