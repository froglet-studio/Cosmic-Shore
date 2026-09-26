using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's hull, as a PURE function of its authored proportions (design:
    /// <c>R_VesselActions/BUTTERFLY.md</c> §2). No Unity objects, no scene, no randomness — feed
    /// it a <see cref="Settings"/> and it hands back a vertex soup. <see cref="ButterflyHullBuilder"/>
    /// owns everything that needs the scene.
    ///
    /// That split is the Scarab's (<see cref="ScarabHullForm"/>) and it is what makes the elemental
    /// morphs possible at all: the four element extremes are just <see cref="Generate"/> run at
    /// perturbed settings, so "level 7 Mass and level 3 Space" is the base build plus a weighted
    /// sum of per-vertex deltas — provided TOPOLOGY never moves. Topology here is a function of
    /// the INTEGER settings only (<see cref="Settings.SpanSegments"/>,
    /// <see cref="Settings.ChordSegments"/>, <see cref="Settings.BodySegments"/>,
    /// <see cref="Settings.BodySides"/>, <see cref="Settings.ScallopCount"/>). Every float may be
    /// morphed; no int may. <see cref="BakeMorphSet"/> asserts it rather than trusting it.
    ///
    /// <para><b>FIVE PARTS, and four of them are wings.</b> A butterfly has four wings, not two,
    /// and the forewing/hindwing pair is most of what reads as "butterfly" rather than "bird" or
    /// "manta" at the range this vessel is flown from. Each wing is its own part with its own root
    /// HINGE pivot, because <see cref="ButterflyAnimation"/> beats them about that hinge and a
    /// wing welded into the body mesh cannot beat. Part 0 (the Core — body and antennae) lives on
    /// the builder's own GameObject and therefore stays in hull space; see the builder.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour
    /// onto slot <b>1</b> of a MeshRenderer hull, so the wings are emitted into submesh 1 and the
    /// body into submesh 0. A butterfly whose BODY wore the team colour and whose wings were grey
    /// would be the wrong way round in the one place a pilot reads domain from across a cell.</para>
    /// </summary>
    public static class ButterflyHullForm
    {
        public const int BodySubmesh = 0;
        public const int WingSubmesh = 1;

        /// <summary>
        /// Every number the geometry depends on. Mirrors <see cref="ButterflyHullBuilder"/>'s
        /// serialized fields one-for-one — the builder is the authored home, this is how the
        /// values travel into the pure function. INTEGER fields decide topology and are never
        /// morphed; float fields move vertices only.
        /// </summary>
        public struct Settings
        {
            // ---- body ----
            public float BodyLength;      // nose-to-tail of the abdomen+thorax spindle
            public float BodyRadius;      // fattest half-width of the spindle
            public int BodySegments;      // rings along the body
            public int BodySides;         // radial resolution of the spindle

            // ---- wings (shared by all four; the hindwing scales off the forewing) ----
            public float Span;            // half-span of the FOREwing, from hinge to tip
            public float ChordRoot;       // forewing chord at the hinge
            public float ChordTip;        // forewing chord at the tip
            public float Sweep;           // how far BACK (-Z) the tip sits relative to the hinge
            public float Camber;          // bow of the membrane out of its own plane
            public float Dihedral;        // resting V, degrees above the horizontal
            public float HindScale;       // hindwing size as a fraction of the forewing
            public float HindSweep;       // extra rearward offset of the hindwing hinge
            public int ScallopCount;      // lobes along the trailing edge (0 = smooth)
            public float ScallopDepth;    // lobe depth as a fraction of the local chord
            public float TipFlare;        // how much the outer third widens before the tip
            public int SpanSegments;      // mesh resolution hinge → tip
            public int ChordSegments;     // mesh resolution leading → trailing edge

            // ---- antennae ----
            public float AntennaLength;   // 0 removes them (a FEATURE GATE — see the morph note)
            public float AntennaThickness;
        }

        /// <summary>
        /// The fleet's morph order (<c>VesselElementalMorphConfigSO</c>): charge, mass, space,
        /// time. Held here as an array rather than re-derived so the bake, the blend and the
        /// builder's <c>ApplyElementMorphWeights</c> can never disagree about index order.
        /// </summary>
        public static readonly Element[] MorphElements =
        {
            Element.Charge, Element.Mass, Element.Space, Element.Time,
        };

        /// <summary>
        /// One emitted piece. Geometry is in HULL space (the builder subtracts
        /// <see cref="Pivot"/> for a child part), so two builds of the same topology are
        /// directly comparable vertex-for-vertex.
        /// </summary>
        public sealed class Part
        {
            public string Name;
            public Vector3 Pivot;                       // hull space; a child's localPosition
            public readonly List<Vector3> Verts = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<int> Body = new();     // submesh 0
            public readonly List<int> Wing = new();     // submesh 1
        }

        /// <summary>Build the whole hull. Deterministic and allocation-bounded.</summary>
        public static List<Part> Generate(Settings s)
        {
            var parts = new List<Part>(5);
            parts.Add(BuildCore(s));
            // Order is load-bearing only in that it must be STABLE across builds — the morph
            // deltas are indexed by part. Left/right and fore/hind are emitted in a fixed order.
            parts.Add(BuildWing(s, side: -1, hind: false));
            parts.Add(BuildWing(s, side: +1, hind: false));
            parts.Add(BuildWing(s, side: -1, hind: true));
            parts.Add(BuildWing(s, side: +1, hind: true));
            return parts;
        }

        // ------------------------------------------------------------------ body

        /// <summary>
        /// The body: a spindle of revolution about +Z, fattest just behind the wing root, tapering
        /// to a blunt head and a pointed abdomen. Antennae are appended into the same part because
        /// they never move independently — only the wings beat.
        /// </summary>
        static Part BuildCore(Settings s)
        {
            var p = new Part { Name = "Core", Pivot = Vector3.zero };

            int rings = Mathf.Max(3, s.BodySegments);
            int sides = Mathf.Max(3, s.BodySides);
            float half = s.BodyLength * 0.5f;

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;                 // 0 = tail, 1 = head
                float z = Mathf.Lerp(-half, half, t);
                float radius = s.BodyRadius * SpindleProfile(t);

                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                    p.Verts.Add(new Vector3(cx * radius, cy * radius, z));
                    // Radial normal is exact for a surface of revolution up to the taper term;
                    // the taper is gentle enough that the radial read is visually right and, more
                    // importantly, it is a smooth function of the settings, which is what the
                    // morph deltas need (a recomputed-from-triangles normal is not).
                    p.Normals.Add(new Vector3(cx, cy, 0f).normalized);
                    p.Uvs.Add(new Vector2(i / (float)sides, t));
                }
            }

            int stride = sides + 1;
            for (int r = 0; r < rings; r++)
            for (int i = 0; i < sides; i++)
            {
                int a = r * stride + i, b = a + 1, c = a + stride, d = c + 1;
                p.Body.Add(a); p.Body.Add(c); p.Body.Add(b);
                p.Body.Add(b); p.Body.Add(c); p.Body.Add(d);
            }

            if (s.AntennaLength > 0.001f)
            {
                AppendAntenna(p, s, -1);
                AppendAntenna(p, s, +1);
            }

            return p;
        }

        /// <summary>Radius profile along the body, 0 = tail tip, 1 = head. Fattest at the thorax
        /// (~0.62), which is where the wings root.</summary>
        static float SpindleProfile(float t)
        {
            // A smooth single-humped profile that reaches ~0 at the tail and ~0.45 at the head,
            // so the abdomen tapers to a point and the head stays blunt.
            float hump = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            return Mathf.Lerp(0.18f, 1f, hump) * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(t * 1.6f));
        }

        /// <summary>A club-tipped antenna, as a thin tapered prism swept forward and outward.</summary>
        static void AppendAntenna(Part p, Settings s, int side)
        {
            const int segs = 4;
            const int sides = 4;
            int baseIndex = p.Verts.Count;
            Vector3 root = new(side * s.BodyRadius * 0.35f, s.BodyRadius * 0.45f, s.BodyLength * 0.42f);

            for (int g = 0; g <= segs; g++)
            {
                float t = g / (float)segs;
                // Swept forward, out and slightly up, with a club at the tip.
                Vector3 centre = root + new Vector3(
                    side * s.AntennaLength * 0.42f * t,
                    s.AntennaLength * 0.30f * Mathf.Sin(t * 1.2f),
                    s.AntennaLength * t);
                float radius = s.AntennaThickness * Mathf.Lerp(1f, 2.1f, Mathf.Pow(t, 4f));

                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    Vector3 n = new(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    p.Verts.Add(centre + n * radius);
                    p.Normals.Add(n);
                    p.Uvs.Add(new Vector2(i / (float)sides, t));
                }
            }

            int stride = sides + 1;
            for (int g = 0; g < segs; g++)
            for (int i = 0; i < sides; i++)
            {
                int a = baseIndex + g * stride + i, b = a + 1, c = a + stride, d = c + 1;
                p.Body.Add(a); p.Body.Add(c); p.Body.Add(b);
                p.Body.Add(b); p.Body.Add(c); p.Body.Add(d);
            }
        }

        // ------------------------------------------------------------------ wings

        /// <summary>
        /// One wing membrane, as a (span × chord) quad grid rooted at its hinge.
        ///
        /// The outline is the whole read, so it is built from three shaped terms rather than a
        /// rectangle: the LEADING edge sweeps back with span, the CHORD widens through a tip
        /// flare before collapsing at the tip, and the TRAILING edge is scalloped into lobes.
        /// Camber bows the membrane out of its own plane so it catches light as a surface rather
        /// than reading as a decal, and dihedral sets the resting V the wingbeat swings about.
        ///
        /// Emitted DOUBLE-SIDED (both windings) because a wing is a membrane with no thickness —
        /// a single-sided one vanishes whenever the camera crosses its plane, which at a butterfly's
        /// beat rate is several times a second.
        /// </summary>
        static Part BuildWing(Settings s, int side, bool hind)
        {
            string name = (hind ? "WingHind" : "WingFore") + (side < 0 ? "L" : "R");

            float scale = hind ? Mathf.Max(0.05f, s.HindScale) : 1f;
            float span = s.Span * scale;
            float chordRoot = s.ChordRoot * scale;
            float chordTip = s.ChordTip * scale;
            float sweep = s.Sweep * scale + (hind ? s.HindSweep : 0f);

            // The hinge sits on the flank at the thorax, the hindwing a little aft and below —
            // which is where a real hindwing roots, and it also stops the two membranes sharing a
            // plane, so the pair reads as four wings rather than one big one.
            Vector3 pivot = new(
                side * s.BodyRadius * 0.85f,
                hind ? -s.BodyRadius * 0.25f : s.BodyRadius * 0.15f,
                hind ? -s.BodyLength * 0.16f : s.BodyLength * 0.10f);

            var p = new Part { Name = name, Pivot = pivot };

            int su = Mathf.Max(2, s.SpanSegments);
            int sv = Mathf.Max(2, s.ChordSegments);
            float dihedral = (hind ? -0.45f : 1f) * s.Dihedral * Mathf.Deg2Rad;

            for (int iu = 0; iu <= su; iu++)
            {
                float u = iu / (float)su;                    // 0 at hinge, 1 at tip
                float chord = ChordAt(s, u, chordRoot, chordTip);
                // Leading edge runs forward-then-back: a butterfly's costa bows out before the
                // wing rakes away, which is what gives the silhouette its curve.
                float lead = Mathf.Sin(u * Mathf.PI * 0.6f) * chordRoot * 0.22f - sweep * u * u;
                float lift = Mathf.Sin(dihedral) * span * u;
                float reach = Mathf.Cos(dihedral) * span * u;

                for (int iv = 0; iv <= sv; iv++)
                {
                    float v = iv / (float)sv;                // 0 leading edge, 1 trailing edge
                    float trail = TrailingScallop(s, u, v);
                    float z = lead - chord * v * trail;

                    // Camber: a half-sine across the chord, tapering to nothing at the tip so the
                    // membrane closes cleanly instead of flaring into a scoop.
                    float bow = Mathf.Sin(v * Mathf.PI) * s.Camber * (1f - u * 0.55f) * scale;

                    p.Verts.Add(new Vector3(side * reach, lift + bow, z));
                    // Normal is the camber's own gradient, flipped per side so both wings face up.
                    float dBow = Mathf.Cos(v * Mathf.PI) * Mathf.PI * s.Camber * (1f - u * 0.55f) * scale;
                    Vector3 n = new Vector3(0f, 1f, dBow * 0.15f).normalized;
                    p.Normals.Add(n);
                    p.Uvs.Add(new Vector2(u, v));
                }
            }

            int stride = sv + 1;
            for (int iu = 0; iu < su; iu++)
            for (int iv = 0; iv < sv; iv++)
            {
                int a = iu * stride + iv, b = a + 1, c = a + stride, d = c + 1;
                if (side < 0)
                {
                    p.Wing.Add(a); p.Wing.Add(c); p.Wing.Add(b);
                    p.Wing.Add(b); p.Wing.Add(c); p.Wing.Add(d);
                    // back face
                    p.Wing.Add(a); p.Wing.Add(b); p.Wing.Add(c);
                    p.Wing.Add(b); p.Wing.Add(d); p.Wing.Add(c);
                }
                else
                {
                    p.Wing.Add(a); p.Wing.Add(b); p.Wing.Add(c);
                    p.Wing.Add(b); p.Wing.Add(d); p.Wing.Add(c);
                    p.Wing.Add(a); p.Wing.Add(c); p.Wing.Add(b);
                    p.Wing.Add(b); p.Wing.Add(c); p.Wing.Add(d);
                }
            }

            return p;
        }

        /// <summary>Chord length at span fraction <paramref name="u"/>, including the tip flare
        /// that makes the outer third broaden before it closes.</summary>
        static float ChordAt(Settings s, float u, float chordRoot, float chordTip)
        {
            float linear = Mathf.Lerp(chordRoot, chordTip, u);
            // The flare is a bump centred at u≈0.7 — a butterfly's widest point is well outboard,
            // and a wing that only narrows reads as a fin.
            float flare = 1f + s.TipFlare * Mathf.Exp(-Mathf.Pow((u - 0.70f) / 0.26f, 2f));
            // Close the tip so the membrane comes to a point rather than a blunt end.
            float close = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(u, 6f)));
            return linear * flare * close;
        }

        /// <summary>
        /// The trailing-edge lobe modulation, in [0,1] — multiplies the chord that vertex reaches.
        /// Returns 1 at the leading edge whatever the settings, so the costa is never scalloped.
        /// </summary>
        static float TrailingScallop(Settings s, float u, float v)
        {
            if (s.ScallopCount <= 0 || s.ScallopDepth <= 0.0001f) return 1f;
            // Lobes are indexed along the SPAN and only bite near the trailing edge (v → 1).
            float lobe = Mathf.Cos(u * s.ScallopCount * Mathf.PI * 2f);
            float bite = s.ScallopDepth * Mathf.Clamp01((v - 0.55f) / 0.45f);
            return 1f - bite * (0.5f - 0.5f * lobe);
        }

        // ------------------------------------------------------------------ elemental morphs

        /// <summary>One element's shape deltas for one part, in HULL space. The Butterfly's hinges
        /// are FIXED under every element (an element changes the wing, never where it is hinged),
        /// so unlike the Scarab there is no pivot delta to carry — asserted in
        /// <see cref="BakeMorphSet"/> rather than assumed.</summary>
        public sealed class PartMorphDelta
        {
            public Vector3[] VertDeltas;
            public Vector3[] NormalDeltas;
            public bool Any;
        }

        /// <summary>The base build plus the four element extremes, pre-differenced.</summary>
        public sealed class MorphSet
        {
            public List<Part> BaseParts;
            /// <summary>[element index, in <see cref="MorphElements"/> order][part index].</summary>
            public PartMorphDelta[][] Deltas;
            /// <summary>Per part: the box containing EVERY reachable weight combination, so an
            /// animated morph never pays a bounds recalculation and can never shrink culling.</summary>
            public Vector3[] BoundsMin;
            public Vector3[] BoundsMax;
            /// <summary>Per part: does any element move it at all? Parts that never move are
            /// skipped outright by the blend.</summary>
            public bool[] PartMorphs;
        }

        /// <summary>
        /// The four element extremes, as perturbations of the authored settings. Floats only —
        /// touching an int here changes topology and <see cref="AssertSameTopology"/> throws.
        ///
        /// <para>Each element says what it says on this hull, which is the same sentence its
        /// ability says (BUTTERFLY.md §3): <b>Charge</b> is the dust, so the wing edge sharpens and
        /// the lobes deepen; <b>Mass</b> is the spread, so the whole wing grows; <b>Space</b> is
        /// reach, so the wing goes LONG and THIN (a soaring aspect ratio, deliberately a different
        /// change from Mass's uniform growth, or the two elements would read as one dial); and
        /// <b>Time</b> is the fold, so the body draws out and the wings rake back into a swift's
        /// silhouette — a shape that reads as about to leave.</para>
        /// </summary>
        public static Settings ApplyElementExtreme(Settings s, Element element)
        {
            switch (element)
            {
                case Element.Charge:
                    s.ScallopDepth *= 2.1f;
                    s.TipFlare *= 1.45f;
                    s.ChordTip *= 0.80f;
                    s.Camber *= 1.25f;
                    break;
                case Element.Mass:
                    s.Span *= 1.30f;
                    s.ChordRoot *= 1.35f;
                    s.ChordTip *= 1.35f;
                    s.HindScale *= 1.15f;
                    s.BodyRadius *= 1.12f;
                    break;
                case Element.Space:
                    s.Span *= 1.55f;
                    s.ChordRoot *= 0.78f;
                    s.ChordTip *= 0.70f;
                    s.Camber *= 0.75f;
                    s.Dihedral *= 0.55f;
                    break;
                case Element.Time:
                    s.BodyLength *= 1.28f;
                    s.Sweep *= 1.95f;
                    s.HindSweep *= 1.6f;
                    s.ChordRoot *= 0.92f;
                    s.AntennaLength *= 1.30f;
                    break;
            }
            return s;
        }

        /// <summary>
        /// Bake the base hull and all four element extremes in one pass. Costs four extra
        /// <see cref="Generate"/> calls at build time and nothing per frame.
        /// </summary>
        public static MorphSet BakeMorphSet(Settings s)
        {
            var baseParts = Generate(s);
            var set = new MorphSet
            {
                BaseParts = baseParts,
                Deltas = new PartMorphDelta[MorphElements.Length][],
                BoundsMin = new Vector3[baseParts.Count],
                BoundsMax = new Vector3[baseParts.Count],
                PartMorphs = new bool[baseParts.Count],
            };

            for (int e = 0; e < MorphElements.Length; e++)
            {
                var extreme = Generate(ApplyElementExtreme(s, MorphElements[e]));
                AssertSameTopology(baseParts, extreme, MorphElements[e]);

                var deltas = new PartMorphDelta[baseParts.Count];
                for (int p = 0; p < baseParts.Count; p++)
                {
                    var bp = baseParts[p];
                    var xp = extreme[p];
                    var d = new PartMorphDelta
                    {
                        VertDeltas = new Vector3[bp.Verts.Count],
                        NormalDeltas = new Vector3[bp.Verts.Count],
                    };
                    for (int i = 0; i < bp.Verts.Count; i++)
                    {
                        d.VertDeltas[i] = xp.Verts[i] - bp.Verts[i];
                        d.NormalDeltas[i] = xp.Normals[i] - bp.Normals[i];
                        d.Any |= d.VertDeltas[i].sqrMagnitude > 1e-10f;
                    }
                    deltas[p] = d;
                    set.PartMorphs[p] |= d.Any;
                }
                set.Deltas[e] = deltas;
            }

            // Bounds interval: the corner-wise union over every reachable weight combination.
            // Each element's contribution is independent and monotone in its own weight, so the
            // extreme box is reached by taking min(0, delta) and max(0, delta) per element and
            // summing — no need to enumerate the 16 corners.
            for (int p = 0; p < baseParts.Count; p++)
            {
                var bp = baseParts[p];
                Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (int i = 0; i < bp.Verts.Count; i++)
                {
                    Vector3 lo = bp.Verts[i], hi = bp.Verts[i];
                    for (int e = 0; e < MorphElements.Length; e++)
                    {
                        var d = set.Deltas[e][p].VertDeltas[i];
                        lo += Vector3.Min(Vector3.zero, d);
                        hi += Vector3.Max(Vector3.zero, d);
                    }
                    min = Vector3.Min(min, lo);
                    max = Vector3.Max(max, hi);
                }
                set.BoundsMin[p] = min;
                set.BoundsMax[p] = max;
            }

            return set;
        }

        /// <summary>
        /// Blend one part at the given element weights (<see cref="MorphElements"/> order, each in
        /// [0,1]) into the supplied buffers. Pure, so the whole lattice is testable offline.
        /// </summary>
        public static void BlendPart(MorphSet set, int partIndex, float[] weights,
                                     List<Vector3> verts, List<Vector3> normals)
        {
            var basePart = set.BaseParts[partIndex];
            verts.Clear();
            normals.Clear();

            for (int i = 0; i < basePart.Verts.Count; i++)
            {
                Vector3 v = basePart.Verts[i];
                Vector3 n = basePart.Normals[i];
                for (int e = 0; e < MorphElements.Length; e++)
                {
                    var d = set.Deltas[e][partIndex];
                    v += d.VertDeltas[i] * weights[e];
                    n += d.NormalDeltas[i] * weights[e];
                }
                verts.Add(v);
                normals.Add(n.sqrMagnitude > 1e-8f ? n.normalized : basePart.Normals[i]);
            }
        }

        /// <summary>
        /// Throw if an element extreme changed TOPOLOGY. This is not defensive decoration: a float
        /// that crosses a feature gate (<see cref="Settings.AntennaLength"/> reaching 0, a scallop
        /// count derived from a float) silently changes the vertex count, and a delta array built
        /// against a different topology corrupts the blend into geometry nobody authored. Loud here
        /// beats wrong on screen.
        /// </summary>
        static void AssertSameTopology(List<Part> baseParts, List<Part> extreme, Element element)
        {
            if (baseParts.Count != extreme.Count)
                throw new InvalidOperationException(
                    $"[ButterflyHullForm] {element} extreme changed the PART count " +
                    $"({baseParts.Count} → {extreme.Count}). An element morph may move floats only.");

            for (int p = 0; p < baseParts.Count; p++)
            {
                if (baseParts[p].Verts.Count == extreme[p].Verts.Count
                    && baseParts[p].Body.Count == extreme[p].Body.Count
                    && baseParts[p].Wing.Count == extreme[p].Wing.Count)
                    continue;

                throw new InvalidOperationException(
                    $"[ButterflyHullForm] {element} extreme changed the topology of part " +
                    $"'{baseParts[p].Name}' (verts {baseParts[p].Verts.Count} → {extreme[p].Verts.Count}, " +
                    $"body tris {baseParts[p].Body.Count} → {extreme[p].Body.Count}, " +
                    $"wing tris {baseParts[p].Wing.Count} → {extreme[p].Wing.Count}). " +
                    "ApplyElementExtreme must touch floats only, and must not drive one across a " +
                    "feature gate (AntennaLength → 0).");
            }
        }
    }
}
