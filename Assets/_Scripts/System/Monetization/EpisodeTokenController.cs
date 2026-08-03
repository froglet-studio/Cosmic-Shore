using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The MonoBehaviour the episode-store UI binds to. Owns the config and the active purchase
    /// provider, and forwards to <see cref="EpisodeTokenService"/> for anything that touches the
    /// wallet.
    ///
    /// UI authors: this is the whole surface. Bind buttons to <see cref="BuyBundle"/> and
    /// <see cref="UnlockEpisode"/>, read <see cref="TokenBalance"/> and
    /// <see cref="CanUnlock"/> for state, and subscribe to the events for refreshes. There is
    /// deliberately no way from here to add tokens without a purchase.
    ///
    /// Drop it on a persistent menu object and assign the config asset.
    /// </summary>
    public class EpisodeTokenController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField, Tooltip("EpisodeTokenConfig asset: bundles, prices, and tokens-per-episode.")]
        SO_EpisodeTokenConfig config;

        /// <summary>Raised after any balance change, with the new balance. UI should refresh.</summary>
        public event Action<int> OnBalanceChanged;

        /// <summary>Raised when an episode becomes owned, with the episode id.</summary>
        public event Action<string> OnEpisodeUnlocked;

        /// <summary>
        /// Raised when a purchase attempt finishes. (success, message) - the message is
        /// player-facing and safe to show directly.
        /// </summary>
        public event Action<bool, string> OnPurchaseFinished;

        IEpisodeTokenPurchaseProvider _provider;
        bool _purchaseInFlight;

        public SO_EpisodeTokenConfig Config => config;

        /// <summary>Unspent tokens.</summary>
        public int TokenBalance => EpisodeTokenService.TokenBalance;

        /// <summary>Bundles to display in the store. Never null.</summary>
        public IReadOnlyList<SO_EpisodeTokenConfig.TokenBundle> Bundles =>
            config != null && config.bundles != null
                ? config.bundles
                : Array.Empty<SO_EpisodeTokenConfig.TokenBundle>();

        /// <summary>True when a storefront is wired up and able to transact.</summary>
        public bool CanPurchase => _provider != null && _provider.IsAvailable && !_purchaseInFlight;

        /// <summary>True when the player owns enough tokens for one more episode.</summary>
        public bool CanUnlock => EpisodeTokenService.CanUnlockEpisode(config);

        void Awake() => _provider = ResolveProvider();

        void OnEnable()
        {
            EpisodeTokenService.OnTokenBalanceChanged += HandleBalanceChanged;
            EpisodeTokenService.OnEpisodeUnlocked += HandleEpisodeUnlocked;
        }

        void OnDisable()
        {
            EpisodeTokenService.OnTokenBalanceChanged -= HandleBalanceChanged;
            EpisodeTokenService.OnEpisodeUnlocked -= HandleEpisodeUnlocked;
        }

        void HandleBalanceChanged(int balance) => OnBalanceChanged?.Invoke(balance);
        void HandleEpisodeUnlocked(string id) => OnEpisodeUnlocked?.Invoke(id);

        /// <summary>
        /// Picks the storefront for this build. Today only the editor stand-in exists; a Steam
        /// provider slots in here once the Steamworks SDK and its verification backend land, with
        /// no change to wallet, UI, or entitlement code.
        /// </summary>
        IEpisodeTokenPurchaseProvider ResolveProvider()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (config != null && config.allowUnverifiedGrantsInEditor)
                return new EditorFakeTokenPurchaseProvider(config);
#endif
            // No real storefront is wired yet. Returning null makes CanPurchase false, so buy
            // controls stay disabled rather than appearing to work and taking nothing.
            return null;
        }

        // ──────────────────────────────────────────────
        //  Buying tokens
        // ──────────────────────────────────────────────

        /// <summary>Starts a purchase for a bundle SKU. Safe to call from a Button onClick.</summary>
        public void BuyBundle(string productId)
        {
            if (config == null)
            {
                Finish(false, "Store is unavailable.");
                return;
            }

            var bundle = config.FindBundle(productId);
            if (bundle == null)
            {
                CSDebug.LogError($"[EpisodeToken] Unknown bundle '{productId}'.");
                Finish(false, "That item is unavailable.");
                return;
            }

            if (_purchaseInFlight)
            {
                CSDebug.Log("[EpisodeToken] Purchase already in flight - ignoring.");
                return;
            }

            if (_provider == null || !_provider.IsAvailable)
            {
                // The honest message. Do not pretend a purchase happened.
                Finish(false, "Purchases are not available in this build yet.");
                return;
            }

            _purchaseInFlight = true;
            CSDebug.Log($"[EpisodeToken] Purchase started: {bundle.productId} " +
                        $"({config.FormatTokens(bundle.tokenCount)}, {config.FormatPrice(bundle.displayPriceUsd)})");

            _provider.PurchaseAsync(bundle, receipt =>
            {
                _purchaseInFlight = false;

                if (receipt == null)
                {
                    Finish(false, "Purchase cancelled.");
                    return;
                }

                bool granted = EpisodeTokenService.GrantTokens(receipt.Value);
                Finish(granted,
                    granted
                        ? $"{config.FormatTokens(receipt.Value.TokenCount)} added."
                        // Payment succeeded but the grant did not land - never silently swallow this.
                        : "Payment went through but we could not add your tokens. Contact support with your order id.");
            });
        }

        // ──────────────────────────────────────────────
        //  Spending tokens
        // ──────────────────────────────────────────────

        /// <summary>Spends tokens to unlock an episode permanently.</summary>
        public bool UnlockEpisode(SO_EpisodeData episode)
        {
            if (episode == null || string.IsNullOrWhiteSpace(episode.episodeId))
            {
                CSDebug.LogWarning("[EpisodeToken] UnlockEpisode called with no episode.");
                return false;
            }
            return EpisodeTokenService.TryUnlockEpisode(episode.episodeId, config);
        }

        /// <summary>True when the player already owns this episode.</summary>
        public bool OwnsEpisode(SO_EpisodeData episode) =>
            episode != null && EpisodeTokenService.OwnsEpisode(episode.episodeId);

        /// <summary>Tokens needed to unlock one episode, for UI labelling.</summary>
        public int TokensPerEpisode => config != null ? Mathf.Max(1, config.tokensPerEpisode) : 1;

        void Finish(bool success, string message)
        {
            if (!success) CSDebug.Log($"[EpisodeToken] {message}");
            OnPurchaseFinished?.Invoke(success, message);
        }
    }
}
