using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Termite QUEEN's hull, as a PURE function of its authored proportions (design:
    /// <c>R_VesselActions/TERMITE.md</c> §2). No Unity objects, no scene, no randomness — feed it
    /// a <see cref="Settings"/> and it hands back a vertex soup. <see cref="TermiteHullBuilder"/>
    /// owns everything that needs the scene.
    ///
    /// <para>The split is the Scarab's and the Butterfly's, deliberately: a third procedural hull
    /// should not invent a third shape of the same machinery. The four element extremes are just
    /// <see cref="Generate"/> run at perturbed settings, so "level 7 Mass and level 3 Space" is the
    /// base build plus a weighted sum of per-vertex deltas — provided TOPOLOGY never moves.
    /// Topology here is a function of the INTEGER settings only; every float may be morphed, no
    /// int may, and <see cref="BakeMorphSet"/> asserts it rather than trusting it.</para>
    ///
    /// <para><b>SIX PARTS.</b> The Core (head, mandibles, antennae, thorax, six legs) renders on
    /// the builder's own GameObject. The ABDOMEN is its own part, hinged at the waist, because it is
    /// the queen's whole silhouette — a physogastric termite queen is mostly abdomen — and
    /// <see cref="TermiteAnimation"/> breathes and sways it about that joint. The four WINGS are
    /// their own parts on their own hinges, because a wing welded into the body cannot fold. They
    /// are nearly equal in size: that is what <i>Isoptera</i> means ("equal wings"), and it is
    /// what separates a termite's silhouette from a wasp's at the range a commander is flown.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour
    /// onto slot <b>1</b>, so the ACCENT submesh carries the wings AND the pale membrane that
    /// stretches between the abdomen's dark plates. That membrane is real anatomy — a queen's
    /// abdomen swells until the membrane between its sclerites is most of its surface — and it is
    /// the right place for the team colour: it is the biggest surface on the hull and the one that
    /// grows with MASS.</para>
    /// </summary>
    public static class TermiteHullForm
    {
        public const int BodySubmesh = 0;
        public const int AccentSubmesh = 1;

        public const int CorePart = 0;
        public const int AbdomenPart = 1;

        /// <summary>
        /// Every number the geometry depends on. Mirrors <see cref="TermiteHullBuilder"/>'s
        /// serialized fields one-for-one. INTEGER fields decide topology and are never morphed.
        /// </summary>
        public struct Settings
        {
            // ---- head ----
            public float HeadRadius;
            public float HeadLength;
            public float MandibleLength;
            public float MandibleThickness;
            public float AntennaLength;
            public float AntennaThickness;

            // ---- thorax + legs ----
            public float ThoraxRadius;       // NOT morphed: the wing hinges and the waist sit on it
            public float ThoraxLength;       // NOT morphed, for the same reason
            public float LegLength;
            public float LegThickness;

            // ---- abdomen ----
            public float AbdomenRadius;
            public float AbdomenLength;
            public float PlateBulge;         // how far each dark plate stands proud of the profile
            public float MembraneRecess;     // how far the pale membrane between plates sinks
            public int AbdomenSegments;      // plates along the abdomen
            public int RingsPerSegment;      // mesh rings per plate (the first is the membrane)

            // ---- wings ----
            public float WingLength;
            public float WingWidth;
            public float WingLift;           // upward bow of the wing along its length
            public float HindScale;          // hindwing length as a fraction of the forewing
            public int WingSpanSegments;
            public int WingChordSegments;

            // ---- resolution ----
            public int Sides;                // radial resolution of every body of revolution
        }

        /// <summary>
        /// The fleet's morph order (<c>VesselElementalMorphConfigSO</c>): charge, mass, space,
        /// time — held here so the bake, the blend and the builder can never disagree.
        /// </summary>
        public static readonly Element[] MorphElements =
        {
            Element.Charge, Element.Mass, Element.Space, Element.Time,
        };

        /// <summary>One emitted piece, geometry in HULL space (the builder subtracts
        /// <see cref="Pivot"/> for a child part).</summary>
        public sealed class Part
        {
            public string Name;
            public Vector3 Pivot;
            public readonly List<Vector3> Verts = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<int> Body = new();     // submesh 0
            public readonly List<int> Accent = new();   // submesh 1 (domain colour)
        }

        /// <summary>Build the whole hull. Deterministic and allocation-bounded.</summary>
        public static List<Part> Generate(Settings s)
        {
            var parts = new List<Part>(6)
            {
                BuildCore(s),
                BuildAbdomen(s),
                // STABLE order — morph deltas are indexed by part.
                BuildWing(s, side: -1, hind: false),
                BuildWing(s, side: +1, hind: false),
                BuildWing(s, side: -1, hind: true),
                BuildWing(s, side: +1, hind: true),
            };
            return parts;
        }

        // ------------------------------------------------------------------ anchors

        /// <summary>Front of the thorax / back of the head, on the body axis.</summary>
        static float ThoraxFront(Settings s) => s.ThoraxLength * 0.5f;

        /// <summary>The WAIST — where the abdomen hinges onto the thorax.</summary>
        public static Vector3 WaistPivot(Settings s) => new(0f, 0f, -s.ThoraxLength * 0.5f);

        /// <summary>Hull-space hinge of one wing. A function of the thorax ONLY, which no element
        /// perturbs — so the hinges are fixed under every morph (asserted in the bake).</summary>
        public static Vector3 WingPivot(Settings s, int side, bool hind) => new(
            side * s.ThoraxRadius * 0.55f,
            s.ThoraxRadius * (hind ? 0.78f : 0.86f),
            hind ? -s.ThoraxLength * 0.24f : s.ThoraxLength * 0.16f);

        // ------------------------------------------------------------------ core

        static Part BuildCore(Settings s)
        {
            var p = new Part { Name = "Core", Pivot = Vector3.zero };
            int sides = Mathf.Max(4, s.Sides);

            // Thorax: a short, slightly pinched body of revolution.
            float tf = ThoraxFront(s);
            AppendRevolution(p, sides, rings: 6, zBack: -tf, zFront: tf, flattenY: 0.9f,
                profile: t => s.ThoraxRadius * (0.45f + 0.55f * Mathf.Sin(t * Mathf.PI)), accent: false);

            // Head: rounded, flattened top-to-bottom (a termite's head is a flat capsule), sitting
            // just in front of the thorax with a slight overlap so the neck never shows a gap.
            float headBack = tf - s.HeadLength * 0.12f;
            float headFront = headBack + s.HeadLength;
            AppendRevolution(p, sides, rings: 8, zBack: headBack, zFront: headFront, flattenY: 0.78f,
                profile: t => s.HeadRadius * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.55f),
                accent: false);

            // Mandibles: two curved prongs that sweep forward then HOOK inward toward each other.
            for (int side = -1; side <= 1; side += 2)
            {
                var centres = new List<Vector3>();
                var radii = new List<float>();
                Vector3 root = new(side * s.HeadRadius * 0.42f, -s.HeadRadius * 0.28f,
                                   headFront - s.HeadLength * 0.12f);
                const int n = 6;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    centres.Add(root + new Vector3(
                        side * s.MandibleLength * (0.38f * Mathf.Sin(t * Mathf.PI * 0.8f) - 0.62f * t * t),
                        -s.MandibleLength * 0.08f * t,
                        s.MandibleLength * t));
                    radii.Add(s.MandibleThickness * Mathf.Lerp(1f, 0.18f, t));
                }
                AppendTube(p, centres, radii, 5, accent: false);
            }

            // Antennae: long, forward and outward, drooping at the tip; beaded like a real
            // termite's (moniliform) so they read as antennae rather than wires.
            for (int side = -1; side <= 1; side += 2)
            {
                var centres = new List<Vector3>();
                var radii = new List<float>();
                Vector3 root = new(side * s.HeadRadius * 0.5f, s.HeadRadius * 0.3f,
                                   headFront - s.HeadLength * 0.3f);
                const int n = 12;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    centres.Add(root + new Vector3(
                        side * s.AntennaLength * 0.5f * t,
                        s.AntennaLength * (0.28f * Mathf.Sin(t * Mathf.PI * 0.7f) - 0.18f * t * t),
                        s.AntennaLength * 0.82f * t));
                    // Beads: alternate ring radii (every other ring fat), a pure function of the
                    // ring index so topology is fixed.
                    float bead = (i % 2 == 0) ? 1.25f : 0.8f;
                    radii.Add(s.AntennaThickness * bead * Mathf.Lerp(1f, 0.7f, t));
                }
                AppendTube(p, centres, radii, 4, accent: false);
            }

            // Six legs: three pairs on the thorax, each a femur out-and-up and a tibia down.
            float[] pairZ = { 0.30f, 0f, -0.32f };
            float[] footZ = { 0.30f, 0.02f, -0.34f };
            for (int pair = 0; pair < 3; pair++)
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 root = new(side * s.ThoraxRadius * 0.75f, -s.ThoraxRadius * 0.45f,
                                   pairZ[pair] * s.ThoraxLength);
                Vector3 knee = root + new Vector3(side * s.LegLength * 0.46f, s.LegLength * 0.12f,
                                                  footZ[pair] * s.LegLength * 0.35f);
                Vector3 foot = knee + new Vector3(side * s.LegLength * 0.22f, -s.LegLength * 0.52f,
                                                  footZ[pair] * s.LegLength * 0.6f);
                var centres = new List<Vector3>();
                var radii = new List<float>();
                const int n = 6;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    centres.Add(t < 0.5f
                        ? Vector3.Lerp(root, knee, t * 2f)
                        : Vector3.Lerp(knee, foot, (t - 0.5f) * 2f));
                    radii.Add(s.LegThickness * Mathf.Lerp(1f, 0.45f, t));
                }
                AppendTube(p, centres, radii, 5, accent: false);
            }

            return p;
        }

        // ------------------------------------------------------------------ abdomen

        /// <summary>
        /// The queen's abdomen: a long swollen body of revolution trailing behind the waist, made
        /// of <see cref="Settings.AbdomenSegments"/> dark PLATES separated by recessed bands of
        /// pale MEMBRANE. The plates are submesh 0; the membrane bands are submesh 1 and so take
        /// the domain colour. Closed at the tip (the final ring collapses to the axis).
        /// </summary>
        static Part BuildAbdomen(Settings s)
        {
            Vector3 pivot = WaistPivot(s);
            var p = new Part { Name = "Abdomen", Pivot = pivot };

            int sides = Mathf.Max(4, s.Sides);
            int rps = Mathf.Max(2, s.RingsPerSegment);
            int rings = Mathf.Max(1, s.AbdomenSegments) * rps;

            for (int r = 0; r <= rings; r++)
            {
                float u = r / (float)rings;                                   // 0 waist, 1 tip
                float z = pivot.z - s.AbdomenLength * u;
                float baseR = s.AbdomenRadius *
                              Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.06f, 1f, u))), 0.6f);

                int k = r % rps;
                float band = k == 0
                    ? 1f - s.MembraneRecess
                    : 1f + s.PlateBulge * Mathf.Sin(Mathf.PI * k / rps);
                float radius = baseR * band;

                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                    // Slightly flattened, and arched so the top line rises a little behind the
                    // waist — a queen's abdomen sags less than a sausage would.
                    p.Verts.Add(new Vector3(cx * radius, cy * radius * 0.88f + 0.12f * baseR, z));
                    p.Normals.Add(new Vector3(cx, cy, 0f).normalized);
                    p.Uvs.Add(new Vector2(i / (float)sides, u));
                }
            }

            int stride = sides + 1;
            for (int r = 0; r < rings; r++)
            {
                // The interval AFTER a membrane ring is membrane: the recessed band between plates.
                var target = r % rps == 0 ? p.Accent : p.Body;
                for (int i = 0; i < sides; i++)
                {
                    int a = r * stride + i, b = a + 1, c = a + stride, d = c + 1;
                    // The abdomen runs BACKWARD (-z) with ring index, so the winding is the
                    // mirror of a forward revolution to keep the faces pointing out.
                    target.Add(a); target.Add(b); target.Add(c);
                    target.Add(b); target.Add(d); target.Add(c);
                }
            }

            return p;
        }

        // ------------------------------------------------------------------ wings

        /// <summary>
        /// One wing, as a (span × chord) quad grid rooted at its hinge and extending straight out
        /// to its own side. The FOLD (swept back over the abdomen at rest) and the buzz are the
        /// animation's; the geometry is the spread pose so a hinge rotation is all it takes.
        ///
        /// Termite wings are long, narrow and rounded — broadest past halfway and closing to a
        /// blunt tip. Emitted double-sided, because a membrane with no thickness vanishes whenever
        /// the camera crosses its plane.
        /// </summary>
        static Part BuildWing(Settings s, int side, bool hind)
        {
            string name = (hind ? "WingHind" : "WingFore") + (side < 0 ? "L" : "R");
            Vector3 pivot = WingPivot(s, side, hind);
            var p = new Part { Name = name, Pivot = pivot };

            float length = s.WingLength * (hind ? Mathf.Max(0.1f, s.HindScale) : 1f);
            int su = Mathf.Max(2, s.WingSpanSegments);
            int sv = Mathf.Max(1, s.WingChordSegments);

            for (int iu = 0; iu <= su; iu++)
            {
                float u = iu / (float)su;
                float chord = WingChordAt(s, u);
                float lead = chord * 0.3f;
                float lift = s.WingLift * Mathf.Sin(u * Mathf.PI * 0.5f);

                for (int iv = 0; iv <= sv; iv++)
                {
                    float v = iv / (float)sv;
                    float z = lead - chord * v;
                    p.Verts.Add(pivot + new Vector3(side * length * u, lift, z));
                    p.Normals.Add(Vector3.up);
                    p.Uvs.Add(new Vector2(u, v));
                }
            }

            int stride = sv + 1;
            for (int iu = 0; iu < su; iu++)
            for (int iv = 0; iv < sv; iv++)
            {
                int a = iu * stride + iv, b = a + 1, c = a + stride, d = c + 1;
                // Both windings: front face and back face.
                p.Accent.Add(a); p.Accent.Add(b); p.Accent.Add(c);
                p.Accent.Add(b); p.Accent.Add(d); p.Accent.Add(c);
                p.Accent.Add(a); p.Accent.Add(c); p.Accent.Add(b);
                p.Accent.Add(b); p.Accent.Add(c); p.Accent.Add(d);
            }

            return p;
        }

        /// <summary>Chord at span fraction <paramref name="u"/>: narrow at the root, broadest past
        /// halfway, closing to a rounded tip.</summary>
        static float WingChordAt(Settings s, float u)
        {
            float broaden = 0.38f + 0.62f * Mathf.Sin(u * Mathf.PI * 0.5f);
            float close = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(u, 8f)));
            // Never fully zero at the tip ring, so the last quads keep a sane area (the tip reads
            // as round at any range this hull is seen from).
            return s.WingWidth * Mathf.Max(0.06f, broaden * close);
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>A closed-ish body of revolution about +Z from <paramref name="zBack"/> to
        /// <paramref name="zFront"/>, radius <paramref name="profile"/>(t) at t in [0,1].</summary>
        static void AppendRevolution(Part p, int sides, int rings, float zBack, float zFront,
                                     float flattenY, Func<float, float> profile, bool accent)
        {
            int baseIndex = p.Verts.Count;
            var target = accent ? p.Accent : p.Body;
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float z = Mathf.Lerp(zBack, zFront, t);
                float radius = Mathf.Max(0f, profile(t));
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                    p.Verts.Add(new Vector3(cx * radius, cy * radius * flattenY, z));
                    p.Normals.Add(new Vector3(cx, cy, 0f).normalized);
                    p.Uvs.Add(new Vector2(i / (float)sides, t));
                }
            }

            int stride = sides + 1;
            for (int r = 0; r < rings; r++)
            for (int i = 0; i < sides; i++)
            {
                int a = baseIndex + r * stride + i, b = a + 1, c = a + stride, d = c + 1;
                target.Add(a); target.Add(c); target.Add(b);
                target.Add(b); target.Add(c); target.Add(d);
            }
        }

        /// <summary>
        /// A tapered tube along a polyline, with a PARALLEL-TRANSPORTED frame so it never twists
        /// or pinches where the polyline turns. The frame is seeded from a fixed reference axis,
        /// which makes it a pure function of the centres — the morph deltas need that.
        /// </summary>
        static void AppendTube(Part p, List<Vector3> centres, List<float> radii, int sides, bool accent)
        {
            int n = centres.Count;
            if (n < 2) return;
            int baseIndex = p.Verts.Count;
            var target = accent ? p.Accent : p.Body;

            Vector3 tangent = (centres[1] - centres[0]).normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 normal = Vector3.Cross(tangent, reference).normalized;

            for (int g = 0; g < n; g++)
            {
                Vector3 nextTangent = g < n - 1
                    ? (centres[g + 1] - centres[g]).normalized
                    : (centres[g] - centres[g - 1]).normalized;
                if (g > 0)
                {
                    // Parallel transport: remove the component of the old normal along the new
                    // tangent and renormalize.
                    normal = (normal - nextTangent * Vector3.Dot(normal, nextTangent)).normalized;
                    if (normal.sqrMagnitude < 1e-6f)
                        normal = Vector3.Cross(nextTangent, reference).normalized;
                }
                tangent = nextTangent;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    p.Verts.Add(centres[g] + dir * Mathf.Max(0f, radii[g]));
                    p.Normals.Add(dir);
                    p.Uvs.Add(new Vector2(i / (float)sides, g / (float)(n - 1)));
                }
            }

            int stride = sides + 1;
            for (int g = 0; g < n - 1; g++)
            for (int i = 0; i < sides; i++)
            {
                int a = baseIndex + g * stride + i, b = a + 1, c = a + stride, d = c + 1;
                target.Add(a); target.Add(c); target.Add(b);
                target.Add(b); target.Add(c); target.Add(d);
            }
        }

        // ------------------------------------------------------------------ elemental morphs

        /// <summary>One element's shape deltas for one part, in HULL space. Hinges and the waist
        /// are FIXED under every element, so there is no pivot delta to carry — asserted in
        /// <see cref="BakeMorphSet"/>.</summary>
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
            public Vector3[] BoundsMin;
            public Vector3[] BoundsMax;
            public bool[] PartMorphs;
        }

        /// <summary>
        /// The four element extremes. Each says on this hull what its ability says in the deck
        /// (TERMITE.md §3):
        /// <list type="bullet">
        /// <item><b>Charge</b> is the Charge cards' THREAT (Autothysis, the decoy crystal) — the
        /// mandibles lengthen into sickles, the head broadens, the antennae reach.</item>
        /// <item><b>Mass</b> is the DRONE count — and a queen's fecundity is her abdomen, so
        /// Mass swells it: physogastry, the literal biology of a queen producing more workers.</item>
        /// <item><b>Space</b> is REACH (Teleport, New Mound, the drones' forage radius) — the wings
        /// and antennae go long.</item>
        /// <item><b>Time</b> is the pheromone CLOCK and the queen's speed — the body draws out and
        /// slims, the legs lengthen, the wings bow: a queen built to move.</item>
        /// </list>
        /// Floats only; never the thorax (the hinges and the waist sit on it).
        /// </summary>
        public static Settings ApplyElementExtreme(Settings s, Element element)
        {
            switch (element)
            {
                case Element.Charge:
                    s.MandibleLength *= 1.9f;
                    s.MandibleThickness *= 1.35f;
                    s.HeadRadius *= 1.14f;
                    s.AntennaLength *= 1.25f;
                    break;
                case Element.Mass:
                    s.AbdomenRadius *= 1.4f;
                    s.AbdomenLength *= 1.22f;
                    s.PlateBulge *= 1.6f;
                    s.MembraneRecess *= 0.6f;
                    break;
                case Element.Space:
                    s.WingLength *= 1.45f;
                    s.WingWidth *= 0.9f;
                    s.AntennaLength *= 1.5f;
                    break;
                case Element.Time:
                    s.AbdomenRadius *= 0.8f;
                    s.AbdomenLength *= 1.15f;
                    s.LegLength *= 1.3f;
                    s.HeadLength *= 1.12f;
                    s.WingLift *= 2.2f;
                    break;
            }
            return s;
        }

        /// <summary>Bake the base hull and all four element extremes in one pass. Four extra
        /// <see cref="Generate"/> calls at build time, nothing per frame.</summary>
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

            // Corner-wise union over every reachable weight combination: each element's
            // contribution is independent and linear in its own weight, so the extreme box is the
            // base plus the sum of min(0,delta) / max(0,delta) per element.
            for (int p = 0; p < baseParts.Count; p++)
            {
                var bp = baseParts[p];
                Vector3 min = Vector3.positiveInfinity;
                Vector3 max = Vector3.negativeInfinity;
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

        /// <summary>Blend one part at the given element weights (<see cref="MorphElements"/>
        /// order, each in [0,1]) into the supplied buffers. Pure.</summary>
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
        /// Throw if an element extreme changed TOPOLOGY or moved a PIVOT. A float that crosses a
        /// feature gate silently changes the vertex count, and a delta array built against a
        /// different topology corrupts the blend; a moved hinge would tear a wing off the body
        /// under a morph, because the morph writes vertices and never <c>localPosition</c>.
        /// </summary>
        static void AssertSameTopology(List<Part> baseParts, List<Part> extreme, Element element)
        {
            if (baseParts.Count != extreme.Count)
                throw new InvalidOperationException(
                    $"[TermiteHullForm] {element} extreme changed the PART count " +
                    $"({baseParts.Count} -> {extreme.Count}). An element morph may move floats only.");

            for (int p = 0; p < baseParts.Count; p++)
            {
                var a = baseParts[p];
                var b = extreme[p];
                if ((a.Pivot - b.Pivot).sqrMagnitude > 1e-8f)
                    throw new InvalidOperationException(
                        $"[TermiteHullForm] {element} extreme moved the pivot of part '{a.Name}' " +
                        $"({a.Pivot} -> {b.Pivot}). Elements must not touch ThoraxRadius or ThoraxLength.");

                if (a.Verts.Count == b.Verts.Count && a.Body.Count == b.Body.Count
                    && a.Accent.Count == b.Accent.Count)
                    continue;

                throw new InvalidOperationException(
                    $"[TermiteHullForm] {element} extreme changed the topology of part '{a.Name}' " +
                    $"(verts {a.Verts.Count} -> {b.Verts.Count}, body tris {a.Body.Count} -> " +
                    $"{b.Body.Count}, accent tris {a.Accent.Count} -> {b.Accent.Count}). " +
                    "ApplyElementExtreme must touch floats only.");
            }
        }
    }
}
