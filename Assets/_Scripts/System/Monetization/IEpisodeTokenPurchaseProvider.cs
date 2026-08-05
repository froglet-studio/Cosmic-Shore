using System;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// A storefront that can sell episode tokens. The token wallet
    /// (<see cref="EpisodeTokenService"/>) only accepts grants that come back through one of these,
    /// so swapping storefronts never touches wallet or entitlement code.
    ///
    /// The contract has one hard rule: <b>a provider must not produce a receipt until the
    /// storefront has confirmed payment.</b> Everything downstream trusts the receipt.
    ///
    /// Planned implementations:
    /// <list type="bullet">
    /// <item><b>Steam</b> - the ISteamMicroTxn flow. Required for a Steam build: Valve's rule is
    /// that in-game purchases go through the Steam Wallet, so an external web checkout is not a
    /// compliant option there. Needs the Steamworks SDK plus a small backend to call
    /// InitTxn/FinalizeTxn and verify the order.</item>
    /// <item><b>Web checkout</b> - the existing <c>IAPManager</c> flow, valid on non-Steam
    /// distribution only, and only once a backend verifies the order server-side.</item>
    /// </list>
    /// </summary>
    public interface IEpisodeTokenPurchaseProvider
    {
        /// <summary>Short id recorded on receipts for support and analytics, e.g. "steam".</summary>
        string ProviderId { get; }

        /// <summary>
        /// True when this provider can transact right now (SDK initialised, user signed in,
        /// storefront reachable). UI should hide or disable buy controls when false.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Starts a purchase. <paramref name="onComplete"/> receives a verified receipt on success,
        /// or null when the purchase was cancelled or could not be verified. Implementations must
        /// invoke the callback exactly once, on the main thread.
        /// </summary>
        void PurchaseAsync(SO_EpisodeTokenConfig.TokenBundle bundle,
                           Action<EpisodeTokenService.OrderReceipt?> onComplete);
    }

    /// <summary>
    /// Editor-only stand-in so the token spend flow and its UI can be built and tested before any
    /// storefront exists. It fabricates receipts, so it is fenced three ways: development builds
    /// only, an explicit opt-in flag on the config asset, and a refusal to run in a release player.
    ///
    /// This must never be the provider a shipping build resolves.
    /// </summary>
    public sealed class EditorFakeTokenPurchaseProvider : IEpisodeTokenPurchaseProvider
    {
        readonly SO_EpisodeTokenConfig _config;

        public EditorFakeTokenPurchaseProvider(SO_EpisodeTokenConfig config) => _config = config;

        public string ProviderId => "editor_fake";

        public bool IsAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return _config != null && _config.allowUnverifiedGrantsInEditor;
#else
                return false;
#endif
            }
        }

        public void PurchaseAsync(SO_EpisodeTokenConfig.TokenBundle bundle,
                                  Action<EpisodeTokenService.OrderReceipt?> onComplete)
        {
            if (!IsAvailable)
            {
                // Belt and braces: even if something hands this provider to a release build, it
                // cannot mint an entitlement.
                CSDebug.LogError("[EpisodeToken] Fake purchase provider refused: not a development " +
                                 "build, or allowUnverifiedGrantsInEditor is off.");
                onComplete?.Invoke(null);
                return;
            }

            if (bundle == null || bundle.tokenCount <= 0)
            {
                onComplete?.Invoke(null);
                return;
            }

            // Unique per call so repeated test purchases are not swallowed by idempotency.
            string orderId = $"editor-{bundle.productId}-{Guid.NewGuid():N}";
            CSDebug.LogWarning($"[EpisodeToken] FAKE purchase granted ({bundle.tokenCount} token(s)). " +
                               "Editor/development only - no money changed hands.");

            onComplete?.Invoke(new EpisodeTokenService.OrderReceipt(
                orderId, bundle.productId, bundle.tokenCount, ProviderId));
        }
    }
}
