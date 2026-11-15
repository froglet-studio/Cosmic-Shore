using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shockfront's tunables other than the ripple target (EndConditionOverridesSO) and the hit value
    /// (ShockfrontScoringRuleSO): the AI hunters. Config separation - authored by
    /// <c>Tools/Build/author_shockfront_assets.py</c>, never by hand.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShockfrontSettings",
        menuName = "ScriptableObjects/Arcade/ShockfrontSettings")]
    public class ShockfrontSettingsSO : ScriptableObject
    {
        [Header("AI hunters")]
        [Tooltip("Seconds between AI rival re-selections. The chase point tracks a live position every frame; only " +
                 "the CHOICE of rival is made on this cadence.")]
        [Min(0.1f)] public float aiRetargetSeconds = 1.5f;

        [Tooltip("Seconds of the rival's own velocity the chase point leads by, so a hunter cuts across a rival's " +
                 "line instead of trailing it. The Stoat's own autopilot slings whenever the chase point is 30 deg or " +
                 "more off its nose, and the pair it slings out of closes where it was laid - beside the rival.")]
        [Min(0f)] public float aiInterceptLeadSeconds = 0.8f;

        [Tooltip("How strongly a hunter prefers a HUMAN rival: a human counts as this many times nearer.")]
        [Min(1f)] public float aiHumanFocus = 2f;
    }
}
