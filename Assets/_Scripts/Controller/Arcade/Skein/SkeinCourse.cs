using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One laid rail: a contiguous run of prisms on one strand, its own open Trail.</summary>
    public readonly struct SkeinRail
    {
        /// <summary>Which strand of the cable this run belongs to.</summary>
        public readonly int Strand;
        /// <summary>First and last node index into that strand's polyline, inclusive.</summary>
        public readonly int Start, End;
        /// <summary>The domain this run is painted. Never <see cref="Data.Domains.Blue"/>.</summary>
        public readonly Data.Domains Domain;
        /// <summary>True when the far end is an AIMED break - i.e. this rail launches.</summary>
        public readonly bool Launches;

        public SkeinRail(int strand, int start, int end, Data.Domains domain, bool launches)
        {
            Strand = strand; Start = start; End = end; Domain = domain; Launches = launches;
        }

        public int PrismCount => End - Start + 1;
    }

    /// <summary>
    /// One ring of a Skein course. A gate centred on a STRAND has its axis along that strand, so a
    /// correct rider threads it without steering; the two SPINE collars swallow the whole cable.
    /// </summary>
    public readonly struct SkeinGate
    {
        public readonly Vector3 Position;
        public readonly Vector3 Axis;      // unit; the direction the course flows through the mouth
        public readonly float Radius;
        /// <summary>The strand this gate sits on, or -1 for the start/finish collars.</summary>
        public readonly int Strand;

        public SkeinGate(Vector3 position, Vector3 axis, float radius, int strand)
        {
            Position = position; Axis = axis; Radius = radius; Strand = strand;
        }

        public bool IsCollar => Strand < 0;
    }

    /// <summary>
    /// Tuning for one generated cable. Every number here is mirrored in
    /// <c>Tools/Build/skein_budget.py</c>, which PROVES the arena offline; changing one here
    /// without changing it there is the drift that file exists to make impossible.
    /// </summary>
    public struct SkeinCourseSettings
    {
        // ── the spine: a (2,3) torus knot ────────────────────────────────────
        public float MajorRadius;      // R
        public float MinorRadius;      // r - ALSO the self-clearance theorem's constant
        // ── the two shells ───────────────────────────────────────────────────
        public float InnerRadius;      // a_in
        public float OuterRadius;      // a_out
        public int StrandCount;        // N, the intensity dial
        // ── segmentation ─────────────────────────────────────────────────────
        public float SegmentSpine;
        public float FlareSpine;
        public float FlareFloor;
        public float BreakGap;
        public float PrismSpacing;
        // ── the trim's acceptance conditions ─────────────────────────────────
        public float EndAimRadius, EndAimMin, EndAimMax, RayClearance, ArrivalAngleMax;
        public int MinSegmentPrisms;
        // ── gates ────────────────────────────────────────────────────────────
        public int GateCount;
        public float GateMouth, CollarMouth, GateSeparation;

        /// <summary>
        /// INTENSITY IS THE RAIL COUNT, and nothing else moves.
        ///
        /// <para>The knot, the shells, the lay, the gate count, the gate mouth, the segment
        /// length, the prism, the spacing and the spawn ring are identical at all four levels,
        /// so the arena's silhouette, its hollow core, its launch geometry and its fairness
        /// argument never move. What climbs is how many lanes there are to read, how often the
        /// shells cross, and how much tighter the same-shell separation gets.</para>
        ///
        /// <para>Both ends are derived rather than chosen. <b>N_min = 5</b>: at two outer strands
        /// a gate on the outer shell is a coin flip. <b>N_max = 9</b>: the tightest same-shell
        /// separation <c>2 * a_in * sin(pi / n_in)</c> is 64.7 u at N=9 and must clear the 40 u
        /// gate mouth with margin, or a ring becomes threadable from the lane next door - which
        /// is the exclusivity the whole mode is built on.</para>
        /// </summary>
        public static SkeinCourseSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new SkeinCourseSettings
            {
                MajorRadius = 560f,
                MinorRadius = 200f,
                InnerRadius = 55f,
                OuterRadius = 120f,
                StrandCount = new[] { 5, 6, 7, 9 }[i - 1],

                SegmentSpine = 450f,
                // DERIVED and pinned from BOTH sides, with about 40 u of slack. Below ~185 the
                // per-prism turn through the flare exceeds the pilot's sustained budget of
                // TurnRate * spacing / grindSpeed = 90 * 8 / 150 = 4.80 deg (measured: 65 u ->
                // 16.81 deg, 150 -> 5.90, 200 -> 4.52, 215 -> 4.41). Above SegmentSpine / 2
                // adjacent flares OVERLAP and the radius profile self-intersects, which reads as
                // a ~91 deg per-prism turn that no flare length shifts.
                FlareSpine = 215f,
                FlareFloor = 55f,
                BreakGap = 40f,
                PrismSpacing = 8f,

                EndAimRadius = 12f,
                EndAimMin = 60f,
                EndAimMax = 420f,
                RayClearance = 24f,
                // 60 leaves 30 degrees of margin under the 90 at which TrailFollower.Attach
                // seeds Backward and carries the pilot back up the course at 150 u/s.
                ArrivalAngleMax = 60f,
                MinSegmentPrisms = 40,

                GateCount = 24,
                GateMouth = 40f,
                CollarMouth = 150f,
                GateSeparation = 200f,
            };
        }
    }

    /// <summary>
    /// Builds a Skein cable: a trefoil-knot spine wrapped in two shells of open, aimed rails, and
    /// the ordered ring course laid along them.
    ///
    /// <para><b>Pure and deterministic.</b> No <c>UnityEngine.Random</c> (global state), no
    /// <c>System.Random</c> (implementation-defined across runtimes), no <c>Time</c>, no scene
    /// access, no <c>Transform</c>. The generator owns a fully specified xorshift32 copied byte
    /// for byte from <see cref="SwitchbackCourse"/>, so the same seed yields the same cable on
    /// any machine and the whole thing is unit-testable offline. The controller adds the cell
    /// centre once, on the server, before broadcasting.</para>
    ///
    /// <para><b>The offline model is the authority.</b> <c>Tools/Build/skein_budget.py</c> mirrors
    /// this file and asserts every property below over a seed sweep; it caught, among others,
    /// that an outer rail cannot aim without a flare (0 of 384 sampled indices), that the first
    /// flare was 3.5x too sharp to ride, and that two of its own proof gates could not fail.
    /// <b>Change a number here and re-run it.</b></para>
    ///
    /// <para><b>Three facts hold EXACTLY</b>, and they are what make this a knot rather than a
    /// wandering walk:</para>
    /// <list type="number">
    /// <item><c>|C'(t)| = sqrt(9r^2 + 4(R + r cos 3t)^2)</c> - the cross terms cancel
    /// identically, so arc length is a clean 1-D integral rather than a polyline sum.</item>
    /// <item>The spine's minimum non-local self-distance is <b>exactly 2r</b>, at every t: the two
    /// passes at a shared azimuth are t and t+pi, where cos3t flips sign and cos2t does not. So
    /// there is no seed that can generate an unplayable arena, nothing to reject, and no redraw
    /// budget. A wandering spine has to PROVE it does not pass near itself, and at this cable
    /// diameter it cannot.</item>
    /// <item><c>u(t) . C'(t) == 0</c> for the torus outward normal (measured 2.4e-16), so the
    /// frame is exact. Frenet tears the braid through an inflection; parallel transport does not
    /// close on a loop.</item>
    /// </list>
    /// </summary>
    public static class SkeinCourse
    {
        const int SpineSamples = 2048;
        const int TrimScan = 40;
        const float PhaseJitter = 0.30f;   // of a slot
        const float CutJitter = 0.22f;     // of SegmentSpine

        /// <summary>
        /// Deterministic 32-bit xorshift, identical to <see cref="SwitchbackCourse"/>'s and to the
        /// offline model's. Specified arithmetic on unsigned ints, so the sequence is a property
        /// of the SEED rather than of the runtime - unlike <c>System.Random</c>.
        /// </summary>
        struct Rng
        {
            uint _s;

            public Rng(int seed)
            {
                // 0 is the xorshift fixed point: it would emit nothing but zeros forever, which
                // yields a degenerate cable silently rather than throwing.
                uint s = unchecked((uint)seed);
                _s = s != 0u ? s : 0x9E3779B9u;
            }

            public uint NextUInt()
            {
                uint x = _s;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _s = x;
                return x;
            }

            public float Unit() => NextUInt() / 4294967296f;
            public float Range(float a, float b) => a + (b - a) * Unit();
        }

        // ── the spine ────────────────────────────────────────────────────────

        static Vector3 SpinePoint(float t, float R, float r)
        {
            float a = R + r * Mathf.Cos(3f * t);
            return new Vector3(a * Mathf.Cos(2f * t), a * Mathf.Sin(2f * t), r * Mathf.Sin(3f * t));
        }

        /// <summary>|C'(t)|, closed form - the cross terms in |C'|^2 cancel identically.</summary>
        static float SpineSpeed(float t, float R, float r)
        {
            float a = R + r * Mathf.Cos(3f * t);
            return Mathf.Sqrt(9f * r * r + 4f * a * a);
        }

        static Vector3 SpineTangent(float t, float R, float r)
        {
            float a = R + r * Mathf.Cos(3f * t);
            float ap = -3f * r * Mathf.Sin(3f * t);
            return new Vector3(ap * Mathf.Cos(2f * t) - 2f * a * Mathf.Sin(2f * t),
                               ap * Mathf.Sin(2f * t) + 2f * a * Mathf.Cos(2f * t),
                               3f * r * Mathf.Cos(3f * t)).normalized;
        }

        /// <summary>The torus OUTWARD normal. u . C' == 0 identically.</summary>
        static Vector3 TorusNormal(float t) =>
            new Vector3(Mathf.Cos(3f * t) * Mathf.Cos(2f * t),
                Mathf.Cos(3f * t) * Mathf.Sin(2f * t),
                Mathf.Sin(3f * t));

        /// <summary>The spine sampled once, with arc length and the torus frame at every station.</summary>
        sealed class Spine
        {
            public readonly Vector3[] Pos, Tan, U, V;
            public readonly float[] S;
            public readonly float L;
            readonly int _n;

            public Spine(float R, float r, int n)
            {
                _n = n;
                Pos = new Vector3[n + 1]; Tan = new Vector3[n + 1];
                U = new Vector3[n + 1]; V = new Vector3[n + 1]; S = new float[n + 1];

                for (int i = 0; i <= n; i++)
                {
                    float t = 2f * Mathf.PI * i / n;
                    Pos[i] = SpinePoint(t, R, r);
                    Tan[i] = SpineTangent(t, R, r);
                    U[i] = TorusNormal(t);
                    V[i] = Vector3.Cross(Tan[i], U[i]);
                }
                // Arc length by the trapezoid rule on the CLOSED-FORM speed, never on chords: a
                // polyline underestimates a curve's length and that error would land in every
                // derived helix angle.
                for (int i = 0; i < n; i++)
                {
                    float h = 2f * Mathf.PI / n;
                    S[i + 1] = S[i] + 0.5f * h * (SpineSpeed(2f * Mathf.PI * i / n, R, r)
                                                + SpineSpeed(2f * Mathf.PI * (i + 1) / n, R, r));
                }
                L = S[n];
            }

            public void FrameAtArc(float s, out Vector3 p, out Vector3 t, out Vector3 u, out Vector3 v)
            {
                s -= Mathf.Floor(s / L) * L;
                int lo = 0, hi = _n;
                while (lo < hi) { int mid = (lo + hi) >> 1; if (S[mid] <= s) lo = mid + 1; else hi = mid; }
                int i = Mathf.Max(0, lo - 1);
                float span = S[i + 1] - S[i];
                float f = span > 1e-6f ? (s - S[i]) / span : 0f;

                p = Vector3.Lerp(Pos[i], Pos[i + 1], f);
                t = Vector3.Slerp(Tan[i], Tan[i + 1], f).normalized;
                Vector3 uu = Vector3.Lerp(U[i], U[i + 1], f);
                // Re-orthogonalise: the blend leaves the plane, and the strand offset is
                // expressed in this frame, so a drifting u bends every rail off the shell.
                u = (uu - Vector3.Dot(uu, t) * t).normalized;
                v = Vector3.Cross(t, u);
            }
        }

        // ── the shells ───────────────────────────────────────────────────────

        static float SmoothStep(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>One rail's centreline, sampled at a fixed pitch of ITS OWN arc length.</summary>
        sealed class Strand
        {
            public readonly int Index;
            public readonly bool Outer;
            public readonly float A, Lam, Phi;
            public readonly float[] Cuts;
            public readonly List<Vector3> Pts = new List<Vector3>();
            public readonly List<float> Arc = new List<float>();
            readonly float _floor, _flare;

            public int N => Pts.Count;

            public Strand(Spine spine, int index, bool outer, float a, float lam, float phi,
                          float[] cuts, float floor, float flare, float spacing)
            {
                Index = index; Outer = outer; A = a; Lam = lam; Phi = phi;
                Cuts = cuts; _floor = floor; _flare = flare;

                // Sample at `spacing` of the strand's TRUE arc length, by walking the spine finely
                // and emitting a node every `spacing` of accumulated chord.
                //
                // The obvious sampler - step the spine by spacing / f, with f the constant-radius
                // arclength factor - is wrong wherever the RADIUS is moving. That is precisely the
                // flare, which is precisely where the geometry is hardest, and the error surfaces
                // as a per-prism turn far outside the pilot's budget rather than as anything
                // visibly wrong.
                const float step = 0.5f;
                Vector3 prev = Point(spine, 0f);
                Pts.Add(prev); Arc.Add(0f);
                float s = 0f, acc = 0f;
                while (s < spine.L)
                {
                    s += step;
                    Vector3 cur = Point(spine, s);
                    acc += Vector3.Distance(cur, prev);
                    prev = cur;
                    if (acc >= spacing) { Pts.Add(cur); Arc.Add(s); acc = 0f; }
                }
            }

            /// <summary>
            /// The strand's radius at a spine arc length: a SYMMETRIC V dipping to the floor at
            /// each break.
            ///
            /// <para>Symmetric, not one-sided, and that is a correctness fix rather than a
            /// flourish. A one-sided flare leaves the radius snapping floor -> A the instant past
            /// the break, a 65 u discontinuity in the underlying curve. The ride never traverses
            /// it (it is the break gap), but every measurement over the strand does, and it reads
            /// as a ~91 deg per-prism turn that NO flare length shifts - that invariance under the
            /// parameter is the tell that a measured number is a discontinuity, not a curvature.</para>
            ///
            /// <para>Note what this actually does: with a symmetric smoothstep the radial
            /// derivative is ZERO at the vertex, so a break does not "dive inward" at all. The
            /// flare brings an outer rail DOWN to the inner shell, and it then launches exactly
            /// like an inner rail. That is why a flared outer break's arrival angles match an
            /// inner break's rather than being steeper.</para>
            /// </summary>
            public float RadiusAt(float s)
            {
                if (Cuts == null || Cuts.Length == 0) return A;
                for (int i = 0; i < Cuts.Length; i++)
                {
                    float d = Mathf.Abs(s - Cuts[i]);
                    if (d <= _flare) return _floor + (A - _floor) * SmoothStep(d / _flare);
                }
                return A;
            }

            public Vector3 Point(Spine spine, float s)
            {
                spine.FrameAtArc(s, out var p, out _, out var u, out var v);
                float th = Phi + s / Lam;
                float a = RadiusAt(s);
                return p + u * (a * Mathf.Cos(th)) + v * (a * Mathf.Sin(th));
            }
        }

        static float LamForTurns(float L, int w) => L / (2f * Mathf.PI * w);
        static float HelixAngleDeg(float a, float lam) => Mathf.Atan2(a, lam) * Mathf.Rad2Deg;
        static float ArclengthFactor(float a, float lam) => Mathf.Sqrt(a * a + lam * lam) / lam;

        /// <summary>
        /// The twist is SOLVED, not authored: the largest integer twist that still satisfies both
        /// derived bounds, because more twist is more crossings and a more legible braid.
        ///
        /// <para><c>psi_in &lt;= 25 deg</c> - the inner shell must be the fast lane by a readable
        /// margin; above 25 the two shells' course speeds converge and the mode's central choice
        /// evaporates. <c>f_out &lt;= 2.0</c> - an outer lane must stay at least 1.4x faster than
        /// FLYING it (150 / (1.4 * 52.6) = 2.038), or the shell stops being a road and becomes a
        /// punishment.</para>
        ///
        /// <para>BOTH SHELLS WIND THE SAME WAY. Counter-winding halves the crossing period, which
        /// is the tempting reason for it, and makes the inner/outer tangent dot numerator
        /// <c>lam_in*lam_out - a_in*a_out</c>, which goes NEGATIVE at any practical lay: a launch
        /// onto a counter-wound strand lands at more than 90 degrees to it, Attach seeds Backward,
        /// and the pilot is carried back up the course at grind speed.</para>
        /// </summary>
        static void SolveTwists(float L, float aIn, float aOut, out int wIn, out int wOut)
        {
            wIn = 1;
            while (HelixAngleDeg(aIn, LamForTurns(L, wIn + 1)) <= 25f && wIn < 512) wIn++;
            wOut = 1;
            while (ArclengthFactor(aOut, LamForTurns(L, wOut + 1)) <= 2f && wOut < 512) wOut++;
        }

        // ── cuts, jittered per seed ──────────────────────────────────────────

        /// <summary>
        /// The spine arcs at which one strand breaks - staggered by strand so a break happens
        /// somewhere in the cable every SegmentSpine / N of spine rather than all at one station,
        /// and JITTERED per seed so two matches at one intensity are not the same course.
        ///
        /// <para>The jitter is bounded rather than free, and that is the whole trick: the cable's
        /// SHAPE is fixed, so every proof about clearance, nesting and rideability holds at every
        /// seed, and what varies is WHERE IT BREAKS - which is exactly what "rails that randomly
        /// end" means. Adjacent flares must not overlap at ANY seed, so each accepted cut is
        /// clamped against the previous one rather than trusted to stay clear of it.</para>
        /// </summary>
        static float[] CutArcs(Spine spine, int strandIndex, int n, int seed, in SkeinCourseSettings s)
        {
            // Unchecked 32-bit, matching the offline model's arbitrary-precision-then-mask
            // exactly. Left as `int` arithmetic it overflows and the two disagree on the cable.
            var rng = new Rng(unchecked((int)((uint)seed * 2654435761u
                                            + (uint)strandIndex * 40503u + 1u)));
            var outArcs = new List<float>(32);
            float offset = strandIndex * s.SegmentSpine / n
                         + rng.Range(-CutJitter, CutJitter) * s.SegmentSpine;
            for (int k = 0; ; k++)
            {
                float arc = offset + k * s.SegmentSpine
                          + rng.Range(-CutJitter, CutJitter) * s.SegmentSpine;
                if (arc >= spine.L - s.FlareSpine) break;
                if (arc > s.FlareSpine &&
                    (outArcs.Count == 0 || arc - outArcs[outArcs.Count - 1] >= 2f * s.FlareSpine + 1f))
                    outArcs.Add(arc);
            }
            return outArcs.ToArray();
        }

        static Strand[] BuildStrands(Spine spine, int seed, in SkeinCourseSettings s,
                                     int wIn, int wOut)
        {
            int nIn = (s.StrandCount + 1) / 2;
            int nOut = s.StrandCount / 2;
            float lamIn = LamForTurns(spine.L, wIn);
            float lamOut = LamForTurns(spine.L, wOut);

            var rng = new Rng(unchecked((int)((uint)seed * 747796405u + 2891336453u)));
            var strands = new Strand[s.StrandCount];
            int idx = 0;
            for (int k = 0; k < nIn; k++, idx++)
            {
                float phi = 2f * Mathf.PI * (k + rng.Range(-PhaseJitter, PhaseJitter)) / nIn;
                strands[idx] = new Strand(spine, idx, false, s.InnerRadius, lamIn, phi,
                                          CutArcs(spine, idx, s.StrandCount, seed, s),
                                          s.FlareFloor, s.FlareSpine, s.PrismSpacing);
            }
            for (int k = 0; k < nOut; k++, idx++)
            {
                float phi = 2f * Mathf.PI * (k + rng.Range(-PhaseJitter, PhaseJitter)) / nOut;
                strands[idx] = new Strand(spine, idx, true, s.OuterRadius, lamOut, phi,
                                          CutArcs(spine, idx, s.StrandCount, seed, s),
                                          s.FlareFloor, s.FlareSpine, s.PrismSpacing);
            }
            return strands;
        }

        // ── the trim: a break is AIMED, and the aim is MEASURED ──────────────

        struct Break
        {
            public int Strand, Index, Target, TargetNode;
            public float LandingArc, Miss, Arrival, RayLen, Clearance;
        }

        /// <summary>
        /// Closest approach of a ray to a polyline, sampled at its nodes (whose pitch IS the prism
        /// spacing, the same resolution the ride walks). Coarse-to-fine: a stride-8 pass finds the
        /// neighbourhood, then a local refine - the full O(n) scan per candidate would be ~82M dot
        /// products at N=9 and this is one-time server work behind the arena-build veil.
        /// </summary>
        static void ClosestOnPolyline(List<Vector3> pts, Vector3 origin, Vector3 dir,
                                      float lo, float hi, out float miss, out int node, out float range)
        {
            miss = float.MaxValue; node = -1; range = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                int stride = pass == 0 ? 8 : 1;
                int from = pass == 0 ? 0 : Mathf.Max(0, node - 8);
                int to = pass == 0 ? pts.Count : Mathf.Min(pts.Count, node + 9);
                if (pass == 1 && node < 0) return;
                for (int i = from; i < to; i += stride)
                {
                    float t = Vector3.Dot(pts[i] - origin, dir);
                    if (t < lo || t > hi) continue;
                    float d = Vector3.Distance(pts[i], origin + dir * t);
                    if (d < miss) { miss = d; node = i; range = t; }
                }
            }
        }

        /// <summary>Indices to try, nearest the raw cut first, alternating back then forward. An
        /// outer strand's aim lives at the BOTTOM of its flare, which IS the cut, so a
        /// backward-only scan searches the one part of the curve where the rail is still
        /// climbing.</summary>
        static IEnumerable<int> ScanOrder(int scan)
        {
            yield return 0;
            for (int k = 1; k <= scan; k++) { yield return -k; yield return k; }
        }

        static bool TryTrim(Strand[] strands, int si, int rawIndex, in SkeinCourseSettings s,
                            out Break b)
        {
            b = default;
            var self = strands[si];
            float cosMax = Mathf.Cos(s.ArrivalAngleMax * Mathf.Deg2Rad);

            foreach (int off in ScanOrder(TrimScan))
            {
                int idx = rawIndex + off;
                if (idx < s.MinSegmentPrisms || idx >= self.N - 1) continue;

                Vector3 origin = self.Pts[idx];
                Vector3 dir = (self.Pts[idx] - self.Pts[idx - 1]).normalized;

                int bestT = -1, bestNode = -1;
                float bestMiss = 0f, bestRange = float.MaxValue, bestArrival = 0f;
                for (int tj = 0; tj < strands.Length; tj++)
                {
                    if (tj == si) continue;
                    var t = strands[tj];
                    ClosestOnPolyline(t.Pts, origin, dir, s.EndAimMin, s.EndAimMax,
                                      out float miss, out int node, out float rng);
                    if (miss > s.EndAimRadius || node <= 0 || node >= t.N - 1) continue;
                    Vector3 tangent = t.Pts[node + 1] - t.Pts[node - 1];
                    float arr = Vector3.Angle(dir, tangent);
                    arr = Mathf.Min(arr, 180f - arr);   // a rail rides both ways; Attach picks
                    if (arr > s.ArrivalAngleMax) continue;
                    if (rng < bestRange)
                    { bestT = tj; bestNode = node; bestMiss = miss; bestRange = rng; bestArrival = arr; }
                }
                if (bestT < 0) continue;

                float clearance = float.MaxValue;
                for (int oj = 0; oj < strands.Length; oj++)
                {
                    if (oj == si || oj == bestT) continue;
                    ClosestOnPolyline(strands[oj].Pts, origin, dir, s.EndAimMin, bestRange,
                                      out float m, out _, out _);
                    clearance = Mathf.Min(clearance, m);
                }
                if (clearance < s.RayClearance) continue;

                b = new Break
                {
                    Strand = si, Index = idx, Target = bestT, TargetNode = bestNode,
                    LandingArc = strands[bestT].Arc[bestNode],
                    Miss = bestMiss, Arrival = bestArrival, RayLen = bestRange, Clearance = clearance,
                };
                return true;
            }
            _ = cosMax;
            return false;
        }

        // ── cut, paint, walk ─────────────────────────────────────────────────

        static readonly Data.Domains[] Triad =
        {
            Data.Domains.Jade, Data.Domains.Ruby, Data.Domains.Gold,
        };

        /// <summary>
        /// Balance the per-domain PRISM share to within 1% by greedily re-colouring the largest
        /// deviation.
        ///
        /// <para>MEASURED, not constructed, and that is the point: colouring by a rule that looks
        /// like fairness (siblings differ, a child differs from its parent) over-constrains and
        /// swings the share by several points - here the raw construction measured
        /// 37.5 / 29.5 / 33.0, one team holding a quarter more of the cable than another. So the
        /// construction seeds a colouring and the measurement fixes it.</para>
        /// </summary>
        static void Rebalance(List<SkeinRail> rails, Data.Domains[] colours)
        {
            int total = 0;
            for (int i = 0; i < rails.Count; i++) total += rails[i].PrismCount;
            if (total == 0) return;

            for (int round = 0; round < 400; round++)
            {
                var tot = new int[3];
                for (int i = 0; i < rails.Count; i++)
                    tot[System.Array.IndexOf(Triad, colours[i])] += rails[i].PrismCount;

                int hi = 0, lo = 0;
                for (int d = 1; d < 3; d++) { if (tot[d] > tot[hi]) hi = d; if (tot[d] < tot[lo]) lo = d; }
                if ((tot[hi] - tot[lo]) / (float)total <= 0.005f) return;

                float want = (tot[hi] - tot[lo]) * 0.5f;
                int best = -1; float bestCost = float.MaxValue;
                for (int i = 0; i < rails.Count; i++)
                {
                    if (colours[i] != Triad[hi]) continue;
                    float cost = Mathf.Abs(rails[i].PrismCount - want);
                    if (cost < bestCost) { best = i; bestCost = cost; }
                }
                if (best < 0) return;
                colours[best] = Triad[lo];
            }
        }

        /// <summary>No gate centre within `sep2` of one already placed - the invariant that stops
        /// one pass threading two rings, enforced DURING the walk rather than asserted after.</summary>
        static bool Clears(List<Vector3> placed, Vector3 p, float sep2)
        {
            for (int i = 0; i < placed.Count; i++)
                if ((placed[i] - p).sqrMagnitude < sep2) return false;
            return true;
        }

        /// <summary>
        /// The gate walk. Gate n+1 is not placed and then checked for reachability - it is placed
        /// ON A TRANSFER THE TRIM ALREADY VERIFIED, which is what gives the mode its floor: hold
        /// the throttle, ride every rail to its end, take every free aimed launch, and you arrive
        /// on the rail carrying your next ring. No lane-change skill is required to FINISH; all
        /// the skill is in finishing sooner.
        ///
        /// <para>Two details are load-bearing and both were found by measuring. SEPARATION IS
        /// ENFORCED DURING THE WALK rather than asserted after it - asserted after, the walk falls
        /// into a CYCLE and lays gates 11-13, 16-18 and 21-23 on top of each other. And THE
        /// LEAD-IN IS A RANGE, not a constant: one lead-in gives each break exactly one legal gate
        /// position, so once a dozen rings are down every remaining candidate collides and the
        /// walk starves at 22 of 24. Sliding a ring further down the SAME transfer costs the pilot
        /// nothing - they are already on that rail, committed.</para>
        /// </summary>
        static List<SkeinGate> WalkGates(Spine spine, Strand[] strands, List<Break> breaks,
                                         in SkeinCourseSettings s)
        {
            var byStrand = new List<Break>[strands.Length];
            for (int i = 0; i < strands.Length; i++) byStrand[i] = new List<Break>();
            foreach (var b in breaks) byStrand[b.Strand].Add(b);
            for (int i = 0; i < strands.Length; i++) byStrand[i].Sort((x, y) => x.Index.CompareTo(y.Index));

            spine.FrameAtArc(0f, out var c0, out var t0, out _, out _);
            var gates = new List<SkeinGate> { new SkeinGate(c0, t0, s.CollarMouth, -1) };
            var placed = new List<Vector3> { c0 };
            var used = new HashSet<int>();

            int cursor = 0;
            float cursorArc = 0f;
            float sep2 = s.GateSeparation * s.GateSeparation;

            for (int n = 0; n < s.GateCount - 2; n++)
            {
                var chain = byStrand[cursor];
                if (chain.Count == 0) break;

                // Candidates in ride order from the cursor, wrapping once, preferring breaks this
                // course has not used. Revisiting a BREAK is fine - what must never repeat is a
                // gate POSITION, which Clears guarantees; refusing outright starves the walk
                // whenever the cursor lands on an outer strand, which carries about a third as
                // many aimed breaks as an inner one.
                var order = new List<Break>(chain.Count * 2);
                for (int pass = 0; pass < 2; pass++)
                    foreach (var b in chain)
                    {
                        bool ahead = strands[cursor].Arc[b.Index] > cursorArc;
                        bool fresh = !used.Contains(b.Strand * 100003 + b.Index);
                        if (pass == 0 && !fresh) continue;
                        if (ahead) order.Insert(0, b); else order.Add(b);
                    }

                bool placedOne = false;
                foreach (var b in order)
                {
                    for (int li = 0; li < 26 && !placedOne; li++)
                    {
                        float arc = b.LandingArc + 150f + 70f * li;
                        var tgt = strands[b.Target];
                        Vector3 pos = tgt.Point(spine, arc % spine.L);
                        if (!Clears(placed, pos, sep2)) continue;

                        // The axis is the TARGET RAIL'S tangent, so a correct rider threads the
                        // ring without steering - which is what lets the mouth be small enough to
                        // be exclusive to one lane.
                        Vector3 axis = (tgt.Point(spine, (arc + 4f) % spine.L)
                                      - tgt.Point(spine, (arc - 4f) % spine.L)).normalized;
                        gates.Add(new SkeinGate(pos, axis, s.GateMouth, b.Target));
                        placed.Add(pos);
                        used.Add(b.Strand * 100003 + b.Index);
                        cursor = b.Target; cursorArc = arc;
                        placedOne = true;
                    }
                    if (placedOne) break;
                }
                if (!placedOne) break;
            }

            gates.Add(new SkeinGate(c0, t0, s.CollarMouth, -1));   // the finish collar
            return gates;
        }

        /// <summary>
        /// Build a cable. Returns false rather than a partial result when the walk cannot lay the
        /// full ring course - a target naming a ring that does not exist is a match that cannot
        /// end, so the caller backs off (halving the ask) exactly as SwitchbackController does.
        ///
        /// <para><b>Returning false is a designed path, not an error, and the caller must handle
        /// it by RE-ROLLING the seed.</b> Measured over 60 seeds at each intensity: 231/240 lay a
        /// full course first try (I1 55/60, I2 59/60, I3 57/60, I4 60/60), and every one of the 9
        /// that did not recovered inside 4 re-rolls. The alternative - shipping a short course -
        /// is a target naming a ring that does not exist, i.e. a match that cannot end, which is
        /// the failure SwitchbackController's halving back-off exists to prevent.</para>
        ///
        /// <para>Origin-relative: the caller adds the cell centre once, on the server, before
        /// broadcasting. The GEOMETRY travels, never the seed - the trim's accept/reject decisions
        /// and the gate walk are BRANCHES, so one flipped <c>Acos</c> bit between Mono and IL2CPP
        /// yields a completely different cable rather than a slightly different one.</para>
        /// </summary>
        public static bool TryGenerate(int seed, in SkeinCourseSettings s,
                                       out List<SkeinRail> rails, out List<SkeinGate> gates,
                                       out int prismCount)
        {
            var b = BuildAll(seed, s);
            if (b == null) { rails = null; gates = null; prismCount = 0; return false; }
            rails = b.Rails; gates = b.Gates; prismCount = b.PrismCount;
            return true;
        }

        /// <summary>
        /// Cut every strand at its jittered stations and TRIM each cut to an aimed break.
        ///
        /// <para>A CUT THAT CANNOT BE AIMED IS NOT A BREAK: the strand runs on to its next station
        /// and the segment is simply longer. That is what makes "every break in this arena is
        /// aimed" a property of the construction rather than something the generator hopes for -
        /// an unaimed break is a rail that ends pointing at nothing, which is the one thing this
        /// mode must never contain.</para>
        /// </summary>
        static void CutAndTrim(Spine spine, Strand[] strands, in SkeinCourseSettings s,
                               List<Break> breaks, List<SkeinRail> laid)
        {
            for (int si = 0; si < strands.Length; si++)
            {
                var self = strands[si];
                int prev = 0;
                foreach (float arc in self.Cuts)
                {
                    int raw = 0; float bestD = float.MaxValue;
                    for (int i = 0; i < self.N; i++)
                    {
                        float d = Mathf.Abs(self.Arc[i] - arc);
                        if (d < bestD) { bestD = d; raw = i; }
                    }
                    if (raw < s.MinSegmentPrisms || raw >= self.N - 1) continue;
                    if (!TryTrim(strands, si, raw, s, out var b)) continue;

                    breaks.Add(b);
                    if (b.Index - prev >= s.MinSegmentPrisms)
                        laid.Add(new SkeinRail(si, prev, b.Index, Data.Domains.Jade, true));
                    prev = b.Index + Mathf.RoundToInt(s.BreakGap / s.PrismSpacing);
                }
                if (self.N - 1 - prev >= s.MinSegmentPrisms)
                    laid.Add(new SkeinRail(si, prev, self.N - 1, Data.Domains.Jade, false));
            }
        }

        /// <summary>
        /// Everything an arena needs to LAY this cable: the rails, the rings, and one pose per
        /// prism, built in a single pass.
        ///
        /// <para>Batch on purpose. A per-prism accessor would rebuild the spine and every strand
        /// on each call, which is fine for one lookup and fatal for the ~10,000 a lay needs; this
        /// walks the strands once and hands back a flat array per rail.</para>
        /// </summary>
        public sealed class SkeinBuild
        {
            public List<SkeinRail> Rails;
            public List<SkeinGate> Gates;
            /// <summary>Parallel to <see cref="Rails"/>: the prism poses of each rail, in INDEX
            /// ORDER ALONG THE RACE DIRECTION. That ordering is load-bearing rather than tidy -
            /// TrailFollower.Attach seeds its direction from dot(Course, HeadingAt), so if a rail
            /// is laid against the flow then "Backward" has no relation to "the wrong way" and the
            /// whole arrival-angle analysis is undefined.</summary>
            public List<Vector3[]> Positions;
            public List<Quaternion[]> Rotations;
            public int PrismCount;
        }

        /// <summary>Build the cable and every prism pose in it. Null when the walk could not lay a
        /// full ring course - the caller re-rolls the seed (see <see cref="TryGenerate"/>).</summary>
        public static SkeinBuild BuildAll(int seed, in SkeinCourseSettings s)
        {
            if (s.StrandCount < 2 || s.GateCount < 3) return null;

            var spine = new Spine(s.MajorRadius, s.MinorRadius, SpineSamples);
            SolveTwists(spine.L, s.InnerRadius, s.OuterRadius, out int wIn, out int wOut);
            var strands = BuildStrands(spine, seed, s, wIn, wOut);

            var breaks = new List<Break>(256);
            var laid = new List<SkeinRail>(256);
            CutAndTrim(spine, strands, s, breaks, laid);
            if (laid.Count == 0) return null;

            var colours = new Data.Domains[laid.Count];
            for (int i = 0; i < laid.Count; i++)
                colours[i] = Triad[((laid[i].Strand + i) % 3 + 3) % 3];
            Rebalance(laid, colours);

            var b = new SkeinBuild
            {
                Rails = new List<SkeinRail>(laid.Count),
                Positions = new List<Vector3[]>(laid.Count),
                Rotations = new List<Quaternion[]>(laid.Count),
            };

            for (int i = 0; i < laid.Count; i++)
            {
                var r = laid[i];
                b.Rails.Add(new SkeinRail(r.Strand, r.Start, r.End, colours[i], r.Launches));

                var st = strands[r.Strand];
                int n = r.End - r.Start + 1;
                var pos = new Vector3[n];
                var rot = new Quaternion[n];
                for (int k = 0; k < n; k++)
                {
                    int node = r.Start + k;
                    pos[k] = st.Pts[node];
                    Vector3 along = (st.Pts[Mathf.Min(node + 1, st.N - 1)]
                                   - st.Pts[Mathf.Max(node - 1, 0)]).normalized;
                    spine.FrameAtArc(st.Arc[node], out var c, out _, out _, out _);
                    Vector3 outward = (pos[k] - c).normalized;
                    if (outward.sqrMagnitude < 1e-6f || Mathf.Abs(Vector3.Dot(outward, along)) > 0.99f)
                        outward = Vector3.up;
                    // Local +Z ALONG the rail, +Y radially out of the spine: the pose the 1D ride
                    // expects, and the pose the Track Projector lays, so a rail a pilot projects
                    // reads as arena rail.
                    rot[k] = Quaternion.LookRotation(along, outward);
                }
                b.Positions.Add(pos);
                b.Rotations.Add(rot);
                b.PrismCount += n;
            }

            b.Gates = WalkGates(spine, strands, breaks, s);
            if (b.Gates.Count != s.GateCount) return null;
            return b;
        }

    }
}
