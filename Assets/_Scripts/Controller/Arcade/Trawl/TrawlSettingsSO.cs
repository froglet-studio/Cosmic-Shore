using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Trawl's tunables other than the prism target (EndConditionOverridesSO) and the results wording
    /// (TrawlScoringRuleSO): the AI skippers. The field (Harvest's sheaves, chaff and bales) is the cell's. Config
    /// separation - authored by <c>Tools/Build/author_trawl_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity (<c>AIDifficulty.cs</c>). Every ladder is monotone toward harder; the generator asserts it and that each
    /// C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "TrawlSettings",
        menuName = "ScriptableObjects/Arcade/TrawlSettings")]
    public class TrawlSettingsSO : ScriptableObject
    {
        [Header("AI skippers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which stand of hostile mass to trawl through (and re-rolling the aim " +
                 "error). Longer commits to a pass; shorter chases the densest stand as the field thins.")]
        public float[] aiRetargetSeconds = { 7f, 6f, 5f, 4f };

        [Tooltip("How far (u) off the stand's centre the AI's run is aimed, in a direction re-rolled each retarget. A " +
                 "core is 40-60 u across, so a wide aim trawls its edge, or nothing.")]
        public float[] aiAimError = { 70f, 40f, 18f, 6f };

        [Header("AI skippers - shared")]
        [Tooltip("How far (u) PAST the stand the AI aims, along the line it came in on: the net trails the hull, so a " +
                 "run that stops at the stand leaves the net short of it.")]
        [Min(0f)] public float aiOvershoot = 160f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
