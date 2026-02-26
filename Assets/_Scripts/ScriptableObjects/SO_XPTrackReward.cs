using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Defines a single reward that can be unlocked at an XP milestone.
    /// </summary>
    [CreateAssetMenu(
        fileName = "XPTrackReward_",
        menuName = "ScriptableObjects/XPTrack/XPTrackReward")]
    public class SO_XPTrackReward : ScriptableObject
    {
        [Header("Reward Info")]
        [Tooltip("Unique identifier for this reward")]
        public string rewardId;

        [Tooltip("Display name shown to the player")]
        public string rewardName;

        [Tooltip("Icon displayed in the XP track and unlock panel")]
        public Sprite icon;

        [Header("Unlock Data (extensible)")]
        [Tooltip("Description of what is unlocked")]
        public string unlockDescription;

        [Tooltip("Optional: type tag for future unlock logic (e.g. 'Ship', 'Skin', 'Title')")]
        public string unlockType;

        [Tooltip("Optional: reference ID for the unlocked content")]
        public string unlockReferenceId;
    }
}
