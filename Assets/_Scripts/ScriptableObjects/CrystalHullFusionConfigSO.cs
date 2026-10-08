using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Which (vessel, element) pairs FUSE a collected elemental crystal onto the hull instead of
    /// playing the generic capture flourish, and how each one feels. One asset for the game at
    /// <c>Resources/CrystalHullFusionConfig</c>; a pair with no entry plays the generic capture
    /// (<see cref="CrystalCaptureConfigSO"/>) exactly as before, so the asset IS the opt-in.
    ///
    /// The fusion runs in four beats (record: <c>Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md</c>):
    ///
    ///   1. PEEL     - each of the crystal's solids folds into its outer face, and those faces lift
    ///                 off the crystal as separate panels. The crystal stays where it was taken.
    ///   2. FLIGHT   - every panel flies to its own patch of the hull and comes down onto it along
    ///                 the hull's normal, its corners landing on the hull model's own vertices.
    ///   3. MATE     - the panels lie on the skin wearing the hull's normals, flare, and the charge
    ///                 discharge runs continuously round their edges.
    ///   4. DISSOLVE - they sink a hair into the skin and dissolve, in the pilot's domain colour.
    ///
    /// An entry is per (vessel, element) because the request is "a different one for each vessel
    /// element combo": the same mechanism, tuned per hull and per crystal.
    /// </summary>
    [CreateAssetMenu(fileName = "CrystalHullFusionConfig", menuName = "ScriptableObjects/" + nameof(CrystalHullFusionConfigSO))]
    public class CrystalHullFusionConfigSO : ScriptableObject
    {
        public const string ResourcePath = "CrystalHullFusionConfig";

        /// <summary>Which beat of the fusion a given elapsed time falls in.</summary>
        public enum Phase { Peel = 0, Flight = 1, Mate = 2, Dissolve = 3, Done = 4, Approach = 5 }

        [Serializable]
        public class Entry
        {
            [Header("Pair")]
            [Tooltip("The hull this fusion plays on.")]
            public VesselClassType vessel = VesselClassType.Squirrel;

            [Tooltip("The crystal element this fusion plays for.")]
            public Element element = Element.Charge;

            [Header("Bake")]
            [Tooltip("The edit-time solution for this pair - written by FrogletTools > Vessels > Bake " +
                     "Crystal Hull Fusions. With a current bake a pickup does no geometry at all. Empty " +
                     "or stale, the game solves it on a worker thread at runtime instead (and says so).")]
            public CrystalHullFusionBakeSO bake;

            [Header("Approach")]
            [Tooltip("How far from the hull's centre the faces peel, in hull radii (measured on the " +
                     "posed hull). A crystal collected farther out than this - the Sparrow and Grizzly " +
                     "skim crystals 30 units away - first flies in WHOLE to this distance, keeping pace " +
                     "with the hull, and peels there; nearer, it peels where it was taken.")]
            [Min(1f)] public float approachStandoff = 2.5f;

            [Tooltip("Seconds a far-collected crystal takes to fly in to the standoff before it peels. " +
                     "Added in front of the four beats only when the crystal was that far out.")]
            [Min(0.01f)] public float approachSeconds = 0.22f;

            [Header("Timing (seconds)")]
            [Tooltip("Solids fold into their outer faces and the faces lift off the crystal.")]
            [Min(0.01f)] public float peelSeconds = 0.16f;

            [Tooltip("Every face flies to its patch of the hull and comes down onto it.")]
            [Min(0.01f)] public float flightSeconds = 0.42f;

            [Tooltip("The faces lie on the skin, flaring and discharging.")]
            [Min(0f)] public float mateSeconds = 0.3f;

            [Tooltip("The faces sink into the skin and dissolve.")]
            [Min(0.01f)] public float dissolveSeconds = 0.3f;

            [Tooltip("Slow-motion for inspection: every beat is multiplied by this. 1 = authored " +
                     "speed. Set 10 to watch the faces land one by one; never ship it above 1.")]
            [Range(1f, 20f)] public float playbackScale = 1f;

            [Header("Peel")]
            [Tooltip("How far the faces lift off the crystal before they fly, in crystal radii.")]
            [Range(0f, 2f)] public float peelDistance = 0.45f;

            [Header("Flight")]
            [Tooltip("How far out from the skin a face swings before it comes down onto its patch, " +
                     "in hull mean half-extents. The curve ends travelling straight down the hull " +
                     "normal, so a face lands on the skin instead of sliding in through the hull.")]
            [Range(0f, 3f)] public float flightBow = 0.9f;

            [Tooltip("How much of the flight is spent staggering faces: the face nearest the hull " +
                     "lands first, the far side last. 0 = all at once.")]
            [Range(0f, 0.9f)] public float flightStagger = 0.45f;

            [Tooltip("Half-angle of the cone the CONTACT patch is searched in, round the direction " +
                     "the crystal came from.")]
            [Range(1f, 45f)] public float spotConeDegrees = 12f;

            [Header("Landing")]
            [Tooltip("A landed face's radius against half the gap to its nearest neighbour. 1 = " +
                     "neighbouring faces just touch at the tightest spacing; above 1 they overlap " +
                     "into a continuous skin.")]
            [Range(0.3f, 2f)] public float tileFill = 1.15f;

            [Tooltip("Gap between a landed face and the skin, in landed-face radii, so the two " +
                     "never z-fight.")]
            [Range(0f, 0.3f)] public float surfaceLift = 0.04f;

            [Header("Mate")]
            [Tooltip("Brightness multiplier as the faces land. Hue preserved (Docs/PALETTE.md) - a " +
                     "flare, never a wash toward white.")]
            [Min(1f)] public float flareGain = 2.6f;

            [Tooltip("Charge-shader discharge intensity multiplier while the faces lie on the skin. " +
                     "Ignored by a crystal shader with no _ArcIntensity.")]
            [Min(1f)] public float arcBoost = 2f;

            [Tooltip("Charge-shader discharge silence while the faces lie on the skin. 0 = every " +
                     "edge fires continuously. Ignored by a crystal shader with no _ArcDuty.")]
            [Range(0f, 0.95f)] public float mateArcDuty = 0f;

            [Header("Dissolve")]
            [Tooltip("How deep the faces sink into the skin as they dissolve, in landed-face radii.")]
            [Range(0f, 1f)] public float sinkDepth = 0.12f;

            [Header("Colour")]
            [Tooltip("Carry the crystal's colour pair onto the pilot's DOMAIN crystal pair over the " +
                     "flight - the pickup becomes the pilot's own as it joins the hull. Off keeps the " +
                     "crystal's own colour the whole way.")]
            public bool convergeToDomainColour = true;

            public float Scale => Mathf.Max(1f, playbackScale);

            /// <summary>Total wall-clock length of the fusion.</summary>
            public float TotalSeconds => (peelSeconds + flightSeconds + mateSeconds + dissolveSeconds) * Scale;

            /// <summary>Seconds from start to the first moment every face is down — when the pickup
            /// sound belongs.</summary>
            public float MateSecondsFromStart => (peelSeconds + flightSeconds) * Scale;

            /// <summary>
            /// Resolves an elapsed fusion time into its beat and that beat's normalised progress.
            /// Pure, so the sequence is testable without play mode.
            /// </summary>
            public Phase Resolve(float elapsed, out float u)
            {
                elapsed /= Scale;
                if (elapsed < peelSeconds) { u = Mathf.Clamp01(elapsed / peelSeconds); return Phase.Peel; }
                elapsed -= peelSeconds;
                if (elapsed < flightSeconds) { u = Mathf.Clamp01(elapsed / flightSeconds); return Phase.Flight; }
                elapsed -= flightSeconds;
                if (elapsed < mateSeconds) { u = mateSeconds > 0f ? Mathf.Clamp01(elapsed / mateSeconds) : 1f; return Phase.Mate; }
                elapsed -= mateSeconds;
                if (elapsed < dissolveSeconds) { u = Mathf.Clamp01(elapsed / dissolveSeconds); return Phase.Dissolve; }
                u = 1f;
                return Phase.Done;
            }

            /// <summary>
            /// One face's own flight progress at overall flight progress <paramref name="u"/>. Each
            /// face gets the same LENGTH of travel, offset by its <paramref name="delay01"/>
            /// (0 = lands first, 1 = last), so a late face is not also a faster one.
            /// </summary>
            public float FaceFlightProgress(float u, float delay01)
            {
                float stagger = Mathf.Clamp01(flightStagger);
                float span = Mathf.Max(1e-4f, 1f - stagger);
                return Mathf.Clamp01((u - Mathf.Clamp01(delay01) * stagger) / span);
            }
        }

        [Tooltip("Every (vessel, element) pair that fuses. A pair listed twice uses the first entry.")]
        [SerializeField] List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>The fusion for this pair, or false when the pair plays the generic capture.</summary>
        public bool TryGet(VesselClassType vessel, Element element, out Entry entry)
        {
            foreach (var candidate in entries)
            {
                if (candidate == null || candidate.vessel != vessel || candidate.element != element) continue;
                entry = candidate;
                return true;
            }
            entry = null;
            return false;
        }

        public static float EaseIn(float u) { u = Mathf.Clamp01(u); return u * u; }
        public static float EaseOut(float u) { u = Mathf.Clamp01(u); return 1f - (1f - u) * (1f - u); }
        public static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

        static CrystalHullFusionConfigSO _cached;
        static bool _warnedMissing;

        /// <summary>
        /// The shipped asset, or null when it is missing. Null is a legal answer — every pair then
        /// plays the generic capture — but it is warned once, because a missing asset silently
        /// switches every fusion off and nothing else would say so.
        /// </summary>
        public static CrystalHullFusionConfigSO Load()
        {
            if (_cached) return _cached;
            _cached = Resources.Load<CrystalHullFusionConfigSO>(ResourcePath);
            if (!_cached && !_warnedMissing)
            {
                _warnedMissing = true;
                CSDebug.LogWarning($"[CrystalHullFusion] Resources/{ResourcePath} is missing - no " +
                    "vessel will fuse a crystal onto its hull; every pickup plays the generic capture. " +
                    $"Restore it, or create one via Assets > Create > ScriptableObjects > {nameof(CrystalHullFusionConfigSO)}.");
            }
            return _cached;
        }
    }
}
