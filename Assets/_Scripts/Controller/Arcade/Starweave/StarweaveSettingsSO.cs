using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Starweave's tuning (STARWEAVE.md): the crystal scatter (the intensity dial), the lead feedback and the AI weaver's
    /// policy. The round length lives in <c>EndConditionOverridesSO.starweaveRoundSeconds</c>. Every AI ladder is indexed
    /// by the seat's AI level (Easy, Medium, Hard, Legendary), never by the intensity (<c>AIDifficulty.cs</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Games/Starweave Settings", fileName = "StarweaveSettings")]
    public class StarweaveSettingsSO : ScriptableObject
    {
        [Header("Crystals (the intensity dial)")]
        [Tooltip("Elemental crystals scattered through the cell at the start, per intensity I1-I4. The bare cell grows no " +
                 "lifeforms, so no heart drops: these are the only element progression. A MASS crystal fattens every " +
                 "block the hull lays; a SPACE crystal lengthens the guns that raid a rival's weave.")]
        public int[] elementalCrystalCountByIntensity = { 24, 16, 10, 6 };

        [Tooltip("Radius, u, of the equal-volume scatter round the cell's centre.")]
        [Min(1f)] public float crystalScatterRadius = 900f;

        [Tooltip("Seed of the scatter. Every peer lays the same crystals from it.")]
        public int crystalScatterSeed = 92;

        [Header("Lead feedback")]
        [Tooltip("Seconds into the round before a lead change is announced at all (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 20f;

        [Tooltip("The least gap, in seconds, between two lead-change toasts.")]
        [Min(1f)] public float leadAnnounceMinGapSeconds = 10f;

        [Tooltip("Seconds between the server's lead samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 1f;

        [Header("AI - weave or raid (ladders: Easy, Medium, Hard, Legendary)")]
        [Tooltip("Seconds between an AI's weave/raid decisions.")]
        [Min(0.5f)] public float aiDecisionSeconds = 6f;

        [Tooltip("An AI raids while its domain trails the best rival by more than this fraction. 1 = never raids.")]
        public float[] aiRaidDeficitFraction = { 1f, 0.25f, 0.1f, 0.05f };

        [Tooltip("An AI raids in the last N seconds regardless of the score. 0 = never.")]
        public float[] aiLateRaidSeconds = { 0f, 15f, 25f, 35f };

        [Tooltip("An AI detours for an uncollected elemental crystal within this many units.")]
        public float[] aiCrystalReach = { 250f, 400f, 550f, 750f };

        [Tooltip("A weaving AI flies a slow orbit of the cell at this radius (a fraction of the crystal scatter radius), " +
                 "each on its own great circle so two weavers do not lay one arc twice.")]
        [Range(0.1f, 1f)] public float aiWeaveOrbitFraction = 0.55f;

        [Tooltip("Degrees per second the weaving orbit's waypoint advances. Slow: Creation Mode flies at half cruise.")]
        [Range(1f, 30f)] public float aiWeaveOrbitDegreesPerSecond = 4f;

        /// <summary>The scatter count for <paramref name="intensity"/> (1-based, clamped).</summary>
        public int CrystalCountFor(int intensity)
        {
            if (elementalCrystalCountByIntensity == null || elementalCrystalCountByIntensity.Length == 0) return 0;
            int i = Mathf.Clamp(intensity, 1, elementalCrystalCountByIntensity.Length) - 1;
            return Mathf.Max(0, elementalCrystalCountByIntensity[i]);
        }

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder; 0 for an
        /// empty one.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
