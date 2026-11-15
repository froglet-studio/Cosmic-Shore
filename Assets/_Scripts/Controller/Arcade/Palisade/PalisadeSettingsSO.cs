using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Palisade's tunables other than the round length (EndConditionOverridesSO): the elemental crystal scatter, the lead
    /// feedback and the AI's build-or-raid policy. Config separation - authored by
    /// <c>Tools/Build/author_palisade_assets.py</c>, never by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "PalisadeSettings", menuName = "ScriptableObjects/Arcade/PalisadeSettings")]
    public class PalisadeSettingsSO : ScriptableObject
    {
        [Header("Elemental crystal pickups")]
        [Tooltip("Elemental crystals scattered through the cell at the start, by intensity (index = intensity - 1). A " +
                 "Mass crystal grows the afterimages (x1 -> x2.5 volume across Mass 0-10); at Mass 5 they are laid as " +
                 "danger. Fewer crystals = a harder rung.")]
        public int[] elementalCrystalCountByIntensity = { 24, 16, 10, 6 };

        /// <summary>The crystal count for <paramref name="intensity"/> (1-4, clamped).</summary>
        public int CrystalCountFor(int intensity)
        {
            if (elementalCrystalCountByIntensity == null || elementalCrystalCountByIntensity.Length == 0) return 0;
            int i = Mathf.Clamp(intensity - 1, 0, elementalCrystalCountByIntensity.Length - 1);
            return Mathf.Max(0, elementalCrystalCountByIntensity[i]);
        }

        [Tooltip("The crystals scatter through a ball this wide (u) round the cell centre, equal-volume.")]
        [Min(1f)] public float crystalScatterRadius = 900f;

        [Tooltip("Seed for the scatter. Every peer lays the same crystals from it - no network message.")]
        public int crystalScatterSeed = 98;

        [Header("Lead feedback")]
        [Tooltip("No lead-change toast before this many seconds into the round (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 20f;

        [Tooltip("Minimum seconds between two lead-change toasts: the score is a live stock and can swing often.")]
        [Min(1f)] public float leadAnnounceMinGapSeconds = 10f;

        [Tooltip("Seconds between the server's samples of who leads.")]
        [Min(0.1f)] public float progressSampleSeconds = 1f;

        [Header("AI - build or raid")]
        [Tooltip("Seconds between an AI's build-or-raid decisions.")]
        [Min(0.5f)] public float aiDecisionSeconds = 6f;

        [Tooltip("An AI RAIDS while its domain trails the best rival by more than this fraction of the rival's volume.")]
        [Range(0f, 1f)] public float aiRaidDeficitFraction = 0.1f;

        [Tooltip("In the last this-many seconds every AI raids, whatever the score.")]
        [Min(0f)] public float aiLateRaidSeconds = 25f;

        [Tooltip("A building AI orbits at this fraction of the crystal scatter radius.")]
        [Range(0.1f, 1f)] public float aiBuildOrbitFraction = 0.6f;

        [Tooltip("Degrees a second round its orbit: slow, so the trail and afterimages lie as a wall, not a line.")]
        [Range(1f, 30f)] public float aiBuildOrbitDegreesPerSecond = 6f;

        [Tooltip("A building AI detours for an elemental crystal within this distance (u).")]
        [Min(0f)] public float aiCrystalReach = 450f;
    }
}
