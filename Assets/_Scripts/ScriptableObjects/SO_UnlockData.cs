using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One node in the unlock progression chain.
    ///
    /// An Unlock reveals ANY app feature (game mode, vessel, screen, captain, episode, UI
    /// element — see <see cref="FeatureKind"/>), is gated by a Quest (a completion condition),
    /// and carries the breadcrumb (Call-to-Action) that guides the player to whatever they need
    /// to do next. The quest track decides WHAT'S NEXT; the breadcrumb guides the player THERE.
    ///
    /// Formerly <c>SO_GameModeQuestData</c>. The script GUID is preserved across the rename so
    /// existing authored assets keep resolving; the legacy field names are unchanged so their
    /// serialized values survive.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Unlock_",
        menuName = "ScriptableObjects/Quests/UnlockData")]
    public class SO_UnlockData : ScriptableObject
    {
        [Header("Feature Revealed")]
        [Tooltip("What kind of feature this node unlocks. Drives how the unlock is keyed and " +
                 "what evaluates its quest gate.")]
        public FeatureKind FeatureKind = FeatureKind.GameMode;

        [Tooltip("The game mode this unlock gates. Used when FeatureKind == GameMode.")]
        public GameModes GameMode;

        [Tooltip("Generic feature reference for non-GameMode kinds: vessel name, screen id, " +
                 "captain id, episode id, etc. Ignored when FeatureKind == GameMode.")]
        public string FeatureRef;

        [Tooltip("Display name shown on the quest track (e.g. 'Crystal Capture')")]
        public string DisplayName;

        [Tooltip("Description of the unlock condition shown to the player")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("Icon displayed on the quest track")]
        public Sprite Icon;

        [Header("Unlock Condition (Quest gate)")]
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
        [Tooltip("Position in the unlock chain (0 = first, already unlocked)")]
        public int Order;

        [Header("Flags")]
        [Tooltip("If true, this node is a placeholder for a feature not yet implemented")]
        public bool IsPlaceholder;

        [Header("Breadcrumb (Call-to-Action)")]
        [Tooltip("App-shell element the breadcrumb lights to guide the player toward this " +
                 "unlock's objective (e.g. the game's PlayGame* card). None = no breadcrumb.")]
        public CallToActionTargetType CallToActionTargetID = CallToActionTargetType.None;

        [Tooltip("The user action that dismisses this breadcrumb once performed. Use None for a " +
                 "progression-owned frontier (a multi-play quest, e.g. reach intensity 4): it stays " +
                 "lit until the objective is met and the service retracts it on frontier advance — " +
                 "NOT dismissed after a single play. Use a specific action (e.g. ViewHangarMenu) for " +
                 "a one-shot 'go here' guide.")]
        public UserActionType CompletionUserAction = UserActionType.None;

        [Tooltip("Parent targets lit alongside the main target — the nested path to it (e.g. " +
                 "ArcadeMenu before a game card). Each is dismissed in sequence as it resolves.")]
        public List<CallToActionTargetType> DependencyTargetIDs = new();

        /// <summary>
        /// Runtime flag set when the quest goal is achieved during gameplay.
        /// Not serialized — lives only in the current session, synced from ProgressionData on load.
        /// </summary>
        [System.NonSerialized] public bool IsCompleted;

        /// <summary>
        /// Stable key used to index this unlock in the cloud progression record.
        /// GameMode kinds key by the mode name (back-compat with existing saves);
        /// every other kind keys by <see cref="FeatureRef"/>.
        /// </summary>
        public string UnlockKey =>
            FeatureKind == FeatureKind.GameMode ? GameMode.ToString() : FeatureRef;

        /// <summary>True if this node has an authored breadcrumb target.</summary>
        public bool HasBreadcrumb => CallToActionTargetID != CallToActionTargetType.None;

        /// <summary>
        /// Builds the <see cref="CallToAction"/> breadcrumb instruction for this unlock,
        /// or null when no breadcrumb target is authored. The dependency list is copied so the
        /// CTA system cannot mutate the authored asset.
        /// </summary>
        public CallToAction BuildCallToAction()
        {
            if (!HasBreadcrumb) return null;
            return new CallToAction(
                CallToActionTargetID,
                CompletionUserAction,
                DependencyTargetIDs != null
                    ? new List<CallToActionTargetType>(DependencyTargetIDs)
                    : null);
        }
    }

    /// <summary>
    /// The kind of app feature an <see cref="SO_UnlockData"/> node reveals. Adding a new kind
    /// here is the engineer-facing extension point for unlocking something other than a game mode.
    /// </summary>
    public enum FeatureKind
    {
        GameMode = 0,
        Vessel = 1,
        IntensityTier = 2,
        Screen = 3,
        Captain = 4,
        Episode = 5,
        UIElement = 6,
    }

    /// <summary>
    /// Types of quest completion targets.
    /// Extend as new features require different completion conditions.
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
