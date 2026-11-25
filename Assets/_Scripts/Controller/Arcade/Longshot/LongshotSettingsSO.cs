using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Longshot's tuning (LONGSHOT.md): the lead feedback and the AI rifleman's policy. The field is
    /// <see cref="LongshotField"/>; the round length lives in <c>EndConditionOverridesSO.longshotRoundSeconds</c>. Every
    /// AI ladder is indexed by the seat's AI level (Easy, Medium, Hard, Legendary), never by the intensity
    /// (<c>AIDifficulty.cs</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Games/Longshot Settings", fileName = "LongshotSettings")]
    public class LongshotSettingsSO : ScriptableObject
    {
        [Header("Lead feedback")]
        [Tooltip("Seconds into the round before a lead change is announced at all (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 20f;

        [Tooltip("The least gap, in seconds, between two lead-change toasts.")]
        [Min(1f)] public float leadAnnounceMinGapSeconds = 10f;

        [Tooltip("Seconds between the server's lead samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 1f;

        [Header("AI - the shot (ladders: Easy, Medium, Hard, Legendary)")]
        [Tooltip("The AI fires once its round would break at least this fraction of the run's standing prisms. A " +
                 "patient rifleman waits to be lined up along the run; an impatient one takes what it can.")]
        public float[] aiFireFraction = { 0.25f, 0.45f, 0.65f, 0.8f };

        [Tooltip("Seconds the AI holds the scope up before the shot can go.")]
        public float[] aiScopeSettleSeconds = { 1f, 0.6f, 0.35f, 0.2f };

        [Tooltip("Seconds between an AI re-picking its run while it lines up (it keeps one until it fires on it).")]
        public float[] aiRetargetSeconds = { 14f, 11f, 9f, 7f };

        [Tooltip("An AI picks a rival's trail over the nearest run while its domain trails the best rival by more than " +
                 "this fraction (a straight trail is a lance of the rival's colour). 1 = never.")]
        public float[] aiRivalTrailDeficitFraction = { 1f, 0.3f, 0.15f, 0.05f };

        [Header("AI - lining up")]
        [Tooltip("The AI lines up this far back along a run's line from its near end (u) - the longer, the more of the " +
                 "run the cone's growth forgives.")]
        [Min(20f)] public float aiStandOff = 260f;

        [Tooltip("Then it follows a carrot this far ahead of itself on the run's line (u), which pulls it onto the line.")]
        [Min(10f)] public float aiCarrot = 120f;

        [Tooltip("The AI raises the scope only once it is within this many units of the run's line.")]
        [Min(1f)] public float aiScopeLateral = 25f;

        [Tooltip("The AI gives up a scoped shot it cannot take after this many seconds, lowers the scope and lines up " +
                 "again.")]
        [Min(0.5f)] public float aiShotGiveUpSeconds = 4f;

        [Tooltip("Seconds between an AI's rival-trail decisions.")]
        [Min(0.5f)] public float aiDecisionSeconds = 5f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder; 0 for an
        /// empty one.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
