using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tapestry's tunables other than the round length (EndConditionOverridesSO): the elemental
    /// crystal scatter, the lead feedback and the AI's paint/raid policy. Config separation -
    /// authored by <c>Tools/Build/author_tapestry_assets.py</c>, never by hand.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TapestrySettings",
        menuName = "ScriptableObjects/Arcade/TapestrySettings")]
    public class TapestrySettingsSO : ScriptableObject
    {
        [Header("Elemental crystal pickups")]
        [Tooltip("Elemental crystals scattered through the cell at the start, BY INTENSITY (1-4). " +
                 "The Barren cell grows no lifeforms, so these are the ONLY element progression in " +
                 "the match - which makes their count the mode's intensity dial: a MASS crystal " +
                 "widens your brush (Mass mode's width is 5x at Mass 0, 20x at 15), which on this " +
                 "mode is directly score, so scarce crystals mean a narrower, more contested board.")]
        public int[] elementalCrystalCountByIntensity = { 24, 16, 10, 6 };

        /// <summary>The scatter count for <paramref name="intensity"/> (1-based, clamped).</summary>
        public int CrystalCountFor(int intensity)
        {
            if (elementalCrystalCountByIntensity == null || elementalCrystalCountByIntensity.Length == 0) return 0;
            int i = Mathf.Clamp(intensity, 1, elementalCrystalCountByIntensity.Length) - 1;
            return Mathf.Max(0, elementalCrystalCountByIntensity[i]);
        }

        [Tooltip("Radius of the scatter shell (equal-volume draw). Inside the Barren membrane " +
                 "(1200) and outside nothing in particular - the cell is bare.")]
        [Min(1f)] public float crystalScatterRadius = 900f;

        [Tooltip("Deterministic scatter seed - every peer lays the same crystals.")]
        public int crystalScatterSeed = 60;

        [Header("Lead feedback")]
        [Tooltip("Seconds into the round before a lead change is announced at all - in the opening " +
                 "every domain is at almost nothing and the lead flips on every key laid.")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 20f;

        [Tooltip("Minimum seconds between two lead-change toasts. The score is a LIVE stock, so " +
                 "two close domains trade the lead back and forth and an unthrottled toast would " +
                 "be spam.")]
        [Min(1f)] public float leadAnnounceMinGapSeconds = 10f;

        [Tooltip("Seconds between server-side lead samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 1f;

        [Header("AI - paint or raid")]
        [Tooltip("Seconds between an AI's paint/raid decisions. Slow on purpose: a mode switch " +
                 "costs the wake's 1.5 s blend each way, so a bot that flip-flopped would do " +
                 "neither well.")]
        [Min(0.5f)] public float aiDecisionSeconds = 6f;

        [Tooltip("An AI RAIDS (Dust mode, flies at the densest opposing mass) while its domain " +
                 "trails the best rival domain by more than this FRACTION of that rival's volume, " +
                 "and PAINTS (Mass mode) otherwise. A fraction, so it means the same thing at the " +
                 "start of the round and at the end.")]
        [Range(0f, 1f)] public float aiRaidDeficitFraction = 0.1f;

        [Tooltip("An AI raids in the closing seconds regardless - the round's last word is always " +
                 "a raid, because mass laid then counts for less than mass taken.")]
        [Min(0f)] public float aiLateRaidSeconds = 25f;

        [Tooltip("How far ABOVE the opposing mass (along its OWN up axis) a raiding AI aims its " +
                 "hull, so the mass passes through the dust capsule hanging below it rather than " +
                 "into the hull. Half the resting 60-unit capsule.")]
        [Min(0f)] public float aiDustHover = 30f;

        [Tooltip("A painting AI flies a slow orbit of the cell at this radius (a fraction of the " +
                 "scatter shell), detouring for an elemental crystal within aiCrystalReach.")]
        [Range(0.1f, 1f)] public float aiPaintOrbitFraction = 0.6f;

        [Tooltip("Degrees per second the painting orbit's waypoint advances. Under the Butterfly's " +
                 "45 deg/s turn by far, so the loop is flyable and the wake it lays is a broad arc.")]
        [Range(1f, 30f)] public float aiPaintOrbitDegreesPerSecond = 6f;

        [Tooltip("A painting AI detours for an uncollected elemental crystal within this many units.")]
        [Min(0f)] public float aiCrystalReach = 450f;

        [Tooltip("Seconds an AI waits for a mode-switch press to round-trip before asking again.")]
        [Min(0.1f)] public float aiModeRetrySeconds = 1f;
    }
}
