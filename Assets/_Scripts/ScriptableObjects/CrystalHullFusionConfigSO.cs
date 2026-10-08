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
    ///   1. APPROACH - the crystal, still whole, is pulled to the hull side it came from, shrinking
    ///                 and turning with the vessel as it closes.
    ///   2. WRAP     - it opens: every plate slides round the hull to its own spot, the plate that
    ///                 touched staying at the contact and the far plates wrapping to the far side.
    ///   3. HOLD     - the plates sit flush on the skin and flare; the charge discharge fires
    ///                 continuously across them.
    ///   4. SINK     - they flatten into the hull and dissolve, now in the pilot's domain colour.
    ///
    /// An entry is per (vessel, element) because the request is "a different one for each vessel
    /// element combo": the same mechanism, tuned per hull and per crystal.
    /// </summary>
    [CreateAssetMenu(fileName = "CrystalHullFusionConfig", menuName = "ScriptableObjects/" + nameof(CrystalHullFusionConfigSO))]
    public class CrystalHullFusionConfigSO : ScriptableObject
    {
        public const string ResourcePath = "CrystalHullFusionConfig";

        /// <summary>Which beat of the fusion a given elapsed time falls in.</summary>
        public enum Phase { Approach = 0, Wrap = 1, Hold = 2, Sink = 3, Done = 4 }

        [Serializable]
        public class Entry
        {
            [Header("Pair")]
            [Tooltip("The hull this fusion plays on.")]
            public VesselClassType vessel = VesselClassType.Squirrel;

            [Tooltip("The crystal element this fusion plays for.")]
            public Element element = Element.Charge;

            [Header("Timing (seconds)")]
            [Tooltip("The whole crystal is pulled onto the hull.")]
            [Min(0.01f)] public float approachSeconds = 0.22f;

            [Tooltip("The crystal opens and its plates slide round the hull to their spots.")]
            [Min(0.01f)] public float wrapSeconds = 0.34f;

            [Tooltip("The plates sit flush on the skin, flaring and discharging.")]
            [Min(0f)] public float holdSeconds = 0.24f;

            [Tooltip("The plates flatten into the hull and dissolve.")]
            [Min(0.01f)] public float sinkSeconds = 0.3f;

            [Header("Approach")]
            [Tooltip("Size of the crystal when it lands, as a fraction of the hull's mean half-extent.")]
            [Range(0.05f, 1.5f)] public float landRadiusFraction = 0.35f;

            [Tooltip("Acceleration exponent of the pull. 1 = linear drag; higher hangs, then snaps in.")]
            [Min(1f)] public float approachAcceleration = 2.4f;

            [Tooltip("Extra size the crystal pops to at the very start of the pull (0 = none).")]
            [Range(0f, 1f)] public float approachPop = 0.25f;

            [Tooltip("WHOLE turns the crystal tumbles through on its way in. Whole, because the wrap " +
                     "is planned against the crystal's orientation at collection - a fractional turn " +
                     "would land each plate somewhere other than where its spot was chosen from.")]
            [Min(0)] public int approachSpinTurns = 1;

            [Header("Wrap")]
            [Tooltip("A landed plate's footprint against the gap to its nearest neighbour. 1 = " +
                     "neighbouring plates just touch at the tightest spacing; above 1 they overlap " +
                     "into a continuous skin, below 1 they read as separate scales.")]
            [Range(0.3f, 2f)] public float tileFill = 1.1f;

            [Tooltip("A landed plate's thickness against its footprint. Below 1 the plate lies flatter " +
                     "on the skin than the crystal's prisms stood.")]
            [Range(0.05f, 1f)] public float flatten = 0.45f;

            [Tooltip("How far the plates bow OUT from the hull mid-wrap, in the hull's normalised " +
                     "radius, so they slide round the skin rather than through it.")]
            [Range(0f, 1f)] public float wrapLift = 0.3f;

            [Tooltip("How much of the wrap is spent staggering plates. Plates nearest the contact " +
                     "move first and the far side closes last. 0 = all at once.")]
            [Range(0f, 0.9f)] public float wrapStagger = 0.4f;

            [Tooltip("Half-angle of the cone the crystal looks for its CONTACT spot in, round the " +
                     "direction it came from. Within the cone the outermost skin vertex wins.")]
            [Range(1f, 45f)] public float spotConeDegrees = 12f;

            [Tooltip("Gap between a landed plate and the skin, in plate thicknesses, so the two never " +
                     "z-fight.")]
            [Range(0f, 0.5f)] public float surfaceLift = 0.05f;

            [Header("Hold")]
            [Tooltip("Brightness multiplier at the moment the plates clamp. Hue preserved " +
                     "(Docs/PALETTE.md) - this is a flare, never a wash toward white.")]
            [Min(1f)] public float flareGain = 2.6f;

            [Tooltip("How much the plates swell on the clamp beat (1 = none).")]
            [Range(1f, 1.5f)] public float clampPulse = 1.12f;

            [Tooltip("Charge-shader discharge intensity multiplier while the plates hold. " +
                     "Ignored by a crystal shader with no _ArcIntensity.")]
            [Min(1f)] public float arcBoost = 2f;

            [Tooltip("Charge-shader discharge silence while the plates hold. 0 = every edge fires " +
                     "continuously. Ignored by a crystal shader with no _ArcDuty.")]
            [Range(0f, 0.95f)] public float holdArcDuty = 0f;

            [Header("Sink")]
            [Tooltip("How deep the plates sink into the skin, in landed-plate thicknesses.")]
            [Range(0f, 3f)] public float sinkDepth = 1f;

            [Header("Colour")]
            [Tooltip("Carry the crystal's colour pair onto the pilot's DOMAIN crystal pair over the " +
                     "wrap - the pickup becomes the pilot's own as it joins the hull. Off keeps the " +
                     "crystal's own colour the whole way.")]
            public bool convergeToDomainColour = true;

            /// <summary>Total wall-clock length of the fusion.</summary>
            public float TotalSeconds => approachSeconds + wrapSeconds + holdSeconds + sinkSeconds;

            /// <summary>Seconds from start to the clamp — when the pickup sound belongs.</summary>
            public float ClampSeconds => approachSeconds + wrapSeconds;

            /// <summary>
            /// Resolves an elapsed fusion time into its beat and that beat's normalised progress.
            /// Pure, so the sequence is testable without play mode.
            /// </summary>
            public Phase Resolve(float elapsed, out float u)
            {
                if (elapsed < approachSeconds) { u = Mathf.Clamp01(elapsed / approachSeconds); return Phase.Approach; }
                elapsed -= approachSeconds;
                if (elapsed < wrapSeconds) { u = Mathf.Clamp01(elapsed / wrapSeconds); return Phase.Wrap; }
                elapsed -= wrapSeconds;
                if (elapsed < holdSeconds) { u = holdSeconds > 0f ? Mathf.Clamp01(elapsed / holdSeconds) : 1f; return Phase.Hold; }
                elapsed -= holdSeconds;
                if (elapsed < sinkSeconds) { u = Mathf.Clamp01(elapsed / sinkSeconds); return Phase.Sink; }
                u = 1f;
                return Phase.Done;
            }

            /// <summary>
            /// One plate's own wrap progress at overall wrap progress <paramref name="u"/>. Each
            /// plate gets the same LENGTH of travel, offset by its <paramref name="plateDelay01"/>
            /// (0 = the contact plate, 1 = the antipode), so a late plate is not also a faster one.
            /// </summary>
            public float PlateWrapProgress(float u, float plateDelay01)
            {
                float stagger = Mathf.Clamp01(wrapStagger);
                float span = Mathf.Max(1e-4f, 1f - stagger);
                return Mathf.Clamp01((u - Mathf.Clamp01(plateDelay01) * stagger) / span);
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
