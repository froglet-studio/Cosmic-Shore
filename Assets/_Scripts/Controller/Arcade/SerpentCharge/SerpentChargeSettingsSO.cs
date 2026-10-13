using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Charge's tunables other than the prism target (EndConditionOverridesSO) and the
    /// pilot price (SerpentChargeScoringRuleSO): the Charge-crystal economy and the marksman AI.
    /// Config separation - authored by <c>Tools/Build/author_serpent_charge_assets.py</c>, never by
    /// hand.
    ///
    /// <para><b>Every AI number that makes the bot harder is a per-INTENSITY list</b> (index =
    /// intensity - 1, so four entries), because "challenging" is the card's whole promise and the
    /// intensity row is the player's own dial for how much challenge they asked for. The generator
    /// asserts each ladder is monotone in the hard direction, so an edit cannot make intensity 4
    /// easier than intensity 3 by accident.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "SerpentChargeSettings",
        menuName = "ScriptableObjects/Arcade/SerpentChargeSettings")]
    public class SerpentChargeSettingsSO : ScriptableObject
    {
        [Header("Charge crystals")]
        [Tooltip("CHARGE crystals laid per wave. Charge is the sniper's RECOVERY (12 s at rest, " +
                 "5.4 s at Charge 10), so every one collected is a faster next shot - the mode's " +
                 "name and its economy. 0 disables.")]
        [Min(0)] public int chargeCrystalsPerWave = 10;

        [Tooltip("Seconds between waves. Each wave CLEARS the previous one and lays a fresh " +
                 "scatter, broadcast by the server as a wave index, so every peer lays the same " +
                 "crystals without the collection state (which is per-peer) ever having to agree.")]
        [Min(5f)] public float chargeCrystalWaveSeconds = 40f;

        [Tooltip("Inner radius of the shell the crystals scatter through, from the arena centre. " +
                 "Off the nucleus so a crystal never sits inside it.")]
        [Min(0f)] public float crystalScatterInnerRadius = 320f;

        [Tooltip("Outer radius of the scatter shell. Inside Rampage's 1,200 u membrane, so the " +
                 "crystals land in and among the forest belt the rifle is pointed at.")]
        [Min(1f)] public float crystalScatterOuterRadius = 1000f;

        [Tooltip("Seed for the scatter. A wave's layout is a pure function of (seed, wave index), " +
                 "so every peer lays the same crystals in the same places.")]
        public int crystalScatterSeed = 66;

        [Header("AI - cadence (per intensity, 1..4)")]
        [Tooltip("Seconds between an AI's re-plans of its best firing line. Shorter = a bot that " +
                 "notices a fatter line (or an exposed rival) sooner.")]
        public float[] aiSolveSecondsByIntensity = { 1.6f, 1.1f, 0.75f, 0.5f };

        [Tooltip("Candidate lines an AI scores per plan - aim points sampled from the densest " +
                 "hostile stand and from the mass ahead of its nose. More = better lines found.")]
        public int[] aiCandidateLinesByIntensity = { 6, 10, 16, 24 };

        [Tooltip("Degrees of error added to every aim point. The cone is 1.5 deg half-angle, so " +
                 "anything near that visibly costs prisms per shot and most pilot snipes.")]
        public float[] aiAimJitterDegreesByIntensity = { 2.5f, 1.5f, 0.75f, 0.25f };

        [Tooltip("Seconds between the rifle coming off cooldown and the AI being allowed to fire - " +
                 "a reaction time.")]
        public float[] aiReactionSecondsByIntensity = { 1.0f, 0.6f, 0.3f, 0.12f };

        [Tooltip("Fraction of its PLANNED line's value the live nose line must reach before the AI " +
                 "fires. Higher = pickier, fatter shots (it waits for the line it found).")]
        public float[] aiFireFractionByIntensity = { 0.5f, 0.6f, 0.7f, 0.8f };

        [Tooltip("Seconds of readiness after which an AI stops waiting for its planned line and " +
                 "fires anything worth aiMinShotValue - so a bot that cannot settle its nose still " +
                 "keeps the rifle working.")]
        public float[] aiPatienceSecondsByIntensity = { 2.5f, 3f, 3.5f, 4f };

        [Tooltip("Range (u) within which an AI will hunt a rival PILOT as a line. 0 = never hunts " +
                 "(it still takes a pilot that wanders into a line it was firing anyway). The " +
                 "rifle reaches 3,000.")]
        public float[] aiPilotHuntRangeByIntensity = { 0f, 1200f, 2000f, 2800f };

        [Tooltip("Range (u) within which an AI on a long cooldown detours to a Charge crystal. " +
                 "0 = never - the bot keeps its resting recovery all match.")]
        public float[] aiChargeDetourRangeByIntensity = { 0f, 450f, 700f, 1000f };

        [Header("AI - fixed")]
        [Tooltip("Seconds between AI fire-gate ticks (scope check, live-line score, trigger).")]
        [Min(0.02f)] public float aiTickSeconds = 0.1f;

        [Tooltip("The least a shot must be worth (prisms, a sniped pilot priced by the rule) " +
                 "before an impatient AI will spend the cooldown on it.")]
        [Min(1)] public int aiMinShotValue = 8;

        [Tooltip("Closest an aim point may be (u). A point the hull is about to fly through is not " +
                 "a line, and steering at it orbits.")]
        [Min(0f)] public float aiMinAimDistance = 250f;

        [Tooltip("Radius (u) of the sphere around the densest hostile stand that aim points are " +
                 "drawn from.")]
        [Min(10f)] public float aiStandSampleRadius = 260f;

        [Tooltip("Distance (u) ahead of the nose of the second sample sphere - opportunities that " +
                 "cost little turning.")]
        [Min(0f)] public float aiAheadSampleDistance = 900f;

        [Tooltip("Radius (u) of the ahead sample sphere.")]
        [Min(10f)] public float aiAheadSampleRadius = 450f;

        [Tooltip("Value multiplier for a line straight BEHIND the nose (interpolated from 1 at " +
                 "dead ahead). Below 1 so a bot prefers a slightly thinner line it can bring to " +
                 "bear now over a fat one it must turn around for.")]
        [Range(0.05f, 1f)] public float aiTurnPenaltyFloor = 0.35f;

        [Tooltip("Seconds of rifle cooldown that must remain before an AI will leave its line for a " +
                 "Charge crystal - a detour that outlasts the cooldown wastes a shot.")]
        [Min(0f)] public float aiDetourMinCooldownSeconds = 2.5f;

        [Tooltip("Seconds of rifle reload that must remain before an AI lowers its scope to CLOAK " +
                 "(lay a seed wall and vanish). It needs the scope down for one tick and up again " +
                 "after aiScopeRetrySeconds, so this must comfortably exceed that, or the bot would " +
                 "trade a ready rifle for a cloak.")]
        [Min(0f)] public float aiCloakMinCooldownSeconds = 5f;

        [Tooltip("Seconds an AI waits for a scope press to round-trip before pressing again.")]
        [Min(0.1f)] public float aiScopeRetrySeconds = 1f;

        /// <summary>The ladder entry for <paramref name="intensity"/> (1-based), clamped to the
        /// list. An empty list answers <paramref name="fallback"/>.</summary>
        public static float Pick(float[] ladder, int intensity, float fallback)
        {
            if (ladder == null || ladder.Length == 0) return fallback;
            return ladder[Mathf.Clamp(intensity - 1, 0, ladder.Length - 1)];
        }

        /// <inheritdoc cref="Pick(float[], int, float)"/>
        public static int Pick(int[] ladder, int intensity, int fallback)
        {
            if (ladder == null || ladder.Length == 0) return fallback;
            return ladder[Mathf.Clamp(intensity - 1, 0, ladder.Length - 1)];
        }
    }
}
