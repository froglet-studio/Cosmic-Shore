using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Models.ScriptableObjects
{
    /// <summary>
    /// Defines the XP track: a list of milestones, each with an XP threshold and associated reward.
    /// </summary>
    [CreateAssetMenu(
        fileName = "XPTrackData",
        menuName = "ScriptableObjects/XPTrack/XPTrackData")]
    public class SO_XPTrackData : ScriptableObject
    {
        [Header("XP Track Configuration")]
        [Tooltip("XP interval between each milestone (default: 50)")]
        public int xpPerMilestone = 50;

        [Tooltip("Ordered list of milestone rewards. Index 0 = first milestone, etc.")]
        public List<XPMilestone> milestones = new();

        /// <summary>
        /// Returns the milestone index for a given XP value (how many milestones completed).
        /// </summary>
        public int GetMilestoneIndex(int xp)
        {
            if (xpPerMilestone <= 0) return 0;
            return xp / xpPerMilestone;
        }

        /// <summary>
        /// Returns XP progress within the current milestone (0 to xpPerMilestone-1).
        /// </summary>
        public int GetProgressInCurrentMilestone(int xp)
        {
            if (xpPerMilestone <= 0) return 0;
            return xp % xpPerMilestone;
        }

        /// <summary>
        /// Returns normalized progress (0-1) within the current milestone.
        /// </summary>
        public float GetNormalizedProgress(int xp)
        {
            if (xpPerMilestone <= 0) return 0f;
            return (float)(xp % xpPerMilestone) / xpPerMilestone;
        }

        /// <summary>
        /// Returns the reward for a given milestone index, or null if no reward is defined.
        /// </summary>
        public SO_XPTrackReward GetRewardForMilestone(int milestoneIndex)
        {
            if (milestoneIndex < 0 || milestoneIndex >= milestones.Count)
                return null;
            return milestones[milestoneIndex].reward;
        }

        /// <summary>
        /// Returns all newly unlocked milestones between oldXP and newXP.
        /// </summary>
        public List<XPMilestone> GetNewlyUnlockedMilestones(int oldXP, int newXP)
        {
            var unlocked = new List<XPMilestone>();
            if (xpPerMilestone <= 0) return unlocked;

            int oldMilestoneCount = oldXP / xpPerMilestone;
            int newMilestoneCount = newXP / xpPerMilestone;

            for (int i = oldMilestoneCount; i < newMilestoneCount && i < milestones.Count; i++)
            {
                unlocked.Add(milestones[i]);
            }

            return unlocked;
        }
    }

    [System.Serializable]
    public class XPMilestone
    {
        [Tooltip("The reward granted when this milestone is reached")]
        public SO_XPTrackReward reward;
    }
}
