using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Defines a single episode's display data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EpisodeData_",
        menuName = "ScriptableObjects/Episodes/EpisodeData")]
    public class SO_EpisodeData : ScriptableObject
    {
        [Header("Episode Info")]
        [Tooltip("Unique identifier for this episode")]
        [SerializeField] string episodeId;
        public string EpisodeId => episodeId;

        [Tooltip("Display title of the episode")]
        [FormerlySerializedAs("header")]
        [SerializeField] string title;
        public string Title => title;

        [Tooltip("Short description shown on the card")]
        [TextArea(2, 4)]
        [SerializeField] string description;
        public string Description => description;

        [Tooltip("Card artwork/thumbnail")]
        [SerializeField] Sprite cardImage;
        public Sprite CardImage => cardImage;

        [Tooltip("Episode number (for ordering)")]
        [SerializeField] int episodeNumber;
        public int EpisodeNumber => episodeNumber;

        [Tooltip("Price or cost displayed on the card. Free-text fallback when priceUsd is 0.")]
        [SerializeField] string amount;
        public string Amount => amount;

        [Tooltip("Whether this episode is currently available to play")]
        [SerializeField] bool isAvailable;
        public bool IsAvailable => isAvailable;

        [Header("Purchase (Web Checkout)")]
        [Tooltip("Real-money price in USD charged for this episode as 'support'. " +
                 "0 = not purchasable (uses the 'amount' string for display instead).")]
        [SerializeField, Min(0f)] float priceUsd;
        public float PriceUsd => priceUsd;

        [Tooltip("Optional per-episode checkout page URL. When empty, the IAPConfig base URL " +
                 "is used with {productId}/{price} token substitution.")]
        [SerializeField] string checkoutUrl;
        public string CheckoutUrl => checkoutUrl;
    }
}
