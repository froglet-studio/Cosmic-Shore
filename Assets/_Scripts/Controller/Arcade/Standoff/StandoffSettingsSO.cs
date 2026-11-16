using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Standoff's tunables other than the blast target (EndConditionOverridesSO) and the blast value
    /// (StandoffScoringRuleSO): the AI gunners. The posts are the cell's (SpawnableStandoff) and the omni crystals are
    /// the scene's CrystalManager's. Config separation - authored by <c>Tools/Build/author_standoff_assets.py</c>, never
    /// by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Every ladder is monotone
    /// toward harder; the generator asserts it and that each C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "StandoffSettings",
        menuName = "ScriptableObjects/Arcade/StandoffSettings")]
    public class StandoffSettingsSO : ScriptableObject
    {
        [Header("AI gunners - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which rival to go after (and re-rolling the pass side).")]
        public float[] aiRetargetSeconds = { 1.4f, 1f, 0.7f, 0.5f };

        [Tooltip("The farthest rival (u) the AI goes after. Past it, it works from post to post.")]
        public float[] aiHuntRange = { 300f, 420f, 560f, 720f };

        [Tooltip("Seconds of the rival's own velocity the AI leads its pass by.")]
        public float[] aiLeadSeconds = { 0f, 0.2f, 0.4f, 0.6f };

        [Tooltip("How far (u) to one side of the rival the AI aims its pass. The hull's own bomb-jump drops a full " +
                 "bomb ahead on a straight stick and blows it once past it; a full blast reaches 100 u, so a pass " +
                 "nearer than that catches the rival in it.")]
        public float[] aiPassOffset = { 70f, 55f, 40f, 28f };

        [Tooltip("Chance the AI breaks off to cover (behind the nearest post) when it is SILENCED (its blasts score " +
                 "nothing until the silence ends) instead of pressing on.")]
        public float[] aiBreakOffWhenSilencedChance = { 0f, 0.5f, 1f, 1f };

        [Tooltip("How much a SILENCED rival is preferred (it cannot score back): its cost is divided by this. 1 = no " +
                 "preference.")]
        public float[] aiSilencedPreyFocus = { 1f, 1f, 1.5f, 2f };

        [Header("AI gunners - shared")]
        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times cheaper to steal the " +
                 "pass. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 2f;

        [Tooltip("How far (u) behind a post (on its far side from the centre) the AI takes cover.")]
        [Min(5f)] public float aiCoverStandoff = 30f;

        [Tooltip("With no rival in range, the AI works round the posts: it heads for the post nearest its nose, and " +
                 "moves on to the next once it is this close (u).")]
        [Min(10f)] public float aiPostReachedDistance = 60f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
