using System.Collections.Generic;
using CosmicShore.App.Profile;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MiniGamePlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        private const int DefaultTotalPlayerSlots = 4;

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
            SpawnCustomPlayerAndAddToGameData(InitializePlayerData(slotIndex: 0));
            SpawnAIPlayersToFillSlots();
        }

        private int GetMaxPlayerSlots()
        {
            if (_gameData.CurrentArcadeGame != null)
                return Mathf.Clamp(_gameData.CurrentArcadeGame.MaxPlayers, 1, DefaultTotalPlayerSlots);
            return DefaultTotalPlayerSlots;
        }

        /// <summary>
        /// Returns the vessel class for a given player slot.
        /// If _initializeDatas has an entry at that index with a concrete vessel class,
        /// it overrides the game mode's selected vessel.
        /// </summary>
        private VesselClassType GetVesselForSlot(int slotIndex)
        {
            if (_initializeDatas != null && slotIndex < _initializeDatas.Length)
            {
                var overrideClass = _initializeDatas[slotIndex].vesselClass;
                if (overrideClass is not VesselClassType.Random and not VesselClassType.Any)
                    return overrideClass;
            }

            return _gameData.selectedVesselClass.Value;
        }

        private void SpawnAIPlayersToFillSlots()
        {
            int maxSlots = GetMaxPlayerSlots();
            int humanCount = Mathf.Clamp(_gameData.SelectedPlayerCount.Value, 1, maxSlots);
            int aiCount = maxSlots - humanCount;
            if (aiCount <= 0) return;

            var aiDomains = GetAIDomains(humanCount, aiCount);
            var profiles = _aiProfileList != null ? _aiProfileList.PickRandom(aiCount) : null;

            for (int i = 0; i < aiCount; i++)
            {
                int slotIndex = humanCount + i;
                var data = new IPlayer.InitializeData
                {
                    vesselClass   = GetVesselForSlot(slotIndex),
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

        private IPlayer.InitializeData InitializePlayerData(int slotIndex)
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
                vesselClass    = GetVesselForSlot(slotIndex),
                domain         = Domains.Jade,
                PlayerName     = displayName,
                AvatarId       = avatarId,
                AllowSpawning  = true,
                IsAI           = false,
            };
        }
    }
}
