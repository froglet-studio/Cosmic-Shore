using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Honeycomb's tunables other than the round length (EndConditionOverridesSO): the elemental crystal scatter (the
    /// mode's intensity dial) and the lead feedback. Config separation - authored by
    /// <c>Tools/Build/author_honeycomb_assets.py</c>, never by hand.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HoneycombSettings",
        menuName = "ScriptableObjects/Arcade/HoneycombSettings")]
    public class HoneycombSettingsSO : ScriptableObject
    {
        [Header("Elemental crystal pickups")]
        [Tooltip("Elemental crystals scattered through the cell at the start, BY INTENSITY (1-4). The Barren cell grows " +
                 "no lifeforms, so these are the only element progression in the match. On the Honeyguide a MASS level " +
                 "brings the next Mass hive sooner and widens how near the hull must pass one (Hive Chain at Mass 5), " +
                 "so scarce crystals mean fewer hives and a narrower, more contested board.")]
        public int[] elementalCrystalCountByIntensity = { 24, 16, 10, 6 };

        [Tooltip("Radius of the equal-volume scatter round the cell's centre.")]
        [Min(1f)] public float crystalScatterRadius = 900f;

        [Tooltip("Seed of the scatter. Every peer lays the same crystals from it.")]
        public int crystalScatterSeed = 109;

        [Header("Lead feedback")]
        [Tooltip("Seconds into the round before a lead change is announced - the opening lead flips on every prism.")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 20f;

        [Tooltip("Minimum seconds between two lead-change toasts. The score is a LIVE stock, so a close race trades the " +
                 "lead often.")]
        [Min(1f)] public float leadAnnounceMinGapSeconds = 10f;

        [Tooltip("Seconds between server-side lead samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 1f;

        /// <summary>The scatter count for <paramref name="intensity"/> (1-based, clamped).</summary>
        public int CrystalCountFor(int intensity)
        {
            if (elementalCrystalCountByIntensity == null || elementalCrystalCountByIntensity.Length == 0) return 0;
            int i = Mathf.Clamp(intensity, 1, elementalCrystalCountByIntensity.Length) - 1;
            return Mathf.Max(0, elementalCrystalCountByIntensity[i]);
        }
    }
}
