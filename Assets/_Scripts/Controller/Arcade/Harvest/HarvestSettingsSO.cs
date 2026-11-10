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
        [Header("AI reapers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds the AI circles a sheaf's rim, the sheaf inside its turn, so the trailing chain mows the " +
                 "stalks and banks energy in the ball, before it turns in through the core.")]
        public float[] aiMowSeconds = { 2f, 3f, 4f, 5f };

        [Tooltip("How far (u) outside the sheaf's reach the AI's circle runs. Further out, the chain crosses only " +
                 "the stalk tips and the ball stays clear of them (a ball that touches a stalk spends its bank there).")]
        public float[] aiMowStandoff = { 0f, 15f, 30f, 40f };

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

        [Tooltip("Seconds the AI holds its strike line through a core before choosing its next sheaf.")]
        [Min(0.2f)] public float aiStrikeSeconds = 2.5f;

        [Tooltip("Seconds between the AI's checks that its Thresher has left Mass Mode (the hull spawns in it). " +
                 "Each check that finds it still building presses A once.")]
        [Min(0.5f)] public float aiMassModeCheckSeconds = 1.5f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
