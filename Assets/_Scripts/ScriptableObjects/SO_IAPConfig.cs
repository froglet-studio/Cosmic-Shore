using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Designer-tunable configuration for the web-checkout purchase flow.
    ///
    /// The project ships on Steam/PC where real-money support purchases are taken through a
    /// hosted web checkout page (Stripe / Froglet payment page) opened in the system browser
    /// via <c>Application.OpenURL</c> - there is no in-app store SDK. This asset holds the
    /// URLs and display settings so the payment endpoint can be changed without a code build.
    ///
    /// URL templates support these tokens, substituted at open time:
    ///   {productId} - the episode / product identifier
    ///   {price}     - the numeric price (invariant culture, two decimals)
    /// </summary>
    [CreateAssetMenu(
        fileName = "IAPConfig",
        menuName = "ScriptableObjects/Monetization/IAPConfig")]
    public class SO_IAPConfig : ScriptableObject
    {
        [Header("Checkout URLs")]
        [Tooltip("Base checkout URL used when an episode has no per-episode override URL. " +
                 "Supports {productId} and {price} tokens, e.g. " +
                 "https://www.froglet.games/checkout?product={productId}&amount={price} " +
                 "Point this at a HOSTED CHECKOUT page (Stripe Payment Link, etc.) to show a " +
                 "real payment form - a plain site URL just opens the site, not a payment screen.")]
        public string checkoutBaseUrl = "https://www.froglet.games";

        [Tooltip("Generic 'Support Us' page opened by the non-episode support button.")]
        public string supportUrl = "https://www.froglet.games";

        [Header("Display")]
        [Tooltip("Currency symbol prefixed to prices in the UI (e.g. $).")]
        public string currencySymbol = "$";

        [Tooltip("Text shown on an episode buy button when the episode has no explicit price string.")]
        public string defaultBuyLabel = "Support";

        [Header("Behavior")]
        [Tooltip("Open the checkout in the device's external browser. Always true today " +
                 "(no embedded webview plugin is installed); kept as a flag for a future " +
                 "in-app webview option.")]
        public bool openInExternalBrowser = true;

        /// <summary>
        /// Builds the checkout URL for a product, preferring a per-product override and
        /// falling back to <see cref="checkoutBaseUrl"/> with token substitution.
        /// </summary>
        public string BuildCheckoutUrl(string productId, float priceUsd, string overrideUrl = null)
        {
            string template = !string.IsNullOrWhiteSpace(overrideUrl) ? overrideUrl : checkoutBaseUrl;
            if (string.IsNullOrWhiteSpace(template))
                return null;

            return template
                .Replace("{productId}", Uri_EscapeDataString(productId))
                .Replace("{price}", priceUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Formats a price for UI display, e.g. "$4.99".</summary>
        public string FormatPrice(float priceUsd) =>
            $"{currencySymbol}{priceUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";

        static string Uri_EscapeDataString(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : System.Uri.EscapeDataString(value);
    }
}
