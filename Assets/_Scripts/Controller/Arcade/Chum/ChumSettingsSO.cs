using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Chum's tunables other than the round length (EndConditionOverridesSO): who gets credit for a bite of the reef,
    /// the lead feedback and the AI foragers. The reef itself is <see cref="ChumReef"/> (pure, per intensity); the herds
    /// and the omni crystals are the cell's and the scene's. Config separation - authored by
    /// <c>Tools/Build/author_chum_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Every ladder is monotone
    /// toward harder; the generator asserts it and that each C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "ChumSettings",
        menuName = "ScriptableObjects/Arcade/ChumSettings")]
    public class ChumSettingsSO : ScriptableObject
    {
        [Header("Credit for a bite")]
        [Tooltip("A reef prism eaten by a creature is credited to the Angler whose school creature is nearest it, within " +
                 "this many units (a feeding creature holds its mouth on the prism while it eats). Farther than this, or " +
                 "a wild creature nearer, and the bite is nobody's.")]
        [Min(1f)] public float attributeRadius = 90f;

        [Header("Lead feedback")]
        [Tooltip("No lead-change toast before this many seconds into the round (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 15f;

        [Tooltip("At least this many seconds between two lead-change toasts.")]
        [Min(0f)] public float leadAnnounceMinGapSeconds = 8f;

        [Tooltip("Seconds between two looks at who leads.")]
        [Min(0.1f)] public float leadSampleSeconds = 1f;

        [Header("AI foragers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("The AI tames (omni crystals, then grazers under its lure) until its school holds this many creatures, " +
                 "then goes to the reef. A bigger school eats faster but costs the time spent taming.")]
        public float[] aiSchoolTarget = { 2f, 3f, 4f, 5f };

        [Tooltip("Seconds the AI holds the lure over a grazer it is taming.")]
        public float[] aiLureHoldSeconds = { 2f, 2.5f, 3f, 3.5f };

        [Tooltip("The farthest (u) the AI goes for an omni crystal (a creature for its school) rather than the reef.")]
        public float[] aiCrystalReach = { 700f, 1100f, 1500f, 2000f };

        [Tooltip("Seconds the AI stays on one shoal before it picks again (the nearest with prisms left). Shorter is " +
                 "sharper: it leaves a shoal the herds have stripped sooner.")]
        public float[] aiRetargetSeconds = { 9f, 7f, 5f, 4f };

        [Header("AI foragers - shared")]
        [Tooltip("The AI presses the lure once it is within this many units of the grazer it is taming.")]
        [Min(1f)] public float aiLureDistance = 70f;

        [Tooltip("The AI stands off a shoal at this distance (u) and lets the hull's own autopilot paint and throw at " +
                 "it: the throw needs room (AnglerLeapfrogAIPolicySO.minThrowDistance).")]
        [Min(0f)] public float aiStandOff = 220f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
