using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Charge's tunables other than the snipe target (EndConditionOverridesSO) and the
    /// snipe price (SerpentChargeScoringRuleSO): the Charge-crystal economy and the marksman AI.
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

        [Header("AI - difficulty (per intensity, 1..4) - BELIEF, never the stick")]
        [Tooltip("Seconds between an AI's re-reads of the field: which rival to hunt, and a fresh roll " +
                 "of its aim error. Shorter = a bot that switches to the exposed rival sooner.")]
        public float[] aiSolveSecondsByIntensity = { 1.2f, 0.9f, 0.6f, 0.4f };

        [Tooltip("Degrees of error in where the AI believes its rival is (an aim-point offset of up to " +
                 "this, fixed per plan). The rifle's cone is 1.5 deg half-angle, so an error past it " +
                 "misses more than it hits.")]
        public float[] aiAimJitterDegreesByIntensity = { 3.0f, 2.0f, 1.2f, 0.5f };

        [Tooltip("Seconds between the rifle coming off cooldown and the AI being allowed to fire - a " +
                 "reaction time.")]
        public float[] aiReactionSecondsByIntensity = { 1.0f, 0.6f, 0.3f, 0.12f };

        [Tooltip("Range (u) within which an AI prefers its hunt target by how cheaply it can bring them " +
                 "to bear; past it, it simply flies at the nearest visible rival. The rifle reaches 3,000.")]
        public float[] aiPilotHuntRangeByIntensity = { 1500f, 2000f, 2500f, 3000f };

        [Tooltip("Range (u) within which an AI on a long cooldown detours to a Charge crystal. " +
                 "0 = never - the bot keeps its resting recovery all match.")]
        public float[] aiChargeDetourRangeByIntensity = { 0f, 450f, 700f, 1000f };

        [Header("AI - fixed")]
        [Tooltip("Seconds between AI fire-gate ticks (scope check, rival-in-cone test, trigger).")]
        [Min(0.02f)] public float aiTickSeconds = 0.1f;

        [Tooltip("Inside this distance (u) of its rival the AI is too close to line a sniper round up " +
                 "and pulls away to aiStandoffDistance before turning back in.")]
        [Min(0f)] public float aiMinAimDistance = 300f;

        [Tooltip("How far (u) past its rival the AI aims when it pulls away to re-line-up.")]
        [Min(0f)] public float aiStandoffDistance = 700f;

        [Tooltip("Cost multiplier for a rival straight BEHIND the nose (interpolated from 1 at dead " +
                 "ahead). Below 1 so a bot prefers a slightly farther rival it can bring to bear now " +
                 "over a nearer one it must turn around for.")]
        [Range(0.05f, 1f)] public float aiTurnPenaltyFloor = 0.35f;

        [Tooltip("Seconds of rifle cooldown that must remain before an AI will leave its rival for a " +
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

    }
}
