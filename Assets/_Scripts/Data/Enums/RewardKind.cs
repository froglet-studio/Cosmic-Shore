namespace CosmicShore.Data
{
    /// <summary>
    /// What a reward actually hands the player. This is the axis the reward system dispatches
    /// on, so adding a member is how the system grows past crystals - never a parallel grant
    /// path beside <c>RewardService</c>.
    /// </summary>
    public enum RewardKind
    {
        /// <summary>Soft currency, added to <c>ProfileEconomy.CrystalBalance</c>.</summary>
        Crystals = 0,

        /// <summary>
        /// A permanent, non-consumable unlock recorded by id in
        /// <c>ProfileEconomy.UnlockedRewardIds</c>. This is the door skins and toys come through
        /// when they land - they are entitlements, and the profile already persists the list.
        /// </summary>
        Entitlement = 1,
    }

    /// <summary>
    /// How hard a grant refuses to happen twice. A reward that is only ever meant to land once
    /// carries its own key; the alternative is every producer inventing its own latch, and a
    /// latch a producer forgets is a duplicate payout the player keeps.
    /// </summary>
    public enum RewardDedupe
    {
        /// <summary>Grant every time it is asked for. Correct for a repeatable payout.</summary>
        None = 0,

        /// <summary>
        /// Grant at most once for this account, ever. Deduped against the same persisted
        /// <c>UnlockedRewardIds</c> list entitlements use, so this needs no new cloud schema.
        /// </summary>
        Account = 1,
    }
}
