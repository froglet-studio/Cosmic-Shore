using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The procedural base head: an ANATOMICAL SCULPT expressed as a signed-distance field —
    /// braincase, frontal boss, brow ridges, orbital cavities, zygomatic arches, maxilla,
    /// mandible with chin and gonial angles, throat, and a MODELLED nose (dorsum, tip, alae,
    /// columella), lips (cupid's bow, vermilion, fissure) and mentolabial sulcus — sampled along
    /// rays from the skull centre onto a ring × segment grid. It is a pure function of (detail,
    /// form constants, shape): no components, no scene, no randomness.
    ///
    /// HEAD SPACE: origin at the skull centre ON THE EYE LINE, +Y crown, +Z the face, +X the
    /// character's left. Head height ≈ 1 (chin −0.5 … crown +0.5). Every feature generator emits
    /// into this space through its site, so a beetle mandible and a whale melon meet the head at
    /// the same scale.
    ///
    /// Topology is decided by the INTEGER <see cref="HeadDetail"/> only. Axes move vertices and
    /// never change counts, which <c>HeadTopologyTests</c> asserts across a shape sweep.
    ///
    /// This is the file to edit for "the jaw reads wrong", "the brow is too heavy", "the nose is
    /// too small", "the lips don't read": every primitive's position, size and axis curve lives
    /// in <see cref="AnalyticSurface.BuildAnatomy"/>; the site table in <see cref="DefaultSites"/>.
    /// </summary>
    public sealed class ProceduralBaseHead : IBaseHead
    {
        /// <summary>The head's global form. Floats only; retune freely.</summary>
        [Serializable]
        public struct Form
        {
            public Vector3 CraniumCentre, CraniumSemi;   // the braincase ellipsoid
            public float UnionSoftness;                  // smooth-union radius for the big volumes
            public float NeckAxisZ, NeckRadiusX, NeckRadiusZ, NeckTopY, NeckBottomY;
            public float CollarFlare;                    // bust base widening at the bottom rings
            public float SphereZoneDeg;                  // polar angle where ring sampling hands over to the neck
            public float SphereZoneFraction;             // fraction of rings spent on the sphere zone
            public float MarchStart, MarchStep;          // outermost-crossing search along each ray

            public static Form Default => new Form
            {
                CraniumCentre = new Vector3(0f, 0.11f, -0.04f),
                CraniumSemi = new Vector3(0.355f, 0.39f, 0.44f),
                UnionSoftness = 0.05f,
                NeckAxisZ = -0.05f,
                NeckRadiusX = 0.19f,
                NeckRadiusZ = 0.165f,
                NeckTopY = -0.50f,
                NeckBottomY = -1.02f,
                CollarFlare = 0.9f,
                SphereZoneDeg = 158f,
                SphereZoneFraction = 0.76f,
                MarchStart = 0.85f,
                MarchStep = 0.04f,
            };
        }

        readonly HeadDetail _detail;
        readonly Form _form;
        HeadTopology _topology;

        public ProceduralBaseHead(HeadDetail detail) : this(detail, Form.Default) { }

        public ProceduralBaseHead(HeadDetail detail, Form form)
        {
            _detail = detail.Sanitized();
            _form = form;
        }

        public HeadDetail Detail => _detail;
        public Form FormConstants => _form;

        // ------------------------------------------------------------------ sites
        // Angles are in DEGREES (θ from the crown, φ from the face toward the character's left)
        // and are the directions of the anatomy authored in BuildAnatomy — the eye site points
        // at the orbital cavity, the mouth site at the lip fissure, and so on.

        static readonly HeadSiteSpec[] DefaultSites =
        {
            new HeadSiteSpec { Name = "Crown",       ThetaDeg = 4f,    PhiDeg = 0f,   RingDeg = 14f },
            new HeadSiteSpec { Name = "CrownFront",  ThetaDeg = 27f,   PhiDeg = 0f,   RingDeg = 16f },
            new HeadSiteSpec { Name = "CrownBack",   ThetaDeg = 31f,   PhiDeg = 180f, RingDeg = 14f },
            new HeadSiteSpec { Name = "Brow",        ThetaDeg = 79f,   PhiDeg = 17f,  RingDeg = 6f,  Bilateral = true, SpreadAxis = HeadAxis.OrbitalSpacing, SpreadDegPerUnit = 4f },
            new HeadSiteSpec { Name = "Eye",         ThetaDeg = 90.5f, PhiDeg = 22.5f, RingDeg = 9f, Bilateral = true, SpreadAxis = HeadAxis.OrbitalSpacing, SpreadDegPerUnit = 5f },
            new HeadSiteSpec { Name = "NoseBridge",  ThetaDeg = 88f,   PhiDeg = 0f,   RingDeg = 6f },
            new HeadSiteSpec { Name = "NoseTip",     ThetaDeg = 109f,  PhiDeg = 0f,   RingDeg = 6f },
            new HeadSiteSpec { Name = "Muzzle",      ThetaDeg = 109f,  PhiDeg = 0f,   RingDeg = 21f },
            new HeadSiteSpec { Name = "Mouth",       ThetaDeg = 127f,  PhiDeg = 0f,   RingDeg = 12f },
            new HeadSiteSpec { Name = "MouthCorner", ThetaDeg = 127f,  PhiDeg = 16f,  RingDeg = 4f,  Bilateral = true, SpreadAxis = HeadAxis.MouthWidth, SpreadDegPerUnit = 4f },
            new HeadSiteSpec { Name = "Chin",        ThetaDeg = 141f,  PhiDeg = 0f,   RingDeg = 8f },
            new HeadSiteSpec { Name = "EarSide",     ThetaDeg = 100f,  PhiDeg = 93f,  RingDeg = 12f, Bilateral = true },
            new HeadSiteSpec { Name = "EarTop",      ThetaDeg = 36f,   PhiDeg = 58f,  RingDeg = 9f,  Bilateral = true },
            new HeadSiteSpec { Name = "Cheek",       ThetaDeg = 115f,  PhiDeg = 34f,  RingDeg = 6f,  Bilateral = true, SpreadAxis = HeadAxis.MuzzleWidth, SpreadDegPerUnit = 4f },
            new HeadSiteSpec { Name = "Temple",      ThetaDeg = 67f,   PhiDeg = 58f,  RingDeg = 6f,  Bilateral = true },
        };

        public IReadOnlyList<HeadSiteSpec> Sites => DefaultSites;

        public Vector3 SiteDirection(HeadSiteSpec site, HeadShape shape)
        {
            float phi = site.PhiDeg;
            if (site.SpreadDegPerUnit != 0f) phi += shape.Clamped(site.SpreadAxis) * site.SpreadDegPerUnit;
            return GeometryKit.Dir(site.ThetaDeg * Mathf.Deg2Rad, phi * Mathf.Deg2Rad);
        }

        // ------------------------------------------------------------------ topology

        public HeadTopology Topology => _topology ??= BuildTopology();

        int Rings => _detail.Rings;
        int Segments => _detail.Segments;
        int Cols => _detail.Segments + 1;

        HeadTopology BuildTopology()
        {
            int rows = Rings + 1, cols = Cols;
            var t = new HeadTopology { VertexCount = rows * cols, Uvs = new Vector2[rows * cols] };
            var tris = new List<int>(Rings * Segments * 6);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    t.Uvs[r * cols + c] = new Vector2(c / (float)Segments, 1f - r / (float)Rings);
            for (int r = 0; r < Rings; r++)
                for (int c = 0; c < Segments; c++)
                {
                    int a = r * cols + c, b = a + 1, cc = a + cols + 1, d = a + cols;
                    // Winding chosen so normals face outward for a surface swept crown → neck,
                    // φ increasing toward +X.
                    tris.Add(a); tris.Add(d); tris.Add(cc);
                    tris.Add(a); tris.Add(cc); tris.Add(b);
                }
            t.Triangles = tris.ToArray();
            return t;
        }

        // ------------------------------------------------------------------ evaluation

        public IHeadSurface Surface(HeadShape shape) => new AnalyticSurface(_form, shape);

        public Vector3[] Evaluate(HeadShape shape)
        {
            var surface = new AnalyticSurface(_form, shape);
            int rows = Rings + 1, cols = Cols;
            var verts = new Vector3[rows * cols];
            float sphereZone = _form.SphereZoneDeg * Mathf.Deg2Rad;
            float tSplit = _form.SphereZoneFraction;
            int lastSphereRow = Mathf.Clamp(Mathf.RoundToInt(tSplit * Rings), 2, Rings - 2);

            // Sphere zone: rays from the centre.
            for (int r = 0; r <= lastSphereRow; r++)
            {
                float theta = sphereZone * r / lastSphereRow;
                for (int c = 0; c < cols; c++)
                {
                    float phi = (c / (float)Segments - 0.5f) * GeometryKit.Tau; // u = 0.5 is the face
                    verts[r * cols + c] = surface.Sample(GeometryKit.Dir(theta, phi));
                }
            }

            // Neck zone: analytic rings blended off the last sphere ring.
            int neckRows = Rings - lastSphereRow;
            float neckTop = surface.NeckTopY, neckBottom = surface.NeckBottomY;
            for (int r = lastSphereRow + 1; r <= Rings; r++)
            {
                float u = (r - lastSphereRow) / (float)neckRows;      // (0, 1]
                float y = Mathf.Lerp(neckTop, neckBottom, u);
                float blend = GeometryKit.Smooth(u / 0.45f);           // 0 at the seam → 1 by 45% down
                float flare = 1f + _form.CollarFlare * GeometryKit.SafePow(GeometryKit.Smooth((u - 0.70f) / 0.30f), 1.6f);
                for (int c = 0; c < cols; c++)
                {
                    float phi = (c / (float)Segments - 0.5f) * GeometryKit.Tau;
                    Vector3 fromSphere = verts[lastSphereRow * cols + c];
                    fromSphere.y = Mathf.Min(fromSphere.y, neckTop) - (neckTop - y);   // carried straight down
                    Vector3 neck = surface.NeckPoint(y, phi, flare);
                    verts[r * cols + c] = Vector3.Lerp(fromSphere, neck, blend);
                }
            }
            return verts;
        }

        /// <summary>Shape-derived anatomy + the sampler. One instance per shape; no shared state.</summary>
        public sealed class AnalyticSurface : IHeadSurface
        {
            readonly Form _f;
            readonly float _neckRx, _neckRz, _neckZ;
            public readonly float NeckTopY, NeckBottomY;
            readonly List<Prim> _solids = new(40);
            readonly List<Prim> _cuts = new(12);

            public Vector3 Centre => Vector3.zero;

            public AnalyticSurface(Form f, HeadShape s)
            {
                _f = f;
                float neckThick = GeometryKit.Dial(s.Clamped(HeadAxis.NeckThickness), 0.80f, 1.45f);
                _neckRx = f.NeckRadiusX * neckThick;
                _neckRz = f.NeckRadiusZ * neckThick;
                _neckZ = f.NeckAxisZ;
                NeckTopY = f.NeckTopY;
                NeckBottomY = f.NeckBottomY * GeometryKit.Dial(s.Clamped(HeadAxis.NeckLength), 0.75f, 1.2f);
                BuildAnatomy(s);
            }

            // ---- primitives ------------------------------------------------------------------

            enum PrimKind { Ellipsoid, RoundCone, Sphere }

            struct Prim
            {
                public PrimKind Kind;
                public Vector3 A, B, R;   // ellipsoid: A centre, R semi-axes; cone: A→B with R1/R2; sphere: A, R1
                public float R1, R2, K;
            }

            void Solid(Prim p) => _solids.Add(p);
            void Cut(Prim p) => _cuts.Add(p);

            static Prim Ellipsoid(Vector3 c, Vector3 r, float k) =>
                new Prim { Kind = PrimKind.Ellipsoid, A = c, R = new Vector3(Mathf.Max(1e-3f, r.x), Mathf.Max(1e-3f, r.y), Mathf.Max(1e-3f, r.z)), K = k };
            static Prim Cone(Vector3 a, Vector3 b, float r1, float r2, float k) =>
                new Prim { Kind = PrimKind.RoundCone, A = a, B = b, R1 = Mathf.Max(1e-4f, r1), R2 = Mathf.Max(1e-4f, r2), K = k };
            static Prim Sphere(Vector3 c, float r, float k) =>
                new Prim { Kind = PrimKind.Sphere, A = c, R1 = Mathf.Max(1e-4f, r), K = k };

            static Vector3 Mirror(Vector3 p) => new Vector3(-p.x, p.y, p.z);

            void SolidPair(Prim p)
            {
                Solid(p);
                p.A = Mirror(p.A); p.B = Mirror(p.B);
                Solid(p);
            }

            void CutPair(Prim p)
            {
                Cut(p);
                p.A = Mirror(p.A); p.B = Mirror(p.B);
                Cut(p);
            }

            static float SdEllipsoid(Vector3 q, Vector3 r)
            {
                float k0 = Mathf.Sqrt(q.x * q.x / (r.x * r.x) + q.y * q.y / (r.y * r.y) + q.z * q.z / (r.z * r.z));
                float k1 = Mathf.Sqrt(q.x * q.x / (r.x * r.x * r.x * r.x) + q.y * q.y / (r.y * r.y * r.y * r.y) + q.z * q.z / (r.z * r.z * r.z * r.z));
                if (k1 < 1e-9f) return -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
                return k0 * (k0 - 1f) / k1;
            }

            static float SdRoundCone(Vector3 p, Vector3 a, Vector3 b, float r1, float r2)
            {
                Vector3 ba = b - a;
                float l2 = Vector3.Dot(ba, ba);
                if (l2 < 1e-10f) return (p - a).magnitude - Mathf.Max(r1, r2);
                float rr = r1 - r2;
                float a2 = l2 - rr * rr;
                float il2 = 1f / l2;
                Vector3 pa = p - a;
                float y = Vector3.Dot(pa, ba);
                float z = y - l2;
                Vector3 xv = pa * l2 - ba * y;
                float x2 = Vector3.Dot(xv, xv);
                float y2 = y * y * l2;
                float z2 = z * z * l2;
                float k = Mathf.Sign(rr) * rr * rr * x2;
                if (Mathf.Sign(z) * a2 * z2 > k) return Mathf.Sqrt(x2 + z2) * il2 - r2;
                if (Mathf.Sign(y) * a2 * y2 < k) return Mathf.Sqrt(x2 + y2) * il2 - r1;
                return (Mathf.Sqrt(Mathf.Max(0f, x2 * a2 * il2)) + y * rr) * il2 - r1;
            }

            static float Sd(in Prim p, Vector3 q)
            {
                switch (p.Kind)
                {
                    case PrimKind.Ellipsoid: return SdEllipsoid(q - p.A, p.R);
                    case PrimKind.RoundCone: return SdRoundCone(q, p.A, p.B, p.R1, p.R2);
                    default: return (q - p.A).magnitude - p.R1;
                }
            }

            static float SMin(float a, float b, float k)
            {
                if (k <= 1e-6f) return Mathf.Min(a, b);
                float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
                return Mathf.Min(a, b) - h * h * k * 0.25f;
            }

            static float SMax(float a, float b, float k)
            {
                if (k <= 1e-6f) return Mathf.Max(a, b);
                float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
                return Mathf.Max(a, b) + h * h * k * 0.25f;
            }

            /// <summary>Negative inside the head.</summary>
            float Field(Vector3 p)
            {
                float d = float.MaxValue;
                for (int i = 0; i < _solids.Count; i++)
                {
                    var pr = _solids[i];
                    float sd = Sd(pr, p);
                    d = d == float.MaxValue ? sd : SMin(d, sd, pr.K);
                }
                for (int i = 0; i < _cuts.Count; i++)
                {
                    var pr = _cuts[i];
                    d = SMax(d, -Sd(pr, p), pr.K);
                }
                return d;
            }

            /// <summary>
            /// Radius along a direction from the centre: the OUTERMOST crossing, found by marching
            /// inward from <see cref="Form.MarchStart"/> until the field goes negative, then
            /// bisecting. Marching from outside is what keeps a brow overhang or a nose tip from
            /// producing a dent where a single bisection would land on the wrong crossing.
            /// </summary>
            float BaseRadius(Vector3 dir)
            {
                float step = Mathf.Max(0.004f, _f.MarchStep);
                float t = Mathf.Max(0.2f, _f.MarchStart);
                float hi = t;
                while (t > step)
                {
                    if (Field(dir * t) < 0f) break;
                    hi = t;
                    t -= step;
                }
                if (t <= step) return step;          // never went inside: degenerate direction
                float lo = t;
                for (int i = 0; i < 16; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    if (Field(dir * mid) < 0f) lo = mid; else hi = mid;
                }
                return 0.5f * (lo + hi);
            }

            public Vector3 Sample(Vector3 direction)
            {
                Vector3 d = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.up;
                return d * BaseRadius(d);
            }

            public Vector2 Uv(Vector3 direction)
            {
                GeometryKit.ToAngles(direction, out float theta, out float phi);
                float sphereZone = _f.SphereZoneDeg * Mathf.Deg2Rad;
                float t = Mathf.Min(theta / sphereZone, 1f) * _f.SphereZoneFraction;
                return new Vector2(0.5f + phi / GeometryKit.Tau, 1f - t);
            }

            public Vector3 Direction(Vector2 uv)
            {
                float sphereZone = _f.SphereZoneDeg * Mathf.Deg2Rad;
                float phi = (uv.x - 0.5f) * GeometryKit.Tau;
                float t = 1f - uv.y;
                float theta = t <= _f.SphereZoneFraction
                    ? sphereZone * t / _f.SphereZoneFraction
                    : sphereZone + (Mathf.PI - sphereZone) * Mathf.Clamp01((t - _f.SphereZoneFraction) / (1f - _f.SphereZoneFraction));
                return GeometryKit.Dir(theta, phi);
            }

            public Vector3 NeckPoint(float y, float phi, float flare)
            {
                float sp = Mathf.Sin(phi), cp = Mathf.Cos(phi);
                return new Vector3(_neckRx * flare * sp, y, _neckZ + _neckRz * flare * cp);
            }

            // ---- the anatomy ---------------------------------------------------------------

            /// <summary>
            /// The whole face, as primitives whose positions and sizes are functions of the axes.
            /// Positions are HEAD UNITS on the neutral human; the LOWER FACE is passed through
            /// <c>LF</c>, which scales it vertically (LowerFaceHeight) and pushes it forward
            /// (MuzzleLength) so a muzzle carries the nose, lips and chin out with it.
            /// </summary>
            void BuildAnatomy(HeadShape s)
            {
                float kBig = _f.UnionSoftness;
                float cw = GeometryKit.Dial(s.Clamped(HeadAxis.CranialWidth), 0.88f, 1.18f);
                float ch = GeometryKit.Dial(s.Clamped(HeadAxis.CranialHeight), 0.90f, 1.14f);
                float cl = GeometryKit.Dial(s.Clamped(HeadAxis.CranialLength), 0.90f, 1.14f);
                float fore = s.Clamped(HeadAxis.ForeheadBulge);
                float brow = s.Clamped(HeadAxis.BrowRidge);
                float orbitSp = s.Clamped(HeadAxis.OrbitalSpacing);
                float orbitSz = GeometryKit.Dial(s.Clamped(HeadAxis.OrbitalSize), 0.82f, 1.28f);
                float orbitD = GeometryKit.Dial(s.Clamped(HeadAxis.OrbitalDepth), 0.45f, 1.55f);
                float cheek = GeometryKit.Dial(s.Clamped(HeadAxis.CheekboneWidth), 0.88f, 1.18f);
                float muzzle = s.Clamped(HeadAxis.MuzzleLength);
                float muzzleW = GeometryKit.Dial(s.Clamped(HeadAxis.MuzzleWidth), 0.80f, 1.35f);
                float noseP = GeometryKit.Dial(s.Clamped(HeadAxis.NoseProjection), 0.0f, 1.7f);
                float noseW = GeometryKit.Dial(s.Clamped(HeadAxis.NoseWidth), 0.78f, 1.45f);
                float lips = GeometryKit.Dial(s.Clamped(HeadAxis.LipFullness), 0.0f, 1.8f);
                float mouthW = GeometryKit.Dial(s.Clamped(HeadAxis.MouthWidth), 0.82f, 1.3f);
                float jawW = GeometryKit.Dial(s.Clamped(HeadAxis.JawWidth), 0.84f, 1.2f);
                float chinP = s.Clamped(HeadAxis.ChinProjection);
                float lower = GeometryKit.Dial(s.Clamped(HeadAxis.LowerFaceHeight), 0.86f, 1.15f);

                // Lower-face transform: scale y below the eye line, push forward with the muzzle.
                Vector3 LF(Vector3 p)
                {
                    if (p.y < 0f) p.y *= lower;
                    float m = muzzle >= 0f ? 0.24f * muzzle : 0.09f * muzzle;
                    p.z += m * GeometryKit.Smooth((-p.y) / 0.22f);
                    return p;
                }
                float LY(float y) => y < 0f ? y * lower : y;

                // ---- braincase --------------------------------------------------------------
                Solid(Ellipsoid(_f.CraniumCentre, new Vector3(_f.CraniumSemi.x * cw, _f.CraniumSemi.y * ch, _f.CraniumSemi.z * cl), kBig));
                // Parietal fullness: a wider, shorter volume that squares the sides of the skull.
                Solid(Ellipsoid(new Vector3(0f, 0.15f * ch, -0.08f * cl), new Vector3(0.372f * cw, 0.30f * ch, 0.37f * cl), 0.07f));
                // Occiput.
                Solid(Sphere(new Vector3(0f, 0.03f, -0.29f * cl), 0.21f, 0.08f));
                // Frontal boss / melon: forward and larger with ForeheadBulge; receding below zero.
                float foreZ = fore >= 0f ? 0.17f + 0.12f * fore : 0.17f + 0.07f * fore;
                float foreS = fore >= 0f ? 1f + 0.32f * fore : 1f + 0.15f * fore;
                Solid(Ellipsoid(new Vector3(0f, 0.20f + 0.06f * Mathf.Max(0f, fore), foreZ), new Vector3(0.27f * cw * foreS, 0.21f * foreS, 0.24f * foreS), 0.08f));
                // Temple flats.
                CutPair(Sphere(new Vector3(0.56f * cw, 0.17f, 0.05f), 0.215f, 0.09f));

                // ---- brow ---------------------------------------------------------------------
                float browR = 0.040f * (1f + 0.55f * brow);
                float browZ = 0.02f * brow;
                SolidPair(Cone(new Vector3(0.035f, 0.075f, 0.405f + browZ), new Vector3(0.215f + 0.02f * orbitSp, 0.062f, 0.355f + browZ), browR, browR * 0.9f, 0.03f));
                Solid(Sphere(new Vector3(0f, 0.06f, 0.395f + browZ * 0.5f), 0.052f + 0.01f * brow, 0.035f));

                // ---- orbits -------------------------------------------------------------------
                float ex = 0.15f + 0.03f * orbitSp;
                CutPair(Ellipsoid(new Vector3(ex, -0.006f, 0.402f), new Vector3(0.086f * orbitSz, 0.066f * orbitSz, 0.052f * orbitD), 0.03f));

                // ---- cheekbones ---------------------------------------------------------------
                SolidPair(Ellipsoid(new Vector3(0.215f * cheek, -0.08f, 0.255f), new Vector3(0.085f, 0.06f, 0.09f), 0.06f));
                SolidPair(Cone(new Vector3(0.24f * cheek, -0.075f, 0.26f), new Vector3(0.345f * cw, -0.04f, 0.0f), 0.055f, 0.048f, 0.05f));

                // ---- mid-face -----------------------------------------------------------------
                Solid(Ellipsoid(LF(new Vector3(0f, -0.14f, 0.215f)), new Vector3(0.255f * muzzleW, 0.17f * lower, 0.20f + 0.10f * Mathf.Max(0f, muzzle)), 0.08f));
                // Lower face: the maxilla/mandible front the lips sit on.
                Solid(Ellipsoid(LF(new Vector3(0f, -0.33f, 0.235f)), new Vector3(0.195f * Mathf.Sqrt(jawW) * muzzleW, 0.17f * lower, 0.19f + 0.08f * Mathf.Max(0f, muzzle)), 0.06f));
                // Buccal (cheek flesh) between the arch and the jaw.
                SolidPair(Ellipsoid(new Vector3(0.17f * jawW, LY(-0.20f), 0.07f), new Vector3(0.09f, 0.13f * lower, 0.15f), 0.09f));

                // ---- mandible -----------------------------------------------------------------
                Vector3 chin = LF(new Vector3(0.05f, -0.445f, 0.335f + 0.05f * chinP));
                Vector3 gonion = new Vector3(0.24f * jawW, LY(-0.29f), -0.03f);
                Vector3 condyle = new Vector3(0.285f * jawW, -0.03f, -0.07f);
                SolidPair(Cone(chin, gonion, 0.068f, 0.060f, 0.05f));
                SolidPair(Cone(gonion, condyle, 0.064f, 0.055f, 0.05f));
                Solid(Ellipsoid(LF(new Vector3(0f, -0.44f, 0.34f + 0.06f * chinP)), new Vector3(0.11f * jawW, 0.075f, 0.085f * (1f + 0.2f * chinP)), 0.045f));
                // Throat: the underside between jaw and neck.
                Solid(Ellipsoid(new Vector3(0f, LY(-0.43f), 0.04f), new Vector3(0.22f, 0.16f, 0.25f), 0.07f));

                // ---- nose ---------------------------------------------------------------------
                // Everything scales with NoseProjection, and sinks into the face as it goes to
                // zero, so a beak's axis override leaves a smooth muzzle behind.
                if (noseP > 0.01f)
                {
                    float sink = (1f - Mathf.Min(1f, noseP)) * 0.07f;
                    Vector3 nasion = new Vector3(0f, 0.02f, 0.395f - sink);
                    Vector3 tip = LF(new Vector3(0f, -0.172f, 0.515f - sink));
                    Solid(Cone(nasion, tip, 0.028f * noseP, 0.040f * noseP, 0.025f));
                    Solid(Sphere(tip, 0.046f * noseP, 0.02f));
                    SolidPair(Sphere(LF(new Vector3(0.050f * noseW, -0.192f, 0.455f - sink)), 0.037f * noseP, 0.02f));
                    Solid(Cone(tip, LF(new Vector3(0f, -0.218f, 0.44f - sink)), 0.018f * noseP, 0.016f * noseP, 0.02f));
                    // Nostril dents on the underside of each ala.
                    CutPair(Sphere(LF(new Vector3(0.030f * noseW, -0.238f, 0.462f - sink)), 0.019f * noseP, 0.012f));
                }

                // ---- mouth --------------------------------------------------------------------
                if (lips > 0.01f)
                {
                    float lr = Mathf.Min(1.4f, lips);
                    Vector3 cornerU = LF(new Vector3(0.125f * mouthW, -0.304f, 0.372f));
                    Vector3 peak = LF(new Vector3(0.026f, -0.289f, 0.410f));
                    Vector3 dip = LF(new Vector3(0f, -0.294f, 0.410f));
                    SolidPair(Cone(cornerU, peak, 0.008f * lr, 0.016f * lr, 0.015f));
                    SolidPair(Cone(peak, dip, 0.016f * lr, 0.015f * lr, 0.015f));
                    Vector3 cornerL = LF(new Vector3(0.11f * mouthW, -0.313f, 0.376f));
                    Vector3 lowMid = LF(new Vector3(0f, -0.332f, 0.406f));
                    SolidPair(Cone(cornerL, lowMid, 0.009f * lr, 0.021f * lr, 0.015f));
                    // Philtrum columns.
                    SolidPair(Cone(LF(new Vector3(0.016f, -0.225f, 0.428f)), LF(new Vector3(0.02f, -0.278f, 0.416f)), 0.007f, 0.009f, 0.02f));
                    // The fissure: a thin cut between the lips, corners lifted a touch.
                    CutPair(Cone(LF(new Vector3(0.128f * mouthW, -0.307f, 0.395f)), LF(new Vector3(0f, -0.304f, 0.426f)), 0.005f, 0.008f, 0.008f));
                    // Mentolabial sulcus: the crease between lower lip and chin.
                    Cut(Sphere(LF(new Vector3(0f, -0.38f, 0.508f)), 0.062f, 0.05f));
                }
            }
        }
    }
}
