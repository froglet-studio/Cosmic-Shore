using System.Collections.Generic;
using CosmicShore.App.Profile;
using CosmicShore.Models;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.XP
{
    /// <summary>
    /// Calculates and awards XP after a game ends.
    /// Attach to a persistent GameObject (e.g. alongside PlayerDataService).
    /// </summary>
    public class XPRewardService : MonoBehaviour
    {
        public static XPRewardService Instance { get; private set; }

        [Header("XP Track Data")]
        [SerializeField] private SO_XPTrackData xpTrackData;

        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        /// <summary>
        /// Stores the XP earned in the most recent game, for UI display.
        /// </summary>
        public int LastXPEarned { get; private set; }

        /// <summary>
        /// Stores newly unlocked milestones from the most recent XP award.
        /// </summary>
        public List<XPMilestone> LastUnlockedMilestones { get; private set; } = new();

        /// <summary>
        /// The XP value before the most recent award (for animation purposes).
        /// </summary>
        public int PreviousXP { get; private set; }

        private static readonly HashSet<GameModes> XPEligibleModes = new()
        {
            GameModes.MultiplayerJoust,
            GameModes.HexRace,
            GameModes.MultiplayerCrystalCapture
        };

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Calculates XP earned based on game mode, player count, placement, and whether the game is offline.
        /// Call this after SortRoundStats has been called and winner is determined.
        /// </summary>
        /// <returns>The XP amount earned (0 if not eligible or player left).</returns>
        public int CalculateXP()
        {
            if (gameData == null || gameData.LocalPlayer == null)
                return 0;

            if (!XPEligibleModes.Contains(gameData.GameMode))
                return 0;

            int placement = GetLocalPlayerPlacement();
            if (placement <= 0)
                return 0; // Player left or not found

            bool isOffline = !gameData.IsMultiplayerMode;
            int playerCount = gameData.RoundStatsList.Count;

            return CalculateXPForPlacement(placement, playerCount, isOffline);
        }

        /// <summary>
        /// Calculates and awards XP, syncing to cloud.
        /// Call this after the game ends.
        /// </summary>
        public int AwardXP()
        {
            int xpAmount = CalculateXP();
            LastXPEarned = xpAmount;
            LastUnlockedMilestones.Clear();

            if (xpAmount <= 0)
                return 0;

            var profileService = PlayerDataService.Instance;
            if (profileService == null)
            {
                Debug.LogWarning("[XPRewardService] PlayerDataService.Instance is null, cannot award XP.");
                return xpAmount;
            }

            PreviousXP = profileService.GetXP();
            int newXP = PreviousXP + xpAmount;

            // Check for newly unlocked milestones
            if (xpTrackData != null)
            {
                LastUnlockedMilestones = xpTrackData.GetNewlyUnlockedMilestones(PreviousXP, newXP);

                foreach (var milestone in LastUnlockedMilestones)
                {
                    if (milestone.reward != null && !string.IsNullOrEmpty(milestone.reward.rewardId))
                    {
                        profileService.UnlockReward(milestone.reward.rewardId);
                    }
                }
            }

            profileService.AddXP(xpAmount);

            Debug.Log($"[XPRewardService] Awarded {xpAmount} XP. Previous: {PreviousXP}, New: {newXP}");
            return xpAmount;
        }

        /// <summary>
        /// Gets the local player's placement (1-based index) from sorted RoundStatsList.
        /// Returns 0 if the local player is not found.
        /// </summary>
        int GetLocalPlayerPlacement()
        {
            if (gameData.LocalPlayer == null || gameData.RoundStatsList == null)
                return 0;

            string localName = gameData.LocalPlayer.Name;
            for (int i = 0; i < gameData.RoundStatsList.Count; i++)
            {
                if (gameData.RoundStatsList[i].Name == localName)
                    return i + 1;
            }

            return 0;
        }

        /// <summary>
        /// XP rules:
        /// Offline (vs AI): 1st=20, 2nd=10, 3rd+=5
        /// Online 2-player: 1st=20, 2nd=10
        /// Online 3-player: 1st=20, 2nd=10, 3rd=5
        /// </summary>
        static int CalculateXPForPlacement(int placement, int playerCount, bool isOffline)
        {
            if (isOffline)
            {
                // AI mode: 1st=20, 2nd=10, 3rd and below=5
                return placement switch
                {
                    1 => 20,
                    2 => 10,
                    _ => 5
                };
            }

            // Online modes
            if (playerCount == 2)
            {
                return placement switch
                {
                    1 => 20,
                    _ => 10
                };
            }

            // 3+ player online
            return placement switch
            {
                1 => 20,
                2 => 10,
                _ => 5
            };
        }

        /// <summary>
        /// Returns the XP track data SO for UI access.
        /// </summary>
        public SO_XPTrackData GetXPTrackData() => xpTrackData;
    }
}
