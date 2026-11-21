using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's procedural hull as a PURE function (design: <c>R_VesselActions/STOAT.md</c> §2):
    /// settings in, parts out, no scene and no Unity object — so <c>StoatHullFormTests</c> and
    /// <c>Tools/Build/stoat_hull_harness</c> run it as-is. <see cref="StoatHullBuilder"/> only emits
    /// what this returns. The split, the morph set and the topology assert are the
    /// <see cref="ButterflyHullForm"/> / <see cref="ScarabHullForm"/> shape, deliberately.
    ///
    /// <para><b>The look</b> is round 2's Option 2 in the Stoat viewer, the one the user chose: a
    /// stoat cut from DIAMOND PLATES (a four-sided cross-section, point up, so the spine reads as a
    /// ridge), a wedge head, a domain-coloured DORSAL RIDGE of fins on every other plate and a
    /// domain-coloured CRYSTAL at the tail tip. Every face is flat-shaded (unshared vertices), the
    /// prism-cut read the HyperSea's crystals and prisms already have.</para>
    ///
    /// <para><b>Parts are separate so the lope can bend the body</b> (<see cref="StoatAnimation"/>):
    /// one per body plate, the head, one per tail plate, the crystal tip and four legs. Part 0 is
    /// the CORE — the middle plate — which renders on the builder's own GameObject (the one
    /// <c>VesselCustomization</c> paints); every other part is a child re-seated on its pivot.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour
    /// onto slot <b>1</b>, so the ridge fins, the crystal tip and the eyes are submesh 1 and the
    /// body plates submesh 0 — the accents are what a pilot reads the domain from.</para>
    /// </summary>
    public static class StoatHullForm
    {
        public const int BodySubmesh = 0;
        public const int AccentSubmesh = 1;

        /// <summary>The order every morph array uses.</summary>
        public static readonly Element[] MorphElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public enum PartKind { Core = 0, Segment = 1, Head = 2, Tail = 3, TailTip = 4, Leg = 5 }

        /// <summary>World units, +Z forward, +Y up. Ints are TOPOLOGY and never morph.</summary>
        public struct Settings
        {
            public int Segments;          // body plates, nose to hip
            public int Sides;             // plates around the body: 4 = diamond
            public float BodyLength;      // first plate's front face to the last plate's back face
            public float ChestRadius;     // the front plate
            public float HipRadius;       // the back plate
            public float PlateTaper;      // each plate's back face as a fraction of its front
            public float HeadRadius;      // half-width of the wedge head's base
            public float HeadLength;      // neck to snout tip
            public float EarSize;
            public float EyeSize;
            public int TailSegments;
            public float TailLength;
            public float TailRootRadius;
            public float TailTipRadius;
            public float CrystalSize;     // the tail-tip crystal's half-width (domain colour)
            public float CrystalStretch;  // its length as a multiple of CrystalSize
            public float LegLength;
            public float LegThickness;
            public float FinHeight;       // the dorsal ridge, as a fraction of the plate radius
            public float FinWidth;        // its base half-width, as a fraction of the plate radius
        }

        /// <summary>Round 2, Option 2 ("plated, body-only bound"), at fleet scale: the viewer's
        /// numbers × 1.6, which puts the body near the Squirrel's ~4 u it replaces.</summary>
        public static Settings Defaults => new()
        {
            Segments = 5,
            Sides = 4,
            BodyLength = 4.0f,
            ChestRadius = 0.45f,
            HipRadius = 0.26f,
            PlateTaper = 0.9f,
            HeadRadius = 0.43f,
            HeadLength = 1.1f,
            EarSize = 0.16f,
            EyeSize = 0.075f,
            TailSegments = 4,
            TailLength = 2.1f,
            TailRootRadius = 0.16f,
            TailTipRadius = 0.08f,
            CrystalSize = 0.22f,
            CrystalStretch = 1.7f,
            LegLength = 0.26f,
            LegThickness = 0.07f,
            FinHeight = 0.9f,
            FinWidth = 0.32f,
        };

        public sealed class Part
        {
            public string Name;
            public PartKind Kind;
            /// <summary>Index within its kind (plate number, tail number, leg number).</summary>
            public int Index;
            /// <summary>Where this part's transform sits, in hull space. Geometry is in hull space.</summary>
            public Vector3 Pivot;
            public readonly List<Vector3> Verts = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<int> Body = new();
            public readonly List<int> Accent = new();
        }

        // ------------------------------------------------------------------ layout (shared with the animation)

        public static int CoreIndex(int segments) => Mathf.Max(1, segments) / 2;

        /// <summary>A plate's position along the body, 0 at the nose plate and 1 at the hip plate.</summary>
        public static float SegmentT(int index, int segments) =>
            segments <= 1 ? 0.5f : index / (float)(segments - 1);

        public static float SegmentLength(in Settings s) => s.BodyLength / Mathf.Max(1, s.Segments);

        /// <summary>Centre of plate <paramref name="index"/> on the body axis (z), nose plate first.</summary>
        public static float SegmentCenterZ(in Settings s, int index) =>
            s.BodyLength * 0.5f - (index + 0.5f) * SegmentLength(s);

        public static float SegmentRadius(in Settings s, int index) =>
            Mathf.Lerp(s.ChestRadius, s.HipRadius, SegmentT(index, s.Segments));

        // ------------------------------------------------------------------ generate

        public static List<Part> Generate(Settings s)
        {
            if (s.Segments < 2) throw new ArgumentException("[StoatHullForm] Segments must be >= 2.");
            if (s.Sides < 3) throw new ArgumentException("[StoatHullForm] Sides must be >= 3.");
            if (s.TailSegments < 1) throw new ArgumentException("[StoatHullForm] TailSegments must be >= 1.");

            var parts = new List<Part>();
            int core = CoreIndex(s.Segments);
            float seg = SegmentLength(s);
            // A plate's first vertex sits straight UP, so a four-sided body is a diamond with its
            // point along the spine — the ridge the fins stand on.
            float angle0 = Mathf.PI * 0.5f;

            // Part 0 is the core plate (it renders on the builder's own renderer), then the rest
            // of the plates in order.
            parts.Add(BodyPlate(s, core, seg, angle0, PartKind.Core));
            for (int i = 0; i < s.Segments; i++)
                if (i != core) parts.Add(BodyPlate(s, i, seg, angle0, PartKind.Segment));

            parts.Add(Head(s, angle0));

            // Tail: plates tapering from the root to the tip, then the crystal.
            float tailSeg = s.TailLength / s.TailSegments;
            float tailStart = -s.BodyLength * 0.5f;
            float tailY = s.HipRadius * 0.25f;   // the tail leaves the back of the hip, not its axis
            for (int j = 0; j < s.TailSegments; j++)
            {
                float t0 = j / (float)s.TailSegments, t1 = (j + 1) / (float)s.TailSegments;
                float r0 = Mathf.Lerp(s.TailRootRadius, s.TailTipRadius, t0);
                float r1 = Mathf.Lerp(s.TailRootRadius, s.TailTipRadius, t1);
                float zf = tailStart - j * tailSeg, zb = zf - tailSeg;
                var p = new Part { Name = "StoatTail" + j, Kind = PartKind.Tail, Index = j,
                                   Pivot = new Vector3(0f, tailY, zf) };
                AddFrustum(p, new Vector3(0f, tailY, zf), new Vector3(0f, tailY, zb), r0, r1, s.Sides, angle0, BodySubmesh, caps: true);
                parts.Add(p);
            }
            float tipZ = tailStart - s.TailLength;
            var tip = new Part { Name = "StoatTailTip", Kind = PartKind.TailTip, Index = 0,
                                 Pivot = new Vector3(0f, tailY, tipZ) };
            float cl = s.CrystalSize * s.CrystalStretch;
            AddOctahedron(tip, new Vector3(0f, tailY, tipZ - cl * 0.85f), s.CrystalSize, s.CrystalSize, cl, AccentSubmesh);
            parts.Add(tip);

            // Legs: thin four-sided struts under the front and back plates, pivoted at the hip so
            // the lope can swing them.
            int front = 1, back = s.Segments - 2;
            Leg(parts, s, "StoatLegFL", 0, -1, front, angle0);
            Leg(parts, s, "StoatLegFR", 1, +1, front, angle0);
            Leg(parts, s, "StoatLegBL", 2, -1, back, angle0);
            Leg(parts, s, "StoatLegBR", 3, +1, back, angle0);

            return parts;
        }

        static Part BodyPlate(in Settings s, int i, float seg, float angle0, PartKind kind)
        {
            float zc = SegmentCenterZ(s, i);
            float r = SegmentRadius(s, i);
            var p = new Part
            {
                Name = kind == PartKind.Core ? "StoatCore" : "StoatPlate" + i,
                Kind = kind,
                Index = i,
                Pivot = new Vector3(0f, 0f, zc),
            };
            // Plates overlap their neighbours by 4% so an arched spine never opens a slit.
            float half = seg * 0.52f;
            AddFrustum(p, new Vector3(0f, 0f, zc + half), new Vector3(0f, 0f, zc - half), r, r * s.PlateTaper, s.Sides, angle0,
                BodySubmesh, caps: true);
            // The dorsal ridge: a fin on every other plate, standing on the spine's point.
            if (i % 2 == 0)
            {
                float fh = r * s.FinHeight, fw = r * s.FinWidth;
                AddPyramid(p, new Vector3(0f, r * 0.92f, zc), new Vector3(0f, r * 0.92f + fh, zc), fw, 4, Mathf.PI * 0.25f,
                    AccentSubmesh, Vector3.forward);
            }
            return p;
        }

        static Part Head(in Settings s, float angle0)
        {
            float neckZ = s.BodyLength * 0.5f;
            var p = new Part { Name = "StoatHead", Kind = PartKind.Head, Index = 0, Pivot = new Vector3(0f, 0f, neckZ) };
            // The wedge: a four-sided pyramid, base at the neck (tucked a little into the chest
            // plate), apex at the snout.
            var baseC = new Vector3(0f, 0f, neckZ - s.HeadLength * 0.08f);
            var apex = new Vector3(0f, -s.HeadRadius * 0.15f, neckZ + s.HeadLength);
            AddPyramid(p, baseC, apex, s.HeadRadius, s.Sides, angle0, BodySubmesh, Vector3.up, baseCap: true);
            // Ears: tetrahedra on the top flanks, just behind the eyes.
            for (int side = -1; side <= 1; side += 2)
            {
                var ear = new Vector3(side * s.HeadRadius * 0.45f, s.HeadRadius * 0.62f, neckZ + s.HeadLength * 0.12f);
                AddPyramid(p, ear, ear + new Vector3(side * s.EarSize * 0.35f, s.EarSize * 1.4f, -s.EarSize * 0.3f),
                    s.EarSize, 3, 0f, BodySubmesh, Vector3.forward, baseCap: true);
            }
            // Eyes, in the domain colour: the one accent on the face.
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector3(side * s.HeadRadius * 0.42f, s.HeadRadius * 0.2f, neckZ + s.HeadLength * 0.35f);
                AddOctahedron(p, eye, s.EyeSize, s.EyeSize, s.EyeSize, AccentSubmesh);
            }
            // The nose: a body-coloured nub on the snout (two material slots only: body and domain).
            AddOctahedron(p, apex + new Vector3(0f, 0f, s.HeadRadius * 0.05f), s.HeadRadius * 0.12f, s.HeadRadius * 0.12f,
                s.HeadRadius * 0.12f, BodySubmesh);
            return p;
        }

        static void Leg(List<Part> parts, in Settings s, string name, int index, int side, int plate, float angle0)
        {
            float zc = SegmentCenterZ(s, plate);
            float r = SegmentRadius(s, plate);
            var hip = new Vector3(side * r * 0.55f, -r * 0.55f, zc);
            var p = new Part { Name = name, Kind = PartKind.Leg, Index = index, Pivot = hip };
            var foot = hip + new Vector3(0f, -(r * 0.45f + s.LegLength), 0f);
            AddFrustum(p, hip, foot, s.LegThickness, s.LegThickness * 0.7f, 4, angle0, BodySubmesh, caps: true);
            parts.Add(p);
        }

        // ------------------------------------------------------------------ primitives (flat-shaded)

        /// <summary>
        /// A prism frustum from <paramref name="a"/> (radius <paramref name="ra"/>) to
        /// <paramref name="b"/> (radius <paramref name="rb"/>), <paramref name="sides"/> flat faces.
        /// The ring is built in the plane perpendicular to a→b, with angle 0 along +X and angle π/2
        /// along +Y for a z-axis frustum (the hull's up), so <paramref name="angle0"/> = π/2 puts
        /// a vertex on top.
        /// </summary>
        static void AddFrustum(Part p, Vector3 a, Vector3 b, float ra, float rb, int sides, float angle0, int submesh, bool caps)
        {
            var axis = (b - a).normalized;
            Basis(axis, out var u, out var v);
            var ringA = new Vector3[sides];
            var ringB = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float th = angle0 + k * Mathf.PI * 2f / sides;
                var dir = u * Mathf.Cos(th) + v * Mathf.Sin(th);
                ringA[k] = a + dir * ra;
                ringB[k] = b + dir * rb;
            }
            var mid = (a + b) * 0.5f;
            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                AddQuad(p, ringA[k], ringA[n], ringB[n], ringB[k], mid, submesh);
            }
            if (!caps) return;
            for (int k = 1; k < sides - 1; k++)
            {
                // The caps face along the axis: the front one back toward a, the back one past b.
                AddTri(p, ringA[0], ringA[k], ringA[k + 1], a, submesh, hint: -axis);
                AddTri(p, ringB[0], ringB[k], ringB[k + 1], b, submesh, hint: axis);
            }
        }

        /// <summary>A pyramid: a <paramref name="sides"/>-gon base of radius <paramref name="r"/> at
        /// <paramref name="baseC"/> (in the plane perpendicular to base→apex), apex at
        /// <paramref name="apex"/>. <paramref name="refUp"/> fixes the base ring's angle 0 direction
        /// when the axis is near it.</summary>
        static void AddPyramid(Part p, Vector3 baseC, Vector3 apex, float r, int sides, float angle0, int submesh, Vector3 refUp,
            bool baseCap = false)
        {
            var axis = (apex - baseC).normalized;
            Basis(axis, out var u, out var v, refUp);
            var ring = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float th = angle0 + k * Mathf.PI * 2f / sides;
                ring[k] = baseC + (u * Mathf.Cos(th) + v * Mathf.Sin(th)) * r;
            }
            var centroid = baseC + (apex - baseC) * 0.25f;
            for (int k = 0; k < sides; k++)
                AddTri(p, ring[k], ring[(k + 1) % sides], apex, centroid, submesh);
            if (!baseCap) return;
            for (int k = 1; k < sides - 1; k++)
                AddTri(p, ring[0], ring[k], ring[k + 1], centroid, submesh);
        }

        static void AddOctahedron(Part p, Vector3 c, float rx, float ry, float rz, int submesh)
        {
            var px = c + new Vector3(rx, 0f, 0f); var nx = c - new Vector3(rx, 0f, 0f);
            var py = c + new Vector3(0f, ry, 0f); var ny = c - new Vector3(0f, ry, 0f);
            var pz = c + new Vector3(0f, 0f, rz); var nz = c - new Vector3(0f, 0f, rz);
            AddTri(p, px, py, pz, c, submesh); AddTri(p, py, nx, pz, c, submesh);
            AddTri(p, nx, ny, pz, c, submesh); AddTri(p, ny, px, pz, c, submesh);
            AddTri(p, py, px, nz, c, submesh); AddTri(p, nx, py, nz, c, submesh);
            AddTri(p, ny, nx, nz, c, submesh); AddTri(p, px, ny, nz, c, submesh);
        }

        static void AddQuad(Part p, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside, int submesh)
        {
            AddTri(p, a, b, c, inside, submesh);
            AddTri(p, a, c, d, inside, submesh);
        }

        /// <summary>
        /// One flat-shaded triangle, wound so its FRONT faces away from <paramref name="inside"/>.
        /// Unity's front face is the side <c>cross(b−a, c−a)</c> points to (clockwise seen from the
        /// front), so a triangle whose cross product points inward is flipped. A flat cap, whose
        /// centroid can sit ON the inside point, passes the outward direction as <paramref name="hint"/>.
        /// </summary>
        static void AddTri(Part p, Vector3 a, Vector3 b, Vector3 c, Vector3 inside, int submesh, Vector3? hint = null)
        {
            var n = Vector3.Cross(b - a, c - a);
            var outward = hint ?? ((a + b + c) / 3f - inside);
            if (Vector3.Dot(n, outward) < 0f) { var t = b; b = c; c = t; n = -n; }
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            int i0 = p.Verts.Count;
            p.Verts.Add(a); p.Verts.Add(b); p.Verts.Add(c);
            p.Normals.Add(n); p.Normals.Add(n); p.Normals.Add(n);
            p.Uvs.Add(new Vector2(a.z, a.y)); p.Uvs.Add(new Vector2(b.z, b.y)); p.Uvs.Add(new Vector2(c.z, c.y));
            var list = submesh == AccentSubmesh ? p.Accent : p.Body;
            list.Add(i0); list.Add(i0 + 1); list.Add(i0 + 2);
        }

        /// <summary>Two unit vectors perpendicular to <paramref name="axis"/> and to each other. For
        /// an axis along ±Z, u = +X and v = +Y, so angle π/2 is straight up.</summary>
        static void Basis(Vector3 axis, out Vector3 u, out Vector3 v, Vector3? refUp = null)
        {
            var up = refUp ?? Vector3.up;
            if (Mathf.Abs(Vector3.Dot(axis, up)) > 0.98f) up = Mathf.Abs(axis.z) < 0.9f ? Vector3.forward : Vector3.right;
            // v = the component of up perpendicular to the axis, u = v × axis, flipped to +X for a
            // body-axis (±Z) frustum so both ends of the body share one ring orientation.
            v = (up - axis * Vector3.Dot(up, axis)).normalized;
            u = Vector3.Cross(v, axis).normalized;
            if (Vector3.Dot(u, Vector3.right) < 0f && Mathf.Abs(axis.z) > 0.9f) u = -u;
        }

        // ------------------------------------------------------------------ elemental morphs

        /// <summary>
        /// The four element extremes, as perturbations of the authored settings — floats only
        /// (an int changes topology and <see cref="BakeMorphSet"/> throws). Each says on the hull
        /// what the element means in the fleet's convention:
        /// <b>Charge</b> (threat/energy) — the ridge fins stand tall, the tail crystal swells and
        /// the eyes burn bigger; <b>Mass</b> (size/volume) — the plates and the head thicken;
        /// <b>Space</b> (reach) — the body and the tail draw out long; <b>Time</b> (rate/mobility)
        /// — the legs lengthen and the ears prick up, a stoat ready to bolt.
        /// </summary>
        public static Settings ApplyElementExtreme(Settings s, Element element)
        {
            switch (element)
            {
                case Element.Charge:
                    s.FinHeight *= 2.2f;
                    s.FinWidth *= 1.25f;
                    s.CrystalSize *= 1.8f;
                    s.EyeSize *= 1.5f;
                    break;
                case Element.Mass:
                    s.ChestRadius *= 1.35f;
                    s.HipRadius *= 1.35f;
                    s.HeadRadius *= 1.2f;
                    s.TailRootRadius *= 1.3f;
                    break;
                case Element.Space:
                    s.BodyLength *= 1.3f;
                    s.TailLength *= 1.4f;
                    s.HeadLength *= 1.15f;
                    break;
                case Element.Time:
                    s.LegLength *= 1.8f;
                    s.EarSize *= 1.6f;
                    s.TailTipRadius *= 0.7f;
                    break;
            }
            return s;
        }

        public sealed class PartMorphDelta
        {
            public Vector3[] VertDeltas;
            public Vector3[] NormalDeltas;
            public bool Any;
        }

        /// <summary>The base build plus the four element extremes, pre-differenced. Deltas are
        /// in HULL space and pivots stay at the BASE build's — an element changes the geometry,
        /// never where a part is hinged, so the morph (vertices) and the lope (transforms) never
        /// share a channel.</summary>
        public sealed class MorphSet
        {
            public List<Part> BaseParts;
            /// <summary>[element index, in <see cref="MorphElements"/> order][part index].</summary>
            public PartMorphDelta[][] Deltas;
            public Vector3[] BoundsMin;
            public Vector3[] BoundsMax;
            public bool[] PartMorphs;
        }

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

            // Each element's contribution is independent and linear in its weight, so the box
            // over every weight combination is base + Σ min(0,d) .. base + Σ max(0,d).
            for (int p = 0; p < baseParts.Count; p++)
            {
                var bp = baseParts[p];
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
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

        /// <summary>Blend one part at the given weights (<see cref="MorphElements"/> order, each
        /// 0..1) into the supplied buffers, in hull space.</summary>
        public static void BlendPart(MorphSet set, int partIndex, float[] weights, List<Vector3> verts, List<Vector3> normals)
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

        static void AssertSameTopology(List<Part> baseParts, List<Part> extreme, Element element)
        {
            if (baseParts.Count != extreme.Count)
                throw new InvalidOperationException(
                    $"[StoatHullForm] {element} extreme changed the PART count ({baseParts.Count} → {extreme.Count}). " +
                    "An element morph may move floats only.");
            for (int p = 0; p < baseParts.Count; p++)
            {
                if (baseParts[p].Verts.Count == extreme[p].Verts.Count
                    && baseParts[p].Body.Count == extreme[p].Body.Count
                    && baseParts[p].Accent.Count == extreme[p].Accent.Count)
                    continue;
                throw new InvalidOperationException(
                    $"[StoatHullForm] {element} extreme changed the topology of part '{baseParts[p].Name}' " +
                    $"(verts {baseParts[p].Verts.Count} → {extreme[p].Verts.Count}). ApplyElementExtreme must touch floats only.");
            }
        }
    }
}
