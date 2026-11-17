using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Woodpecker's tunables other than the prism target (EndConditionOverridesSO) and the scoring rule: the AI's
    /// pecking runs. Config separation - authored by <c>Tools/Build/author_woodpecker_assets.py</c>, never by hand.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WoodpeckerSettings",
        menuName = "ScriptableObjects/Arcade/WoodpeckerSettings")]
    public class WoodpeckerSettingsSO : ScriptableObject
    {
        [Header("AI - pecking runs")]
        [Tooltip("Seconds between an AI's re-reads of the densest hostile mass (Cell.GetExplosionTarget). Between reads " +
                 "it keeps flying the same run, so it pecks its way through a stand rather than turning away halfway.")]
        [Min(0.5f)] public float aiRetargetSeconds = 4f;
    }
}
