using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Flitstrike's tunables other than the strike target (EndConditionOverridesSO) and the strike value
    /// (FlitstrikeScoringRuleSO): the AI hunters. Config separation - authored by
    /// <c>Tools/Build/author_flitstrike_assets.py</c>, never by hand.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FlitstrikeSettings",
        menuName = "ScriptableObjects/Arcade/FlitstrikeSettings")]
    public class FlitstrikeSettingsSO : ScriptableObject
    {
        [Header("AI hunters")]
        [Tooltip("Seconds between AI rival re-selections. The chase point tracks a live position every frame; only the " +
                 "CHOICE of rival is made on this cadence.")]
        [Min(0.25f)] public float aiRetargetSeconds = 1.5f;

        [Tooltip("Seconds of the rival's own velocity the AI leads its chase point by. The Flit is an 18 u dart along " +
                 "the hull's nose, so the AI wants the rival dead ahead and close - a short lead.")]
        [Range(0f, 3f)] public float aiInterceptLeadSeconds = 0.4f;

        [Tooltip("How strongly the AI prefers HUMAN rivals over AI rivals: an AI rival must be this many times closer " +
                 "than a human to steal the chase. 1 = pure nearest.")]
        [Min(1f)] public float aiHumanFocus = 2f;
    }
}
