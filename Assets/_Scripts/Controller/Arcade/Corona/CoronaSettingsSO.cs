using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Corona's tunables other than the bloom target (EndConditionOverridesSO) and the bloom value
    /// (CoronaScoringRuleSO): the AI stingers. The arena is the cell's (SpawnableCorona) and the omni crystals are the
    /// scene's CrystalManager's. Config separation - authored by <c>Tools/Build/author_corona_assets.py</c>, never by
    /// hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Every ladder is monotone
    /// toward harder; the generator asserts it and that each C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "CoronaSettings",
        menuName = "ScriptableObjects/Arcade/CoronaSettings")]
    public class CoronaSettingsSO : ScriptableObject
    {
        [Header("AI stingers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which rival to graze (and re-rolling the pass and gate offsets).")]
        public float[] aiRetargetSeconds = { 1.2f, 0.9f, 0.6f, 0.4f };

        [Tooltip("The farthest rival (u) the AI goes after. Past it, it keeps skimming.")]
        public float[] aiHuntRange = { 350f, 500f, 650f, 850f };

        [Tooltip("Seconds of the rival's own velocity the AI leads its pass by.")]
        public float[] aiLeadSeconds = { 0f, 0.15f, 0.3f, 0.45f };

        [Tooltip("How far (u) to one side of the rival's hull the AI aims its pass. The skimmer reaches 10 u at Space " +
                 "0, so a wide pass misses the graze.")]
        public float[] aiGrazeOffset = { 9f, 7f, 5f, 4f };

        [Tooltip("How far (u) off a shell the AI flies while it skims for energy. The skimmer reaches 10 u; nearer is " +
                 "more skims, and more risk of the hull touching a tile (which sets off every bomb).")]
        public float[] aiSkimStandoff = { 10f, 9f, 8f, 7f };

        [Tooltip("How far off a gate's centre the AI aims, as a fraction of the gate's radius. Off centre means nearer " +
                 "the rim tiles.")]
        public float[] aiGateError = { 0.5f, 0.35f, 0.2f, 0.1f };

        [Tooltip("The AI heads for the heart to cash out once this many bombs are planted (more bombs, bigger blooms).")]
        public float[] aiCashAtBombs = { 1f, 2f, 3f, 3f };

        [Tooltip("...or once the shared fuse has this few seconds left, whatever the count (a fuse that runs out " +
                 "blooms small).")]
        public float[] aiCashAtFuseSeconds = { 11f, 9f, 7f, 6f };

        [Tooltip("Chance the AI breaks off to the heart when it is SILENCED (it cannot plant or score until the " +
                 "silence ends) instead of chasing on.")]
        public float[] aiBreakOffWhenSilencedChance = { 0f, 0.5f, 1f, 1f };

        [Tooltip("How much a SILENCED rival is preferred (it cannot score back): its cost is divided by this. 1 = no " +
                 "preference.")]
        public float[] aiSilencedPreyFocus = { 1f, 1f, 1.5f, 2f };

        [Header("AI stingers - shared")]
        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times cheaper to steal the " +
                 "pass. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 2f;

        [Tooltip("How far (u) before a gate the AI lines up on its axis, and how far past it it aims once inside.")]
        [Min(10f)] public float aiGateApproach = 45f;

        [Tooltip("How far (u) ahead along its heading the AI looks for the next stretch of shell to skim.")]
        [Min(10f)] public float aiSkimLookahead = 60f;

        [Tooltip("Clearance (u) the AI keeps from the shell inside its band when a straight line to its goal would cut " +
                 "through it: it bends round instead.")]
        [Min(5f)] public float aiBandClearance = 25f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
