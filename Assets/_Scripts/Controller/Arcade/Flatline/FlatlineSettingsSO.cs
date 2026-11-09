using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Flatline's tunables other than the point target (EndConditionOverridesSO, per intensity) and the point values
    /// (FlatlineScoringRuleSO): progress feedback, the crystal field on the seams, and the AI hunters. Config
    /// separation - authored by <c>Tools/Build/author_flatline_assets.py</c>, never by hand.
    ///
    /// <para>The crystal SITES are the arena's (<c>SpawnableFlatline.CollectCrystalSites</c>: on the seams where panes
    /// cross), so they move with the panes at every intensity; this asset only says how fast a taken one grows back.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Each ladder is monotone toward
    /// harder; the generator asserts it.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "FlatlineSettings",
        menuName = "ScriptableObjects/Arcade/FlatlineSettings")]
    public class FlatlineSettingsSO : ScriptableObject
    {
        [Header("Progress feedback")]
        [Tooltip("Seconds between the field's regrowth checks and the hint's idle check.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Header("Crystal field (on the seams)")]
        [Tooltip("Seconds before a collected crystal grows back in its place. Every crystal is a spare ring for the " +
                 "Brittle Star, so this is how often the seams re-arm the cell.")]
        [Min(0.5f)] public float fieldRegrowSeconds = 10f;

        [Tooltip("Seed for each site's element, drawn from the elemental crystal set. Every peer draws alike.")]
        public int fieldSeed = 84;

        [Header("AI hunters (index = AI difficulty - 1: Easy, Medium, Hard, Legendary)")]
        [Tooltip("Seconds between a hunter's choices of rival. Shorter is sharper.")]
        public float[] aiRetargetSeconds = { 1.6f, 1.1f, 0.7f, 0.4f };

        [Tooltip("Seconds of the rival's flight the hunter leads it by when it steers (0 = straight at it). Longer " +
                 "cuts the rival off instead of chasing its tail.")]
        public float[] aiLeadSeconds = { 0f, 0.25f, 0.5f, 0.75f };

        [Tooltip("A hunter with no spare ring detours to a crystal this close (u). Farther than this it keeps " +
                 "hunting with the cords and guns it has.")]
        public float[] aiCrystalDetourRange = { 120f, 180f, 260f, 340f };

        [Tooltip("How much nearer a HUMAN rival counts when a hunter chooses whom to chase (2 = half the distance).")]
        [Min(1f)] public float aiHumanFocus = 1.5f;

        /// <summary>The ladder's value for a seat's AI level (1-based), clamped to the ladder's ends.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
