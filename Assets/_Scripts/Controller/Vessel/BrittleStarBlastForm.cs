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
    /// <para><b>Nothing in it is flat</b> (Garrett, 2026-10-18: "nothing in the environment should ever look flat like
    /// this, that is reserved for ui"). Every part is a faceted VOLUME with five flat-shaded facets round it - the
    /// prism-cut read of the hull, the crystals and the prisms - so the domain material lights it like the hull:
    /// <list type="bullet">
    /// <item><b>The front</b>: a faceted torus at the disk's true edge, its outer ridge exactly the blast's reach and
    /// its height exactly the disk's, swelling inward into five CRESTS where the arms meet it.</item>
    /// <item><b>The ring, grown</b>: the thrown ring's own pentagon, faceted beams with a crystal KNOT at each corner,
    /// expanding behind the front.</item>
    /// <item><b>Five arms</b>, the animal's: tapered faceted beams from each of the ring's points out to its crest,
    /// curling back against the spin and coiling in and out of the plane (a wave running root to tip), each led by a
    /// crystal HEAD - the ring's pod, flying out.</item>
    /// </list>
    /// All of it turns together: the phase starts at the ring's angle when it blew and keeps the ring's spin, braking to
    /// a stop (<see cref="PhaseAt"/>), and the arms and the ring thin as the blast spends itself.</para>
    ///
    /// <para><b>It scales with the blast</b>: every thickness is a share of the front's radius, so a beam reads as a
    /// volume from the hull whether the disk is 80 u or 780 u across (a few units of beam across a nucleus-wide disk read
    /// as wire, which is flat again).</para>
    ///
    /// <para><b>Honest where it counts</b> (tested): nothing is drawn past the front, so the edge shows exactly how far
    /// the blast reaches. The disk that does the damage is only a couple of units thick, so the volume necessarily stands
    /// above and below it: the front by <see cref="Settings.FrontHeight"/>, the arms, the ring and their crystals within
    /// <see cref="Settings.Relief"/>. The shape maps onto itself turned by a fifth of a turn.</para>
    /// </summary>
    public static class BrittleStarBlastForm
    {
        /// <summary>Segments round the front: a multiple of five, so the soup is exactly five-fold.</summary>
        public const int FrontSegments = 120;
        /// <summary>Segments along each arm.</summary>
        public const int ArmSegments = 12;
        /// <summary>Facets round every beam: five, the class's number.</summary>
        public const int Facets = 5;

        public struct Settings
        {
            public float RingRadius;       // the thrown ring's radius: where the arms start
            public float Front;            // the disk's radius now (its true edge)
            public float HalfThickness;    // the disk's half-thickness: the front is never thinner
            public float FrontWidth;       // the front's radial width between crests, x the front's radius
            public float FrontHeight;      // the front's height, x the front's radius
            public float Phase;            // radians: the ring's rotation now (PhaseAt)
            public float T01;              // 0 the blast, 1 spent
            public float CrestDepth;       // a crest's extra width, x BandWidth
            public float PentagonLag;      // the grown ring's corners at this share of the front
            public float ArmWidth;         // an arm's diameter at its root, x the front's radius
            public float ArmWiggle;        // the arms' sideways wave, x their length (the coil out of the plane fills the relief)
            public float ArmCurl;          // radians the arm's tip trails its root, against the spin
            public float Relief;           // how far the arms, the ring and their crystals may stand out of the plane, x the front's radius
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

        /// <summary>Clear the lists and fill them with the explosion at <paramref name="s"/>: flat-shaded facets (every
        /// face its own vertices), wound so Unity's front face looks along the normal. Returns how many of the vertices
        /// are the FRONT's (they come first).</summary>
        public static int Build(in Settings s, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            verts.Clear(); normals.Clear(); tris.Clear();
            float front = Mathf.Max(0.01f, s.Front);
            float h = Mathf.Max(0.05f, s.HalfThickness);
            float t = Mathf.Clamp01(s.T01);
            float fade = Mathf.Sqrt(1f - t);
            float ring = Mathf.Clamp(s.RingRadius, 0f, front);
            float frontHalfHeight = Mathf.Max(h, 0.5f * s.FrontHeight * front);
            float relief = Mathf.Max(frontHalfHeight, s.Relief * front);
            int n = BrittleStarHullForm.Points;

            // The front: a faceted torus whose outer ridge is the true edge. Its radial half-width swells at the five
            // crests, where the arms meet it.
            float band = Mathf.Min(front, Mathf.Max(0.05f, s.FrontWidth * front));
            float crests = s.Phase - s.ArmCurl;
            for (int i = 0; i < FrontSegments; i++)
            {
                float a0 = crests + i * Mathf.PI * 2f / FrontSegments, a1 = crests + (i + 1) * Mathf.PI * 2f / FrontSegments;
                Ring(front, band, s.CrestDepth, crests, a0, out var c0, out var r0);
                Ring(front, band, s.CrestDepth, crests, a1, out var c1, out var r1);
                Beam(verts, normals, tris, c0, Dir(a0), r0, frontHalfHeight, c1, Dir(a1), r1, frontHalfHeight);
            }
            int frontVertices = verts.Count;

            // The ring, grown: its pentagon as five faceted beams, a crystal knot at each corner.
            float tube = Mathf.Min(relief, 0.2f * s.ArmWidth * front) * fade + 0.05f;
            float corner = Mathf.Max(0f, front * Mathf.Clamp01(s.PentagonLag) - 3f * tube);   // the knot's tip at the lag
            for (int i = 0; i < n; i++)
            {
                float a0 = s.Phase + BrittleStarHullForm.PointAngle(i), a1 = s.Phase + BrittleStarHullForm.PointAngle((i + 1) % n);
                Vector3 p0 = Dir(a0) * corner, p1 = Dir(a1) * corner;
                Vector3 side = Vector3.Cross(Vector3.forward, (p1 - p0).normalized);
                Beam(verts, normals, tris, p0, side, tube, tube, p1, side, tube, tube);
                Crystal(verts, normals, tris, p0, Dir(a0), 3f * tube, Mathf.Min(relief, 3f * tube));
            }

            // Five arms: from each of the ring's points to its crest, curling back and coiling in and out of the plane,
            // thinner toward the tip, each led by a crystal head. The head's far point stays inside the front.
            float span = front - ring;
            if (span <= 0.05f) return frontVertices;
            float rootRadius = Mathf.Min(relief, 0.5f * s.ArmWidth * front) * fade + 0.05f;
            float head = Mathf.Min(relief, 2.6f * rootRadius);
            float reach = span - 2f * head;
            if (reach <= 0.05f) return frontVertices;
            for (int i = 0; i < n; i++)
            {
                float root = s.Phase + BrittleStarHullForm.PointAngle(i);
                ArmPoint(s, root, ring, reach, rootRadius, relief, t, 0f, out var c0, out var d0, out var w0);
                for (int k = 1; k <= ArmSegments; k++)
                {
                    ArmPoint(s, root, ring, reach, rootRadius, relief, t, k / (float)ArmSegments, out var c1, out var d1, out var w1);
                    Beam(verts, normals, tris, c0, d0, w0, w0, c1, d1, w1, w1);
                    c0 = c1; d0 = d1; w0 = w1;
                }
                Vector3 outward = new Vector3(c0.x, c0.y, 0f).normalized;
                Crystal(verts, normals, tris, c0 + outward * head, outward, head, Mathf.Min(relief - Mathf.Abs(c0.z), head));
            }
            return frontVertices;
        }

        /// <summary>The front's centre line and radial half-width at angle <paramref name="a"/>: its outer ridge at the
        /// front exactly.</summary>
        static void Ring(float front, float band, float crestDepth, float crests, float a, out Vector3 centre, out float half)
        {
            half = 0.5f * Mathf.Min(front, band * (1f + crestDepth * CrestAt(a, crests)));
            centre = Dir(a) * (front - half);
        }

        /// <summary>An arm's centre <paramref name="u"/> of the way from root (the ring) to its head, the in-plane
        /// direction square to it (its beam's width axis), and its radius there. The wave swings it sideways and, a
        /// quarter-wave on, out of the plane - a coil - within the relief.</summary>
        static void ArmPoint(in Settings s, float root, float ring, float reach, float rootRadius, float relief, float t,
                             float u, out Vector3 centre, out Vector3 side, out float radius)
        {
            radius = rootRadius * (1f - 0.7f * u);
            float wave = Mathf.PI * 2f * (1.5f * u - 2.5f * t);
            float amplitude = s.ArmWiggle * reach * u;
            float rho = Mathf.Max(0.01f, ring + reach * u);
            float angle = root - s.ArmCurl * u * u + amplitude * Mathf.Sin(wave) / rho;
            float z = Mathf.Max(0f, relief - radius) * u * Mathf.Cos(wave);
            Vector3 along = Dir(angle);
            centre = along * rho + Vector3.forward * z;
            side = new Vector3(-along.y, along.x, 0f);
        }

        static Vector3 Dir(float a) => new(Mathf.Cos(a), Mathf.Sin(a), 0f);

        /// <summary>A faceted beam from <paramref name="c0"/> to <paramref name="c1"/>: a pentagon at each end, its
        /// corners at ±<c>w</c> along the in-plane axis <paramref name="s0"/> / <paramref name="s1"/> (the first corner
        /// points that way) and ±<c>z</c> out of the plane; five flat facets between them. Consecutive beams share their
        /// end pentagons, so a chain of them reads as one faceted tube.</summary>
        static void Beam(List<Vector3> v, List<Vector3> nrm, List<int> tri, Vector3 c0, Vector3 s0, float w0, float z0,
                         Vector3 c1, Vector3 s1, float w1, float z1)
        {
            for (int j = 0; j < Facets; j++)
            {
                Vector3 a = Corner(c0, s0, w0, z0, j), b = Corner(c0, s0, w0, z0, j + 1);
                Vector3 c = Corner(c1, s1, w1, z1, j + 1), d = Corner(c1, s1, w1, z1, j);
                Vector3 mid = (a + b + c + d) * 0.25f, axis = (c0 + c1) * 0.5f;
                Vector3 n = Vector3.Cross(c - a, d - b);
                if (n.sqrMagnitude < 1e-12f) continue;
                if (Vector3.Dot(n, mid - axis) < 0f) n = -n;
                Quad(v, nrm, tri, a, b, c, d, n.normalized);
            }
        }

        static Vector3 Corner(Vector3 centre, Vector3 side, float w, float z, int j)
        {
            float psi = j * Mathf.PI * 2f / Facets;
            return centre + side * (w * Mathf.Cos(psi)) + Vector3.forward * (z * Mathf.Sin(psi));
        }

        /// <summary>A pentagonal bipyramid - a crystal - at <paramref name="centre"/>, its two tips ±<paramref name="along"/>
        /// × <paramref name="size"/>, its girdle <paramref name="size"/> × 0.6 wide and ±<paramref name="depth"/> × 0.6
        /// out of the plane.</summary>
        static void Crystal(List<Vector3> v, List<Vector3> nrm, List<int> tri, Vector3 centre, Vector3 along, float size,
                            float depth)
        {
            Vector3 axis = along.sqrMagnitude > 1e-8f ? along.normalized : Vector3.right;
            Vector3 side = Vector3.Cross(Vector3.forward, axis);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.up;
            side = side.normalized;
            Vector3 up = Vector3.Cross(axis, side).normalized;
            Vector3 tipA = centre + axis * size, tipB = centre - axis * size;
            float girdle = 0.6f * size, lift = 0.6f * Mathf.Max(0.01f, depth);
            for (int j = 0; j < Facets; j++)
            {
                float p0 = j * Mathf.PI * 2f / Facets, p1 = (j + 1) * Mathf.PI * 2f / Facets;
                Vector3 g0 = centre + side * (girdle * Mathf.Cos(p0)) + up * (lift * Mathf.Sin(p0));
                Vector3 g1 = centre + side * (girdle * Mathf.Cos(p1)) + up * (lift * Mathf.Sin(p1));
                Tri(v, nrm, tri, tipA, g0, g1, centre);
                Tri(v, nrm, tri, tipB, g1, g0, centre);
            }
        }

        static void Tri(List<Vector3> v, List<Vector3> nrm, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) { (b, c) = (c, b); n = -n; }
            int i0 = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n = n.normalized;
            nrm.Add(n); nrm.Add(n); nrm.Add(n);
            tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2);
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
