using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Haul's tuning (HAUL.md): the lead feedback and the AI hauler's policy. The field is <see cref="HaulField"/>; the
    /// round length lives in <c>EndConditionOverridesSO.haulRoundSeconds</c>. Every AI ladder is indexed by the seat's
    /// AI level (Easy, Medium, Hard, Legendary), never by the intensity (<c>AIDifficulty.cs</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Games/Haul Settings", fileName = "HaulSettings")]
    public class HaulSettingsSO : ScriptableObject
    {
        [Header("Lead feedback")]
        [Tooltip("Seconds into the round before a lead change is announced at all (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 20f;

        [Tooltip("The least gap, in seconds, between two lead-change toasts.")]
        [Min(1f)] public float leadAnnounceMinGapSeconds = 10f;

        [Tooltip("Seconds between the server's lead samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 1f;

        [Header("AI - haul or raid (ladders: Easy, Medium, Hard, Legendary)")]
        [Tooltip("Seconds between an AI's haul/raid decisions.")]
        [Min(0.5f)] public float aiDecisionSeconds = 5f;

        [Tooltip("An AI raids (flies its ball through a rival's hoard) while its domain trails the best rival by more " +
                 "than this fraction. 1 = never raids.")]
        public float[] aiRaidDeficitFraction = { 1f, 0.3f, 0.15f, 0.05f };

        [Tooltip("An AI raids in the last N seconds regardless of the score. 0 = never.")]
        public float[] aiLateRaidSeconds = { 0f, 15f, 25f, 35f };

        [Tooltip("Seconds between an AI re-picking its bale (it keeps one until the bale is hauled).")]
        public float[] aiRetargetSeconds = { 12f, 9f, 7f, 5f };

        [Tooltip("A hauling AI circles its bale at this many units, so the ball on its chain sweeps through the bale.")]
        [Min(10f)] public float aiOrbitRadius = 160f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder; 0 for an
        /// empty one.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
