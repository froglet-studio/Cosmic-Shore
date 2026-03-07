using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Models
{
    /// <summary>
    /// Defines a single game-mode quest in the progression chain.
    /// Each quest gates the next game mode: the player must meet the
    /// target condition in this mode before the next mode unlocks.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameModeQuest_",
        menuName = "ScriptableObjects/Quests/GameModeQuestData")]
    public class SO_GameModeQuestData : ScriptableObject
    {
        [Header("Quest Identity")]
        [Tooltip("The game mode this quest is associated with")]
        public GameModes GameMode;

        [Tooltip("Display name shown on the quest track (e.g. 'Crystal Capture')")]
        public string DisplayName;

        [Tooltip("Description of the unlock condition shown to the player")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("Icon displayed on the quest track")]
        public Sprite Icon;

        [Header("Unlock Condition")]
        [Tooltip("What type of stat must be met to complete this quest")]
        public QuestTargetType TargetType;

        [Tooltip("The threshold value the player must reach (e.g. 30 crystals, or 4 for IntensityUnlocked)")]
        public float TargetValue;

        [Header("Intensity Unlock (when TargetType = IntensityUnlocked)")]
        [Tooltip("What stat type to evaluate for unlocking intensity tiers. Set to Placeholder for play-count-based unlocking.")]
        public QuestTargetType IntensityUnlockStatType = QuestTargetType.Placeholder;

        [Tooltip("Stat threshold at intensity 2 to unlock intensity 3 (e.g., 30 crystals, or 215 seconds for race time)")]
        public float Intensity3StatTarget;

        [Tooltip("Stat threshold at intensity 3 to unlock intensity 4 and complete quest (e.g., 25 crystals)")]
        public float Intensity4StatTarget;

        [Tooltip("Number of games at intensity 2 required to unlock intensity 3 (used when IntensityUnlockStatType = Placeholder)")]
        [Min(1)]
        public int PlaysToUnlockIntensity3 = 3;

        [Tooltip("Number of games at intensity 3 required to unlock intensity 4 (used when IntensityUnlockStatType = Placeholder)")]
        [Min(1)]
        public int PlaysToUnlockIntensity4 = 3;

        [Tooltip("Goal description shown when the player taps a locked intensity 3 button")]
        [TextArea(1, 3)]
        public string Intensity3GoalDescription = "Play Intensity 2 to unlock";

        [Tooltip("Goal description shown when the player taps a locked intensity 4 button")]
        [TextArea(1, 3)]
        public string Intensity4GoalDescription = "Play Intensity 3 to unlock";

        [Header("Order")]
        [Tooltip("Position in the quest chain (0 = first, already unlocked)")]
        public int Order;

        [Header("Flags")]
        [Tooltip("If true, this quest is a placeholder for a mode not yet implemented")]
        public bool IsPlaceholder;

        [Tooltip("If true, this quest unlocks a feature (e.g. Vessel Hangar) rather than a game mode. " +
                 "The DisplayName is used as the feature key for persistence.")]
        public bool IsFeatureUnlock;

        /// <summary>
        /// Runtime flag set when the quest goal is achieved during gameplay.
        /// Not serialized — lives only in the current session, synced from ProgressionData on load.
        /// </summary>
        [System.NonSerialized] public bool IsCompleted;
    }

    /// <summary>
    /// Types of quest completion targets.
    /// Extend as new game modes require different win conditions.
    /// </summary>
    public enum QuestTargetType
    {
        /// <summary>Collect N or more crystals in a single match</summary>
        CrystalsCollected = 0,

        /// <summary>Finish a race in under N seconds</summary>
        RaceTimeUnder = 1,

        /// <summary>Win N jousts in a single match</summary>
        JoustsWon = 2,

        /// <summary>Achieve a score of N or higher</summary>
        ScoreAbove = 3,

        /// <summary>Reach a survival time of N seconds or more</summary>
        SurvivalTime = 4,

        /// <summary>Win a match (placement = 1st)</summary>
        WinMatch = 5,

        /// <summary>Unlock intensity 4 by playing enough games at each intensity tier</summary>
        IntensityUnlocked = 6,

        /// <summary>Placeholder — quest auto-completes or uses custom logic</summary>
        Placeholder = 99,
    }
}
