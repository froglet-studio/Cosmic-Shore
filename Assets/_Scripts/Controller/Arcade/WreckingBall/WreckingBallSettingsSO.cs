using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every tunable number for Wrecking Ball in one asset
    /// (Assets/_SO_Assets/Games/WreckingBallSettings.asset) - the ScarabScrambleSettingsSO
    /// pattern. The PRISM TARGET is deliberately NOT here: it lives in EndConditionOverridesSO
    /// (FrogletTools > Game Modes > End Game Conditions), like every other mode's win count.
    ///
    /// BALL PHYSICS and the PLATE are deliberately not here either - both are the VESSEL's
    /// (the ball reads its physics off its own prefab, the plate its reach and cooldown off
    /// ScarabCavitationBlast) and behave identically in every arena they are used in. This asset
    /// owns only what the MODE owns: the court, the milestone beats and the AI.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WreckingBallSettings",
        menuName = "ScriptableObjects/Arcade/WreckingBallSettings")]
    public class WreckingBallSettingsSO : ScriptableObject
    {
        [Header("Court (the cell nucleus IS the court)")]
        [Tooltip("Court sphere radius per intensity (index 0 = intensity 1). The nucleus is " +
                 "resized to this and every ball reflects off it, so a shot that misses the " +
                 "forest comes back through it. ONE radius at every intensity on purpose: the " +
                 "forest is planted in a band of the MEMBRANE authored against this court " +
                 "(WreckingBall Spawn Profile 1..4), and a court that grew would leave the " +
                 "forest standing outside the wall where a ball's drag ramp kills it. Intensity " +
                 "is the forest's DENSITY and the crystal SUPPLY, never the court.")]
        public float[] courtRadiusByIntensity = { 720f, 720f, 720f, 720f };

        [Header("Progress feedback")]
        [Tooltip("Fraction of the prism target at which a lead change starts being announced. " +
                 "A FRACTION, so moving the target moves the beat with it. Feedback only.")]
        [Range(0.05f, 0.9f)] public float leadChangeAnnounceFraction = 0.25f;

        [Tooltip("Seconds between server-side progress samples for the lead-change toast.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Header("AI wreckers")]
        [Tooltip("Seconds between an AI's target re-selections (which ball / which crystal). " +
                 "Between samples it keeps flying at the live position of what it picked.")]
        [Min(0.25f)] public float aiRetargetSeconds = 1f;

        [Tooltip("How far BEHIND its ball (on the far side from the densest hostile forest) an " +
                 "AI aims, so driving to the aim point bowls the ball INTO the forest - the " +
                 "Scramble escort's approach lead with the mass cluster standing in for the hoop.")]
        [Min(1f)] public float aiApproachLead = 45f;

        [Tooltip("Seconds of the ball's own velocity an AI leads a MOVING ball by.")]
        [Range(0f, 3f)] public float aiInterceptLeadSeconds = 0.5f;

        [Tooltip("An AI fires its cavitation plate when the densest hostile forest is within this " +
                 "many units. The plate is a cylinder of 10x the hull radius (~45u) sweeping " +
                 "sideways from the hull and MIRRORED behind it, so anything inside roughly this " +
                 "range of the hull is claimed; past it a dash spends the cooldown on empty water. " +
                 "Retune with ScarabCavitationBlast.radiusPerVesselRadius.")]
        [Min(1f)] public float aiDashRange = 70f;

        [Tooltip("Seconds between an AI's dash attempts. The plate carries its own CHARGE-scaled " +
                 "cooldown (2.5 s at rest) and the juke refuses while a roll is live, so this only " +
                 "paces how often the AI ASKS.")]
        [Min(0.1f)] public float aiDashSampleSeconds = 0.5f;

        // ── Helpers (clamped per-intensity lookups; intensity is 1-based) ─────

        public float CourtRadiusForIntensity(int intensity)
        {
            var table = courtRadiusByIntensity;
            if (table == null || table.Length == 0) return 720f;
            return table[Mathf.Clamp(intensity - 1, 0, table.Length - 1)];
        }
    }
}
