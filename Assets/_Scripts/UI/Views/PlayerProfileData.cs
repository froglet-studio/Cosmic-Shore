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
        // v2: the Progression group (player XP) was removed. Profiles saved at v1 still carry a
        // "Progression" key; it is ignored on load, which is the intended no-op migration.
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion = CurrentSchemaVersion;

        public ProfileIdentity Identity = new();
        public ProfileEconomy Economy = new();
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

        // ----- Episode tokens (real-money entitlement) -----

        /// <summary>
        /// Unspent episode tokens. One token unlocks one episode. Only ever incremented by
        /// <c>EpisodeTokenService.GrantTokens</c>, which requires a verified order - never by the
        /// client on its own.
        /// </summary>
        public int EpisodeTokenBalance;

        /// <summary>Lifetime totals, for support/refund questions and funnel analysis.</summary>
        public long LifetimeEpisodeTokensPurchased;
        public long LifetimeEpisodeTokensSpent;

        /// <summary>
        /// Episode ids the player owns. Ownership is permanent: spending a token writes here and
        /// the entitlement is never revoked by gameplay.
        /// </summary>
        public List<string> OwnedEpisodeIds = new();

        /// <summary>
        /// Order ids already redeemed, so a replayed or duplicated grant cannot mint free tokens.
        /// This is what makes <c>GrantTokens</c> idempotent across retries, app restarts, and a
        /// player opening the same receipt twice.
        /// </summary>
        public List<string> RedeemedOrderIds = new();
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
