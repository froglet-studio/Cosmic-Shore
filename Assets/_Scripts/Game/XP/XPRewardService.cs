using System.Collections.Generic;
using CosmicShore.UI.Views;
using CosmicShore.Models.ScriptableObjects;
using CosmicShore.Utility.DataContainers;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Game.XP
{
    /// <summary>
    /// Calculates and awards XP after a game ends.
    /// Place on a GameObject in each game scene (e.g. alongside EndGameCinematicController).
    /// </summary>
    public class XPRewardService : MonoBehaviour
    {
        public static XPRewardService Instance { get; private set; }

        [Header("XP Track Data")]
        [SerializeField] private SO_XPTrackData xpTrackData;

        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        [Inject] private PlayerDataService playerDataService;

        public int LastXPEarned { get; private set; }
        public List<XPMilestone> LastUnlockedMilestones { get; private set; } = new();
        public int PreviousXP { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Calculates XP earned based on placement and player count.
        /// Awards XP for all game modes.
        /// </summary>
        public int CalculateXP()
        {
            if (gameData == null)
            {
                CSDebug.LogWarning("[XPRewardService] gameData is null.");
                return 0;
            }

            if (gameData.LocalPlayer == null)
            {
                CSDebug.LogWarning("[XPRewardService] LocalPlayer is null.");
                return 0;
            }

            CSDebug.Log($"[XPRewardService] CalculateXP - Mode: {gameData.GameMode}, " +
                      $"RoundStats count: {gameData.RoundStatsList?.Count ?? 0}");

            int placement = GetLocalPlayerPlacement();
            if (placement <= 0)
            {
                // Single-player mode or player not in round stats - award base XP
                CSDebug.Log("[XPRewardService] Player not found in RoundStatsList, awarding base XP (10).");
                return 10;
            }

            bool isOffline = !gameData.IsMultiplayerMode;
            int playerCount = gameData.RoundStatsList.Count;

            int xp = CalculateXPForPlacement(placement, playerCount, isOffline);
            CSDebug.Log($"[XPRewardService] Placement: {placement}/{playerCount}, " +
                      $"Offline: {isOffline}, XP: {xp}");
            return xp;
        }

        /// <summary>
        /// Calculates and awards XP, syncing to cloud.
        /// Call this after the game ends.
        /// </summary>
        public int AwardXP()
        {
            CSDebug.Log("[XPRewardService] AwardXP called.");

            int xpAmount = CalculateXP();
            LastXPEarned = xpAmount;
            LastUnlockedMilestones.Clear();

            if (xpAmount <= 0)
            {
                CSDebug.Log("[XPRewardService] XP amount is 0, nothing to award.");
                return 0;
            }

            if (playerDataService == null)
            {
                CSDebug.LogWarning("[XPRewardService] PlayerDataService is null, cannot award XP.");
                return xpAmount;
            }

            if (playerDataService.CurrentProfile == null)
            {
                CSDebug.LogWarning("[XPRewardService] CurrentProfile is null, cannot award XP.");
                return xpAmount;
            }

            PreviousXP = playerDataService.GetXP();
            int newXP = PreviousXP + xpAmount;

            // Check for newly unlocked milestones
            if (xpTrackData != null)
            {
                LastUnlockedMilestones = xpTrackData.GetNewlyUnlockedMilestones(PreviousXP, newXP);

                foreach (var milestone in LastUnlockedMilestones)
                {
                    if (milestone.reward != null && !string.IsNullOrEmpty(milestone.reward.rewardId))
                    {
                        playerDataService.UnlockReward(milestone.reward.rewardId);
                        CSDebug.Log($"[XPRewardService] Unlocked reward: {milestone.reward.rewardName}");
                    }
                }
            }

            playerDataService.AddXP(xpAmount);

            CSDebug.Log($"[XPRewardService] Awarded {xpAmount} XP. Previous: {PreviousXP}, New: {newXP}");
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
