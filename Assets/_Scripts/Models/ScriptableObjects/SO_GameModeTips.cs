using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "GameModeTipsList", menuName = "CosmicShore/UI/GameModeTipsList", order = 0)]
    public class SO_GameModeTips : ScriptableObject
    {
        [Tooltip("Tips shared across all game modes.")]
        [TextArea(2, 4)]
        [SerializeField] private List<string> commonTips = new();

        [Tooltip("Per-game-mode tip entries.")]
        [SerializeField] private List<GameModeTipEntry> modeTips = new();

        public string GetRandomTip(GameModes mode)
        {
            var pool = new List<string>(commonTips);

            var entry = modeTips.Find(e => e.mode == mode);
            if (entry != null && entry.tips is { Count: > 0 })
                pool.AddRange(entry.tips);

            if (pool.Count == 0) return string.Empty;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }
    }

    [Serializable]
    public class GameModeTipEntry
    {
        public GameModes mode;

        [TextArea(2, 4)]
        public List<string> tips = new();
    }
}
