using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Quest-graph-driven constraints on the arcade UI, used to funnel the first orientation:
    /// lock every game card except the tutorial mode, pin the configure modal to one intensity,
    /// and default the player/domain steppers for the authored first-game experience.
    ///
    /// PERSISTED in PlayerPrefs alongside the quest's local progress cursor: the constraints
    /// must survive not only the Menu → game → Menu scene round-trip but also a play-session
    /// restart mid-quest — the resume cursor lands PAST the SetArcadeConstraints node, so if
    /// the state lived only in statics a restart would silently unlock the whole arcade.
    /// Set by <see cref="QuestSetArcadeConstraintsNode"/>, cleared by the same node (Clear
    /// mode), on quest completion, and by the editor's progress reset.
    ///
    /// Consumers: <c>ArcadeExploreView</c> (card locking) and <c>ArcadeGameConfigureModal</c>
    /// (intensity buttons + player/domain stepper defaults).
    /// </summary>
    public static class QuestArcadeConstraints
    {
        const string PrefActive = "QUEST_ARC_Active";
        const string PrefMode = "QUEST_ARC_AllowedMode";
        const string PrefIntensity = "QUEST_ARC_Intensity";
        const string PrefPlayers = "QUEST_ARC_Players";
        const string PrefDomains = "QUEST_ARC_Domains";

        /// <summary>Raised whenever the constraints are applied or cleared — arcade UI
        /// (card grid) subscribes to refresh its lock state immediately.</summary>
        public static event System.Action OnChanged;

        static bool _loaded;
        static bool _active;
        static GameModes _allowedMode = GameModes.Random;
        static int _forcedIntensity;
        static int _forcedPlayerCount;
        static int _forcedDomainCount;

        /// <summary>Master switch — when false every query below is a no-op.</summary>
        public static bool Active { get { EnsureLoaded(); return _active; } }

        /// <summary>The one playable mode while active. <see cref="GameModes.Random"/> = no card restriction.</summary>
        public static GameModes AllowedMode { get { EnsureLoaded(); return _allowedMode; } }

        /// <summary>The only selectable intensity while active (0 = no restriction).</summary>
        public static int ForcedIntensity { get { EnsureLoaded(); return _forcedIntensity; } }

        /// <summary>Exact player count the configure modal defaults to (0 = no override).</summary>
        public static int ForcedPlayerCount { get { EnsureLoaded(); return _forcedPlayerCount; } }

        /// <summary>Domain (team) count the configure modal defaults to (0 = no override).</summary>
        public static int ForcedDomainCount { get { EnsureLoaded(); return _forcedDomainCount; } }

        public static bool IsModeBlocked(GameModes mode)
        {
            if (!Active || AllowedMode == GameModes.Random || mode == AllowedMode)
                return false;

            // A mode the progression chain has EXPLICITLY unlocked outranks the funnel —
            // the funnel exists to focus onboarding, never to lock the quest's own reward
            // (a claimed mode must be playable even if a stale funnel survived).
            var svc = GameModeProgressionService.Instance;
            if (svc != null && svc.IsGameModeUnlocked(mode))
                return false;

            return true;
        }

        /// <summary>True when the funnel's intensity/player/domain overrides apply to this game —
        /// they shape the TUTORIAL mode only, never a newly unlocked one.</summary>
        public static bool AppliesTo(GameModes mode) =>
            Active && (AllowedMode == GameModes.Random || mode == AllowedMode);

        public static bool IsIntensityBlocked(GameModes mode, int intensity) =>
            AppliesTo(mode) && ForcedIntensity > 0 && intensity != ForcedIntensity;

        public static void Apply(GameModes allowedMode, int forcedIntensity, int forcedPlayerCount, int forcedDomainCount)
        {
            EnsureLoaded();
            _active = true;
            _allowedMode = allowedMode;
            _forcedIntensity = forcedIntensity;
            _forcedPlayerCount = forcedPlayerCount;
            _forcedDomainCount = forcedDomainCount;
            Persist();
            OnChanged?.Invoke();
        }

        public static void Clear()
        {
            _loaded = true; // a clear defines the state; nothing to load over it
            _active = false;
            _allowedMode = GameModes.Random;
            _forcedIntensity = 0;
            _forcedPlayerCount = 0;
            _forcedDomainCount = 0;
            Persist();
            OnChanged?.Invoke();
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _active = PlayerPrefs.GetInt(PrefActive, 0) == 1;
            if (!_active) return;

            _allowedMode = (GameModes)PlayerPrefs.GetInt(PrefMode, (int)GameModes.Random);
            _forcedIntensity = PlayerPrefs.GetInt(PrefIntensity, 0);
            _forcedPlayerCount = PlayerPrefs.GetInt(PrefPlayers, 0);
            _forcedDomainCount = PlayerPrefs.GetInt(PrefDomains, 0);
        }

        static void Persist()
        {
            PlayerPrefs.SetInt(PrefActive, _active ? 1 : 0);
            PlayerPrefs.SetInt(PrefMode, (int)_allowedMode);
            PlayerPrefs.SetInt(PrefIntensity, _forcedIntensity);
            PlayerPrefs.SetInt(PrefPlayers, _forcedPlayerCount);
            PlayerPrefs.SetInt(PrefDomains, _forcedDomainCount);
            PlayerPrefs.Save();
        }
    }
}
