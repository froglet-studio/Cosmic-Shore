using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "New GameModeTips", menuName = "CosmicShore/UI/GameModeTips", order = 0)]
    public class SO_GameModeTips : ScriptableObject
    {
        [Tooltip("Tips specific to this game mode.")]
        [TextArea(2, 4)]
        [SerializeField] private List<string> tips = new();

        [Tooltip("Optional shared/common tips list. Entries here are merged with mode-specific tips.")]
        [SerializeField] private SO_GameModeTips commonTips;

        public string GetRandomTip()
        {
            var all = new List<string>(tips);
            if (commonTips != null && commonTips.tips is { Count: > 0 })
                all.AddRange(commonTips.tips);

            if (all.Count == 0) return string.Empty;
            return all[Random.Range(0, all.Count)];
        }
    }
}
