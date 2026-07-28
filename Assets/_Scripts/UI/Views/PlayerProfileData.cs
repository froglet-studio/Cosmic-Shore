using System;
using System.Collections.Generic;

namespace CosmicShore.UI
{
    /// <summary>
    /// Account-level identity, economy, progression and lifecycle facts.
    /// Cloud key: <c>PLAYER_PROFILE</c>.
    ///
    /// Fields are grouped rather than flat, and the grouping is load-bearing - see
    /// Docs/Analytics/DATA_ARCHITECTURE.md §2.1. Each group maps 1:1 to a privacy class
    /// (so a GDPR export or erasure operates on a group, not a field hunt) and 1:1 to a
    /// PostHog person-property prefix (so the mirror is mechanical, not hand-maintained).
    ///
    /// Sole writer: <see cref="PlayerDataService"/>.
    /// </summary>
    [Serializable]
    public class PlayerProfileData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;

        public ProfileIdentity Identity = new();
        public ProfileEconomy Economy = new();
        public ProfileProgression Progression = new();
        public ProfileLifecycle Lifecycle = new();
    }

    /// <summary>Who the account is. Privacy class P2 (personal data) - this whole group is PII.</summary>
    [Serializable]
    public class ProfileIdentity
    {
        /// <summary>
        /// UGS authentication PlayerId. The canonical identity everywhere: Cloud Save,
        /// Leaderboards, UGS Analytics, and the PostHog distinct_id.
        /// </summary>
        public string UserId = "";

        /// <summary>
        /// Player-chosen free text. Searchable in PostHog, but never the identity key -
        /// it is mutable and not unique.
        /// </summary>
        public string DisplayName = "";

        public int AvatarId;
    }

    /// <summary>Balances and entitlements. Loss here is player-visible, so writes must not be dropped.</summary>
    [Serializable]
    public class ProfileEconomy
    {
        public int CrystalBalance;

        /// <summary>Lifetime totals, so hoarding-vs-spending needs no event-store roll-up.</summary>
        public long LifetimeCrystalsEarned;
        public long LifetimeCrystalsSpent;

        public List<string> UnlockedRewardIds = new();
    }

    /// <summary>
    /// Earned, monotonic. Level is intentionally NOT stored - it is derived from
    /// <see cref="Xp"/>, so retuning the curve cannot leave a stale level behind.
    /// </summary>
    [Serializable]
    public class ProfileProgression
    {
        public int Xp;
    }

    /// <summary>Account timeline and last-known client facts. Retention/segmentation denominators.</summary>
    [Serializable]
    public class ProfileLifecycle
    {
        /// <summary>Stamped once, when the profile is first created. Install-relative cohorting.</summary>
        public long FirstSeenUtcMs;

        /// <summary>
        /// Refreshed each session. Was <c>PLAYER_STATS_PROFILE.LastLoginTick</c> (.NET ticks),
        /// which was never a per-game-mode stat.
        /// </summary>
        public long LastSeenUtcMs;

        public int SessionCount;
        public int GamesCompleted;

        /// <summary>
        /// Lifetime sum of the per-game flight time reported on <c>game_completed</c>.
        /// The two must reconcile; a drift between them is a real instrumentation alarm.
        /// </summary>
        public float TotalFlightTimeSeconds;

        public string LastAppVersion = "";
        public string LastPlatform = "";
    }
}
