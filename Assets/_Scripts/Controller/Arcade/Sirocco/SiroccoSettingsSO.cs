using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Sirocco's tunables other than the prism target (EndConditionOverridesSO): lead feedback and
    /// the AI's erosion runs. Config separation - authored by
    /// <c>Tools/Build/author_sirocco_assets.py</c>, never by hand.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SiroccoSettings",
        menuName = "ScriptableObjects/Arcade/SiroccoSettings")]
    public class SiroccoSettingsSO : ScriptableObject
    {
        [Header("Lead feedback")]
        [Tooltip("Fraction of the prism target the leading domain must reach before a lead change " +
                 "is announced - the opening lead flips on every prism. A FRACTION, so moving the " +
                 "target moves it with it.")]
        [Range(0.05f, 0.9f)] public float leadAnnounceFraction = 0.2f;

        [Tooltip("Seconds between server-side progress samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Header("AI - erosion runs")]
        [Tooltip("Seconds between an AI's re-reads of the densest hostile mass. Between reads it " +
                 "keeps flying the same run, so it finishes a pass rather than turning away " +
                 "halfway through a stand on a 45 deg/s hull.")]
        [Min(0.5f)] public float aiRetargetSeconds = 4f;

        [Tooltip("How far ABOVE the hostile mass (along its OWN up axis) an AI aims its hull, so the " +
                 "forest passes through the dust capsule hanging below it rather than into the " +
                 "hull. Half the resting 60-unit capsule.")]
        [Min(0f)] public float aiDustHover = 30f;

        [Tooltip("Seconds an AI waits for a mode-switch press to round-trip before asking again.")]
        [Min(0.1f)] public float aiModeRetrySeconds = 1f;
    }
}
