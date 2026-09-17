using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Broadside's tunables. Everything EXCEPT the point target (which lives in
    /// <c>EndConditionOverridesSO</c>, per the /EndGameConditions skill) and the point VALUES
    /// (which live on <see cref="BroadsideScoringRuleSO"/>, because what a verb is worth is the
    /// scoring rule's business).
    ///
    /// The AI block is the part a mixed-fleet mode needs and a single-hull one does not: seven
    /// hulls hunt the same way (steer at a rival - the platform's own opponent lock, shared with
    /// Joust) and FIRE seven different ways, so the numbers here are per-VERB, not per-hull.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Game Settings/Broadside", fileName = "BroadsideSettings")]
    public class BroadsideSettingsSO : ScriptableObject
    {
        [Header("Feedback")]
        [Tooltip("Seconds between server samples of match progress for the milestone toasts.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Tooltip("Fraction of the point target at which the first milestone toast fires.")]
        [Range(0f, 1f)] public float firstMilestoneFraction = 0.25f;

        [Tooltip("Fraction of the point target at which the second milestone toast fires.")]
        [Range(0f, 1f)] public float secondMilestoneFraction = 0.5f;

        [Header("AI - shared")]
        [Tooltip("Seconds between an AI re-picking which rival it is hunting. The STEERING is " +
                 "the platform's opponent lock (AIPilot.ConfigureForGameMode with seek-players, " +
                 "the same one Joust uses); this controller only adds the per-verb trigger.")]
        [Min(0.1f)] public float aiRetargetSeconds = 2f;

        [Tooltip("How much an AI prefers a HUMAN rival over another bot. Divides the squared " +
                 "distance, so 2 means a human twice as far away is still preferred.")]
        [Min(1f)] public float aiHumanFocus = 3f;

        [Tooltip("Seconds between an AI considering pulling its trigger. Keeps a bot from " +
                 "firing every frame it happens to be in range.")]
        [Min(0.05f)] public float aiFireSampleSeconds = 0.6f;

        [Header("AI - per verb")]
        [Tooltip("Range inside which an AI Scarab asks its juke for a committed dash, so the " +
                 "cavitation plate sweeps a rival. Roughly the plate's own reach.")]
        [Min(1f)] public float aiPlateRange = 160f;

        [Tooltip("Range inside which an AI Urchin taps its chain spikes at a rival. A tap is " +
                 "the aimed shotgun out of both guns, so this is an effective gunnery range.")]
        [Min(1f)] public float aiSpikeRange = 400f;

        [Tooltip("Seconds an AI holds the spike trigger. Kept SHORT: a tap is the aimed " +
                 "shotgun, a long hold charges the omnidirectional burst instead.")]
        [Range(0.02f, 0.4f)] public float aiSpikeTapSeconds = 0.08f;
    }
}
