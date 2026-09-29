using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Designer-tunable configuration for episode tokens.
    ///
    /// A token is the entitlement unit: one token unlocks one episode. Tokens are bought with real
    /// money and are the ONLY thing real money buys - episodes themselves are never priced in
    /// dollars in the client, which keeps a single place to reason about pricing and refunds.
    ///
    /// Every price here is a DISPLAY price only. The authoritative price is whatever the storefront
    /// charges, and the authoritative grant is whatever the verified order says. Nothing in this
    /// asset can mint a token.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EpisodeTokenConfig",
        menuName = "ScriptableObjects/Monetization/EpisodeTokenConfig")]
    public class SO_EpisodeTokenConfig : ScriptableObject
    {
        /// <summary>One purchasable bundle of tokens.</summary>
        [Serializable]
        public class TokenBundle
        {
            [Tooltip("Stable SKU id. Must match the product id on the storefront (Steam item id, " +
                     "DLC app id, or payment-provider product id). Never change it after release - " +
                     "it is how a verified order maps back to a grant.")]
            public string productId = "episode_token_1";

            [Tooltip("Player-facing name, e.g. 'Episode Token' or '3 Episode Tokens'.")]
            public string displayName = "Episode Token";

            [Tooltip("How many tokens this bundle grants.")]
            [Min(1)] public int tokenCount = 1;

            [Tooltip("Display price in USD. The storefront is authoritative; this is for UI only.")]
            [Min(0f)] public float displayPriceUsd = 2f;

            [Tooltip("Optional 'best value' flag for the UI to badge.")]
            public bool highlight;
        }

        [Header("Tokens")]
        [Tooltip("Tokens required to unlock one episode. 1 = the shipped design (1 episode = 1 token).")]
        [Min(1)] public int tokensPerEpisode = 1;

        [Tooltip("Purchasable bundles, in display order.")]
        public List<TokenBundle> bundles = new()
        {
            new TokenBundle { productId = "episode_token_1", displayName = "Episode Token",   tokenCount = 1, displayPriceUsd = 2f },
        };

        [Header("Display")]
        [Tooltip("Currency symbol for display prices.")]
        public string currencySymbol = "$";

        [Tooltip("Word for a single token, used in UI strings.")]
        public string tokenNoun = "Episode Token";

        [Tooltip("Plural form of the token noun.")]
        public string tokenNounPlural = "Episode Tokens";

        [Header("Safety")]
        [Tooltip("MUST stay false for any build that takes real money. When true, purchases are " +
                 "granted locally with no order verification - useful only for editor testing of " +
                 "the spend flow. EpisodeTokenService refuses to honour this in a non-development " +
                 "build, so it cannot be shipped on by accident.")]
        public bool allowUnverifiedGrantsInEditor;

        /// <summary>Formats a display price, e.g. "$2.00".</summary>
        public string FormatPrice(float usd) =>
            $"{currencySymbol}{usd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";

        /// <summary>Correct noun for a count, e.g. "1 Episode Token" / "3 Episode Tokens".</summary>
        public string FormatTokens(int count) =>
            $"{count} {(count == 1 ? tokenNoun : tokenNounPlural)}";

        /// <summary>Looks a bundle up by SKU. Returns null when unknown.</summary>
        public TokenBundle FindBundle(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId) || bundles == null) return null;
            foreach (var b in bundles)
                if (b != null && string.Equals(b.productId, productId, StringComparison.Ordinal))
                    return b;
            return null;
        }
    }
}
