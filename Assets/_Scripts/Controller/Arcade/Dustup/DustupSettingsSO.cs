using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Dustup's tunables other than the point target (EndConditionOverridesSO) and the point
    /// value (DustupScoringRuleSO): progress feedback and the AI hunters. Config separation -
    /// authored by <c>Tools/Build/author_dustup_assets.py</c>, never by hand.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DustupSettings",
        menuName = "ScriptableObjects/Arcade/DustupSettings")]
    public class DustupSettingsSO : ScriptableObject
    {
        [Header("Progress feedback")]
        [Tooltip("Fraction of the point target at which the LEADING DOMAIN crosses the first " +
                 "milestone (toast + alert haptic). A FRACTION, so moving the target moves the " +
                 "milestones with it. Feedback only.")]
        [Range(0.05f, 0.9f)] public float firstMilestoneFraction = 0.25f;

        [Tooltip("Fraction of the point target at which the leading domain crosses the second " +
                 "milestone. Feedback only.")]
        [Range(0.1f, 0.95f)] public float secondMilestoneFraction = 0.5f;

        [Tooltip("Seconds between server-side progress samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Header("AI hunters")]
        [Tooltip("Seconds between AI rival re-selections. The chase point tracks a live position " +
                 "every frame; only the CHOICE of rival is made on this cadence.")]
        [Min(0.25f)] public float aiRetargetSeconds = 1.5f;

        [Tooltip("Seconds of the rival's own velocity the AI leads its chase point by. Short: " +
                 "both hulls are slow, and a long lead on a 45 deg/s turner overshoots.")]
        [Range(0f, 3f)] public float aiInterceptLeadSeconds = 0.5f;

        [Tooltip("How strongly the AI prefers HUMAN rivals over AI rivals: an AI rival must be " +
                 "this many times closer than a human to steal the chase. 1 = pure nearest.")]
        [Min(1f)] public float aiHumanFocus = 3f;

        [Tooltip("How far ABOVE its rival (along its OWN up axis) the AI aims its hull. The dust " +
                 "is a capsule hanging BELOW the Butterfly - 60 units long at rest (Space " +
                 "lengthens it to 150) and 24 across - so a hull that flies AT a rival hits them " +
                 "with its body and misses with its dust. Half the resting capsule puts the " +
                 "rival in the middle of it.")]
        [Min(0f)] public float aiDustHover = 30f;

        [Tooltip("Seconds an AI waits for a mode-switch press to round-trip through the server " +
                 "before it asks again. The switch is a TOGGLE, so a second press inside the round " +
                 "trip would undo the first.")]
        [Min(0.1f)] public float aiModeRetrySeconds = 1f;
    }
}
