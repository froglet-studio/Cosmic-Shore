using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The sophisticated-stroke library the "Connect the Dots" toy pulls its constructions from.
    /// Pure geometry - no scene access, deterministic, unit-testable - so it can build monuments
    /// far beyond the hand-authored Taj Mahal by <b>composition</b> rather than by hand-placing
    /// thousands of points.
    ///
    /// Three families live here:
    ///   1. A seedable deterministic PRNG (<see cref="Rng"/>) - never <c>UnityEngine.Random</c>, so
    ///      generation is reproducible across the test harness and never touches global RNG state.
    ///   2. Parametric curve primitives - Catmull-Rom splines, (p,q) torus knots, tube longitudes
    ///      and rings with rotation-minimizing (parallel-transport) frames, Fibonacci-sphere point
    ///      sets, and the truncated-icosahedron (soccer-ball) edge graph. Only primitives with a
    ///      production caller live here - compose new ones when a generator needs them.
    ///   3. The <b>impressionist field</b> - a divergence-free curl-noise flow (<see cref="CurlNoise"/>)
    ///      that <see cref="ImpressionistStrokes"/> integrates into short curvy strokes whose radii of
    ///      curvature stochastically fill a region in every direction ("3D impressionism"). This is
    ///      the technique that makes a lion's mane, a Van Gogh sky, or a galaxy halo read as painted
    ///      rather than plotted.
    ///
    /// All returned polylines follow the painting authoring conventions: local space, base plane at
    /// y=0, front toward +Z, and flyable segment lengths (the caller scales by the painting width W).
    /// </summary>
    public static class PaintingStrokeToolkit
    {
        // ── Deterministic PRNG ───────────────────────────────────────────────
        //
        // A reference type (not a struct) so a single instance threaded through a generator keeps
        // one deterministic sequence - a struct would copy and silently fork the stream.

        public sealed class Rng
        {
            uint _state;

            public Rng(int seed)
            {
                // Hash the seed so nearby seeds (0,1,2…) produce well-separated streams.
                _state = (uint)seed * 747796405u + 2891336453u;
                _state ^= _state >> 16;
                if (_state == 0u) _state = 0x9E3779B9u;
            }

            public uint NextUInt()
            {
                // xorshift32 - cheap, deterministic, adequate for procedural art.
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }

            /// <summary>Uniform in [0,1).</summary>
            public float Next01() => (NextUInt() & 0xFFFFFFu) / (float)0x1000000;

            public float Range(float a, float b) => a + (b - a) * Next01();

            /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
            public int RangeInt(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive) return minInclusive;
                return minInclusive + (int)(Next01() * (maxExclusive - minInclusive));
            }

            public bool Chance(float p) => Next01() < p;

            /// <summary>Uniform point on the unit sphere surface.</summary>
            public Vector3 OnUnitSphere()
            {
                // z uniform in [-1,1], azimuth uniform - the standard area-preserving sampling.
                float z = Range(-1f, 1f);
                float a = Range(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
            }

            /// <summary>Uniform point inside the unit ball (radius^(1/3) so density is even).</summary>
            public Vector3 InUnitBall() => OnUnitSphere() * Mathf.Pow(Next01(), 1f / 3f);
        }

        // ── 3D value noise + curl field (the impressionist engine) ───────────

        static float Hash(int x, int y, int z, int seed)
        {
            // Integer hash → float in [-1,1]. A single well-mixed 32-bit avalanche.
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647 + seed * 971);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x800000 - 1f; // [-1,1)
            }
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f); // smootherstep

        /// <summary>Smooth 3D value noise, ~[-1,1], one octave.</summary>
        public static float ValueNoise(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = Fade(p.x - x0), fy = Fade(p.y - y0), fz = Fade(p.z - z0);

            float c000 = Hash(x0, y0, z0, seed), c100 = Hash(x0 + 1, y0, z0, seed);
            float c010 = Hash(x0, y0 + 1, z0, seed), c110 = Hash(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash(x0, y0, z0 + 1, seed), c101 = Hash(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash(x0, y0 + 1, z0 + 1, seed), c111 = Hash(x0 + 1, y0 + 1, z0 + 1, seed);

            float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
            float y0v = Mathf.Lerp(x00, x10, fy), y1v = Mathf.Lerp(x01, x11, fy);
            return Mathf.Lerp(y0v, y1v, fz);
        }

        // Fixed offsets decorrelate the three components of the vector potential.
        static readonly Vector3 PotB = new(31.416f, 47.853f, 12.793f);
        static readonly Vector3 PotC = new(-19.271f, 83.155f, -57.604f);

        /// <summary>
        /// Divergence-free 3D flow field: curl of a noise vector potential Ψ. Because ∇·(∇×Ψ)=0 the
        /// field has no sources/sinks, so integrated streamlines swirl and fill space without
        /// spiralling into a point - exactly the turbulent, everywhere-curving motion of a Van Gogh
        /// sky. Magnitude is O(1); scale by the desired step length at the call site.
        /// </summary>
        public static Vector3 CurlNoise(Vector3 p, float freq, int seed)
        {
            Vector3 q = p * freq;
            const float e = 0.35f; // finite-difference epsilon in noise space (smooth field → large e ok)

            // Vector potential Ψ = (ψ1, ψ2, ψ3), each an independent noise field.
            // curl = (∂ψ3/∂y − ∂ψ2/∂z, ∂ψ1/∂z − ∂ψ3/∂x, ∂ψ2/∂x − ∂ψ1/∂y)
            float p2_z1 = ValueNoise(q + PotB + new Vector3(0, 0, e), seed);
            float p2_z0 = ValueNoise(q + PotB - new Vector3(0, 0, e), seed);
            float p3_y1 = ValueNoise(q + PotC + new Vector3(0, e, 0), seed);
            float p3_y0 = ValueNoise(q + PotC - new Vector3(0, e, 0), seed);

            float p3_x1 = ValueNoise(q + PotC + new Vector3(e, 0, 0), seed);
            float p3_x0 = ValueNoise(q + PotC - new Vector3(e, 0, 0), seed);
            float p1_z1 = ValueNoise(q + new Vector3(0, 0, e), seed);
            float p1_z0 = ValueNoise(q - new Vector3(0, 0, e), seed);

            float p2_x1 = ValueNoise(q + PotB + new Vector3(e, 0, 0), seed);
            float p2_x0 = ValueNoise(q + PotB - new Vector3(e, 0, 0), seed);
            float p1_y1 = ValueNoise(q + new Vector3(0, e, 0), seed);
            float p1_y0 = ValueNoise(q - new Vector3(0, e, 0), seed);

            float inv = 1f / (2f * e);
            var curl = new Vector3(
                (p3_y1 - p3_y0) - (p2_z1 - p2_z0),
                (p1_z1 - p1_z0) - (p3_x1 - p3_x0),
                (p2_x1 - p2_x0) - (p1_y1 - p1_y0)) * inv;

            return curl.sqrMagnitude > 1e-8f ? curl.normalized : Vector3.up;
        }

        // ── Impressionist strokes ────────────────────────────────────────────

        /// <summary>
        /// Fill a region with <paramref name="count"/> short curved strokes: each seeds from
        /// <paramref name="seedSampler"/> and is integrated along the curl field, so its radius of
        /// curvature varies stochastically and the strokes point in every direction - "3D
        /// impressionism". Each stroke is coloured by <paramref name="domainField"/> evaluated at its
        /// seed, so colour clusters into coherent zones rather than per-stroke confetti.
        /// </summary>
        /// <param name="curlFreq">Base spatial frequency of the flow (world-units^-1). Lower = broader sweeps.</param>
        /// <param name="stepLength">World spacing between consecutive stroke points - keep flyable (≳8).</param>
        /// <param name="minSteps">Lower bound of the stochastic stroke length in points.</param>
        /// <param name="maxSteps">Upper bound of the stochastic stroke length in points (arc length varies).</param>
        /// <param name="freqJitter">Per-stroke curl-frequency spread → varied radii of curvature.</param>
        /// <param name="upBias">Extra push toward +Y each step (0=none). Turns a fill into rising flame tongues.</param>
        /// <param name="project">Optional post-step reprojection (e.g. snap onto a sky shell or flatten a
        /// galaxy disk) so the impressionist strokes hug a surface instead of drifting off it.</param>
        public static List<PaintingStroke> ImpressionistStrokes(
            int count,
            Func<Rng, Vector3> seedSampler,
            Func<Vector3, Domains> domainField,
            Rng rng,
            int noiseSeed,
            float curlFreq,
            float stepLength,
            int minSteps,
            int maxSteps,
            float freqJitter = 0.5f,
            float upBias = 0f,
            Func<Vector3, Vector3> project = null,
            string namePrefix = "Impression")
        {
            var strokes = new List<PaintingStroke>(Mathf.Max(0, count));
            for (int i = 0; i < count; i++)
            {
                Vector3 seed = seedSampler(rng);
                if (project != null) seed = project(seed);
                Vector3 p = seed;
                Domains domain = domainField(seed);

                int steps = rng.RangeInt(minSteps, maxSteps + 1);
                // Per-stroke frequency + slight step scaling vary the radius of curvature stroke to stroke.
                float freq = curlFreq * (1f + rng.Range(-freqJitter, freqJitter));
                float step = stepLength * rng.Range(0.85f, 1.25f);
                int strokeSeed = noiseSeed + i * 101; // fixed per stroke → one coherent laminar streak

                var pts = new List<Vector3>(steps + 1) { p };
                Vector3 prevDir = Vector3.zero;
                for (int s = 0; s < steps; s++)
                {
                    Vector3 dir = CurlNoise(p, freq, strokeSeed);
                    if (upBias != 0f) dir = (dir + Vector3.up * upBias).normalized;
                    // Bias toward continuing forward so a stroke reads as one confident brush pass,
                    // not a jittery scribble, while still bending with the field.
                    if (prevDir != Vector3.zero)
                        dir = Vector3.Slerp(prevDir, dir, 0.5f).normalized;
                    p += dir * step;
                    if (project != null) p = project(p);
                    pts.Add(p);
                    prevDir = dir;
                }

                if (pts.Count >= 2)
                    strokes.Add(new PaintingStroke { name = $"{namePrefix} {i + 1}", domain = domain, points = pts });
            }
            return strokes;
        }

        /// <summary>
        /// A single radial spray stroke from <paramref name="center"/> outward along <paramref name="dir"/>,
        /// bent by the curl field - the primitive behind a lion's mane strand, a phoenix flame tongue, or a
        /// crest tuft. Grows from rInner to rOuter; <paramref name="upBias"/> makes it climb (flame).
        /// </summary>
        public static List<Vector3> RadialCurlStroke(Vector3 center, Vector3 dir, float rInner, float rOuter,
            float stepLength, float curlFreq, float curlAmp, float upBias, int seed, int maxSteps = 40)
        {
            dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.up;
            Vector3 p = center + dir * rInner;
            var pts = new List<Vector3> { p };
            float rOuterSq = rOuter * rOuter;
            for (int s = 0; s < maxSteps; s++)
            {
                Vector3 curl = CurlNoise(p, curlFreq, seed) * curlAmp;
                Vector3 move = (dir + curl + Vector3.up * upBias).normalized * stepLength;
                p += move;
                pts.Add(p);
                if ((p - center).sqrMagnitude >= rOuterSq) break;
            }
            return pts;
        }

        /// <summary>A cambered feather/quill: a Catmull-Rom shaft root→tip with an S-camber and mild curl wobble.</summary>
        public static List<Vector3> FeatherStroke(Vector3 root, Vector3 tip, float camber, float curlFreq,
            int nPts, int seed)
        {
            Vector3 axis = tip - root;
            float len = axis.magnitude;
            if (len < 1e-4f) return new List<Vector3> { root, tip };
            axis /= len;
            Basis(axis, out Vector3 side, out Vector3 up, out _);

            var ctrl = new List<Vector3>();
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                Vector3 baseP = Vector3.Lerp(root, tip, t);
                float s = Mathf.Sin(t * Mathf.PI);              // 0 at ends, 1 mid → S-camber
                Vector3 wob = CurlNoise(baseP, curlFreq, seed) * (camber * 0.4f);
                ctrl.Add(baseP + up * (camber * len * s * Mathf.Sign(0.5f - t)) + wob);
            }
            return CatmullRom(ctrl, Mathf.Max(1, nPts / 4));
        }

        // ── Surface / petal / terrain helpers ────────────────────────────────

        /// <summary>
        /// 1D fractal ridgeline (Fournier midpoint displacement) across x∈[x0,x1] at depth <paramref name="z"/>,
        /// baseline <paramref name="yBase"/>. Roughness falls by 2^(−H) per subdivision. The mountain-maker.
        /// </summary>
        public static List<Vector3> MidpointRidge(int seed, float x0, float x1, float z, float yBase,
            float amp, float H, int iters)
        {
            var rng = new Rng(seed);
            var ys = new List<float> { yBase + rng.Range(-0.3f * amp, 0.3f * amp),
                                       yBase + rng.Range(-0.3f * amp, 0.3f * amp) };
            float a = amp;
            for (int k = 0; k < iters; k++)
            {
                var next = new List<float>(ys.Count * 2 - 1);
                for (int i = 0; i < ys.Count - 1; i++)
                {
                    next.Add(ys[i]);
                    float mid = (ys[i] + ys[i + 1]) * 0.5f + rng.Range(-a, a);
                    next.Add(mid);
                }
                next.Add(ys[ys.Count - 1]);
                ys = next;
                a *= Mathf.Pow(2f, -H);
            }

            var pts = new List<Vector3>(ys.Count);
            for (int i = 0; i < ys.Count; i++)
            {
                float t = i / (float)(ys.Count - 1);
                pts.Add(new Vector3(Mathf.Lerp(x0, x1, t), Mathf.Max(0.02f * amp, ys[i]), z));
            }
            return pts;
        }

        /// <summary>Mirror a ridgeline under a waterline plane (y' = 2·yPlane − y), clamped to a floor - a lake reflection.</summary>
        public static List<Vector3> ReflectY(IReadOnlyList<Vector3> pts, float yPlane, float floorY)
        {
            var outPts = new List<Vector3>();
            foreach (var p in pts)
            {
                if (p.y > 2f * yPlane) continue; // would reflect below the floor - skip
                outPts.Add(new Vector3(p.x, Mathf.Max(floorY, 2f * yPlane - p.y), p.z));
            }
            return outPts;
        }

        /// <summary>
        /// A "happy little" fir tree as one flyable conifer-silhouette stroke: up the left flank with
        /// tiered branch notches, over the crown, down the right flank. Returns local points around
        /// <paramref name="baseC"/> rising along <paramref name="up"/>.
        /// </summary>
        public static List<Vector3> FirTree(Vector3 baseC, Vector3 up, float height, float halfWidth, int seed)
        {
            var rng = new Rng(seed);
            up = up.normalized;
            Basis(up, out Vector3 side, out Vector3 depth, out _);
            side = depth; // draw the silhouette in the depth plane so trees face varied ways (non-planar fan)

            int tiers = 5;
            var pts = new List<Vector3>();
            // Up the left flank (t < tiers so the widthless top tier can't duplicate the crown): each
            // tier steps out then in (a branch notch), narrowing toward the crown.
            for (int t = 0; t < tiers; t++)
            {
                float f = t / (float)tiers;
                float w = halfWidth * (1f - f) * rng.Range(0.85f, 1.1f);
                float y = height * f;
                pts.Add(baseC + up * y - side * w);
                pts.Add(baseC + up * (y + height / tiers * 0.5f) - side * (w * 0.35f));
            }
            pts.Add(baseC + up * height); // crown
            // Down the right flank.
            for (int t = tiers - 1; t >= 0; t--)
            {
                float f = t / (float)tiers;
                float w = halfWidth * (1f - f) * rng.Range(0.85f, 1.1f);
                float y = height * f;
                pts.Add(baseC + up * (y + height / tiers * 0.5f) + side * (w * 0.35f));
                pts.Add(baseC + up * y + side * w);
            }
            pts.Add(baseC); // back to the trunk base
            return pts;
        }

        /// <summary>
        /// Soccer-ball (truncated icosahedron) faces as ordered vertex loops on the unit sphere: 12
        /// pentagons + 20 hexagons. Faces are reconstructed by gathering the polyhedron's vertices
        /// nearest each face-center direction (icosahedron verts → pentagon centers, dodecahedron verts →
        /// hexagon centers) and sorting them by azimuth around that centre.
        /// </summary>
        public static void SoccerBallFaces(out List<Vector3[]> pentagons, out List<Vector3[]> hexagons)
        {
            TruncatedIcosahedron(out Vector3[] verts, out int[] edgeA, out int[] edgeB);

            // Adjacency as a hash of the unordered pair (i<j) → i*64+j (60 verts, j<64).
            var adj = new HashSet<int>(edgeA.Length);
            for (int e = 0; e < edgeA.Length; e++)
            {
                int i = Mathf.Min(edgeA[e], edgeB[e]), j = Mathf.Max(edgeA[e], edgeB[e]);
                adj.Add(i * 64 + j);
            }

            pentagons = new List<Vector3[]>(12);
            hexagons = new List<Vector3[]>(20);
            foreach (var c in IcosahedronDirs())
                pentagons.Add(GatherFace(verts, adj, c, 5));
            foreach (var c in DodecahedronDirs())
                hexagons.Add(GatherFace(verts, adj, c, 6));
        }

        static Vector3[] GatherFace(Vector3[] verts, HashSet<int> adj, Vector3 centerDir, int k)
        {
            centerDir = centerDir.normalized;
            // The k vertices with the largest dot to the face centre are that face's ring members.
            var idx = new List<int>();
            for (int i = 0; i < verts.Length; i++) idx.Add(i);
            idx.Sort((x, y) => Vector3.Dot(verts[y], centerDir).CompareTo(Vector3.Dot(verts[x], centerDir)));
            var face = idx.GetRange(0, Mathf.Min(k, idx.Count));

            // Order the ring by WALKING the edge graph (each face vertex has exactly two face-neighbours):
            // azimuth sorting around an off-centroid axis misorders faces with a zero-component normal.
            bool Adjacent(int a, int b) => adj.Contains(Mathf.Min(a, b) * 64 + Mathf.Max(a, b));

            var ring = new List<int> { face[0] };
            var used = new HashSet<int> { face[0] };
            for (int step = 1; step < face.Count; step++)
            {
                int cur = ring[ring.Count - 1], next = -1;
                foreach (int cand in face)
                    if (!used.Contains(cand) && Adjacent(cur, cand)) { next = cand; break; }
                if (next < 0) break; // degenerate (shouldn't happen on a true face)
                ring.Add(next);
                used.Add(next);
            }

            var loop = new Vector3[ring.Count + 1];
            for (int i = 0; i < ring.Count; i++) loop[i] = verts[ring[i]];
            loop[ring.Count] = verts[ring[0]]; // close
            return loop;
        }

        static IEnumerable<Vector3> IcosahedronDirs()
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var seen = new List<Vector3>();
            foreach (var perm in EvenPermutations(new Vector3(0f, 1f, phi)))
                foreach (var s in SignCombos())
                {
                    var d = Vector3.Scale(perm, s).normalized;
                    if (AddUnique(seen, d)) yield return d;
                }
        }

        static IEnumerable<Vector3> DodecahedronDirs()
        {
            // Hexagon centres = icosahedron face centroids = dodecahedron vertices DUAL to the icosa
            // (0,±1,±φ): {(±1,±1,±1)} ∪ cyclic-perms of (0,±φ,±1/φ). Note φ and 1/φ order - the swapped
            // (0,1/φ,φ) points at the wrong axis and yields non-face vertex clusters.
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var seen = new List<Vector3>();
            var bases = new[] { new Vector3(1f, 1f, 1f), new Vector3(0f, phi, 1f / phi) };
            foreach (var b in bases)
                foreach (var perm in EvenPermutations(b))
                    foreach (var s in SignCombos())
                    {
                        var d = Vector3.Scale(perm, s).normalized;
                        if (AddUnique(seen, d)) yield return d;
                    }
        }

        /// <summary>
        /// A coherent spatial domain field: low-frequency noise partitions space into large single-
        /// colour zones (Jade / Ruby / Gold). Use as the <c>domainField</c> for impressionist fills so
        /// the fill reads as colour swaths rather than random speckle.
        /// </summary>
        public static Func<Vector3, Domains> DomainRegions(int seed, float freq,
            Domains a = Domains.Jade, Domains b = Domains.Ruby, Domains c = Domains.Gold)
        {
            return p =>
            {
                float n = ValueNoise(p * freq, seed) * 0.5f + 0.5f; // [0,1]
                return n < 0.4f ? a : n < 0.72f ? b : c;
            };
        }

        /// <summary>Catmull-Rom spline through <paramref name="ctrl"/>. Smooth, passes through every control point.</summary>
        public static List<Vector3> CatmullRom(IList<Vector3> ctrl, int samplesPerSeg, bool closed = false)
        {
            var outPts = new List<Vector3>();
            if (ctrl == null || ctrl.Count < 2) return outPts;
            samplesPerSeg = Mathf.Max(1, samplesPerSeg);
            int n = ctrl.Count;
            int segs = closed ? n : n - 1;

            Vector3 Get(int i)
            {
                if (closed) return ctrl[((i % n) + n) % n];
                return ctrl[Mathf.Clamp(i, 0, n - 1)];
            }

            for (int seg = 0; seg < segs; seg++)
            {
                Vector3 p0 = Get(seg - 1), p1 = Get(seg), p2 = Get(seg + 1), p3 = Get(seg + 2);
                int last = seg == segs - 1 && !closed ? samplesPerSeg : samplesPerSeg - 1;
                for (int j = 0; j <= last; j++)
                {
                    float t = j / (float)samplesPerSeg;
                    outPts.Add(CatmullRomPoint(p0, p1, p2, p3, t));
                }
            }
            if (closed && outPts.Count > 0) outPts.Add(outPts[0]);
            return outPts;
        }

        static Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// A (p,q) torus knot winding <paramref name="p"/> times around the axis and <paramref name="q"/>
        /// times through the hole. gcd(p,q)=1 gives a single closed non-self-intersecting curve -
        /// mesmerising when spun. Lies on a torus of major radius R, tube radius r; axis is +Y.
        /// </summary>
        public static List<Vector3> TorusKnot(int p, int q, float R, float r, int segments)
        {
            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 2f;
                float ring = R + r * Mathf.Cos(q * t);
                float x = ring * Mathf.Cos(p * t);
                float z = ring * Mathf.Sin(p * t);
                float y = r * Mathf.Sin(q * t);
                pts.Add(new Vector3(x, y, z));
            }
            return pts;
        }

        /// <summary>3D Lissajous curve - three phase-offset sinusoids. A hypnotic non-planar ribbon.</summary>
        public static List<Vector3> Lissajous3D(Vector3 center, Vector3 amp,
            float ax, float ay, float az, float dx, float dy, float dz, int segments)
        {
            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 2f;
                pts.Add(center + new Vector3(
                    amp.x * Mathf.Sin(ax * t + dx),
                    amp.y * Mathf.Sin(ay * t + dy),
                    amp.z * Mathf.Sin(az * t + dz)));
            }
            return pts;
        }

        // ── Point distributions ──────────────────────────────────────────────

        const float GoldenAngle = 2.399963229728653f; // π(3−√5) radians ≈ 137.5°

        /// <summary>Evenly-distributed points on the unit sphere via the Fibonacci lattice.</summary>
        public static Vector3[] FibonacciSphere(int n)
        {
            if (n < 1) return Array.Empty<Vector3>();
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float y = 1f - (i + 0.5f) / n * 2f;      // 1 → −1
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float phi = GoldenAngle * i;
                pts[i] = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
            }
            return pts;
        }

        // ── Truncated icosahedron (soccer ball) ──────────────────────────────

        /// <summary>
        /// The 60 vertices and 90 edges of a truncated icosahedron (the classic soccer ball /
        /// buckminsterfullerene: 12 pentagons + 20 hexagons) on the unit sphere. Vertices are the even
        /// permutations of (0,±1,±3φ), (±1,±(2+φ),±2φ), (±φ,±2,±(2φ+1)), normalised; edges join every
        /// vertex pair at the (unique smallest) edge distance.
        /// </summary>
        public static void TruncatedIcosahedron(out Vector3[] vertices, out int[] edgeA, out int[] edgeB)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>(60);

            // Even permutations of three base triples, all sign combinations.
            var bases = new[]
            {
                new Vector3(0f, 1f, 3f * phi),
                new Vector3(1f, 2f + phi, 2f * phi),
                new Vector3(phi, 2f, 2f * phi + 1f),
            };
            foreach (var b in bases)
                foreach (var perm in EvenPermutations(b))
                    foreach (var s in SignCombos())
                        AddUnique(verts, Vector3.Scale(perm, s));

            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            vertices = verts.ToArray();

            // Edge length on this construction is 2 (before normalising); after normalising the
            // nearest-neighbour distance is the unique minimum - pair up by that distance.
            float minD = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
                for (int j = i + 1; j < vertices.Length; j++)
                    minD = Mathf.Min(minD, (vertices[i] - vertices[j]).sqrMagnitude);

            var ea = new List<int>(); var eb = new List<int>();
            float thresh = minD * 1.15f;
            for (int i = 0; i < vertices.Length; i++)
                for (int j = i + 1; j < vertices.Length; j++)
                    if ((vertices[i] - vertices[j]).sqrMagnitude <= thresh) { ea.Add(i); eb.Add(j); }
            edgeA = ea.ToArray();
            edgeB = eb.ToArray();
        }

        static IEnumerable<Vector3> EvenPermutations(Vector3 t)
        {
            // The three even (cyclic) permutations of a triple.
            yield return new Vector3(t.x, t.y, t.z);
            yield return new Vector3(t.z, t.x, t.y);
            yield return new Vector3(t.y, t.z, t.x);
        }

        static IEnumerable<Vector3> SignCombos()
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        yield return new Vector3(sx, sy, sz);
        }

        /// <summary>Add if not already present (1e-4 tolerance); true when added.</summary>
        static bool AddUnique(List<Vector3> verts, Vector3 v)
        {
            foreach (var e in verts) if ((e - v).sqrMagnitude < 1e-4f) return false;
            verts.Add(v);
            return true;
        }

        // ── Flight-continuity stroke ordering ────────────────────────────────

        /// <summary>
        /// Reorder strokes so the FLIGHT chains: each stroke starts near where the previous one
        /// ended (greedy nearest-next-start tour). Continuity takes precedence; among candidates
        /// within a near-tie band of the best gap (4% of the painting diagonal), the LEAST curvy
        /// stroke goes first, so fine detail still lands later. The order stays domain-contiguous
        /// - strokes group by domain and each group is entered at its most continuous stroke - so
        /// the trail recolours at most once per domain. Stroke 0 keeps its place (the authored
        /// opening stroke and its gate position). Returns a NEW list of the same stroke objects;
        /// deterministic, so progress-store stroke indices are stable across sessions.
        /// </summary>
        public static List<PaintingStroke> OrderForFlightContinuity(IReadOnlyList<PaintingStroke> strokes)
        {
            int n = strokes?.Count ?? 0;
            var result = new List<PaintingStroke>(n);
            if (n == 0) return result;
            if (n == 1) { result.Add(strokes[0]); return result; }

            // Near-tie band scales with the painting, not the stroke.
            Vector3 min = strokes[0].points[0], max = min;
            var curviness = new float[n];
            for (int i = 0; i < n; i++)
            {
                var pts = strokes[i].points;
                float turn = 0f, arc = 0f;
                for (int k = 0; k < pts.Count; k++)
                {
                    min = Vector3.Min(min, pts[k]);
                    max = Vector3.Max(max, pts[k]);
                    if (k == 0) continue;
                    arc += Vector3.Distance(pts[k - 1], pts[k]);
                    if (k < pts.Count - 1)
                        turn += Vector3.Angle(pts[k] - pts[k - 1], pts[k + 1] - pts[k]);
                }
                curviness[i] = arc > 1e-3f ? turn / arc : 0f; // degrees per unit flown
            }
            float band = 0.04f * (max - min).magnitude;

            // Domain groups in first-appearance order; the tour enters each group at its most
            // continuous stroke and chains greedily inside it.
            var domainOrder = new List<Domains>();
            var groups = new Dictionary<Domains, List<int>>();
            for (int i = 0; i < n; i++)
            {
                if (!groups.TryGetValue(strokes[i].domain, out var g))
                {
                    g = new List<int>();
                    groups[strokes[i].domain] = g;
                    domainOrder.Add(strokes[i].domain);
                }
                g.Add(i);
            }

            Vector3 penPos = strokes[0].points[0];
            int entry = 0;
            var remainingDomains = new List<Domains>(domainOrder);
            Domains currentDomain = strokes[0].domain;

            while (true)
            {
                remainingDomains.Remove(currentDomain);
                var group = groups[currentDomain];

                // Greedy tolerance-band tour within the group, starting from the entry stroke.
                group.Remove(entry);
                result.Add(strokes[entry]);
                penPos = strokes[entry].points[^1];
                while (group.Count > 0)
                {
                    float bestGap = float.MaxValue;
                    foreach (int i in group)
                        bestGap = Mathf.Min(bestGap, Vector3.Distance(penPos, strokes[i].points[0]));

                    int pick = -1;
                    float pickCurve = float.MaxValue;
                    foreach (int i in group)
                    {
                        if (Vector3.Distance(penPos, strokes[i].points[0]) > bestGap + band) continue;
                        if (curviness[i] < pickCurve || (curviness[i] == pickCurve && i < pick))
                        {
                            pick = i;
                            pickCurve = curviness[i];
                        }
                    }

                    group.Remove(pick);
                    result.Add(strokes[pick]);
                    penPos = strokes[pick].points[^1];
                }

                if (remainingDomains.Count == 0) break;

                // Next domain group: the one whose best entry chains closest to the pen.
                float bestSeam = float.MaxValue;
                foreach (var dom in remainingDomains)
                    foreach (int i in groups[dom])
                    {
                        float d = Vector3.Distance(penPos, strokes[i].points[0]);
                        if (d < bestSeam)
                        {
                            bestSeam = d;
                            currentDomain = dom;
                            entry = i;
                        }
                    }
            }

            return result;
        }

        // ── Small helpers ────────────────────────────────────────────────────

        /// <summary>Two unit vectors perpendicular to <paramref name="axis"/> (and the normalised axis).</summary>
        public static void Basis(Vector3 axis, out Vector3 u, out Vector3 v, out Vector3 a)
        {
            a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.up;
            Vector3 seed = Mathf.Abs(Vector3.Dot(a, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            u = Vector3.Cross(a, seed).normalized;
            v = Vector3.Cross(a, u).normalized;
        }

        /// <summary>Lift every point of every stroke so the lowest sits exactly on y=0 (the base plane).</summary>
        public static void RebaseToGround(List<PaintingStroke> strokes)
        {
            if (strokes == null || strokes.Count == 0) return;
            float minY = float.MaxValue;
            foreach (var s in strokes)
                if (s?.points != null)
                    foreach (var p in s.points) minY = Mathf.Min(minY, p.y);
            if (minY == float.MaxValue || Mathf.Abs(minY) < 1e-4f) return;

            foreach (var s in strokes)
            {
                if (s?.points == null) continue;
                for (int i = 0; i < s.points.Count; i++)
                {
                    var p = s.points[i];
                    s.points[i] = new Vector3(p.x, p.y - minY, p.z);
                }
            }
        }

        /// <summary>
        /// Drop points closer than <paramref name="minSeg"/> to their predecessor so dense parametric
        /// sampling (tight spiral cores, converging petal edges) never emits a degenerate segment.
        /// The final point is always represented; it merges into its predecessor when too close.
        /// </summary>
        public static List<Vector3> MinSegFilter(IReadOnlyList<Vector3> pts, float minSeg)
        {
            var outPts = new List<Vector3>();
            if (pts == null || pts.Count == 0) return outPts;
            outPts.Add(pts[0]);
            if (pts.Count < 2) return outPts;
            for (int i = 1; i < pts.Count - 1; i++)
                if (Vector3.Distance(pts[i], outPts[outPts.Count - 1]) >= minSeg)
                    outPts.Add(pts[i]);
            Vector3 last = pts[pts.Count - 1];
            if (Vector3.Distance(last, outPts[outPts.Count - 1]) >= minSeg * 0.5f)
                outPts.Add(last);
            else
                outPts[outPts.Count - 1] = last;
            return outPts;
        }

        /// <summary>
        /// Rotation-minimizing frames along a polyline (projection transport). Unlike calling
        /// <see cref="Basis"/> per point, the frame never flips as the tangent sweeps - required for
        /// clean tube longitudes on curves like torus knots whose tangents cover the whole sphere.
        /// </summary>
        public static void TransportFrames(IReadOnlyList<Vector3> spine, out Vector3[] normals, out Vector3[] binormals)
        {
            int n = spine?.Count ?? 0;
            normals = new Vector3[n];
            binormals = new Vector3[n];
            if (n < 2) return;

            Vector3 tan0 = (spine[1] - spine[0]).normalized;
            Basis(tan0, out Vector3 nrm, out _, out _);
            for (int i = 0; i < n; i++)
            {
                Vector3 tan = i == 0 ? tan0
                    : i == n - 1 ? (spine[i] - spine[i - 1]).normalized
                    : (spine[i + 1] - spine[i - 1]).normalized;
                nrm -= tan * Vector3.Dot(nrm, tan);
                if (nrm.sqrMagnitude < 1e-10f) Basis(tan, out nrm, out _, out _);
                nrm.Normalize();
                normals[i] = nrm;
                binormals[i] = Vector3.Cross(tan, nrm);
            }
        }

        /// <summary>
        /// <paramref name="count"/> longitudinal frame lines on a tube around <paramref name="spine"/>,
        /// twisting <paramref name="twistTurns"/> full turns end to end (keep it whole so closed curves
        /// seal). On a CLOSED spine, parallel transport comes back rotated by the loop's holonomy -
        /// left uncorrected, every longitude ends offset around the tube and the loop doesn't quite
        /// connect. The holonomy is measured and distributed as a compensating counter-twist, and the
        /// final point is snapped exactly onto the first.
        /// </summary>
        public static List<List<Vector3>> TubeLongitudes(IReadOnlyList<Vector3> spine, float radius,
            int count, int twistTurns)
        {
            TransportFrames(spine, out Vector3[] normals, out Vector3[] binormals);
            int n = spine.Count;

            bool closed = n > 3 && Vector3.Distance(spine[0], spine[n - 1]) < Mathf.Max(1e-3f, 0.02f * radius);
            float holonomyRad = 0f;
            if (closed)
            {
                Vector3 tan0 = (spine[1] - spine[0]).normalized;
                holonomyRad = Vector3.SignedAngle(normals[0], normals[n - 1], tan0) * Mathf.Deg2Rad;
            }

            var lines = new List<List<Vector3>>(count);
            for (int k = 0; k < count; k++)
            {
                var pts = new List<Vector3>(n);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    float ang = Mathf.PI * 2f * (k / (float)count) + Mathf.PI * 2f * twistTurns * t
                                - holonomyRad * t;   // cancel the transport holonomy → the loop seals
                    pts.Add(spine[i] + normals[i] * (Mathf.Cos(ang) * radius) + binormals[i] * (Mathf.Sin(ang) * radius));
                }
                if (closed) pts[n - 1] = pts[0]; // kill the last float-drift so shared surfaces truly connect
                lines.Add(pts);
            }
            return lines;
        }

        /// <summary>One circumference ring of the tube at spine index <paramref name="i"/> (frames from <see cref="TransportFrames"/>).</summary>
        public static List<Vector3> TubeRing(IReadOnlyList<Vector3> spine, Vector3[] normals, Vector3[] binormals,
            int i, float radius, int seg)
        {
            var pts = new List<Vector3>(seg + 1);
            for (int j = 0; j <= seg; j++)
            {
                float a = Mathf.PI * 2f * j / seg;
                pts.Add(spine[i] + normals[i] * (Mathf.Cos(a) * radius) + binormals[i] * (Mathf.Sin(a) * radius));
            }
            return pts;
        }

        /// <summary>
        /// The 30 hexagon–hexagon shared edges of the truncated icosahedron - chemically, C60's 6:6
        /// DOUBLE bonds (every edge not belonging to a pentagon). Endpoints on the unit sphere.
        /// </summary>
        public static void SoccerBallDoubleBonds(out Vector3[] bondA, out Vector3[] bondB)
        {
            TruncatedIcosahedron(out Vector3[] verts, out int[] edgeA, out int[] edgeB);

            // Pentagon membership: gather each pentagon's ring the same way SoccerBallFaces does.
            var adj = new HashSet<int>(edgeA.Length);
            for (int e = 0; e < edgeA.Length; e++)
                adj.Add(Mathf.Min(edgeA[e], edgeB[e]) * 64 + Mathf.Max(edgeA[e], edgeB[e]));
            var pentEdges = new HashSet<int>();
            foreach (var c in IcosahedronDirs())
            {
                var loop = GatherFace(verts, adj, c, 5);
                for (int i = 0; i < loop.Length - 1; i++)
                {
                    int a = IndexOfVertex(verts, loop[i]);
                    int b = IndexOfVertex(verts, loop[i + 1]);
                    pentEdges.Add(Mathf.Min(a, b) * 64 + Mathf.Max(a, b));
                }
            }

            var la = new List<Vector3>(30);
            var lb = new List<Vector3>(30);
            for (int e = 0; e < edgeA.Length; e++)
            {
                int key = Mathf.Min(edgeA[e], edgeB[e]) * 64 + Mathf.Max(edgeA[e], edgeB[e]);
                if (pentEdges.Contains(key)) continue;
                la.Add(verts[edgeA[e]]);
                lb.Add(verts[edgeB[e]]);
            }
            bondA = la.ToArray();
            bondB = lb.ToArray();
        }

        static int IndexOfVertex(Vector3[] verts, Vector3 v)
        {
            for (int i = 0; i < verts.Length; i++)
                if ((verts[i] - v).sqrMagnitude < 1e-6f) return i;
            return -1;
        }

        /// <summary>
        /// Ride checkpoints for a stroke polyline: indices spaced at least <paramref name="minSpacing"/>
        /// of arc apart, preferring vertices whose local turn is gentle (≤ <paramref name="maxTurnDeg"/>)
        /// - a checkpoint on a hairpin punishes a fast vessel that can't sit on the apex. On a stretch
        /// that is tight everywhere, the flattest vertex since the last checkpoint is used once the arc
        /// exceeds 2.5× spacing, so progress can never stall. Index 0 (the start gate) and the final
        /// index (the stroke-end jack) are always included; a checkpoint landing within 0.4× spacing of
        /// the end is dropped in its favour. Closed loops always keep at least one mid checkpoint
        /// (near the half-arc) so a ring whose end sits at its own gate cannot complete unridden.
        /// </summary>
        public static List<int> RideCheckpoints(IReadOnlyList<Vector3> pts, float minSpacing, float maxTurnDeg)
        {
            var cps = new List<int> { 0 };
            int n = pts?.Count ?? 0;
            if (n < 2) return cps;

            float sinceLast = 0f;
            int bestIdx = -1;
            float bestTurn = float.MaxValue;
            for (int i = 1; i < n - 1; i++)
            {
                sinceLast += Vector3.Distance(pts[i - 1], pts[i]);
                if (sinceLast < minSpacing) continue;

                float turn = Vector3.Angle(pts[i] - pts[i - 1], pts[i + 1] - pts[i]);
                if (turn < bestTurn) { bestTurn = turn; bestIdx = i; }

                if (turn <= maxTurnDeg)
                {
                    cps.Add(i);
                    sinceLast = 0f;
                    bestIdx = -1; bestTurn = float.MaxValue;
                }
                else if (sinceLast > minSpacing * 2.5f && bestIdx >= 0)
                {
                    // everything since the last checkpoint is tight - take the flattest vertex seen
                    cps.Add(bestIdx);
                    float arc = 0f;
                    for (int k = bestIdx + 1; k <= i; k++) arc += Vector3.Distance(pts[k - 1], pts[k]);
                    sinceLast = arc;
                    bestIdx = -1; bestTurn = float.MaxValue;
                }
            }

            // The stroke end is always a checkpoint; drop a predecessor that crowds it.
            if (cps.Count > 1 && cps[cps.Count - 1] != 0)
            {
                float arcToEnd = 0f;
                for (int k = cps[cps.Count - 1] + 1; k < n; k++) arcToEnd += Vector3.Distance(pts[k - 1], pts[k]);
                if (arcToEnd < minSpacing * 0.4f) cps.RemoveAt(cps.Count - 1);
            }
            cps.Add(n - 1);

            // Closed (or near-closed) loops MUST keep a mid checkpoint: with only [start, end] the
            // end ring sits at the gate the vessel just flew and the stroke would complete
            // instantly, unridden. Force the flattest vertex nearest the loop's half-arc.
            if (cps.Count == 2 && n > 3 &&
                Vector3.Distance(pts[0], pts[n - 1]) < minSpacing)
            {
                float total = 0f;
                for (int k = 1; k < n; k++) total += Vector3.Distance(pts[k - 1], pts[k]);

                int midIdx = -1;
                float bestScore = float.MaxValue;
                float arc = 0f;
                for (int i = 1; i < n - 1; i++)
                {
                    arc += Vector3.Distance(pts[i - 1], pts[i]);
                    float turn = Vector3.Angle(pts[i] - pts[i - 1], pts[i + 1] - pts[i]);
                    // Prefer near-half-arc, tie-broken toward gentle turns.
                    float score = Mathf.Abs(arc - total * 0.5f) + turn * 0.5f;
                    if (score < bestScore) { bestScore = score; midIdx = i; }
                }
                if (midIdx > 0) cps.Insert(1, midIdx);
            }
            return cps;
        }

        /// <summary>
        /// Resample a polyline so no segment exceeds <paramref name="maxSeg"/> - inserts points along
        /// long spans so a fast, broad curve stays flyable (the runner advances point-to-point).
        /// </summary>
        public static List<Vector3> EnforceMaxSegment(IReadOnlyList<Vector3> pts, float maxSeg)
        {
            var outPts = new List<Vector3>();
            if (pts == null || pts.Count == 0) return outPts;
            outPts.Add(pts[0]);
            for (int i = 1; i < pts.Count; i++)
            {
                Vector3 a = pts[i - 1], b = pts[i];
                float d = Vector3.Distance(a, b);
                int subdiv = Mathf.Max(1, Mathf.CeilToInt(d / Mathf.Max(1f, maxSeg)));
                for (int s = 1; s <= subdiv; s++)
                    outPts.Add(Vector3.Lerp(a, b, s / (float)subdiv));
            }
            return outPts;
        }
    }
}
