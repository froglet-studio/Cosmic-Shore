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
        public string episodeId;

        [Tooltip("Display title of the episode")]
        [FormerlySerializedAs("header")]
        public string title;

        [Tooltip("Short description shown on the card")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Card artwork/thumbnail")]
        public Sprite cardImage;

        [Tooltip("Episode number (for ordering)")]
        public int episodeNumber;

        [Tooltip("Price or cost displayed on the card. Free-text fallback when priceUsd is 0.")]
        public string amount;

        [Tooltip("Whether this episode is currently available to play")]
        public bool isAvailable;

        [Header("Purchase (Web Checkout)")]
        [Tooltip("Real-money price in USD charged for this episode as 'support'. " +
                 "0 = not purchasable (uses the 'amount' string for display instead).")]
        [Min(0f)] public float priceUsd;

        [Tooltip("Optional per-episode checkout page URL. When empty, the IAPConfig base URL " +
                 "is used with {productId}/{price} token substitution.")]
        public string checkoutUrl;
    }
}
