using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's procedural hull as a PURE function (design: <c>R_VesselActions/THRESHER.md</c> §5b):
    /// settings in, parts out, no scene and no Unity object — so <c>ThresherHullFormTests</c> and
    /// <c>Tools/Build/thresher_hull_harness</c> run it as-is. <see cref="ThresherHullBuilder"/> only emits
    /// what this returns. The split, the morph set and the topology assert are the
    /// <see cref="StoatHullForm"/> / <see cref="ScarabHullForm"/> shape, deliberately.
    ///
    /// <para><b>The look</b> is the animal the vessel is named for: a thresher shark, cut from flat
    /// facets like the HyperSea's prisms. A six-sided fuselage lofted from a pointed snout to a slim
    /// caudal peduncle, a swept dorsal fin, two drooping pectorals, a small lower caudal lobe — and the
    /// thresher's signature, an UPPER caudal lobe nearly as long as the body, curving up and back like a
    /// scythe. The chain leaves the tail fork through a ring (the HITCH), so the wrecking ball reads as
    /// the end of the shark's whip, which is what a thresher shark's tail is.</para>
    ///
    /// <para><b>Parts are separate so the swim can bend the tail</b> (<see cref="ThresherAnimation"/>):
    /// the body (part 0, the CORE, which renders on the builder's own GameObject — the one
    /// <c>VesselCustomization</c> paints), two pectorals, the tail base (peduncle, lower lobe and the
    /// hitch) and the upper lobe in <see cref="Settings.LobeSegments"/> segments, each hinged where the
    /// previous one ends, so a sweep can travel down it as a wave.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour onto
    /// slot <b>1</b>, so the accents — the dorsal fin, the eyes, the lobe's trailing BLADE and the hitch —
    /// are submesh 1 and the hide is submesh 0. A pilot reads the domain off the fin and the scythe.</para>
    /// </summary>
    public static class ThresherHullForm
    {
        public const int BodySubmesh = 0;
        public const int AccentSubmesh = 1;

        /// <summary>The order every morph array uses.</summary>
        public static readonly Element[] MorphElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public enum PartKind { Core = 0, Pectoral = 1, TailBase = 2, Lobe = 3 }

        /// <summary>Stations along the body, nose (0) to peduncle (1), and the girth at each as a fraction of
        /// <see cref="Settings.BodyRadius"/>. The nose apex sits in front of the first station. Topology: fixed.</summary>
        static readonly float[] StationT = { 0.08f, 0.2f, 0.35f, 0.55f, 0.75f, 1f };
        static readonly float[] StationGirth = { 0.5f, 0.86f, 1f, 0.88f, 0.6f, 0.3f };

        /// <summary>Sub-steps per lobe segment along the scythe's curve.</summary>
        public const int LobeStepsPerSegment = 4;

        /// <summary>World units, +Z forward, +Y up. Ints are TOPOLOGY and never morph.</summary>
        public struct Settings
        {
            public int Sides;              // faces around the body: 6 = a ridge on the back and a keel underneath
            public float BodyLength;       // snout tip to the peduncle
            public float BodyRadius;       // half-width at the girthiest station
            public float BodyFlatten;      // height / width of the cross-section
            public float EyeSize;
            public float FinThickness;     // every fin's thickness at its root
            public float DorsalHeight;
            public float DorsalChord;      // the dorsal's base length along the back
            public float DorsalSweep;      // how far back the tip leans, per unit of height
            public float PectoralSpan;
            public float PectoralChord;
            public float PectoralSweep;    // how far back the tip sits, per unit of span
            public float PectoralDroop;    // degrees below horizontal
            public float PeduncleLength;   // the tail base from the body to the caudal fork
            public float LowerLobeLength;
            public int LobeSegments;       // the upper lobe's hinged segments
            public float LobeLength;       // the upper lobe along its curve
            public float LobeChord;        // its width at the root
            public float LobeRise;         // degrees above straight back at the root
            public float LobeCurl;         // extra degrees of rise by the tip
            public float LobeBlade;        // 0..1: the trailing share of the chord drawn in the domain colour
            public float HitchRadius;      // the tow ring at the caudal fork
            public float HitchThickness;
        }

        /// <summary>The shipped proportions: a ~4.4 u body (the Squirrel hull it replaces is ~4 u), a
        /// 3.6 u scythe, ~3.4 u across the pectorals.</summary>
        public static Settings Defaults => new()
        {
            Sides = 6,
            BodyLength = 4.4f,
            BodyRadius = 0.62f,
            BodyFlatten = 0.86f,
            EyeSize = 0.085f,
            FinThickness = 0.08f,
            DorsalHeight = 0.95f,
            DorsalChord = 1.05f,
            DorsalSweep = 0.55f,
            PectoralSpan = 1.35f,
            PectoralChord = 0.9f,
            PectoralSweep = 0.75f,
            PectoralDroop = 18f,
            PeduncleLength = 0.5f,
            LowerLobeLength = 0.75f,
            LobeSegments = 3,
            LobeLength = 3.6f,
            LobeChord = 0.62f,
            LobeRise = 24f,
            LobeCurl = 22f,
            LobeBlade = 0.4f,
            HitchRadius = 0.2f,
            HitchThickness = 0.055f,
        };

        public sealed class Part
        {
            public string Name;
            public PartKind Kind;
            /// <summary>Index within its kind (0 = left pectoral / lobe root segment).</summary>
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

        public static float NoseZ(in Settings s) => s.BodyLength * 0.5f;
        public static float PeduncleZ(in Settings s) => -s.BodyLength * 0.5f;
        /// <summary>The caudal fork: where the peduncle ends, the lobes leave and the chain is hitched.</summary>
        public static Vector3 ForkPoint(in Settings s) => new(0f, 0.02f, PeduncleZ(s) - s.PeduncleLength);
        /// <summary>The centre of the tow ring, in hull space — where the chain is drawn from.</summary>
        public static Vector3 HitchCenter(in Settings s) =>
            ForkPoint(s) + new Vector3(0f, -(s.BodyRadius * 0.18f + s.HitchRadius * 0.8f), s.PeduncleLength * 0.55f);

        static float StationZ(in Settings s, float t) => NoseZ(s) - t * s.BodyLength;

        /// <summary>The body's half-width at <paramref name="t"/> (0 nose .. 1 peduncle), piecewise-linear
        /// through the station table; zero at the snout apex.</summary>
        public static float GirthAt(in Settings s, float t)
        {
            if (t <= 0f) return 0f;
            if (t < StationT[0]) return s.BodyRadius * StationGirth[0] * t / StationT[0];
            for (int i = 1; i < StationT.Length; i++)
                if (t <= StationT[i])
                    return s.BodyRadius * Mathf.Lerp(StationGirth[i - 1], StationGirth[i],
                        (t - StationT[i - 1]) / (StationT[i] - StationT[i - 1]));
            return s.BodyRadius * StationGirth[StationGirth.Length - 1];
        }

        /// <summary>Where the pectorals are hinged, ±X (left = -1).</summary>
        public static Vector3 PectoralRoot(in Settings s, int side)
        {
            const float t = 0.33f;
            float r = GirthAt(s, t);
            return new Vector3(side * r * 0.78f, -r * s.BodyFlatten * 0.42f, StationZ(s, t));
        }

        /// <summary>The upper lobe's centreline at <paramref name="u"/> (0 root .. 1 tip), in hull space.
        /// The angle above straight back grows from <c>LobeRise</c> to <c>LobeRise + LobeCurl</c> (quadratic),
        /// integrated in fixed steps so a morph never changes the vertex count.</summary>
        public static Vector3 LobePoint(in Settings s, float u)
        {
            u = Mathf.Clamp01(u);
            const int steps = 48;
            var p = ForkPoint(s);
            float du = 1f / steps;
            for (int i = 0; i < steps; i++)
            {
                float a = i * du;
                if (a >= u) break;
                float step = Mathf.Min(du, u - a);
                float mid = a + step * 0.5f;
                p += LobeDirection(s, mid) * (s.LobeLength * step);
            }
            return p;
        }

        /// <summary>The lobe's tangent at <paramref name="u"/>: back and up, in the YZ plane.</summary>
        public static Vector3 LobeDirection(in Settings s, float u)
        {
            float phi = (s.LobeRise + s.LobeCurl * u * u) * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Sin(phi), -Mathf.Cos(phi));
        }

        static float LobeChordAt(in Settings s, float u) => s.LobeChord * Mathf.Lerp(1f, 0.06f, Mathf.Pow(u, 0.85f));

        // ------------------------------------------------------------------ generate

        public static List<Part> Generate(Settings s)
        {
            if (s.Sides < 4) throw new ArgumentException("[ThresherHullForm] Sides must be >= 4.");
            if (s.LobeSegments < 1) throw new ArgumentException("[ThresherHullForm] LobeSegments must be >= 1.");

            var parts = new List<Part> { Body(s) };
            parts.Add(Pectoral(s, -1));
            parts.Add(Pectoral(s, +1));
            parts.Add(TailBase(s));
            for (int k = 0; k < s.LobeSegments; k++) parts.Add(LobeSegment(s, k));
            return parts;
        }

        static Vector3 RingPoint(in Settings s, float t, int k)
        {
            float r = GirthAt(s, t);
            // Vertex 0 straight UP: a ridge along the back for the dorsal to stand on, a keel underneath.
            float th = Mathf.PI * 0.5f + k * Mathf.PI * 2f / s.Sides;
            return new Vector3(Mathf.Cos(th) * r, Mathf.Sin(th) * r * s.BodyFlatten, StationZ(s, t));
        }

        static Part Body(in Settings s)
        {
            var p = new Part { Name = "ThresherBody", Kind = PartKind.Core, Index = 0, Pivot = Vector3.zero };
            int n = s.Sides;
            // The snout: a fan from an apex a little below the axis (a shark's snout overhangs its jaw).
            var apex = new Vector3(0f, -s.BodyRadius * 0.08f, NoseZ(s) + s.BodyLength * 0.02f);
            var axisFront = new Vector3(0f, 0f, StationZ(s, StationT[0]));
            for (int k = 0; k < n; k++)
                AddTri(p, apex, RingPoint(s, StationT[0], k), RingPoint(s, StationT[0], (k + 1) % n),
                    (apex + axisFront) * 0.5f + new Vector3(0f, 0f, -s.BodyLength * 0.05f), BodySubmesh);
            // The flanks: flat quads station to station.
            for (int i = 0; i + 1 < StationT.Length; i++)
            {
                var inside = new Vector3(0f, 0f, (StationZ(s, StationT[i]) + StationZ(s, StationT[i + 1])) * 0.5f);
                for (int k = 0; k < n; k++)
                    AddQuad(p, RingPoint(s, StationT[i], k), RingPoint(s, StationT[i], (k + 1) % n),
                        RingPoint(s, StationT[i + 1], (k + 1) % n), RingPoint(s, StationT[i + 1], k), inside, BodySubmesh);
            }
            // The peduncle cap (the tail base butts onto it).
            float tEnd = StationT[StationT.Length - 1];
            var capC = new Vector3(0f, 0f, StationZ(s, tEnd));
            for (int k = 1; k < n - 1; k++)
                AddTri(p, RingPoint(s, tEnd, 0), RingPoint(s, tEnd, k), RingPoint(s, tEnd, k + 1), capC, BodySubmesh,
                    hint: Vector3.back);

            // Eyes, in the domain colour, just behind the snout on the upper flanks.
            float te = 0.14f, re = GirthAt(s, te);
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector3(side * re * 0.8f, re * s.BodyFlatten * 0.32f, StationZ(s, te));
                AddOctahedron(p, eye, s.EyeSize, s.EyeSize, s.EyeSize * 1.4f, AccentSubmesh);
            }

            // The dorsal fin (domain colour): a thin swept blade standing on the back's ridge.
            float tf = 0.3f, tb = tf + s.DorsalChord / s.BodyLength;
            var front = new Vector3(0f, GirthAt(s, tf) * s.BodyFlatten * 0.9f, StationZ(s, tf));
            var back = new Vector3(0f, GirthAt(s, tb) * s.BodyFlatten * 0.9f, StationZ(s, tb));
            var tip = new Vector3(0f, front.y + s.DorsalHeight, front.z - s.DorsalChord * 0.45f - s.DorsalHeight * s.DorsalSweep);
            AddFin(p, front, back, tip, Vector3.right, s.FinThickness, AccentSubmesh);

            // A small second dorsal near the tail, in the hide colour.
            float t2 = 0.82f, t2b = t2 + 0.07f;
            var f2 = new Vector3(0f, GirthAt(s, t2) * s.BodyFlatten * 0.9f, StationZ(s, t2));
            var b2 = new Vector3(0f, GirthAt(s, t2b) * s.BodyFlatten * 0.9f, StationZ(s, t2b));
            var tip2 = new Vector3(0f, f2.y + s.DorsalHeight * 0.22f, f2.z - s.DorsalHeight * 0.3f);
            AddFin(p, f2, b2, tip2, Vector3.right, s.FinThickness * 0.7f, BodySubmesh);
            return p;
        }

        static Part Pectoral(in Settings s, int side)
        {
            var root = PectoralRoot(s, side);
            var p = new Part
            {
                Name = side < 0 ? "ThresherPectoralL" : "ThresherPectoralR",
                Kind = PartKind.Pectoral,
                Index = side < 0 ? 0 : 1,
                Pivot = root,
            };
            float droop = s.PectoralDroop * Mathf.Deg2Rad;
            var span = new Vector3(side * Mathf.Cos(droop), -Mathf.Sin(droop), 0f);
            var front = root + new Vector3(0f, 0f, s.PectoralChord * 0.35f);
            var back = root - new Vector3(0f, 0f, s.PectoralChord * 0.65f);
            var tip = root + span * s.PectoralSpan + new Vector3(0f, 0f, -s.PectoralSpan * s.PectoralSweep);
            // The fin's thickness runs along its own normal (span × back), mostly vertical.
            var normal = Vector3.Cross(span, Vector3.forward).normalized;
            AddFin(p, front, back, tip, normal, s.FinThickness, BodySubmesh);
            return p;
        }

        static Part TailBase(in Settings s)
        {
            var root = new Vector3(0f, 0f, PeduncleZ(s));
            var p = new Part { Name = "ThresherTailBase", Kind = PartKind.TailBase, Index = 0, Pivot = root };
            float r0 = GirthAt(s, 1f), r1 = r0 * 0.6f;
            var fork = ForkPoint(s);
            AddFrustum(p, root + new Vector3(0f, 0f, s.PeduncleLength * 0.08f), fork, r0, r1, s.Sides, s.BodyFlatten, BodySubmesh);
            // The lower caudal lobe: short, down and back, hide-coloured.
            // It leaves BEHIND the hitch, so the ring and the lobe never cross.
            var lf = fork + new Vector3(0f, -r1 * 0.5f, -s.LowerLobeLength * 0.05f);
            var lb = fork + new Vector3(0f, -r1 * 0.2f, -s.LowerLobeLength * 0.45f);
            var ltip = fork + new Vector3(0f, -s.LowerLobeLength * 0.7f, -s.LowerLobeLength * 0.95f);
            AddFin(p, lf, lb, ltip, Vector3.right, s.FinThickness * 0.8f, BodySubmesh);
            // The hitch: a faceted ring under the fork, its axis across the hull, so the chain reads as
            // leaving the tail. Domain colour.
            AddRing(p, HitchCenter(s), Vector3.right, s.HitchRadius, s.HitchThickness, 8, 4, AccentSubmesh);
            return p;
        }

        static Part LobeSegment(in Settings s, int k)
        {
            float u0 = k / (float)s.LobeSegments, u1 = (k + 1) / (float)s.LobeSegments;
            var p = new Part
            {
                Name = "ThresherLobe" + k,
                Kind = PartKind.Lobe,
                Index = k,
                Pivot = LobePoint(s, u0),
            };
            int steps = LobeStepsPerSegment;
            for (int i = 0; i < steps; i++)
            {
                float ua = Mathf.Lerp(u0, u1, i / (float)steps);
                float ub = Mathf.Lerp(u0, u1, (i + 1) / (float)steps);
                LobeSlice(p, s, ua, ub, closeRoot: i == 0 && k == 0, closeTip: i == steps - 1 && k == s.LobeSegments - 1);
            }
            return p;
        }

        /// <summary>One slice of the scythe between <paramref name="ua"/> and <paramref name="ub"/>: its leading
        /// edge on the centreline, its chord running down and back off it, the trailing <c>LobeBlade</c> share
        /// in the domain colour, both faces and both edges closed.</summary>
        static void LobeSlice(Part p, in Settings s, float ua, float ub, bool closeRoot, bool closeTip)
        {
            Vector3 La = LobePoint(s, ua), Lb = LobePoint(s, ub);
            Vector3 Da = LobeDirection(s, ua), Db = LobeDirection(s, ub);
            // The chord: perpendicular to the tangent in the YZ plane, toward the trailing (lower, rear) side.
            Vector3 Ca = new Vector3(0f, -Da.z, Da.y) * -1f, Cb = new Vector3(0f, -Db.z, Db.y) * -1f;
            if (Ca.z > 0f) Ca = -Ca;
            if (Cb.z > 0f) Cb = -Cb;
            float ca = LobeChordAt(s, ua), cb = LobeChordAt(s, ub);
            float split = 1f - Mathf.Clamp01(s.LobeBlade);
            Vector3 Ma = La + Ca * (ca * split), Mb = Lb + Cb * (cb * split);
            Vector3 Ta = La + Ca * ca, Tb = Lb + Cb * cb;
            float ha = s.FinThickness * 0.5f * Mathf.Lerp(1f, 0.35f, ua), hb = s.FinThickness * 0.5f * Mathf.Lerp(1f, 0.35f, ub);
            var xa = new Vector3(ha, 0f, 0f);
            var xb = new Vector3(hb, 0f, 0f);
            // The blade's trailing edge is a knife: it closes to zero thickness.
            for (int side = -1; side <= 1; side += 2)
            {
                var hint = new Vector3(side, 0f, 0f);
                AddQuad(p, La + xa * side, Lb + xb * side, Mb + xb * side, Ma + xa * side, Vector3.zero, BodySubmesh, hint);
                AddQuad(p, Ma + xa * side, Mb + xb * side, Tb, Ta, Vector3.zero, AccentSubmesh, hint);
            }
            // The leading edge.
            var lead = (Da + Db).normalized;
            var up = Vector3.Cross(Vector3.right, lead);
            AddQuad(p, La - xa, La + xa, Lb + xb, Lb - xb, Vector3.zero, BodySubmesh, up.y < 0f ? -up : up);
            if (closeRoot)
            {
                AddQuad(p, La - xa, Ma - xa, Ma + xa, La + xa, Vector3.zero, BodySubmesh, -Da);
                AddTri(p, Ma - xa, Ta, Ma + xa, Vector3.zero, AccentSubmesh, -Da);
            }
            if (closeTip)
            {
                AddQuad(p, Lb - xb, Lb + xb, Mb + xb, Mb - xb, Vector3.zero, BodySubmesh, Db);
                AddTri(p, Mb - xb, Mb + xb, Tb, Vector3.zero, AccentSubmesh, Db);
            }
        }

        // ------------------------------------------------------------------ primitives (flat-shaded)

        /// <summary>A thin fin: triangle front–back–tip, extruded ±<paramref name="thickness"/>/2 along
        /// <paramref name="across"/>, its tip a knife edge (thickness tapers to a quarter).</summary>
        static void AddFin(Part p, Vector3 front, Vector3 back, Vector3 tip, Vector3 across, float thickness, int submesh)
        {
            var h = across.normalized * (thickness * 0.5f);
            var ht = h * 0.25f;
            var c = (front + back + tip) / 3f;
            AddTri(p, front + h, back + h, tip + ht, c, submesh, hint: h);
            AddTri(p, front - h, back - h, tip - ht, c, submesh, hint: -h);
            AddQuad(p, front - h, front + h, tip + ht, tip - ht, c, submesh);
            AddQuad(p, back - h, back + h, tip + ht, tip - ht, c, submesh);
            AddQuad(p, front - h, front + h, back + h, back - h, c, submesh);
        }

        /// <summary>A prism frustum along a→b with an elliptical (flattened) cross-section, vertex 0 straight up.</summary>
        static void AddFrustum(Part p, Vector3 a, Vector3 b, float ra, float rb, int sides, float flatten, int submesh)
        {
            var ringA = new Vector3[sides];
            var ringB = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float th = Mathf.PI * 0.5f + k * Mathf.PI * 2f / sides;
                var d = new Vector3(Mathf.Cos(th), Mathf.Sin(th) * flatten, 0f);
                ringA[k] = a + d * ra;
                ringB[k] = b + d * rb;
            }
            var mid = (a + b) * 0.5f;
            for (int k = 0; k < sides; k++)
                AddQuad(p, ringA[k], ringA[(k + 1) % sides], ringB[(k + 1) % sides], ringB[k], mid, submesh);
            var axis = (b - a).normalized;
            for (int k = 1; k < sides - 1; k++)
            {
                AddTri(p, ringA[0], ringA[k], ringA[k + 1], a, submesh, hint: -axis);
                AddTri(p, ringB[0], ringB[k], ringB[k + 1], b, submesh, hint: axis);
            }
        }

        /// <summary>A faceted torus: <paramref name="segments"/> around the ring, <paramref name="sides"/>
        /// around the tube, axis <paramref name="axis"/>.</summary>
        static void AddRing(Part p, Vector3 center, Vector3 axis, float radius, float tube, int segments, int sides, int submesh)
        {
            axis = axis.normalized;
            var u = Vector3.Cross(axis, Vector3.forward);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.Cross(axis, Vector3.up);
            u.Normalize();
            var v = Vector3.Cross(axis, u);
            var grid = new Vector3[segments, sides];
            var centres = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                centres[i] = center + radial * radius;
                for (int j = 0; j < sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides + Mathf.PI / sides;
                    grid[i, j] = centres[i] + (radial * Mathf.Cos(b) + axis * Mathf.Sin(b)) * tube;
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int i1 = (i + 1) % segments;
                var inside = (centres[i] + centres[i1]) * 0.5f;
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    AddQuad(p, grid[i, j], grid[i, j1], grid[i1, j1], grid[i1, j], inside, submesh);
                }
            }
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

        static void AddQuad(Part p, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside, int submesh, Vector3? hint = null)
        {
            AddTri(p, a, b, c, inside, submesh, hint);
            AddTri(p, a, c, d, inside, submesh, hint);
        }

        /// <summary>
        /// One flat-shaded triangle, wound so its FRONT faces away from <paramref name="inside"/> (or along
        /// <paramref name="hint"/> when given — a thin fin's faces cannot be told apart by a centroid).
        /// Unity's front face is the side <c>cross(b−a, c−a)</c> points to.
        /// </summary>
        static void AddTri(Part p, Vector3 a, Vector3 b, Vector3 c, Vector3 inside, int submesh, Vector3? hint = null)
        {
            var n = Vector3.Cross(b - a, c - a);
            var outward = hint ?? ((a + b + c) / 3f - inside);
            if (Vector3.Dot(n, outward) < 0f) { var t = b; b = c; c = t; n = -n; }
            n = n.sqrMagnitude > 1e-12f ? n.normalized : (outward.sqrMagnitude > 1e-12f ? outward.normalized : Vector3.up);
            int i0 = p.Verts.Count;
            p.Verts.Add(a); p.Verts.Add(b); p.Verts.Add(c);
            p.Normals.Add(n); p.Normals.Add(n); p.Normals.Add(n);
            p.Uvs.Add(new Vector2(a.z, a.y)); p.Uvs.Add(new Vector2(b.z, b.y)); p.Uvs.Add(new Vector2(c.z, c.y));
            var list = submesh == AccentSubmesh ? p.Accent : p.Body;
            list.Add(i0); list.Add(i0 + 1); list.Add(i0 + 2);
        }

        // ------------------------------------------------------------------ elemental morphs

        /// <summary>
        /// The four element extremes, as perturbations of the authored settings — floats only (an int
        /// changes topology and <see cref="BakeMorphSet"/> throws). Each says on the hull what the element
        /// means for THIS vessel (THRESHER.md §3: Charge = the chain's energy, Mass = the ball's mass mode,
        /// Space = the chain's reach, Time = the spin-up):
        /// <b>Charge</b> (threat) — the dorsal stands tall and the scythe's blade widens;
        /// <b>Mass</b> (size) — the body and the hitch thicken;
        /// <b>Space</b> (reach) — the scythe draws out long;
        /// <b>Time</b> (rate/mobility) — the pectorals spread and sweep back, a shark built to turn.
        /// </summary>
        public static Settings ApplyElementExtreme(Settings s, Element element)
        {
            switch (element)
            {
                case Element.Charge:
                    s.DorsalHeight *= 1.8f;
                    s.DorsalSweep *= 1.2f;
                    s.LobeBlade = Mathf.Min(0.85f, s.LobeBlade * 1.8f);
                    s.EyeSize *= 1.5f;
                    break;
                case Element.Mass:
                    s.BodyRadius *= 1.42f;
                    s.HitchRadius *= 1.4f;
                    s.HitchThickness *= 1.5f;
                    s.LobeChord *= 1.2f;
                    break;
                case Element.Space:
                    s.LobeLength *= 1.45f;
                    s.LobeCurl *= 1.3f;
                    s.BodyLength *= 1.1f;
                    break;
                case Element.Time:
                    s.PectoralSpan *= 1.5f;
                    s.PectoralSweep *= 1.35f;
                    s.PectoralChord *= 0.85f;
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

        /// <summary>The base build plus the four element extremes, pre-differenced. Deltas are in HULL space
        /// and pivots stay at the BASE build's — an element changes the geometry, never where a part is
        /// hinged, so the morph (vertices) and the swim (transforms) never share a channel.</summary>
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

            // Each element's contribution is independent and linear in its weight, so the box over every
            // weight combination is base + Σ min(0,d) .. base + Σ max(0,d).
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

        /// <summary>Blend one part at the given weights (<see cref="MorphElements"/> order, each 0..1) into
        /// the supplied buffers, in hull space.</summary>
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
                    $"[ThresherHullForm] {element} extreme changed the PART count ({baseParts.Count} → {extreme.Count}). " +
                    "An element morph may move floats only.");
            for (int p = 0; p < baseParts.Count; p++)
            {
                if (baseParts[p].Verts.Count == extreme[p].Verts.Count
                    && baseParts[p].Body.Count == extreme[p].Body.Count
                    && baseParts[p].Accent.Count == extreme[p].Accent.Count)
                    continue;
                throw new InvalidOperationException(
                    $"[ThresherHullForm] {element} extreme changed the topology of part '{baseParts[p].Name}' " +
                    $"(verts {baseParts[p].Verts.Count} → {extreme[p].Verts.Count}). ApplyElementExtreme must touch floats only.");
            }
        }
    }
}
