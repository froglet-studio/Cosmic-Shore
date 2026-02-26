using UnityEngine;

namespace CosmicShore.Models.ScriptableObjects
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
        public string title;

        [Tooltip("Short description shown on the card")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Card artwork/thumbnail")]
        public Sprite cardImage;

        [Tooltip("Episode number (for ordering)")]
        public int episodeNumber;

        [Tooltip("Price or cost displayed on the card")]
        public string amount;

        [Tooltip("Whether this episode is currently available to play")]
        public bool isAvailable;
    }
}
