using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Sentry's tunables other than the zap target (EndConditionOverridesSO) and the zap value (SentryScoringRuleSO):
    /// the AI wardens' steering. The arena (the shells and the crystal heart) is the cell's, the omni crystals the
    /// scene's CrystalManager's. Config separation - authored by <c>Tools/Build/author_sentry_assets.py</c>, never by
    /// hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it and that each C# initializer below
    /// matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SentrySettings", menuName = "ScriptableObjects/Arcade/SentrySettings")]
    public class SentrySettingsSO : ScriptableObject
    {
        [Header("AI wardens - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between the AI's choices of where to go (the heart, or a rival to cut off).")]
        public float[] aiRetargetSeconds = { 2.5f, 1.8f, 1.2f, 0.8f };

        [Tooltip("The farthest rival (u) the AI goes after. Past it, it goes to hold the crystal heart.")]
        public float[] aiHuntRange = { 250f, 400f, 550f, 700f };

        [Tooltip("Seconds of a rival's own course the AI steers ahead of it. Its soliton forms where the AI is, and the " +
                 "sentinel stands at the way in, so steering for where the rival is GOING plants the guard on its line.")]
        public float[] aiLeadSeconds = { 0f, 0.6f, 1.2f, 1.8f };

        [Tooltip("How far (u) off its pick the AI steers, re-rolled each choice: a wide error is a guard set off the line.")]
        public float[] aiAimError = { 120f, 70f, 35f, 10f };

        [Tooltip("Chance the AI goes to hold the heart even with a rival in range: every pilot comes to the heart for " +
                 "its crystals, so a sentinel at a gate into it is a guard on everyone's way in.")]
        public float[] aiHoldHeartChance = { 0.7f, 0.5f, 0.35f, 0.25f };

        [Header("AI wardens - shared")]
        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times nearer to be chosen. " +
                 "1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 1.5f;

        [Tooltip("How far (u) into the heart's crystal ball the AI aims when it holds the heart.")]
        [Min(0f)] public float aiHeartAimRadius = 60f;
    }
}
