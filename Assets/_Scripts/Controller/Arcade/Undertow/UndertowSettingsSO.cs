using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every tunable number for Undertow in one asset
    /// (Assets/_SO_Assets/Games/UndertowSettings.asset). The POINT TARGET is deliberately NOT
    /// here: it lives in EndConditionOverridesSO (FrogletTools > Game Modes > End Game
    /// Conditions), like every other mode's win count, and the point VALUES live on the mode's
    /// scoring rule, like every other combat mode's. This asset owns only what the MODE owns:
    /// the feedback beats and the AI.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UndertowSettings",
        menuName = "ScriptableObjects/Arcade/UndertowSettings")]
    public class UndertowSettingsSO : ScriptableObject
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
        [Tooltip("Seconds between AI rival re-selections. Between samples the AI keeps chasing " +
                 "the pilot it picked - the provider is sampled every frame, so the chase point " +
                 "tracks a live position even though the CHOICE is made on a slow cadence.")]
        [Min(0.25f)] public float aiRetargetSeconds = 1.25f;

        [Tooltip("Seconds of the rival's own velocity the AI leads its chase point by, so it " +
                 "flies to where the rival is going rather than where they were.")]
        [Range(0f, 3f)] public float aiInterceptLeadSeconds = 0.6f;

        [Tooltip("How strongly the AI prefers HUMAN rivals over AI rivals: an AI rival must be " +
                 "this many times closer than a human to steal the chase. 1 = pure nearest.")]
        [Min(1f)] public float aiHumanFocus = 3f;

        [Tooltip("An AI dashes when a rival is within this many units of its hull. The plate is " +
                 "a cylinder of 10x the hull radius (~45u) sweeping sideways from the hull and " +
                 "MIRRORED behind it, so a rival inside roughly this range is claimed whichever " +
                 "side they are on; past it a dash spends the plate's cooldown on open water. " +
                 "Retune with ScarabCavitationBlast.radiusPerVesselRadius.")]
        [Min(1f)] public float aiDashRange = 60f;

        [Tooltip("An AI ALSO dashes when the densest hostile mass (the cages, the wildlife's " +
                 "bodies) is within this many units and no rival is - the wildlife half of the " +
                 "score. Slightly shorter than the rival range so a creature never out-bids a " +
                 "pilot for the dash.")]
        [Min(1f)] public float aiWildlifeDashRange = 50f;

        [Tooltip("Seconds between an AI's dash attempts. The plate carries its own CHARGE-scaled " +
                 "cooldown and the juke refuses while a roll is live, so this only paces how " +
                 "often the AI ASKS.")]
        [Min(0.1f)] public float aiDashSampleSeconds = 0.4f;
    }
}
