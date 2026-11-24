using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Stingherd's tunables other than the sting target (EndConditionOverridesSO) and the sting value
    /// (StingherdScoringRuleSO): the AI drovers. The arena (Kelpwood's kelp ball, its tadpole herds and omni crystals) is
    /// the cell's. Config separation - authored by <c>Tools/Build/author_stingherd_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Every ladder is monotone
    /// toward harder; the generator asserts it and that each C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "StingherdSettings",
        menuName = "ScriptableObjects/Arcade/StingherdSettings")]
    public class StingherdSettingsSO : ScriptableObject
    {
        [Header("AI drovers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which rival hull to drive the herd at (and re-rolling the aim error).")]
        public float[] aiRetargetSeconds = { 2f, 1.5f, 1f, 0.6f };

        [Tooltip("How far (u) off the rival hull the drive is aimed, in a direction re-rolled each retarget. A herd is " +
                 "tens of units across, so a wide aim drives it past the hull.")]
        public float[] aiAimError = { 120f, 70f, 35f, 10f };

        [Tooltip("How far behind the herd (in herder rings, past the herd's own spread) the jacks stand to drive it. " +
                 "Closer pushes harder and scatters it more.")]
        public float[] aiStandOffRings = { 0.9f, 0.75f, 0.6f, 0.5f };

        [Tooltip("How far apart (in herder rings, either side of the drive line) the two jacks stand. Narrower keeps the " +
                 "herd on the line to the rival.")]
        public float[] aiFlankRings = { 0.8f, 0.7f, 0.6f, 0.5f };

        [Header("AI drovers - shared")]
        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times nearer to steal the " +
                 "drive. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 2f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
