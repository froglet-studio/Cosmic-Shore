using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The look of the Brittle Star's RING EXPLOSION as a PURE function (design: <c>R_VesselActions/BRITTLE_STAR.md</c>
    /// §3.5): settings in, a flat-shaded triangle soup out, in the explosion's local frame (the thrown ring's own frame:
    /// +Z its axis, the ring's five points at <see cref="BrittleStarHullForm.PointAngle"/> + the phase). No scene and no
    /// Unity object, so <c>BrittleStarTests</c> run it as-is and the studio page draws the same shape (Garrett,
    /// 2026-10-18: "a fun effect with 5 fold symmetry that looks awesome and like it came from the ring that exploded").
    ///
    /// <para>Three parts, all as thick as the disk, all turning together. The phase starts at the ring's angle when it
    /// blew and keeps the ring's spin, braking to a stop (<see cref="PhaseAt"/>):
    /// <list type="bullet">
    /// <item><b>The front</b>: the annulus at the disk's true edge (what the blast reaches), with five CRESTS where it
    /// thickens inward, one where each arm meets it - as if the arms drive the wave out.</item>
    /// <item><b>The ring, grown</b>: the thrown ring's own pentagon, expanding a little behind the front.</item>
    /// <item><b>Five arms</b>: the animal's, one from each of the ring's points out to its crest, curling back against the
    /// spin and wriggling with a wave that runs from root to tip, thinning as the blast spends itself.</item>
    /// </list></para>
    ///
    /// <para><b>Honest by construction</b> (tested): every vertex lies inside the disk that does the damage — within
    /// <see cref="Settings.Front"/> of the centre and <see cref="Settings.HalfThickness"/> of its plane — so the effect
    /// never shows the blast reaching where it does not. And the shape maps onto itself turned by a fifth of a turn.</para>
    /// </summary>
    public static class BrittleStarBlastForm
    {
        /// <summary>Segments round the front: a multiple of five, so the soup is exactly five-fold.</summary>
        public const int FrontSegments = 120;
        /// <summary>Segments along each arm.</summary>
        public const int ArmSegments = 12;

        public struct Settings
        {
            public float RingRadius;       // the thrown ring's radius: where the arms start
            public float Front;            // the disk's radius now (its true edge)
            public float HalfThickness;    // the disk's half-thickness
            public float BandWidth;        // the front annulus's width between crests
            public float Phase;            // radians: the ring's rotation now (PhaseAt)
            public float T01;              // 0 the blast, 1 spent
            public float CrestDepth;       // a crest's extra width, x BandWidth
            public float PentagonLag;      // the grown ring's corners at this share of the front
            public float ArmWidth;         // an arm's width at its root, world units
            public float ArmWiggle;        // the arms' sideways wave, x their length
            public float ArmCurl;          // radians the arm's tip trails its root, against the spin
        }

        /// <summary>The ring's rotation <paramref name="t01"/> of the way through the blast: its angle when it blew,
        /// turning on by <paramref name="spinRadians"/> in all as it brakes to a stop (ease-out).</summary>
        public static float PhaseAt(float phase0, float spinRadians, float t01)
        {
            float u = 1f - Mathf.Clamp01(t01);
            return phase0 + spinRadians * (1f - u * u);
        }

        /// <summary>0..1: how much of a crest the front has at angle <paramref name="theta"/>, with the five crests turned
        /// to <paramref name="phase"/>: 1 exactly at each of the ring's five point angles there, 0 halfway between.
        /// <see cref="Build"/> turns them to the arms' tips (the phase less the curl).</summary>
        public static float CrestAt(float theta, float phase)
        {
            float c = Mathf.Cos(BrittleStarHullForm.Points * (theta - phase - BrittleStarHullForm.PointAngle(0)));
            if (c <= 0f) return 0f;
            c *= c;
            return c * c;
        }

        /// <summary>Clear the lists and fill them with the explosion at <paramref name="s"/>. Flat-shaded quads (four
        /// unshared vertices each), wound so Unity's front face looks along the normal.</summary>
        public static void Build(in Settings s, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            verts.Clear(); normals.Clear(); tris.Clear();
            float front = Mathf.Max(0.01f, s.Front);
            float h = Mathf.Max(0.05f, s.HalfThickness);
            float t = Mathf.Clamp01(s.T01);
            float fade = Mathf.Sqrt(1f - t);
            float ring = Mathf.Clamp(s.RingRadius, 0f, front);

            // The front: an annulus at the true edge, its inner edge pushed in at the five crests, where the arms meet it.
            float band = Mathf.Min(front, Mathf.Max(0.05f, s.BandWidth));
            float crests = s.Phase - s.ArmCurl;
            for (int i = 0; i < FrontSegments; i++)
            {
                float a0 = crests + i * Mathf.PI * 2f / FrontSegments, a1 = crests + (i + 1) * Mathf.PI * 2f / FrontSegments;
                float in0 = Mathf.Max(0f, front - Mathf.Min(front, band * (1f + s.CrestDepth * CrestAt(a0, crests))));
                float in1 = Mathf.Max(0f, front - Mathf.Min(front, band * (1f + s.CrestDepth * CrestAt(a1, crests))));
                Slab(verts, normals, tris, Dir(a0) * in0, Dir(a0) * front, Dir(a1) * front, Dir(a1) * in1, h, true, true);
            }

            // The ring, grown: its pentagon, corners at the lag behind the front (never inside the ring's own radius).
            int n = BrittleStarHullForm.Points;
            float corner = Mathf.Max(ring, front * Mathf.Clamp01(s.PentagonLag));
            float width = Mathf.Min(corner, band * 0.6f * fade + 0.05f);
            float inset = width / Mathf.Cos(Mathf.PI / n);   // along the corner's bisector, so every side is `width` wide
            for (int i = 0; i < n; i++)
            {
                float a0 = s.Phase + BrittleStarHullForm.PointAngle(i), a1 = s.Phase + BrittleStarHullForm.PointAngle((i + 1) % n);
                float inner = Mathf.Max(0f, corner - inset);
                Slab(verts, normals, tris, Dir(a0) * inner, Dir(a0) * corner, Dir(a1) * corner, Dir(a1) * inner, h, true, true);
            }

            // Five arms: from each of the ring's points to its crest, curling back and wriggling, thinner toward the tip.
            float span = front - ring;
            if (span <= 0.05f) return;
            float wiggle = s.ArmWiggle * span;
            for (int i = 0; i < n; i++)
            {
                float root = s.Phase + BrittleStarHullForm.PointAngle(i);
                for (int k = 0; k < ArmSegments; k++)
                {
                    ArmEdge(s, root, ring, span, wiggle, fade, t, k / (float)ArmSegments, out var l0, out var r0);
                    ArmEdge(s, root, ring, span, wiggle, fade, t, (k + 1) / (float)ArmSegments, out var l1, out var r1);
                    Slab(verts, normals, tris, l0, r0, r1, l1, h, true, true);
                }
            }
        }

        /// <summary>An arm's left and right edge <paramref name="u"/> of the way from root (the ring) to tip (the front).
        /// The tip is pulled in by its half-width so even its corners stay inside the front.</summary>
        static void ArmEdge(in Settings s, float root, float ring, float span, float wiggle, float fade, float t, float u,
                            out Vector3 left, out Vector3 right)
        {
            float half = 0.5f * Mathf.Max(0.05f, s.ArmWidth) * (1f - 0.7f * u) * fade + 0.025f;
            float rho = Mathf.Max(0.01f, ring + (span - half) * u);
            float lateral = wiggle * u * Mathf.Sin(Mathf.PI * 2f * (1.5f * u - 2.5f * t));
            float angle = root - s.ArmCurl * u * u + lateral / rho;
            Vector3 along = Dir(angle), side = new(-along.y, along.x, 0f);
            Vector3 c = along * rho;
            left = c + side * half;
            right = c - side * half;
        }

        static Vector3 Dir(float a) => new(Mathf.Cos(a), Mathf.Sin(a), 0f);

        /// <summary>A flat quad (a, b, c, d in the plane, a→b the "outward" edge pair) extruded ±<paramref name="h"/>: top,
        /// bottom and, where asked, its two side walls (b–c the outer, d–a the inner).</summary>
        static void Slab(List<Vector3> v, List<Vector3> nrm, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         float h, bool outerWall, bool innerWall)
        {
            Vector3 f = Vector3.forward * h;
            Quad(v, nrm, tri, a + f, b + f, c + f, d + f, Vector3.forward);
            Quad(v, nrm, tri, d - f, c - f, b - f, a - f, Vector3.back);
            if (outerWall) Quad(v, nrm, tri, b + f, b - f, c - f, c + f, Outward(b, c, a));
            if (innerWall) Quad(v, nrm, tri, d + f, d - f, a - f, a + f, Outward(d, a, b));
        }

        /// <summary>The in-plane normal of edge p→q pointing away from <paramref name="away"/>.</summary>
        static Vector3 Outward(Vector3 p, Vector3 q, Vector3 away)
        {
            Vector3 e = q - p, n = new(e.y, -e.x, 0f);
            if (n.sqrMagnitude < 1e-12f) n = new Vector3(p.x, p.y, 0f);
            if (Vector3.Dot(n, (p + q) * 0.5f - away) < 0f) n = -n;
            return n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.right;
        }

        static void Quad(List<Vector3> v, List<Vector3> nrm, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         Vector3 n)
        {
            // Wound so Unity's front face (clockwise seen from outside) faces along n: cross(b - a, c - a) along n, the
            // normal Mesh.RecalculateNormals would give (the executor's band was wound the same way).
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { (b, d) = (d, b); }
            int i0 = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) nrm.Add(n);
            tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2);
            tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3);
        }
    }
}
