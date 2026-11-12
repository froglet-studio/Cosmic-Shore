using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shatterplane's tunables other than the prism target (EndConditionOverridesSO, per intensity): the arena's nucleus,
    /// the crystal field and the AI. Config separation - authored by <c>Tools/Build/author_shatterplane_assets.py</c>,
    /// never by hand.
    ///
    /// <para><b>The nucleus is the ring's reach.</b> The Brittle Star's ring blast is a disk as wide as the nucleus of the
    /// cell it blows in (<c>BrittleStarExecutor.NucleusRadiusAt</c>). The mode never changes the vessel; it chooses its
    /// arena's nucleus, a little bigger than the standard cell's ~392 u (decision D2, Shombith 2026-11-10: "you can make
    /// it a little bigger"), and shows it: the nucleus sphere is exactly how far a ring reaches.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Each ladder is monotone toward
    /// harder; the generator asserts it.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShatterplaneSettings",
        menuName = "ScriptableObjects/Arcade/ShatterplaneSettings")]
    public class ShatterplaneSettingsSO : ScriptableObject
    {
        [Header("Arena")]
        [Tooltip("The arena's nucleus radius (u) - and so the radius of every ring blast in this cell. Play geometry, not " +
                 "a claim: the controller clears NucleusIsControlZone.")]
        [Min(1f)] public float nucleusRadius = 470f;

        [Header("Progress feedback")]
        [Tooltip("Seconds between the field's regrowth checks.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Header("Crystal field (on the plates' axes)")]
        [Tooltip("Seconds before a collected crystal grows back in its place. Every crystal is a spare ring for the " +
                 "Brittle Star, so this is how often a plate's axis re-arms a pilot.")]
        [Min(0.5f)] public float fieldRegrowSeconds = 8f;

        [Tooltip("Seed for each site's element, drawn from the elemental crystal set. Every peer draws alike.")]
        public int fieldSeed = 88;

        [Tooltip("Each intensity's crystal element (index = intensity - 1). None = each site draws a random element " +
                 "from fieldSeed. I3 is Space (Shombith, 2026-11-10: \"intensities 3 make elemental crystals of space " +
                 "just for teams present there\"). Space lengthens the Brittle Star's gun range and its heat capacity.")]
        public Element[] fieldElementByIntensity = { Element.None, Element.None, Element.Space, Element.None };

        [Tooltip("Each intensity's team rule (index = intensity - 1). On: every crystal site holds one crystal per team " +
                 "present in the match, tinted to that team and collectable ONLY by it (ElementalCrystalImpactor.RestrictTo), so " +
                 "no team can starve another of rings. Off: one crystal per site, anyone's.")]
        public bool[] fieldPerTeamByIntensity = { false, false, true, false };

        [Tooltip("How far apart a site's team crystals stand (u), across the plate's axis.")]
        [Min(0f)] public float teamCrystalSpacing = 30f;

        /// <summary>The crystal element authored for an intensity (1-based), or None for a random draw.</summary>
        public Element FieldElementFor(int intensity) =>
            fieldElementByIntensity is { Length: > 0 }
                ? fieldElementByIntensity[Mathf.Clamp(intensity - 1, 0, fieldElementByIntensity.Length - 1)]
                : Element.None;

        /// <summary>Whether an intensity (1-based) lays one crystal per team present.</summary>
        public bool FieldPerTeamFor(int intensity) =>
            fieldPerTeamByIntensity is { Length: > 0 } &&
            fieldPerTeamByIntensity[Mathf.Clamp(intensity - 1, 0, fieldPerTeamByIntensity.Length - 1)];

        [Header("AI (index = AI difficulty - 1: Easy, Medium, Hard, Legendary)")]
        [Tooltip("Seconds between an AI's choices of plate. Shorter is sharper.")]
        public float[] aiRetargetSeconds = { 2.4f, 1.8f, 1.2f, 0.8f };

        [Tooltip("An AI lines up on a plate from a point this far out along its axis (u) before flying in square. " +
                 "Farther gives it longer to settle square; nearer wastes less time.")]
        public float[] aiApproachDistance = { 260f, 300f, 340f, 380f };

        [Tooltip("How square (degrees off the plate's axis) an AI must be before it stops lining up and flies in. " +
                 "Smaller is squarer.")]
        public float[] aiSquareDegrees = { 25f, 18f, 12f, 8f };

        [Tooltip("An AI with no spare ring detours to a crystal this close (u) before it lines up on a plate.")]
        public float[] aiCrystalDetourRange = { 300f, 400f, 500f, 650f };

        /// <summary>The ladder's value for a seat's AI level (1-based), clamped to the ladder's ends.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
