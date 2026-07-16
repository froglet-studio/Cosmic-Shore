using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>Built-in painting generators (see <see cref="PaintingPresetLibrary"/>).</summary>
    public enum PaintingPreset
    {
        None = 0,
        Star = 1,       // single stroke, single domain - the gentle low end
        Rainbow = 2,    // three arcs, one per domain - teaches the domain gates
        Saturn = 3,     // planet + tilted rings - first genuinely 3D painting
        TajMahal = 4,   // the monument: ~55 strokes, three domains, hours of flying

        // ── Grandiose non-planar constructions (make the Taj look like a warm-up) ──
        // Numeric values are LOCKED - never renumber (they are the progress-save key surface).
        Nautilus = 5,       // chambered logarithmic-spiral shell, 3D whorl
        Lotus = 6,          // phyllotaxis petal rings opening in true 3D
        Buckyball = 7,      // truncated-icosahedron soccer ball: 12 pentagons + 20 hexagons, 90 edges
        TorusKnot = 8,      // a (3,2)/(2,5) knot woven on a torus - hypnotic when spun
        DoubleHelix = 9,    // DNA: two phase-offset helices + base-pair rungs
        SpiralGalaxy = 10,  // log-spiral arms + an impressionist starfield disk & halo
        LionsHead = 11,     // a head volume with hundreds of curl-field mane strokes
        Phoenix = 12,       // firebird: body, two feathered wings, an impressionist flame tail
        Peacock = 13,       // a fanned 3D tail of eye-feather strokes
        Rose = 14,          // nested spiral bloom of curved petals
        StarryNight = 15,   // Van Gogh, stepped into: swirl sky, star vortices, moon, cypress, village
        BobRossVista = 16,  // a mountain landscape you fly into: fractal ridges, firs, mirror lake, sun
    }

    /// <summary>
    /// One stroke of a painting: an ordered polyline the vessel flies while its trail paints,
    /// plus the domain (trail colour) the stroke is meant to be painted in. Strokes are separated
    /// by pen-up flight; each stroke begins at a start gate that requests its domain.
    /// </summary>
    [Serializable]
    public class PaintingStroke
    {
        [Tooltip("Player-facing stroke name, shown at the stroke's start gate (e.g. 'North Minaret').")]
        public string name = "Stroke";

        [Tooltip("Domain (trail colour) this stroke is painted in. The start gate requests this domain " +
                 "via the server-authoritative pick RPC - never a client-local write.")]
        public Domains domain = Domains.Jade;

        [Tooltip("Ordered local-space points the vessel flies through. Paintings are authored with " +
                 "their base at y=0 and their front facing +Z.")]
        public List<Vector3> points = new();
    }

    /// <summary>
    /// A multi-stroke, multi-domain "connect the dots" painting for the freestyle Painting toy.
    /// The vessel's own trail does the painting (conserved mass - no caps/TTLs); strokes are
    /// flown in author order, each opened by a start gate that sets the stroke's domain colour.
    ///
    /// Authoring sources, first non-empty wins:
    ///   1. <see cref="strokes"/> authored directly in the inspector,
    ///   2. <see cref="sourceShape"/> - a legacy <see cref="ShapeDefinition"/> converted at runtime
    ///      (pen-up gaps split it into strokes),
    ///   3. <see cref="preset"/> - generated procedurally at <see cref="presetSize"/>.
    /// Generated strokes live in a runtime cache; the asset itself is never mutated.
    /// </summary>
    [CreateAssetMenu(fileName = "Painting_New", menuName = "ScriptableObjects/Toys/Painting Definition")]
    public class PaintingDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Stable id used as the progress-save key. Never reuse or rename once " +
                                 "shipped. Falls back to the asset name when empty.")]
        string paintingId;

        [SerializeField, Tooltip("Player-facing name shown on the painting's toy and gates.")]
        string displayName = "Painting";

        [SerializeField, TextArea, Tooltip("Short description of the painting.")]
        string description;

        [Header("Source (first non-empty wins: strokes → sourceShape → preset)")]
        [SerializeField, Tooltip("Hand-authored strokes. Leave empty to convert sourceShape or generate the preset.")]
        List<PaintingStroke> strokes = new();

        [SerializeField, Tooltip("Optional ShapeDefinition to convert into a painting (pen-up gaps become " +
                                 "stroke boundaries). Lets every existing shape asset become a painting.")]
        ShapeDefinition sourceShape;

        [SerializeField, Tooltip("Domain used for every stroke converted from sourceShape.")]
        Domains sourceShapeDomain = Domains.Gold;

        [SerializeField, Tooltip("Scale applied to sourceShape waypoints (shapes are authored in a ~200-unit box).")]
        float sourceShapeScale = 2.5f;

        [SerializeField, Tooltip("Built-in generator used when no strokes/sourceShape are authored.")]
        PaintingPreset preset = PaintingPreset.Star;

        [SerializeField, Tooltip("Characteristic size (width) of the generated preset, world units.")]
        float presetSize = 420f;

        [Header("Painting")]
        [SerializeField, Tooltip("Base distance for advancing to the next point, world units. The runner " +
                                 "tightens this automatically on fine-detail strokes.")]
        float reachThreshold = 30f;

        // Runtime-resolved strokes for sourceShape / preset paintings - the asset is never mutated.
        List<PaintingStroke> _resolved;
        bool _boundsComputed;
        Bounds _localBounds;

        public string PaintingId => string.IsNullOrEmpty(paintingId) ? name : paintingId;
        public string DisplayName => displayName;
        public string Description => description;
        public float ReachThreshold => Mathf.Max(4f, reachThreshold);

        /// <summary>
        /// The painting's DRAWABLE strokes (authored, converted, or generated; degenerate
        /// &lt;2-point strokes filtered out). Every consumer - toy labels, the runner, the
        /// progress store's totals - must use this list so stroke counts always agree.
        /// Never null; may be empty.
        /// </summary>
        public IReadOnlyList<PaintingStroke> Strokes
        {
            get
            {
                EnsureStrokes();
                return _resolved;
            }
        }

        /// <summary>Local-space bounds over every stroke point (base at y=0, front toward +Z).</summary>
        public Bounds LocalBounds
        {
            get
            {
                EnsureStrokes();
                if (!_boundsComputed)
                {
                    _localBounds = PaintingPresetLibrary.ComputeBounds(_resolved);
                    _boundsComputed = true;
                }
                return _localBounds;
            }
        }

        /// <summary>Resolve strokes from the first non-empty source. Safe to call repeatedly.</summary>
        public void EnsureStrokes()
        {
            if (_resolved != null) return;

            List<PaintingStroke> source;
            if (strokes != null && strokes.Count > 0)
                source = strokes;
            else if (sourceShape)
                source = PaintingPresetLibrary.FromShape(sourceShape, sourceShapeDomain, sourceShapeScale);
            else if (preset != PaintingPreset.None)
                source = PaintingPresetLibrary.Generate(preset, presetSize);
            else
                source = new List<PaintingStroke>();

            // Filter to drawable strokes ONCE, here - if writer (runner) and readers (toy label,
            // progress store) filtered independently, their totals could disagree and the
            // total-mismatch guard in PaintingProgressStore would silently reset progress.
            bool allDrawable = true;
            foreach (var s in source)
                if (s?.points == null || s.points.Count < 2) { allDrawable = false; break; }

            if (allDrawable)
            {
                _resolved = source;
            }
            else
            {
                _resolved = new List<PaintingStroke>(source.Count);
                foreach (var s in source)
                    if (s?.points != null && s.points.Count >= 2)
                        _resolved.Add(s);
            }

            // Flight-continuity pass: each stroke starts near where the previous one ended
            // (domain-contiguous, curvier strokes deferred on near-ties). Applies to every
            // source in this one funnel, returns a NEW list (the serialized asset is never
            // mutated), and is deterministic so progress-store stroke indices stay stable.
            _resolved = PaintingStrokeToolkit.OrderForFlightContinuity(_resolved);
        }

        /// <summary>
        /// Seed a runtime-synthesised painting (the zero-config default gallery). Editor-authored
        /// assets set these fields in the inspector; this exists only for code-built defaults.
        /// </summary>
        internal void SetRuntimeData(string id, string paintingName, string paintingDescription,
            PaintingPreset paintingPreset, float size, float reach)
        {
            paintingId = id;
            displayName = paintingName;
            description = paintingDescription;
            preset = paintingPreset;
            presetSize = size;
            reachThreshold = reach;
        }
    }
}
