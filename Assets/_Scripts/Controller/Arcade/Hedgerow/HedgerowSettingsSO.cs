using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hedgerow's tuning (HEDGEROW.md): the lead feedback and the AI warden's policy. The field is
    /// <see cref="HedgerowField"/>; the round length lives in <c>EndConditionOverridesSO.hedgerowRoundSeconds</c>. Every AI
    /// ladder is indexed by the seat's AI level (Easy, Medium, Hard, Legendary), never by the intensity
    /// (<c>AIDifficulty.cs</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Games/Hedgerow Settings", fileName = "HedgerowSettings")]
    public class HedgerowSettingsSO : ScriptableObject
    {
        [Header("Lead feedback")]
        [Tooltip("Seconds into the round before a lead change is announced at all (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 20f;

        [Tooltip("The least gap, in seconds, between two lead-change toasts.")]
        [Min(1f)] public float leadAnnounceMinGapSeconds = 10f;

        [Tooltip("Seconds between the server's lead samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 1f;

        [Header("AI - hedge or raid (ladders: Easy, Medium, Hard, Legendary)")]
        [Tooltip("Seconds between an AI's hedge/raid decisions (only taken while it flies).")]
        [Min(0.5f)] public float aiDecisionSeconds = 5f;

        [Tooltip("An AI raids (stops beside the densest rival mass, so its wall eats the rival's trail) while its domain " +
                 "trails the best rival by more than this fraction. 1 = never raids.")]
        public float[] aiRaidDeficitFraction = { 1f, 0.3f, 0.15f, 0.05f };

        [Tooltip("An AI raids in the last N seconds regardless of the score. 0 = never.")]
        public float[] aiLateRaidSeconds = { 0f, 15f, 25f, 35f };

        [Tooltip("Seconds between an AI re-picking its hedge while it flies (it keeps one until it stops at it).")]
        public float[] aiRetargetSeconds = { 12f, 9f, 7f, 5f };

        [Tooltip("Seconds an AI stays stopped once its wall is growing. The wall only steals while it is held, so a " +
                 "longer hold eats more of the hedge.")]
        public float[] aiHoldSeconds = { 8f, 12f, 15f, 18f };

        [Header("AI - the stop")]
        [Tooltip("The AI stops this far short of the hedge (u, along its approach), so the seed - its latest trail " +
                 "prism, just behind the hull - is inside the wall's 40 u steal reach of the hedge.")]
        [Min(1f)] public float aiStopShort = 22f;

        [Tooltip("The AI lines up this far back from its stop (u) and runs in across the hedge from there, so its heading " +
                 "at the stop - the wall's normal - is the hedge's approach and the wall stands in the hedge's plane.")]
        [Min(10f)] public float aiRunUp = 140f;

        [Tooltip("The AI stops only within this many units of the hedge's line (sideways off its approach).")]
        [Min(5f)] public float aiStopLateral = 60f;

        [Tooltip("A raid stops this far short of the rival mass it is going for (u).")]
        [Min(1f)] public float aiRaidStopShort = 25f;

        [Tooltip("Seconds before an AI presses the stop toggle again when the hull did not change stance.")]
        [Min(0.2f)] public float aiStanceRetrySeconds = 1.5f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder; 0 for an
        /// empty one.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
