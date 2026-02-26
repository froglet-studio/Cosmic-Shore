using System.Collections.Generic;
using UnityEngine;
using System;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New AI Profile List", menuName = "CosmicShore/AIProfileList", order = 21)]
    public class SO_AIProfileList : ScriptableObject
    {
        [SerializeField] public List<AIProfile> aiProfiles;

        /// <summary>
        /// Pick <paramref name="count"/> unique random profiles.
        /// If the list has fewer entries than requested, profiles may repeat.
        /// </summary>
        public List<AIProfile> PickRandom(int count)
        {
            var result = new List<AIProfile>(count);
            if (aiProfiles == null || aiProfiles.Count == 0)
                return result;

            var pool = new List<AIProfile>(aiProfiles);
            for (int i = 0; i < count; i++)
            {
                if (pool.Count == 0)
                    pool = new List<AIProfile>(aiProfiles);

                int idx = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            return result;
        }

        /// <summary>
        /// Find a profile by name (case-insensitive).
        /// Returns null if not found.
        /// </summary>
        public AIProfile? FindByName(string name)
        {
            if (aiProfiles == null) return null;
            foreach (var p in aiProfiles)
            {
                if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }
    }

    [System.Serializable]
    public struct AIProfile
    {
        public string Name;
        public Sprite AvatarSprite;
    }
}
