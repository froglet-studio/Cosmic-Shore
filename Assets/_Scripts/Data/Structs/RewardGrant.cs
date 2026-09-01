using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// One thing the game is handing the player, described completely enough that
    /// <c>RewardService</c> can grant it, dedupe it, report it to analytics and hand it to the
    /// UI without the producer knowing about any of those.
    ///
    /// Producers describe WHAT was earned; they never touch the wallet. That split is the whole
    /// point - before it, the only earn path in the game wrote the wallet directly from a UI
    /// component, and every other designed reward path was left unwired.
    ///
    /// Plain serializable struct with public fields, matching the other SOAP payloads
    /// (<c>CrystalStats</c>, <c>PrismStats</c>, <c>GameToastData</c>): SOAP's
    /// <c>ScriptableEvent&lt;T&gt;</c> serializes a <c>_debugValue</c> of this type for the
    /// inspector's debug-raise, and Unity does not serialize readonly fields. Construct through
    /// the factories below rather than by hand - they are what make an invalid grant hard to
    /// build.
    /// </summary>
    [Serializable]
    public struct RewardGrant
    {
        /// <summary>Which payout channel this uses. Decides what <see cref="Amount"/> and
        /// <see cref="EntitlementId"/> mean.</summary>
        public RewardKind Kind;

        /// <summary>Crystal count for <see cref="RewardKind.Crystals"/>. Ignored otherwise.</summary>
        public int Amount;

        /// <summary>Unlock id for <see cref="RewardKind.Entitlement"/>. Ignored otherwise.</summary>
        public string EntitlementId;

        /// <summary>
        /// Analytics/telemetry label for WHY this was earned ("game_placement",
        /// "tournament_placement", ...). Flows straight through to the crystal analytics event,
        /// so it is the field that makes the economy funnel readable.
        /// </summary>
        public string Source;

        /// <summary>How hard this refuses to be granted twice.</summary>
        public RewardDedupe Dedupe;

        /// <summary>
        /// Stable identity for <see cref="RewardDedupe.Account"/>. Required when the grant is
        /// deduped and ignored when it is not.
        /// </summary>
        public string DedupeKey;

        RewardGrant(RewardKind kind, int amount, string entitlementId, string source,
                    RewardDedupe dedupe, string dedupeKey)
        {
            Kind = kind;
            Amount = amount;
            EntitlementId = entitlementId;
            Source = source;
            Dedupe = dedupe;
            DedupeKey = dedupeKey;
        }

        /// <summary>A repeatable crystal payout - a match placement, for instance.</summary>
        public static RewardGrant Crystals(int amount, string source)
            => new(RewardKind.Crystals, amount, null, source, RewardDedupe.None, null);

        /// <summary>
        /// A crystal payout that may only ever land once for this account (a first win, a
        /// one-off milestone). <paramref name="dedupeKey"/> is what makes that true across
        /// restarts and devices, so it must be stable - never a timestamp or a GUID.
        /// </summary>
        public static RewardGrant CrystalsOnce(int amount, string source, string dedupeKey)
            => new(RewardKind.Crystals, amount, null, source, RewardDedupe.Account, dedupeKey);

        /// <summary>
        /// A permanent unlock. Entitlements are inherently once-ever, so the id doubles as the
        /// dedupe key - there is no way to author one that grants twice.
        /// </summary>
        public static RewardGrant Entitlement(string entitlementId, string source)
            => new(RewardKind.Entitlement, 0, entitlementId, source, RewardDedupe.Account, entitlementId);

        /// <summary>
        /// True when this describes something worth granting at all. A zero-crystal payout is a
        /// legitimate outcome (last place earns nothing), not an error - it simply never reaches
        /// the wallet or the UI.
        /// </summary>
        public bool IsPayable => Kind switch
        {
            RewardKind.Crystals    => Amount > 0,
            RewardKind.Entitlement => !string.IsNullOrEmpty(EntitlementId),
            _                      => false,
        };
    }
}
