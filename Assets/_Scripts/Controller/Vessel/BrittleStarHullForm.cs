using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's procedural hull as a PURE function (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §2):
    /// settings in, parts out, no scene and no Unity object, so <c>BrittleStarHullFormTests</c> run it as-is.
    /// <see cref="BrittleStarHullBuilder"/> only emits what this returns. The split, the morph set and the topology
    /// assert are the <see cref="StoatHullForm"/> shape, deliberately.
    ///
    /// <para><b>The look</b> is the class document's concept art: a cockpit hub inside a PENTAGON support ring,
    /// joined by five thin arms (the animal's), with an engine pod at the back and a gun barrel at the front of every
    /// point of the pentagon, and one more gun on the cockpit's nose. Every face is flat-shaded (unshared vertices),
    /// the prism-cut read the HyperSea's crystals and prisms already have.</para>
    ///
    /// <para><b>Element morphs</b> are the document's art direction, one per element: Mass a larger cockpit, Charge
    /// larger thrusters and guns, Space elongated barrels, Time the outer ring curving from a pentagon into a circle.
    /// Each element's extreme is this same function at one changed setting; ints are topology and never morph.</para>
    ///
    /// <para><b>Parts.</b> Part 0 is the COCKPIT (it renders on the builder's own GameObject, the one
    /// <c>VesselCustomization</c> paints). The RING (struts, arms, pods and barrels in one mesh) is a child that spins
    /// about the nose axis with the firing rotation and is what Ring Detonation throws. Six FLAME cones (the cockpit's
    /// and one per pod) are children the animation lengthens with thrust.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour onto slot <b>1</b>,
    /// so the nozzles, the cockpit's canopy and the flames are submesh 1 and the plating submesh 0.</para>
    /// </summary>
    public static class BrittleStarHullForm
    {
        public const int BodySubmesh = 0;
        public const int AccentSubmesh = 1;
        /// <summary>Points of the star. The class is a pentagon; this is a constant, not a setting.</summary>
        public const int Points = 5;

        /// <summary>The order every morph array uses.</summary>
        public static readonly Element[] MorphElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public enum PartKind { Cockpit = 0, Ring = 1, Flame = 2 }

        /// <summary>World units, +Z forward, +Y up. Ints are TOPOLOGY and never morph.</summary>
        public struct Settings
        {
            public float CockpitRadius;     // the hub's half-width
            public float CockpitLength;     // tail face to nose tip
            public float CockpitTailZ;      // where the hub's tail sits (fixed, so the flame never moves)
            public float RingRadius;        // centre to each point of the pentagon
            public float RingZ;             // the ring's plane along the body axis
            public float StrutThickness;    // the ring's beams
            public int StrutSegments;       // beams per side (topology: Time curves them into an arc)
            public float Roundness;         // 0 = straight pentagon sides, 1 = a circle through the points
            public float ArmThickness;      // the five arms from the hub to the points
            public float PodRadius;         // each point's engine pod half-width
            public float PodLength;         // pod tail to pod front
            public float PodTailZ;          // the pods' tail plane (fixed, so the flames never move)
            public float BarrelRadius;      // the gun barrel's half-width
            public float BarrelLength;      // pod front to muzzle
            public float FlameRadius;       // a nozzle flame's half-width at its root
        }

        /// <summary>The shipped proportions: a ring 12 u across (radius 6 - 2.5x the first cut, Garrett 2026-10-14, so the
        /// five corner trails of Creation Mode lie well apart) round a cockpit near the Sparrow's size.</summary>
        public static Settings Defaults => new()
        {
            CockpitRadius = 0.62f,
            CockpitLength = 2.1f,
            CockpitTailZ = -0.9f,
            RingRadius = 6f,
            RingZ = -0.35f,
            StrutThickness = 0.22f,
            StrutSegments = 6,
            Roundness = 0f,
            ArmThickness = 0.14f,
            PodRadius = 0.26f,
            PodLength = 1.0f,
            PodTailZ = -0.85f,
            BarrelRadius = 0.07f,
            BarrelLength = 0.75f,
            FlameRadius = 0.2f,
        };

        /// <summary>One element's extreme: the same hull with that element's setting at its far end.</summary>
        public static Settings Extreme(Settings s, Element element)
        {
            switch (element)
            {
                case Element.Mass:
                    s.CockpitRadius *= 1.45f;
                    s.CockpitLength *= 1.25f;
                    break;
                case Element.Charge:
                    s.PodRadius *= 1.5f;
                    s.PodLength *= 1.3f;
                    s.BarrelRadius *= 1.6f;
                    break;
                case Element.Space:
                    s.BarrelLength *= 2.2f;
                    break;
                case Element.Time:
                    s.Roundness = 1f;
                    break;
            }
            return s;
        }

        public sealed class Part
        {
            public string Name;
            public PartKind Kind;
            /// <summary>Index within its kind (-1 = the cockpit's flame, 0..4 = a point).</summary>
            public int Index;
            /// <summary>Where this part's transform sits, in hull space. Geometry is in hull space.</summary>
            public Vector3 Pivot;
            public readonly List<Vector3> Verts = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<int> Body = new();
            public readonly List<int> Accent = new();
        }

        // ------------------------------------------------------------------ layout (shared with the executor)

        /// <summary>The angle of point <paramref name="index"/> round the nose axis: point 0 straight up.</summary>
        public static float PointAngle(int index) => Mathf.PI * 0.5f + index * (Mathf.PI * 2f / Points);

        /// <summary>Point <paramref name="index"/> of the pentagon in hull space (ring plane).</summary>
        public static Vector3 PointPosition(in Settings s, int index)
        {
            float a = PointAngle(index);
            return new Vector3(Mathf.Cos(a) * s.RingRadius, Mathf.Sin(a) * s.RingRadius, s.RingZ);
        }

        /// <summary>The muzzle of point <paramref name="index"/>'s gun, or of the cockpit's (index -1).</summary>
        public static Vector3 MuzzlePosition(in Settings s, int index)
        {
            if (index < 0) return new Vector3(0f, 0f, s.CockpitTailZ + s.CockpitLength + s.BarrelLength * 0.6f);
            var p = PointPosition(s, index);
            return new Vector3(p.x, p.y, s.PodTailZ + s.PodLength + s.BarrelLength);
        }

        /// <summary>Where point <paramref name="index"/>'s flame roots (the pod's tail), or the cockpit's (-1).</summary>
        public static Vector3 NozzlePosition(in Settings s, int index)
        {
            if (index < 0) return new Vector3(0f, 0f, s.CockpitTailZ);
            var p = PointPosition(s, index);
            return new Vector3(p.x, p.y, s.PodTailZ);
        }

        /// <summary>A point on side <paramref name="side"/> (point side → side+1) at <paramref name="t"/> in 0..1,
        /// blended from the straight chord to the circle's arc by <see cref="Settings.Roundness"/>.</summary>
        public static Vector3 RingEdgePoint(in Settings s, int side, float t)
        {
            float a0 = PointAngle(side), a1 = PointAngle(side + 1);
            var p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * s.RingRadius;
            var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * s.RingRadius;
            Vector2 chord = Vector2.Lerp(p0, p1, t);
            float a = Mathf.Lerp(a0, a1, t);
            Vector2 arc = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s.RingRadius;
            Vector2 q = Vector2.Lerp(chord, arc, Mathf.Clamp01(s.Roundness));
            return new Vector3(q.x, q.y, s.RingZ);
        }

        // ------------------------------------------------------------------ generate

        public static List<Part> Generate(Settings s)
        {
            if (s.StrutSegments < 1) throw new ArgumentException("[BrittleStarHullForm] StrutSegments must be >= 1.");

            var parts = new List<Part>();

            // Part 0: the cockpit hub — a pentagonal pod, nose forward, with a domain-coloured canopy and nose gun.
            var cockpit = new Part { Name = "Cockpit", Kind = PartKind.Cockpit, Index = -1, Pivot = Vector3.zero };
            BuildCockpit(cockpit, s);
            parts.Add(cockpit);

            // Part 1: the ring — struts, arms, pods and barrels. Pivot at the hull's axis in the ring plane, so a
            // spin about local Z turns it about the nose axis.
            var ring = new Part { Name = "Ring", Kind = PartKind.Ring, Index = 0, Pivot = new Vector3(0f, 0f, s.RingZ) };
            BuildRing(ring, s);
            parts.Add(ring);

            // Parts 2..7: the flames. Each is a unit-length cone along -Z from its nozzle; the animation scales Z.
            parts.Add(Flame("Flame_C", -1, NozzlePosition(s, -1), s.FlameRadius * 1.4f));
            for (int i = 0; i < Points; i++)
                parts.Add(Flame("Flame_" + i, i, NozzlePosition(s, i), s.FlameRadius));
            return parts;
        }

        static void BuildCockpit(Part part, in Settings s)
        {
            float tail = s.CockpitTailZ;
            float shoulder = tail + s.CockpitLength * 0.55f;
            float nose = tail + s.CockpitLength;
            float r = s.CockpitRadius;
            // Rings of the pentagonal section: tail (narrow), shoulder (full), nose (a point).
            var tailRing = Section(Points, r * 0.6f, tail, 0f);
            var waist = Section(Points, r, tail + s.CockpitLength * 0.2f, 0f);
            var shoulderRing = Section(Points, r * 0.92f, shoulder, 0f);
            Loft(part, tailRing, waist, BodySubmesh);
            Loft(part, waist, shoulderRing, BodySubmesh);
            Cone(part, shoulderRing, new Vector3(0f, 0f, nose), BodySubmesh);
            // The tail face: the domain-coloured nozzle the cockpit's flame roots in.
            Cap(part, tailRing, new Vector3(0f, 0f, tail - r * 0.15f), AccentSubmesh, Centroid(waist));

            // The canopy: a small domain-coloured crystal on the top of the shoulder.
            var canopyBase = new Vector3(0f, r * 0.82f, shoulder - s.CockpitLength * 0.12f);
            Crystal(part, canopyBase, r * 0.32f, r * 0.9f, AccentSubmesh);

            // The nose gun.
            Beam(part, new Vector3(0f, 0f, nose - s.CockpitLength * 0.15f),
                 new Vector3(0f, 0f, nose + s.BarrelLength * 0.6f), s.BarrelRadius, BodySubmesh);
        }

        static void BuildRing(Part part, in Settings s)
        {
            int n = s.StrutSegments;
            for (int side = 0; side < Points; side++)
            {
                for (int k = 0; k < n; k++)
                {
                    Vector3 a = RingEdgePoint(s, side, k / (float)n);
                    Vector3 b = RingEdgePoint(s, side, (k + 1) / (float)n);
                    Beam(part, a, b, s.StrutThickness, BodySubmesh);
                }
            }

            for (int i = 0; i < Points; i++)
            {
                Vector3 point = PointPosition(s, i);
                // The arm: from the hub's waist out to the point, the brittle star's own limb.
                Vector3 dir = new Vector3(point.x, point.y, 0f).normalized;
                Vector3 root = new Vector3(dir.x * s.CockpitRadius * 0.7f, dir.y * s.CockpitRadius * 0.7f, s.RingZ);
                Beam(part, root, point, s.ArmThickness, BodySubmesh);

                // The engine pod: a pentagonal nacelle, nozzle at the back (domain colour), gun out of the front.
                float tail = s.PodTailZ, front = s.PodTailZ + s.PodLength;
                var back = Section(Points, s.PodRadius * 0.7f, tail, 0f, point);
                var mid = Section(Points, s.PodRadius, tail + s.PodLength * 0.3f, 0f, point);
                var fore = Section(Points, s.PodRadius * 0.75f, front, 0f, point);
                Loft(part, back, mid, BodySubmesh);
                Loft(part, mid, fore, BodySubmesh);
                Cap(part, back, new Vector3(point.x, point.y, tail - s.PodRadius * 0.2f), AccentSubmesh, Centroid(mid));
                Cap(part, fore, new Vector3(point.x, point.y, front + s.PodRadius * 0.15f), BodySubmesh, Centroid(mid));
                Beam(part, new Vector3(point.x, point.y, front - s.PodLength * 0.1f),
                     new Vector3(point.x, point.y, front + s.BarrelLength), s.BarrelRadius, BodySubmesh);
            }

            // Geometry is built in hull space; the pivot is subtracted by the builder.
        }

        static Part Flame(string name, int index, Vector3 nozzle, float radius)
        {
            var part = new Part { Name = name, Kind = PartKind.Flame, Index = index, Pivot = nozzle };
            var root = Section(6, radius, nozzle.z, 0f, new Vector3(nozzle.x, nozzle.y, 0f));
            Cone(part, root, new Vector3(nozzle.x, nozzle.y, nozzle.z - 1f), AccentSubmesh);
            Cap(part, root, new Vector3(nozzle.x, nozzle.y, nozzle.z + radius * 0.3f), AccentSubmesh,
                new Vector3(nozzle.x, nozzle.y, nozzle.z - 0.3f));
            return part;
        }

        // ------------------------------------------------------------------ primitives (flat-shaded)
        //
        // Every primitive is CONVEX and knows a point inside itself, and every triangle is oriented so its normal points
        // AWAY from that point. That makes the winding right by construction rather than by the order points happen to be
        // listed in, and it is exact because the pieces are convex (vessel rule 47's caveat is about non-convex pieces).

        static Vector3 Centroid(Vector3[] ring)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < ring.Length; i++) c += ring[i];
            return c / Mathf.Max(1, ring.Length);
        }

        /// <summary>A ring of <paramref name="sides"/> points at depth <paramref name="z"/>, first point straight up,
        /// centred on <paramref name="centre"/>'s x/y.</summary>
        static Vector3[] Section(int sides, float radius, float z, float twist, Vector3 centre = default)
        {
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 0.5f + twist + i * Mathf.PI * 2f / sides;
                ring[i] = new Vector3(centre.x + Mathf.Cos(a) * radius, centre.y + Mathf.Sin(a) * radius, z);
            }
            return ring;
        }

        static void Loft(Part part, Vector3[] a, Vector3[] b, int submesh)
        {
            Vector3 inside = (Centroid(a) + Centroid(b)) * 0.5f;
            int n = a.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(part, a[i], a[j], b[j], submesh, inside);
                Tri(part, a[i], b[j], b[i], submesh, inside);
            }
        }

        static void Cone(Part part, Vector3[] ring, Vector3 tip, int submesh)
        {
            Vector3 inside = (Centroid(ring) * 2f + tip) / 3f;
            int n = ring.Length;
            for (int i = 0; i < n; i++) Tri(part, ring[i], ring[(i + 1) % n], tip, submesh, inside);
        }

        /// <summary>Close <paramref name="ring"/> with a shallow point at <paramref name="centre"/>, facing away from
        /// <paramref name="inside"/> (a point inside the body the cap closes).</summary>
        static void Cap(Part part, Vector3[] ring, Vector3 centre, int submesh, Vector3 inside)
        {
            int n = ring.Length;
            for (int i = 0; i < n; i++) Tri(part, ring[i], ring[(i + 1) % n], centre, submesh, inside);
        }

        /// <summary>A square-section beam from <paramref name="a"/> to <paramref name="b"/>, end-capped.</summary>
        static void Beam(Part part, Vector3 a, Vector3 b, float halfWidth, int submesh)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.forward * 1e-4f;
            Vector3 d = axis.normalized;
            Vector3 helper = Mathf.Abs(Vector3.Dot(d, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
            Vector3 u = Vector3.Cross(d, helper).normalized * halfWidth;
            Vector3 v = Vector3.Cross(d, u).normalized * halfWidth;
            var ra = new[] { a + u, a + v, a - u, a - v };
            var rb = new[] { b + u, b + v, b - u, b - v };
            Vector3 mid = (a + b) * 0.5f;
            Loft(part, ra, rb, submesh);
            Cap(part, ra, a - d * halfWidth * 0.5f, submesh, mid);
            Cap(part, rb, b + d * halfWidth * 0.5f, submesh, mid);
        }

        /// <summary>An upright four-sided crystal: a diamond base, a point above it and one below.</summary>
        static void Crystal(Part part, Vector3 basePoint, float halfWidth, float height, int submesh)
        {
            var ring = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                ring[i] = basePoint + new Vector3(Mathf.Cos(a) * halfWidth, 0f, Mathf.Sin(a) * halfWidth * 1.6f);
            }
            var top = basePoint + Vector3.up * height * 0.6f;
            var bottom = basePoint - Vector3.up * height * 0.25f;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Tri(part, ring[i], ring[j], top, submesh, basePoint);
                Tri(part, ring[i], ring[j], bottom, submesh, basePoint);
            }
        }

        /// <summary>One flat triangle facing away from <paramref name="inside"/>, emitted in Unity's front-face order:
        /// for indices (i0, i1, i2) Unity's front normal is cross(v1 - v0, v2 - v0), the stored normal.</summary>
        static void Tri(Part part, Vector3 a, Vector3 b, Vector3 c, int submesh, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
            {
                (b, c) = (c, b);
                n = -n;
            }
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            int i0 = part.Verts.Count;
            part.Verts.Add(a); part.Verts.Add(b); part.Verts.Add(c);
            part.Normals.Add(n); part.Normals.Add(n); part.Normals.Add(n);
            part.Uvs.Add(new Vector2(0f, 0f)); part.Uvs.Add(new Vector2(1f, 0f)); part.Uvs.Add(new Vector2(0.5f, 1f));
            var list = submesh == AccentSubmesh ? part.Accent : part.Body;
            list.Add(i0); list.Add(i0 + 1); list.Add(i0 + 2);
        }

        // ------------------------------------------------------------------ morph set

        public sealed class MorphSet
        {
            public List<Part> BaseParts;
            /// <summary>[element][part] vertex delta from the base, in hull space.</summary>
            public List<Vector3[]>[] VertDeltas;
            public List<Vector3[]>[] NormalExtremes;
            /// <summary>Whether ANY element moves part p (unmoved parts are never rewritten).</summary>
            public bool[] PartMorphs;
            /// <summary>Per part, the union of the base and every extreme (and so of every blend of them).</summary>
            public Vector3[] BoundsMin, BoundsMax;
        }

        /// <summary>The base hull and the four element extremes in one pass. Throws if an element ever changes
        /// topology (a vertex or index count), loudly, at bake time rather than as a garbled blend.</summary>
        public static MorphSet BakeMorphSet(Settings s)
        {
            var set = new MorphSet
            {
                BaseParts = Generate(s),
                VertDeltas = new List<Vector3[]>[MorphElements.Length],
                NormalExtremes = new List<Vector3[]>[MorphElements.Length],
            };
            int partCount = set.BaseParts.Count;
            set.PartMorphs = new bool[partCount];
            set.BoundsMin = new Vector3[partCount];
            set.BoundsMax = new Vector3[partCount];
            for (int p = 0; p < partCount; p++)
            {
                var verts = set.BaseParts[p].Verts;
                Vector3 mn = verts.Count > 0 ? verts[0] : Vector3.zero, mx = mn;
                for (int v = 0; v < verts.Count; v++) { mn = Vector3.Min(mn, verts[v]); mx = Vector3.Max(mx, verts[v]); }
                set.BoundsMin[p] = mn;
                set.BoundsMax[p] = mx;
            }

            for (int e = 0; e < MorphElements.Length; e++)
            {
                var extreme = Generate(Extreme(s, MorphElements[e]));
                if (extreme.Count != partCount)
                    throw new InvalidOperationException($"[BrittleStarHullForm] {MorphElements[e]} changed the part count.");
                set.VertDeltas[e] = new List<Vector3[]>(partCount);
                set.NormalExtremes[e] = new List<Vector3[]>(partCount);
                for (int p = 0; p < partCount; p++)
                {
                    var bp = set.BaseParts[p];
                    var xp = extreme[p];
                    if (xp.Verts.Count != bp.Verts.Count || xp.Body.Count != bp.Body.Count || xp.Accent.Count != bp.Accent.Count)
                        throw new InvalidOperationException(
                            $"[BrittleStarHullForm] {MorphElements[e]} changed the topology of part '{bp.Name}'.");
                    var deltas = new Vector3[bp.Verts.Count];
                    for (int v = 0; v < deltas.Length; v++)
                    {
                        deltas[v] = xp.Verts[v] - bp.Verts[v];
                        if (deltas[v].sqrMagnitude > 1e-12f) set.PartMorphs[p] = true;
                        set.BoundsMin[p] = Vector3.Min(set.BoundsMin[p], xp.Verts[v]);
                        set.BoundsMax[p] = Vector3.Max(set.BoundsMax[p], xp.Verts[v]);
                    }
                    set.VertDeltas[e].Add(deltas);
                    set.NormalExtremes[e].Add(xp.Normals.ToArray());
                }
            }
            // Deltas are linear, so the union of the base and the four single-element extremes bounds every
            // weight combination only if extremes ADD; widen by the summed deltas to cover all four at once.
            for (int p = 0; p < partCount; p++)
            {
                var bp = set.BaseParts[p];
                for (int v = 0; v < bp.Verts.Count; v++)
                {
                    Vector3 sum = bp.Verts[v];
                    for (int e = 0; e < MorphElements.Length; e++) sum += set.VertDeltas[e][p][v];
                    set.BoundsMin[p] = Vector3.Min(set.BoundsMin[p], sum);
                    set.BoundsMax[p] = Vector3.Max(set.BoundsMax[p], sum);
                }
            }
            return set;
        }

        /// <summary>Blend part <paramref name="p"/> at element weights <paramref name="w"/> (MorphElements order, each
        /// 0..1) into the two output lists, in hull space. Normals blend toward each extreme's and renormalise.</summary>
        public static void BlendPart(MorphSet set, int p, float[] w, List<Vector3> verts, List<Vector3> normals)
        {
            var bp = set.BaseParts[p];
            verts.Clear();
            normals.Clear();
            for (int v = 0; v < bp.Verts.Count; v++)
            {
                Vector3 pos = bp.Verts[v];
                Vector3 nrm = bp.Normals[v];
                for (int e = 0; e < MorphElements.Length; e++)
                {
                    float we = w[e];
                    if (we <= 0f) continue;
                    pos += set.VertDeltas[e][p][v] * we;
                    nrm += (set.NormalExtremes[e][p][v] - bp.Normals[v]) * we;
                }
                verts.Add(pos);
                normals.Add(nrm.sqrMagnitude > 1e-12f ? nrm.normalized : bp.Normals[v]);
            }
        }
    }
}
