using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The tunables of one of the Wrasse's three reef games (Cleaner Ram, Reefbuild, Blastline; WRASSEREEF.md) other than
    /// the end condition (EndConditionOverridesSO) and the point values (the scoring rule). One type, three assets, each
    /// authored by its <c>Tools/Build/author_&lt;mode&gt;_assets.py</c>, never by hand.
    ///
    /// <para><b>The game's lever is the crystal scatter.</b> The Wrasse's kite gets its motes from the board, and how often
    /// each element comes is the hull's own level in it (<c>KiteNavigator.PickElement</c>: base + perLevel x level). So a
    /// reef stocked with the game's element (<see cref="favouredElement"/>, <see cref="favouredShare"/> of the crystals)
    /// brings more of that element's motes to every kite that gathers it, without touching the vessel (LOCKED,
    /// Shombith 2026-10-31). Intensity is SCARCITY: fewer crystals per rung.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity (<c>AIDifficulty.cs</c>). The AI only steers the Grouper; the hull's own navigator AI flies the kite.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "WrasseReefSettings", menuName = "ScriptableObjects/Arcade/WrasseReefSettings")]
    public class WrasseReefSettingsSO : ScriptableObject
    {
        [Header("The reef's crystals (peer-local, deterministic)")]
        [Tooltip("The element this game is about. That share of the crystals is this element, so the kites that gather " +
                 "them see more of its motes on the board.")]
        public Element favouredElement = Element.Charge;

        [Tooltip("Chance (0-1) that a crystal is the favoured element; the rest are drawn from all four elements.")]
        [Range(0f, 1f)] public float favouredShare = 0.6f;

        [Tooltip("Crystals standing in the reef at once, per intensity (index = intensity - 1). Intensity is scarcity, " +
                 "so the list never rises.")]
        public int[] crystalCountByIntensity = { 24, 16, 10, 6 };

        [Tooltip("The crystals scatter through a ball this wide (u) round the arena cell's centre, equal volume per shell.")]
        [Min(0f)] public float crystalScatterRadius = 900f;

        [Tooltip("Lays the same scatter on every peer.")]
        public int crystalScatterSeed = 111;

        [Tooltip("Seconds after a crystal is taken before its slot regrows one, somewhere new (0 = never).")]
        [Min(0f)] public float crystalRegrowSeconds = 12f;

        [Header("Progress feedback")]
        [Tooltip("Seconds between the server's lead samples (a timed game's lead-change toast) and the AI's slow checks.")]
        [Min(0.1f)] public float progressSampleSeconds = 1f;

        [Tooltip("A timed game announces no lead change in its first this-many seconds (an opening scramble is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 20f;

        [Tooltip("Minimum seconds between two lead-change toasts.")]
        [Min(0f)] public float leadAnnounceMinGapSeconds = 10f;

        [Header("AI Grouper - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing what to fly at.")]
        public float[] aiRetargetSeconds = { 3f, 2f, 1.4f, 1f };

        [Tooltip("The farthest crystal (u) the AI detours for.")]
        public float[] aiCrystalReach = { 250f, 350f, 450f, 550f };

        [Tooltip("The farthest rival (u) the AI hunts (Cleaner Ram) or raids (Reefbuild).")]
        public float[] aiHuntRange = { 450f, 600f, 750f, 900f };

        [Tooltip("Seconds of a rival's own velocity the AI leads its nose by (the kite needs the rival on its board).")]
        public float[] aiLeadSeconds = { 0f, 0.2f, 0.35f, 0.5f };

        [Header("AI Grouper - shared")]
        [Tooltip("The AI's cruise orbit, as a fraction of the crystal scatter radius.")]
        [Range(0.05f, 1f)] public float aiOrbitFraction = 0.55f;

        [Tooltip("Degrees per second the cruise orbit turns.")]
        [Min(0f)] public float aiOrbitDegreesPerSecond = 6f;

        [Tooltip("Reefbuild: an AI whose domain trails the leader by more than this fraction flies at the leader's " +
                 "reef so its kite pops and blasts it.")]
        [Range(0f, 1f)] public float aiRaidDeficitFraction = 0.15f;

        [Tooltip("Reefbuild: in the last this-many seconds every AI raids.")]
        [Min(0f)] public float aiLateRaidSeconds = 20f;

        /// <summary>The ladder's value for an AI level (1-based), clamped to the ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }

        /// <summary>Crystals standing at once at an intensity (1-based), clamped to the list.</summary>
        public int CrystalCountFor(int intensity)
        {
            if (crystalCountByIntensity == null || crystalCountByIntensity.Length == 0) return 0;
            return Mathf.Max(0, crystalCountByIntensity[Mathf.Clamp(intensity - 1, 0, crystalCountByIntensity.Length - 1)]);
        }
    }
}
