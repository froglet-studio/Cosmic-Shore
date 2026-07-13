using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Quest-graph-driven constraints on the arcade UI, used to funnel the first orientation:
    /// lock every game card except the tutorial mode, pin the configure modal to one intensity,
    /// and default the player/domain steppers for the best first-game experience.
    ///
    /// Static on purpose: the constraints must survive the Menu → game → Menu scene round-trip
    /// (the runner and all scene UI are destroyed and rebuilt), and statics do. Set by
    /// <see cref="QuestSetArcadeConstraintsNode"/>, cleared by the same node (Clear mode), on
    /// quest completion, and by the editor's progress reset.
    ///
    /// Consumers: <c>ArcadeExploreView</c> (card locking) and <c>ArcadeGameConfigureModal</c>
    /// (intensity buttons + player/domain stepper defaults).
    /// </summary>
    public static class QuestArcadeConstraints
    {
        /// <summary>Raised whenever the constraints are applied or cleared — arcade UI
        /// (card grid) subscribes to refresh its lock state immediately.</summary>
        public static event System.Action OnChanged;

        /// <summary>Master switch — when false every query below is a no-op.</summary>
        public static bool Active { get; private set; }

        /// <summary>The one playable mode while active. <see cref="GameModes.Random"/> = no card restriction.</summary>
        public static GameModes AllowedMode { get; private set; } = GameModes.Random;

        /// <summary>The only selectable intensity while active (0 = no restriction).</summary>
        public static int ForcedIntensity { get; private set; }

        /// <summary>Default the configure modal's player count to the game's maximum.</summary>
        public static bool ForceMaxPlayers { get; private set; }

        /// <summary>Default the configure modal's domain (team) count (0 = no override).</summary>
        public static int ForcedDomainCount { get; private set; }

        public static bool IsModeBlocked(GameModes mode) =>
            Active && AllowedMode != GameModes.Random && mode != AllowedMode;

        public static bool IsIntensityBlocked(int intensity) =>
            Active && ForcedIntensity > 0 && intensity != ForcedIntensity;

        public static void Apply(GameModes allowedMode, int forcedIntensity, bool forceMaxPlayers, int forcedDomainCount)
        {
            Active = true;
            AllowedMode = allowedMode;
            ForcedIntensity = forcedIntensity;
            ForceMaxPlayers = forceMaxPlayers;
            ForcedDomainCount = forcedDomainCount;
            OnChanged?.Invoke();
        }

        public static void Clear()
        {
            Active = false;
            AllowedMode = GameModes.Random;
            ForcedIntensity = 0;
            ForceMaxPlayers = false;
            ForcedDomainCount = 0;
            OnChanged?.Invoke();
        }
    }
}
