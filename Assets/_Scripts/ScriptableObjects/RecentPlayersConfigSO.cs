using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Config for the "recently played with" list (<see cref="CosmicShore.Core.RecentPlayersStore"/>):
    /// how many pilots the list remembers and the save file it lives in. The numbers live here, not
    /// in the store, so a designer can retune the cap without a code change.
    ///
    /// Loaded from <c>Resources/RecentPlayersConfig</c>; a missing asset falls back to these
    /// field-initializer defaults so the list still works in a build that lost the asset.
    /// </summary>
    [CreateAssetMenu(fileName = "RecentPlayersConfig", menuName = "ScriptableObjects/Social/Recent Players Config")]
    public class RecentPlayersConfigSO : ScriptableObject
    {
        public const string ResourcePath = "RecentPlayersConfig";

        [Header("Capacity")]
        [Tooltip("How many pilots the list keeps, newest first. A match with more new pilots than this " +
                 "keeps the newest; a pilot already on the list moves to the front instead of taking a " +
                 "second slot.")]
        [Min(1)] [SerializeField] int maxEntries = 20;

        [Header("Persistence")]
        [Tooltip("File under Application.persistentDataPath the list is saved to (JSON, through DataAccessor, " +
                 "like the favourites and loadouts). Local only: it is a memory of who you met, not a " +
                 "relationship, so it is not Cloud Save data.")]
        [SerializeField] string saveFileName = "recent_players.data";

        public int MaxEntries => Mathf.Max(1, maxEntries);
        public string SaveFileName => string.IsNullOrWhiteSpace(saveFileName) ? "recent_players.data" : saveFileName;

        /// <summary>
        /// The shared config: the Resources asset when present, otherwise an in-memory instance
        /// carrying the field-initializer defaults.
        /// </summary>
        public static RecentPlayersConfigSO LoadOrDefault()
        {
            var config = Resources.Load<RecentPlayersConfigSO>(ResourcePath);
            if (config == null)
                config = CreateInstance<RecentPlayersConfigSO>();
            return config;
        }
    }
}
