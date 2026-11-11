using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Harvest's tunables other than the prism target (EndConditionOverridesSO) and the results wording
    /// (HarvestScoringRuleSO): the AI reapers. Config separation - authored by
    /// <c>Tools/Build/author_harvest_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Every ladder is monotone
    /// toward harder; the generator asserts it and that each C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "HarvestSettings",
        menuName = "ScriptableObjects/Arcade/HarvestSettings")]
    public class HarvestSettingsSO : ScriptableObject
    {
        [Header("Team Space crystals (index = intensity - 1)")]
        [Tooltip("The element of the team crystals. Space grows the Thresher's blast (Shombith, 2026-11-10).")]
        public Element teamCrystalElement = Element.Space;

        [Tooltip("Space elemental crystals floated for EACH team in the match, per intensity (I1, I2, I3, I4). Each " +
                 "wears its team's colour and only that team's pilots can take it (ElementalCrystalImpactor.RestrictTo). " +
                 "0 = none at that intensity.")]
        public int[] teamCrystalsPerTeamByIntensity = { 0, 0, 4, 0 };

        [Tooltip("Least air (u) between a team crystal and any sheaf, cloud, bale or other crystal.")]
        [Min(5f)] public float teamCrystalClearance = 30f;

        [Tooltip("Seconds before a taken team crystal grows back in its place.")]
        [Min(0.5f)] public float teamCrystalRegrowSeconds = 12f;

        [Tooltip("Seconds between the regrowth checks.")]
        [Min(0.1f)] public float crystalCheckSeconds = 0.5f;

        [Header("AI reapers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds the AI circles a sheaf's rim, the sheaf inside its turn, so the trailing chain mows the " +
                 "stalks and banks energy in the ball, before it turns in through the core.")]
        public float[] aiMowSeconds = { 2f, 3f, 4f, 5f };

        [Tooltip("How far (u) from the stalk tips the AI's lap runs, in the wheel's plane: 0 at the tips, negative " +
                 "inside them. Deeper laps cut more (the stalk lab: 38 cuts a lap at the tips, 68 thirty u inside) and " +
                 "risk the ball meeting an uncut stalk and spending its bank there.")]
        public float[] aiMowStandoff = { 0f, -10f, -20f, -25f };

        [Tooltip("The farthest (u) an omni crystal may be for the AI to break off and take it (its 12 s windmill).")]
        public float[] aiCrystalReach = { 250f, 400f, 550f, 700f };

        [Tooltip("Seconds before the AI comes back to a sheaf it has already worked; it takes the next nearest " +
                 "hostile one meanwhile.")]
        public float[] aiRevisitSeconds = { 8f, 12f, 16f, 20f };

        [Header("AI reapers - shared")]
        [Tooltip("Degrees ahead round the circle the AI steers at while mowing: larger is a smoother, wider orbit.")]
        [Range(5f, 90f)] public float aiMowLeadDegrees = 40f;

        [Tooltip("The AI flies this far (u) PAST a core's centre when it turns in, so the hull goes through and the " +
                 "trailing ball is dragged across the core rather than braking short.")]
        [Min(0f)] public float aiStrikeOvershoot = 120f;

        [Tooltip("Seconds the AI holds its dive down the axle through a core before choosing its next sheaf.")]
        [Min(0.2f)] public float aiStrikeSeconds = 2.5f;

        [Tooltip("Longest the AI climbs out of the wheel's plane toward the axle before it dives anyway (s).")]
        [Min(0.2f)] public float aiClimbSeconds = 3f;

        [Tooltip("How close (u) to the axle point counts as there: the dive starts.")]
        [Min(5f)] public float aiAxleArrive = 60f;

        [Tooltip("How far out on the axle the dive starts, as a multiple of the sheaf's reach (its stalk tips).")]
        [Min(1f)] public float aiAxleReachFactor = 1.4f;

        [Tooltip("Seconds between the AI's checks that its Thresher has left Mass Mode (the hull spawns in it). " +
                 "Each check that finds it still building presses A once.")]
        [Min(0.5f)] public float aiMassModeCheckSeconds = 1.5f;

        /// <summary>The team crystals each team gets at <paramref name="intensity"/> (1-4, clamped).</summary>
        public int TeamCrystalsPerTeam(int intensity)
        {
            if (teamCrystalsPerTeamByIntensity == null || teamCrystalsPerTeamByIntensity.Length == 0) return 0;
            return Mathf.Max(0, teamCrystalsPerTeamByIntensity[Mathf.Clamp(intensity - 1, 0, teamCrystalsPerTeamByIntensity.Length - 1)]);
        }

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
