using System;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Drives real-money "support" purchases through a hosted web checkout page.
    ///
    /// The game ships on Steam/PC with no in-app store SDK, so purchases are taken on an
    /// external payment page (Stripe / Froglet page) opened in the system browser via
    /// <see cref="Application.OpenURL"/>. URLs and prices are config-driven through
    /// <see cref="SO_IAPConfig"/> and per-episode fields on <see cref="SO_EpisodeData"/>,
    /// so the payment endpoint can change without a code build.
    ///
    /// Flow:
    ///   1. UI calls <see cref="InitiateEpisodePurchase"/> (or <see cref="InitiateSupportPurchase"/>).
    ///   2. We open the checkout URL in the browser and remember the pending product.
    ///   3. When the app regains focus we raise <see cref="OnReturnedFromCheckout"/> so the UI
    ///      can prompt the player / a verification step can confirm.
    ///   4. Entitlement is granted only when something calls <see cref="ConfirmPendingPurchase"/>.
    ///
    /// NOTE: external-browser checkout has no payment receipt inside the client. Granting an
    /// entitlement on return is "trust the client" until a backend order-verification step is
    /// wired into <see cref="ConfirmPendingPurchase"/>. That seam is intentionally left as the
    /// single grant point so it can be made server-authoritative later.
    ///
    /// <para><b>DE-SCOPED for the invite build.</b> <c>OpenCheckout</c> declines while
    /// <c>SO_CommerceAvailability.AllowRealMoneyCheckout</c> is off, which is its default — so this
    /// class stays wired, keeps formatting prices, and cannot open a payment page
    /// (<c>Docs/STEAM_RELEASE_TASKS.md</c> R4). The verification gap above is the reason it must
    /// stay off until a backend exists, not merely a scheduling one.</para>
    /// </summary>
    public class IAPManager : MonoBehaviour
    {
        public static IAPManager Instance { get; private set; }

        [Header("Web Checkout Configuration")]
        [Tooltip("URLs / currency / display settings for the hosted web checkout.")]
        [SerializeField] private SO_IAPConfig config;

        [Tooltip("Fallback product id for the generic 'Support Us' button when no episode is supplied.")]
        [SerializeField] private string supportProductId = "com.cosmicshore.support_tier1";

        /// <summary>True once a checkout config is available (no SDK to initialize for web checkout).</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>Product id of an opened-but-not-yet-confirmed checkout, or null.</summary>
        public string PendingProductId { get; private set; }

        /// <summary>Raised after a successful (true) / failed (false) entitlement confirmation. Kept for compatibility.</summary>
        public event Action<bool> OnPurchaseComplete;

        // Read only for the offline gate on checkout.
        [Reflex.Attributes.Inject] CosmicShore.Utility.GameDataSO _gameData;

        /// <summary>Raised when a checkout page is opened in the browser. Arg: product id.</summary>
        public event Action<string> OnCheckoutOpened;

        /// <summary>Raised when the app regains focus while a checkout is pending. Arg: pending product id.</summary>
        public event Action<string> OnReturnedFromCheckout;

        SO_IAPConfig Config
        {
            get
            {
                if (config != null) return config;
                if (_runtimeDefaultConfig == null)
                    _runtimeDefaultConfig = ScriptableObject.CreateInstance<SO_IAPConfig>();
                return _runtimeDefaultConfig;
            }
        }
        SO_IAPConfig _runtimeDefaultConfig;
        bool _reportedDeScopedCheckout;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Web checkout has no SDK handshake - it is ready as soon as a config (real or
            // default) is resolvable. We are "initialized" whenever we have a base URL to open.
            IsInitialized = !string.IsNullOrWhiteSpace(Config.checkoutBaseUrl) ||
                            !string.IsNullOrWhiteSpace(Config.supportUrl);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Formats a USD price for UI display using the configured currency symbol (e.g. "$4.99").</summary>
        public string FormatPrice(float priceUsd) => Config.FormatPrice(priceUsd);

        /// <summary>
        /// Opens the web checkout for a specific episode at its configured price.
        /// </summary>
        public void InitiateEpisodePurchase(SO_EpisodeData episode)
        {
            if (episode == null)
            {
                CSDebug.LogWarning("[IAPManager] InitiateEpisodePurchase called with null episode.");
                return;
            }

            string productId = string.IsNullOrEmpty(episode.episodeId) ? supportProductId : episode.episodeId;
            string url = Config.BuildCheckoutUrl(productId, episode.priceUsd, episode.checkoutUrl);
            OpenCheckout(productId, url);
        }

        /// <summary>
        /// Opens the generic "Support Us" checkout page.
        /// </summary>
        public void InitiateSupportPurchase()
        {
            string url = string.IsNullOrWhiteSpace(Config.supportUrl)
                ? Config.BuildCheckoutUrl(supportProductId, 0f, null)
                : Config.supportUrl;
            OpenCheckout(supportProductId, url);
        }

        void OpenCheckout(string productId, string url)
        {
            // DE-SCOPED: nothing in the invite build sells anything, and external-browser checkout
            // still has no way to verify an order (see the class comment and
            // Docs/MENU_PROGRESSION_AND_IAP.md section 5). This is the choke point BOTH entry points
            // share, so it is where the posture is enforced rather than on either screen - an
            // un-gated or re-enabled screen still cannot open a payment page. The UI refuses with a
            // reason of its own; a service has no graphics, so here it declines and says why once.
            if (!SO_CommerceAvailability.Instance.AllowRealMoneyCheckout)
            {
                // Once per session, not per press: unlike the offline gate below this is a build
                // POSTURE rather than a transient condition, so it would otherwise report the same
                // unchanging fact on every press for the life of the build. It is still worth saying
                // once, because reaching here at all means a UI gate was bypassed or lost.
                if (!_reportedDeScopedCheckout)
                {
                    _reportedDeScopedCheckout = true;
                    CSDebug.LogWarning($"[IAPManager] Real-money checkout is de-scoped for this build - " +
                                       $"declining '{productId}'. Flip allowRealMoneyCheckout on " +
                                       "Resources/CommerceAvailability to re-open it.");
                }

                OnPurchaseComplete?.Invoke(false);
                return;
            }

            // OFFLINE session: checkout is a hosted web page. Opening a browser at a URL that
            // cannot load - and then arming a pending purchase waiting on a confirmation that
            // can never arrive - is worse than declining. The store UI should be gated
            // (OfflineUIGate); this is the choke point both purchase entry points share, so
            // an un-wired screen still cannot start a doomed checkout.
            if (_gameData != null && _gameData.IsOfflineSession)
            {
                CSDebug.LogWarning("[IAPManager] Offline session - purchases are unavailable.");
                OnPurchaseComplete?.Invoke(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                CSDebug.LogWarning($"[IAPManager] No checkout URL configured for '{productId}'. " +
                                   "Wire an SO_IAPConfig (checkoutBaseUrl / supportUrl) on the IAPManager.");
                OnPurchaseComplete?.Invoke(false);
                return;
            }

            PendingProductId = productId;
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[IAPManager] Opening web checkout for '{productId}': {url}");
            Application.OpenURL(url);
            OnCheckoutOpened?.Invoke(productId);
        }

        /// <summary>
        /// Called by a verification step / UI once the player returns and the purchase is
        /// confirmed (or rejected). This is the single entitlement-grant seam - make it
        /// server-authoritative by verifying the order before invoking with success=true.
        /// </summary>
        public void ConfirmPendingPurchase(bool success)
        {
            string productId = PendingProductId;
            PendingProductId = null;

            if (success)
                CSDebug.LogVerbose(CSLogChannel.CloudData, $"[IAPManager] Purchase confirmed for '{productId}'.");
            else
                CSDebug.LogWarning($"[IAPManager] Purchase NOT confirmed for '{productId}'.");

            OnPurchaseComplete?.Invoke(success);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            // Returning to the app while a checkout is open is the moment to confirm/verify.
            if (hasFocus && !string.IsNullOrEmpty(PendingProductId))
                OnReturnedFromCheckout?.Invoke(PendingProductId);
        }
    }
}
