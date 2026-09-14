using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The procedural base head: a smooth union of three implicit volumes (cranium, jaw mass,
    /// neck) sampled along rays from the head centre onto a ring × segment grid, then shaped by
    /// angular BUMPS (brow, sockets, nose, cheekbones, lips, chin, muzzle, melon) whose sizes
    /// and amplitudes are functions of the <see cref="HeadShape"/> axes. It is a pure function
    /// of (detail, form constants, shape): no components, no scene, no randomness.
    ///
    /// HEAD SPACE: origin at the skull centre, +Y crown, +Z the face, +X the character's left.
    /// Head height ≈ 1 (chin −0.5 … crown +0.5). Every feature generator emits into this space
    /// through its site, so a beetle mandible and a whale melon meet the head at the same scale.
    ///
    /// Topology is decided by the INTEGER <see cref="HeadDetail"/> only. Axes move vertices and
    /// never change counts, which <c>HeadTopologyTests</c> asserts across a shape sweep.
    ///
    /// This is the file to edit for "the jaw reads wrong", "the brow is too heavy", "the face
    /// is too flat": every bump's position, size and amplitude curve lives in
    /// <see cref="BuildBumps"/>, every volume in <see cref="AnalyticSurface"/>'s constructor.
    /// </summary>
    public sealed class ProceduralBaseHead : IBaseHead
    {
        /// <summary>The head's authored form. Floats only; retune freely.</summary>
        [Serializable]
        public struct Form
        {
            public Vector3 CraniumCentre, CraniumSemi;
            public Vector3 JawCentre, JawSemi;
            public float JawTaper;          // 0..1 how much the jaw mass narrows toward the chin
            public float FaceFrontExponent; // >2 flattens the face plane
            public float CrownExponent;     // >2 flattens the top of the skull
            public float UnionSoftness;
            public float NeckAxisZ, NeckRadiusX, NeckRadiusZ, NeckTopY, NeckBottomY;
            public float CollarFlare;       // bust base widening at the bottom rings
            public float SphereZoneDeg;     // polar angle where ring sampling hands over to the neck
            public float SphereZoneFraction;// fraction of rings spent on the sphere zone

            public static Form Default => new Form
            {
                CraniumCentre = new Vector3(0f, 0.10f, -0.04f),
                CraniumSemi = new Vector3(0.345f, 0.36f, 0.43f),
                JawCentre = new Vector3(0f, -0.20f, 0.05f),
                JawSemi = new Vector3(0.29f, 0.33f, 0.36f),
                JawTaper = 0.42f,
                FaceFrontExponent = 2.6f,
                CrownExponent = 2.5f,
                UnionSoftness = 0.22f,
                NeckAxisZ = -0.06f,
                NeckRadiusX = 0.165f,
                NeckRadiusZ = 0.15f,
                NeckTopY = -0.46f,
                NeckBottomY = -0.98f,
                CollarFlare = 0.9f,
                SphereZoneDeg = 150f,
                SphereZoneFraction = 0.74f,
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
        // Angles are in DEGREES here because they are authored by eye against the reference
        // proportions above (θ from the crown, φ from the face toward the character's left).

        static readonly HeadSiteSpec[] DefaultSites =
        {
            new HeadSiteSpec { Name = "Crown",       ThetaDeg = 6f,   PhiDeg = 0f,   RingDeg = 14f },
            new HeadSiteSpec { Name = "CrownFront",  ThetaDeg = 30f,  PhiDeg = 0f,   RingDeg = 16f },
            new HeadSiteSpec { Name = "CrownBack",   ThetaDeg = 28f,  PhiDeg = 180f, RingDeg = 14f },
            new HeadSiteSpec { Name = "Brow",        ThetaDeg = 77f,  PhiDeg = 16f,  RingDeg = 6f,  Bilateral = true, SpreadAxis = HeadAxis.OrbitalSpacing, SpreadDegPerUnit = 4f },
            new HeadSiteSpec { Name = "Eye",         ThetaDeg = 90f,  PhiDeg = 19f,  RingDeg = 9f,  Bilateral = true, SpreadAxis = HeadAxis.OrbitalSpacing, SpreadDegPerUnit = 6f },
            new HeadSiteSpec { Name = "NoseBridge",  ThetaDeg = 93f,  PhiDeg = 0f,   RingDeg = 6f },
            new HeadSiteSpec { Name = "NoseTip",     ThetaDeg = 102f, PhiDeg = 0f,   RingDeg = 6f },
            new HeadSiteSpec { Name = "Muzzle",      ThetaDeg = 107f, PhiDeg = 0f,   RingDeg = 21f },
            new HeadSiteSpec { Name = "Mouth",       ThetaDeg = 114f, PhiDeg = 0f,   RingDeg = 14f },
            new HeadSiteSpec { Name = "MouthCorner", ThetaDeg = 113f, PhiDeg = 14f,  RingDeg = 4f,  Bilateral = true, SpreadAxis = HeadAxis.MouthWidth, SpreadDegPerUnit = 4f },
            new HeadSiteSpec { Name = "Chin",        ThetaDeg = 127f, PhiDeg = 0f,   RingDeg = 8f },
            new HeadSiteSpec { Name = "EarSide",     ThetaDeg = 92f,  PhiDeg = 96f,  RingDeg = 12f, Bilateral = true },
            new HeadSiteSpec { Name = "EarTop",      ThetaDeg = 42f,  PhiDeg = 48f,  RingDeg = 9f,  Bilateral = true },
            new HeadSiteSpec { Name = "Cheek",       ThetaDeg = 105f, PhiDeg = 30f,  RingDeg = 6f,  Bilateral = true, SpreadAxis = HeadAxis.MuzzleWidth, SpreadDegPerUnit = 4f },
            new HeadSiteSpec { Name = "Temple",      ThetaDeg = 70f,  PhiDeg = 62f,  RingDeg = 6f,  Bilateral = true },
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
                float blend = GeometryKit.Smooth(u / 0.4f);            // 0 at the seam → 1 by 40% down
                float flare = 1f + _form.CollarFlare * GeometryKit.SafePow(GeometryKit.Smooth((u - 0.72f) / 0.28f), 1.6f);
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

        /// <summary>Shape-derived numbers + the sampler. One instance per shape; no shared state.</summary>
        public sealed class AnalyticSurface : IHeadSurface
        {
            readonly Form _f;
            readonly Vector3 _craniumC, _craniumS, _jawC, _jawS;
            readonly float _jawTaper, _frontExp, _k;
            readonly float _neckRx, _neckRz, _neckZ;
            public readonly float NeckTopY, NeckBottomY;
            readonly Bump[] _bumps;

            public Vector3 Centre => Vector3.zero;

            public AnalyticSurface(Form f, HeadShape s)
            {
                _f = f;
                _craniumC = f.CraniumCentre;
                _craniumS = new Vector3(
                    f.CraniumSemi.x * GeometryKit.Dial(s.Clamped(HeadAxis.CranialWidth), 0.86f, 1.22f),
                    f.CraniumSemi.y * GeometryKit.Dial(s.Clamped(HeadAxis.CranialHeight), 0.88f, 1.16f),
                    f.CraniumSemi.z * GeometryKit.Dial(s.Clamped(HeadAxis.CranialLength), 0.90f, 1.14f));
                float lowerFace = GeometryKit.Dial(s.Clamped(HeadAxis.LowerFaceHeight), 0.80f, 1.18f);
                _jawC = f.JawCentre + new Vector3(0f, (1f - lowerFace) * f.JawSemi.y * 0.5f, 0f);
                _jawS = new Vector3(
                    f.JawSemi.x * GeometryKit.Dial(s.Clamped(HeadAxis.JawWidth), 0.82f, 1.22f),
                    f.JawSemi.y * lowerFace,
                    f.JawSemi.z * GeometryKit.Dial(s.Clamped(HeadAxis.ChinProjection), 0.94f, 1.06f));
                _jawTaper = f.JawTaper * GeometryKit.Dial(s.Clamped(HeadAxis.JawWidth), 1.35f, 0.55f);
                _frontExp = f.FaceFrontExponent * GeometryKit.Dial(s.Clamped(HeadAxis.MuzzleLength), 1.25f, 0.8f);
                _k = f.UnionSoftness;
                float neckThick = GeometryKit.Dial(s.Clamped(HeadAxis.NeckThickness), 0.78f, 1.55f);
                _neckRx = f.NeckRadiusX * neckThick;
                _neckRz = f.NeckRadiusZ * neckThick;
                _neckZ = f.NeckAxisZ;
                NeckTopY = f.NeckTopY;
                NeckBottomY = f.NeckBottomY * GeometryKit.Dial(s.Clamped(HeadAxis.NeckLength), 0.72f, 1.2f);
                _bumps = BuildBumps(s);
            }

            // ---- implicit volumes -------------------------------------------------------

            float Field(Vector3 p)
            {
                Vector3 dc = p - _craniumC;
                float yTerm = dc.y > 0f
                    ? GeometryKit.SafePow(dc.y / _craniumS.y, _f.CrownExponent)     // flatter, wider crown
                    : dc.y * dc.y / (_craniumS.y * _craniumS.y);
                float cranium = dc.x * dc.x / (_craniumS.x * _craniumS.x) + yTerm
                                + dc.z * dc.z / (_craniumS.z * _craniumS.z) - 1f;

                Vector3 dj = p - _jawC;
                float down = GeometryKit.Clamp01((_jawC.y - p.y) / _jawS.y);          // 0 at jaw centre, 1 at chin
                float jawX = _jawS.x * (1f - _jawTaper * down * down);
                float zTerm = dj.z > 0f
                    ? GeometryKit.SafePow(dj.z / _jawS.z, _frontExp)
                    : dj.z * dj.z / (_jawS.z * _jawS.z);
                float jaw = dj.x * dj.x / (jawX * jawX) + dj.y * dj.y / (_jawS.y * _jawS.y) + zTerm - 1f;

                return SmoothMin(cranium, jaw, _k);
            }

            static float SmoothMin(float a, float b, float k)
            {
                float h = GeometryKit.Clamp01(0.5f + 0.5f * (b - a) / k);
                return b + (a - b) * h - k * h * (1f - h);
            }

            /// <summary>Base radius along a direction from the centre (bisection on the field).</summary>
            float BaseRadius(Vector3 dir)
            {
                float lo = 0f, hi = 2.0f;
                for (int i = 0; i < 28; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    if (Field(dir * mid) < 0f) lo = mid; else hi = mid;
                }
                return 0.5f * (lo + hi);
            }

            public Vector3 Sample(Vector3 direction)
            {
                Vector3 d = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.up;
                float r = BaseRadius(d);
                float s = 0f;
                for (int i = 0; i < _bumps.Length; i++) s += _bumps[i].Evaluate(d);
                if (s < -0.85f) s = -0.85f;
                return d * (r * (1f + s));
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

            // ---- bumps ---------------------------------------------------------------------

            /// <summary>
            /// An elliptical radial bump around a direction. <c>Rx/Ry</c> are sines of angular
            /// radii along the tangent frame; amplitude is a fraction of the base radius.
            /// </summary>
            public struct Bump
            {
                public Vector3 Dir, Right, Up;
                public float Rx, Ry, Amplitude, Sharpness;

                public float Evaluate(Vector3 d)
                {
                    float cosA = Vector3.Dot(d, Dir);
                    if (cosA <= 0f) return 0f;
                    float x = Vector3.Dot(d, Right) / Rx;
                    float y = Vector3.Dot(d, Up) / Ry;
                    float dist = GeometryKit.SafeSqrt(x * x + y * y);
                    float k = GeometryKit.Bell(dist);
                    if (Sharpness != 1f) k = GeometryKit.SafePow(k, Sharpness);
                    return Amplitude * k;
                }
            }

            static Bump MakeBump(float thetaDeg, float phiDeg, float rxDeg, float ryDeg, float amplitude, float sharpness = 1f)
            {
                var dir = GeometryKit.Dir(thetaDeg * Mathf.Deg2Rad, phiDeg * Mathf.Deg2Rad);
                GeometryKit.Frame(dir, Vector3.up, out var right, out var up);
                return new Bump
                {
                    Dir = dir, Right = right, Up = up,
                    Rx = Mathf.Sin(Mathf.Clamp(rxDeg, 0.5f, 89f) * Mathf.Deg2Rad),
                    Ry = Mathf.Sin(Mathf.Clamp(ryDeg, 0.5f, 89f) * Mathf.Deg2Rad),
                    Amplitude = amplitude, Sharpness = sharpness,
                };
            }

            static void AddPair(List<Bump> list, float thetaDeg, float phiDeg, float rxDeg, float ryDeg, float amplitude, float sharpness = 1f)
            {
                list.Add(MakeBump(thetaDeg, phiDeg, rxDeg, ryDeg, amplitude, sharpness));
                list.Add(MakeBump(thetaDeg, -phiDeg, rxDeg, ryDeg, amplitude, sharpness));
            }

            /// <summary>
            /// The whole facial-feature table. Every position, size and amplitude curve lives here.
            /// </summary>
            static Bump[] BuildBumps(HeadShape s)
            {
                var list = new List<Bump>(24);
                float spread = s.Clamped(HeadAxis.OrbitalSpacing);
                float eyePhi = 19f + 6f * spread;
                float orbit = GeometryKit.Dial(s.Clamped(HeadAxis.OrbitalSize), 0.72f, 1.45f);
                float muzzle = s.Clamped(HeadAxis.MuzzleLength);
                float muzzleW = GeometryKit.Dial(s.Clamped(HeadAxis.MuzzleWidth), 0.7f, 1.5f);
                float noseP = GeometryKit.Dial(s.Clamped(HeadAxis.NoseProjection), 0.0f, 1.9f);
                float noseW = GeometryKit.Dial(s.Clamped(HeadAxis.NoseWidth), 0.7f, 1.6f);
                float mouthW = GeometryKit.Dial(s.Clamped(HeadAxis.MouthWidth), 0.75f, 1.5f);
                float lips = GeometryKit.Dial(s.Clamped(HeadAxis.LipFullness), 0.0f, 2.0f);
                float lowerFace = GeometryKit.Dial(s.Clamped(HeadAxis.LowerFaceHeight), 0.85f, 1.12f);

                // Brow ridge: a pair of bars over the sockets, heavier and lower when the axis rises.
                float brow = s.Clamped(HeadAxis.BrowRidge);
                AddPair(list, 77f + 2f * brow, 15f + 4f * spread, 17f, 6.5f, 0.035f * GeometryKit.Dial(brow, 0.15f, 2.3f));

                // Orbital sockets: depressions the eyeballs sit in. Bigger orbits, wider and deeper.
                float socketDepth = -0.055f * GeometryKit.Dial(s.Clamped(HeadAxis.OrbitalDepth), 0.35f, 1.7f);
                AddPair(list, 90f, eyePhi, 10f * orbit, 8f * orbit, socketDepth);

                // Melon / forehead: Cetacea's signature volume; negative = a sloped, receding brow.
                float melon = s.Clamped(HeadAxis.ForeheadBulge);
                list.Add(MakeBump(58f, 0f, 34f, 27f, melon >= 0f ? 0.26f * melon : 0.06f * melon, 0.85f));

                // Muzzle: one broad forward volume centred on the mid-face; the axis is signed so a
                // flat (Coleoptera / Testudines) face recedes rather than protrudes.
                float muzzleAmp = 0.025f + (muzzle >= 0f ? 0.42f * muzzle : 0.10f * muzzle);
                list.Add(MakeBump(104f, 0f, 24f * muzzleW, 21f * lowerFace, muzzleAmp, 0.8f));

                // Nose: bridge, tip, and the two alar flares.
                list.Add(MakeBump(92f, 0f, 5.5f * noseW, 10f, 0.05f * noseP));
                list.Add(MakeBump(101.5f, 0f, 6.5f * noseW, 7f, 0.15f * noseP, 0.85f));
                AddPair(list, 103.5f, 7f * noseW, 4.6f, 3.8f, 0.06f * noseP);

                // Cheekbones.
                AddPair(list, 88f, 46f, 12f, 9f, 0.032f * GeometryKit.Dial(s.Clamped(HeadAxis.CheekboneWidth), 0.15f, 2.1f));

                // Mouth: upper lip, crease, lower lip. The crease is what a mouth READS as; the
                // lips are what makes it a face rather than a drawing of one.
                float lipTheta = 106f + 6f * lowerFace;
                list.Add(MakeBump(lipTheta + 3.2f, 0f, 13f * mouthW, 2.6f, 0.042f * lips, 0.9f));
                list.Add(MakeBump(lipTheta + 6.2f, 0f, 14f * mouthW, 1.4f, -0.026f, 1.4f));
                list.Add(MakeBump(lipTheta + 9.2f, 0f, 11f * mouthW, 2.9f, 0.044f * lips, 0.9f));

                // Chin.
                list.Add(MakeBump(125f, 0f, 11f, 10f, 0.075f * GeometryKit.Dial(s.Clamped(HeadAxis.ChinProjection), 0.1f, 1.9f)));
                // Jaw angle: a pair of gonial bumps so the jaw reads as a hinge, not a balloon.
                AddPair(list, 118f, 52f, 12f, 10f, 0.03f * GeometryKit.Dial(s.Clamped(HeadAxis.JawWidth), 0.3f, 2.0f));

                return list.ToArray();
            }
        }
    }
}
