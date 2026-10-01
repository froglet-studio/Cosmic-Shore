using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An authored list of Mentor tips - one per scoring-metric family, optionally one per mode
    /// and one per hull. The Mentor plays them in tier, then priority, order
    /// (Docs/ModePreview/TRAINING_PLAN.md §4.4).
    /// </summary>
    [CreateAssetMenu(fileName = "Tips", menuName = "ScriptableObjects/Drill/Tip List")]
    public sealed class TipListSO : ScriptableObject
    {
        [SerializeField] List<MentorTip> tips = new();

        public IReadOnlyList<MentorTip> Tips => tips;
    }
}
