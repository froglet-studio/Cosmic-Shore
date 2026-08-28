using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The episode-token wallet: the single writer for token balance and episode ownership.
    ///
    /// Design, in one line: <b>money buys tokens, tokens buy episodes, and only a verified order
    /// can create a token.</b>
    ///
    /// Two rules make this safe to point at real money:
    ///
    /// 1. <b>Grants require a verified order.</b> <see cref="GrantTokens"/> takes an
    ///    <see cref="OrderReceipt"/> that a purchase provider produces only after the storefront
    ///    confirms payment. There is deliberately no "add tokens" call a UI button can reach.
    /// 2. <b>Grants are idempotent.</b> Every receipt carries an order id, recorded in
    ///    <c>ProfileEconomy.RedeemedOrderIds</c>. Replaying the same receipt - on retry, on restart,
    ///    or because the player opened the same confirmation twice - grants nothing the second time.
    ///
    /// Spending is local and immediate because it costs the player nothing to get wrong in their
    /// favour: the token was already paid for, and ownership is permanent once written.
    /// </summary>
    public static class EpisodeTokenService
    {
        /// <summary>Balance changed. Payload is the new token balance.</summary>
        public static event Action<int> OnTokenBalanceChanged;

        /// <summary>An episode became owned. Payload is the episode id.</summary>
        public static event Action<string> OnEpisodeUnlocked;

        /// <summary>A grant was applied. Payload is (tokens granted, new balance).</summary>
        public static event Action<int, int> OnTokensGranted;

        /// <summary>
        /// Proof that a storefront took payment. Produced by an
        /// <see cref="IEpisodeTokenPurchaseProvider"/> after verification, never by UI.
        /// </summary>
        public readonly struct OrderReceipt
        {
            /// <summary>Storefront order/transaction id. The idempotency key - must be unique and stable.</summary>
            public readonly string OrderId;

            /// <summary>SKU purchased, matched against <see cref="SO_EpisodeTokenConfig.bundles"/>.</summary>
            public readonly string ProductId;

            /// <summary>Tokens this order grants, as told by the verified order, not by the client.</summary>
            public readonly int TokenCount;

            /// <summary>Which provider verified it, for analytics and support ("steam", "stripe", ...).</summary>
            public readonly string Provider;

            public OrderReceipt(string orderId, string productId, int tokenCount, string provider)
            {
                OrderId = orderId;
                ProductId = productId;
                TokenCount = tokenCount;
                Provider = provider;
            }

            public bool IsValid =>
                !string.IsNullOrWhiteSpace(OrderId) && TokenCount > 0;
        }

        // ──────────────────────────────────────────────
        //  Reads
        // ──────────────────────────────────────────────

        static ProfileEconomy Economy => PlayerDataService.Instance?.CurrentProfile?.Economy;

        /// <summary>Unspent tokens. 0 when the profile has not loaded yet.</summary>
        public static int TokenBalance => Economy?.EpisodeTokenBalance ?? 0;

        /// <summary>True when the player already owns this episode.</summary>
        public static bool OwnsEpisode(string episodeId)
        {
            var economy = Economy;
            if (economy == null || string.IsNullOrWhiteSpace(episodeId)) return false;
            return economy.OwnedEpisodeIds != null && economy.OwnedEpisodeIds.Contains(episodeId);
        }

        /// <summary>Episodes the player owns. Never null.</summary>
        public static IReadOnlyList<string> OwnedEpisodes =>
            Economy?.OwnedEpisodeIds ?? new List<string>();

        /// <summary>True when the player has enough tokens to unlock one more episode.</summary>
        public static bool CanUnlockEpisode(SO_EpisodeTokenConfig config) =>
            TokenBalance >= TokensPerEpisode(config);

        static int TokensPerEpisode(SO_EpisodeTokenConfig config) =>
            Mathf.Max(1, config != null ? config.tokensPerEpisode : 1);

        // ──────────────────────────────────────────────
        //  Grant (verified purchases only)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Credits tokens from a verified order. Idempotent: a repeated order id is ignored and
        /// reported as success, because the player already has what they paid for.
        /// </summary>
        /// <returns>True when the receipt was accepted (granted now, or already granted before).</returns>
        public static bool GrantTokens(OrderReceipt receipt)
        {
            if (!receipt.IsValid)
            {
                CSDebug.LogError("[EpisodeToken] Rejected an invalid receipt (missing order id or zero tokens).");
                return false;
            }

            var economy = Economy;
            if (economy == null)
            {
                // Losing a paid grant is the worst outcome here, so refuse loudly rather than
                // silently dropping it. The caller is expected to retry once the profile loads.
                CSDebug.LogError($"[EpisodeToken] Cannot grant order '{receipt.OrderId}': profile not loaded. " +
                                 "The caller must retry after PlayerDataService is initialised.");
                return false;
            }

            economy.RedeemedOrderIds ??= new List<string>();

            if (economy.RedeemedOrderIds.Contains(receipt.OrderId))
            {
                CSDebug.Log($"[EpisodeToken] Order '{receipt.OrderId}' already redeemed - ignoring replay.");
                return true;
            }

            economy.RedeemedOrderIds.Add(receipt.OrderId);
            economy.EpisodeTokenBalance += receipt.TokenCount;
            economy.LifetimeEpisodeTokensPurchased += receipt.TokenCount;

            PersistNow();

            CSDebug.Log($"[EpisodeToken] Granted {receipt.TokenCount} token(s) from {receipt.Provider} " +
                        $"order '{receipt.OrderId}'. Balance: {economy.EpisodeTokenBalance}");

            OnTokensGranted?.Invoke(receipt.TokenCount, economy.EpisodeTokenBalance);
            OnTokenBalanceChanged?.Invoke(economy.EpisodeTokenBalance);
            return true;
        }

        // ──────────────────────────────────────────────
        //  Spend
        // ──────────────────────────────────────────────

        /// <summary>
        /// Spends tokens to unlock an episode permanently. Returns false when the player cannot
        /// afford it or already owns it (owning it is not an error - it just spends nothing).
        /// </summary>
        public static bool TryUnlockEpisode(string episodeId, SO_EpisodeTokenConfig config)
        {
            if (string.IsNullOrWhiteSpace(episodeId))
            {
                CSDebug.LogWarning("[EpisodeToken] TryUnlockEpisode called with no episode id.");
                return false;
            }

            var economy = Economy;
            if (economy == null) return false;

            economy.OwnedEpisodeIds ??= new List<string>();

            if (economy.OwnedEpisodeIds.Contains(episodeId))
            {
                CSDebug.Log($"[EpisodeToken] '{episodeId}' is already owned - nothing spent.");
                return false;
            }

            int cost = TokensPerEpisode(config);
            if (economy.EpisodeTokenBalance < cost)
            {
                CSDebug.Log($"[EpisodeToken] Cannot unlock '{episodeId}': need {cost}, have {economy.EpisodeTokenBalance}.");
                return false;
            }

            economy.EpisodeTokenBalance -= cost;
            economy.LifetimeEpisodeTokensSpent += cost;
            economy.OwnedEpisodeIds.Add(episodeId);

            PersistNow();

            CSDebug.Log($"[EpisodeToken] Unlocked '{episodeId}' for {cost} token(s). " +
                        $"Balance: {economy.EpisodeTokenBalance}");

            OnEpisodeUnlocked?.Invoke(episodeId);
            OnTokenBalanceChanged?.Invoke(economy.EpisodeTokenBalance);
            return true;
        }

        // ──────────────────────────────────────────────
        //  Persistence
        // ──────────────────────────────────────────────

        /// <summary>
        /// Entitlements are the one thing a dropped write is unacceptable for, so this pushes to
        /// Cloud Save immediately rather than riding the debounce.
        /// </summary>
        static void PersistNow()
        {
            var service = PlayerDataService.Instance;
            if (service == null) return;

            try
            {
                service.PersistProfileNow();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[EpisodeToken] Immediate persist failed: {e.Message}. " +
                                 "The debounced save is the fallback.");
            }
        }
    }
}
