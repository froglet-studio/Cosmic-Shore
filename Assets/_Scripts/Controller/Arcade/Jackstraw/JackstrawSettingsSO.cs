using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Jackstraw's tunables other than the strike target (EndConditionOverridesSO) and the strike value
    /// (JackstrawScoringRuleSO): progress feedback, the crystals in the heap's clearings, and the AI hunters.
    /// Config separation - authored by <c>Tools/Build/author_jackstraw_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Every ladder is monotone
    /// toward harder; the generator asserts it and that each C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "JackstrawSettings",
        menuName = "ScriptableObjects/Arcade/JackstrawSettings")]
    public class JackstrawSettingsSO : ScriptableObject
    {
        [Header("Progress feedback")]
        [Tooltip("Seconds between the crystal regrowth checks and the hint's idle check.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Header("Crystals (in the heap's clearings, every intensity)")]
        [Tooltip("Elemental crystals floated in each clearing of the heap. Mixed elements; a silenced pilot " +
                 "refuels here, out of the sticks' cover. 0 = none.")]
        [Min(0)] public int crystalsPerClearing = 3;

        [Tooltip("A crystal sits within this fraction of its clearing's radius of the clearing's centre, so a " +
                 "swing that takes it never clips a stick.")]
        [Range(0.1f, 0.9f)] public float clearingFill = 0.5f;

        [Tooltip("Seconds before a collected crystal grows back in its place.")]
        [Min(0.5f)] public float crystalRegrowSeconds = 8f;

        [Tooltip("Seed for the crystals' places and elements. Every peer lays the same crystals from it - no " +
                 "network message.")]
        public int crystalSeed = 84;

        [Header("AI hunters - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which rival to hunt (and re-rolling the miss offset).")]
        public float[] aiRetargetSeconds = { 1.2f, 0.9f, 0.6f, 0.4f };

        [Tooltip("The farthest rival (u) the AI hunts. Past it, it holds the heap's centre.")]
        public float[] aiHuntRange = { 600f, 800f, 1000f, 1400f };

        [Tooltip("Seconds of the rival's own velocity the AI leads its run by.")]
        public float[] aiLeadSeconds = { 0f, 0.15f, 0.3f, 0.45f };

        [Tooltip("How far (u) off the rival the AI's run is aimed, in a direction re-rolled each retarget. The ball " +
                 "trails up to a chain behind the ship, so a near miss by the hull can still be a strike; a wide one " +
                 "is a real miss.")]
        public float[] aiMissRadius = { 45f, 30f, 15f, 5f };

        [Tooltip("Chance the AI runs for the nearest clearing's crystals when it is silenced (it cannot score until " +
                 "the silence ends) instead of chasing on.")]
        public float[] aiRefuelWhenSilencedChance = { 0f, 0.5f, 1f, 1f };

        [Tooltip("How much a SILENCED rival is preferred (it cannot hit back): its cost is divided by this. 1 = no " +
                 "preference.")]
        public float[] aiSilencedPreyFocus = { 1f, 1f, 1.5f, 2f };

        [Header("AI hunters - shared")]
        [Tooltip("The AI flies this far (u) PAST the rival along its run, so the hull goes through and the trailing " +
                 "ball and chain sweep across them, rather than braking short.")]
        [Min(0f)] public float aiOvershoot = 60f;

        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times cheaper to steal " +
                 "the hunt. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 2f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
