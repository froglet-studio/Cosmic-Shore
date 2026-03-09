using UnityEngine;

namespace CosmicShore.Models
{
    /// <summary>
    /// Defines a single episode's display data — header and description only.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EpisodeData_",
        menuName = "ScriptableObjects/Episodes/EpisodeData")]
    public class SO_EpisodeData : ScriptableObject
    {
        [Header("Episode Info")]
        [Tooltip("Header text displayed on the episode card")]
        [SerializeField] private string header;

        [Tooltip("Description shown on the episode card")]
        [TextArea(2, 5)]
        [SerializeField] private string description;

        public string Header => header;
        public string Description => description;
    }
}
