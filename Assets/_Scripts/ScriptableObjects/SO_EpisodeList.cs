using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Collection of all episodes for the Episode Screen.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EpisodeList",
        menuName = "ScriptableObjects/Episodes/EpisodeList")]
    public class SO_EpisodeList : ScriptableObject
    {
        [Tooltip("All episodes in order")]
        public List<SO_EpisodeData> episodes = new();
    }
}
