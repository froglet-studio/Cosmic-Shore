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
        // ── the breathing cable ──────────────────────────────────────────────
        public float MidRadius;        // A_MID
        public float SwingRadius;      // A_SWING
        public int RadialCycles;       // integer, or the strand does not close on the knot
        public int StrandCount;        // N, the intensity dial
        // ── segmentation ─────────────────────────────────────────────────────
        public float SegmentRun;       // the RIDEABLE arc between a strand's holes
        public float MinSegmentSpine;
        public float BreakGap;
        public int GateLaps;           // laps of the cable that make one race

        /// <summary>The break-to-break PERIOD, derived. With a 600 u gap the run and the period
        /// stopped being interchangeable: authoring the period left the holes longer than the
        /// segments and a strand became mostly missing.</summary>
        public float SegmentSpine => SegmentRun + BreakGap;
        public float PrismSpacing;
        // ── the trim's acceptance conditions ─────────────────────────────────
        public float EndAimRadius, EndAimMin, EndAimMax, RayClearance, ArrivalAngleMax;
        public int MinSegmentPrisms;
        // ── gates ────────────────────────────────────────────────────────────
        public int GateCount;
        public float GateMouthMax, MouthSeparationFraction, CollarMouth, GateSeparation;

        public float MinRadius => MidRadius - SwingRadius;
        public float MaxRadius => MidRadius + SwingRadius;

        /// <summary>
        /// A ring's mouth, DERIVED from the cable rather than authored. See
        /// <see cref="SkeinCourse.ClosestPairSeparation"/> - a mouth wider than the distance to
        /// the neighbouring strand is a ring a pilot on the WRONG rail threads, which destroys
        /// the one-rail addressing the whole ordered-gate contract rests on. At N=9 the closest
        /// pair is 32.6 u and the authored 40 u mouth did exactly that.
        /// </summary>
        public float GateMouth => Mathf.Min(
            GateMouthMax,
            MouthSeparationFraction * SkeinCourse.ClosestPairSeparation(StrandCount, MidRadius, SwingRadius));

        /// <summary>
        /// INTENSITY IS THE RAIL COUNT, and nothing else moves.
        ///
        /// <para>The knot, the radius band, the lay, the gate count, the segment length, the
        /// prism, the spacing and the spawn ring are identical at all four levels, so the
        /// arena's silhouette, its hollow core, its launch geometry and its fairness argument
        /// never move. What climbs is how many lanes there are to read - and, derived from
        /// that, how tight the strands run and therefore how small the rings get.</para>
        ///
        /// <para>Both ends are derived rather than chosen. <b>N_min = 5</b>: below five strands
        /// the phase spread is too coarse to cover the radius band at every station, so the
        /// cable has radial holes. <b>N_max = 9</b>: the closest strand pair is 32.6 u there,
        /// which must clear both the 24 u ride-envelope floor and twice the 9 u MASS-5 shield
        /// reach; a tenth strand takes it under the armour bound and two lanes fuse.</para>
        /// </summary>
        public static SkeinCourseSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new SkeinCourseSettings
            {
                MajorRadius = 560f,
                MinorRadius = 200f,
                // a_k(s) = 90 + 45 sin(2pi*3*s/L + phi_k) - band [45, 135].
                // RadialCycles MUST be an integer or the strand does not close at s = L.
                MidRadius = 90f,
                SwingRadius = 45f,
                RadialCycles = 3,
                StrandCount = new[] { 5, 6, 7, 9 }[i - 1],

                // SEGMENT_RUN_SECONDS * grindSpeed = 2.5 * 300.
                SegmentRun = 750f,
                // MIN_SEGMENT_SECONDS * grindSpeed = 2.0 * 300. A segment shorter than this is
                // not a rail, it is a bump.
                MinSegmentSpine = 600f,
                // BREAK_GAP_SECONDS * grindSpeed = 3.0 * 300, i.e. 112 missing prisms. At 40 u
                // a pilot sailed over the hole and re-attached to the SAME strand, so a break
                // was a cosmetic stutter; at 900 u the strand has curved clear of its own
                // tangent long before it resumes, so the only thing on the far side of a break
                // is a DIFFERENT curve. Proven, not assumed, by skein_budget.py's
                // prove_no_self_bridge - which is also what SET this number: a swept search
                // found that widening the gap does NOT monotonically improve the self-bridge
                // clearance, so it is the measured best rather than the story's prediction.
                BreakGap = 900f,
                // Laps of the SPINE the 24 rings are spread over - not laps a pilot flies, which
                // is 1. Six rather than three because the vessel got twice as fast: a ring's
                // spacing has to cover one rideable run plus the longest launch, and both of
                // those are times.
                GateLaps = 6,
                PrismSpacing = 8f,

                EndAimRadius = 12f,
                // LAUNCH_DECISION_SECONDS * LAUNCH speed = 1.4 * (300 * 1.2). The LAUNCH speed,
                // not the grind: GunVesselTransformer.endLaunchSpeedKick throws a pilot off the
                // end of a ribbon at 1.2x what they were riding, so the same thinking time
                // costs more distance. The old 60 u floor was 0.40 s of free flight and the
                // trim ALSO preferred the nearest qualifying landing, so a launch read as
                // shooting straight into the next segment.
                EndAimMin = 504f,
                // LAUNCH_MAX_SECONDS * LAUNCH speed = 2.5 * 360, and the one window edge with a
                // MEASURED ceiling: past ~936 u a launch grazes the strand it left (15.7 u
                // against the 24 u floor at 1008) and the pilot can bridge the hole instead of
                // changing strands, which is the whole mechanic.
                EndAimMax = 900f,
                RayClearance = 24f,
                // 60 leaves 30 degrees of margin under the 90 at which TrailFollower.Attach
                // seeds Backward and carries the pilot back up the course at 300 u/s.
                ArrivalAngleMax = 60f,
                MinSegmentPrisms = 40,

                GateCount = 24,
                GateMouthMax = 40f,
                MouthSeparationFraction = 0.85f,
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
        const int GateNudges = 24;         // arc nudges before a ring gives up on clearing
        const float GateNudgeStep = 35f;   // u of spine per nudge - small against the march

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

        /// <summary>
        /// Where the course STARTS and FINISHES - the spine collar at arc 0, in cell-local
        /// coordinates, with the direction the course flows through it.
        ///
        /// <para>Answerable with no seed, no intensity and no generation, which is the property
        /// that makes it usable during the SPAWN CHAIN: the spine is a closed-form trefoil in
        /// the two authored radii alone, so <c>t = 0</c> is a constant of the mode. Only the
        /// STRAND count varies per intensity, and the collar is on the spine rather than on a
        /// strand.</para>
        ///
        /// <para>Read by <c>SkeinController</c> to line the pilots up on the first gate before
        /// the cable itself exists - see <c>IPlayerSpawnLine</c>.</para>
        /// </summary>
        public static void StartPose(in SkeinCourseSettings s, out Vector3 position, out Vector3 axis)
        {
            position = SpinePoint(0f, s.MajorRadius, s.MinorRadius);
            axis = SpineTangent(0f, s.MajorRadius, s.MinorRadius);
        }

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

        // ── the breathing cable ──────────────────────────────────────────────

        /// <summary>
        /// The minimum separation between any two strands of an n-strand cable, in the spine's
        /// normal plane. THE THEOREM THE WHOLE CABLE RESTS ON - see skein_budget.py's
        /// closest_pair_separation, of which this is the transcription.
        ///
        /// <para>Strand k sits at angle <c>phi_k + s/lam</c> and radius
        /// <c>M + S*sin(2pi*m*s/L + phi_k)</c> - the SAME phi in both. So for any pair the
        /// shared twist <c>s/lam</c> cancels and the angular separation <c>D = phi_k - phi_j</c>
        /// is CONSTANT in s, while the radial phase separation is that same D - which means the
        /// pair of radii traces one ellipse rather than roaming the whole box. Distance is then
        /// the law of cosines in ONE variable:</para>
        ///
        /// <code>d(psi)^2 = a_j^2 + a_k^2 - 2 a_j a_k cos D</code>
        ///
        /// <para>Nothing about the spine enters it, which is why the bound holds at every
        /// station of every seed. Give the radius an independent phase and the theorem is gone.</para>
        /// </summary>
        public static float ClosestPairSeparation(int strandCount, float mid, float swing,
                                                  int samples = 512)
        {
            float best = float.MaxValue;
            for (int k = 1; k < strandCount; k++)          // pair (0, k) covers every distinct D
            {
                float d = 2f * Mathf.PI * k / strandCount;
                float cosD = Mathf.Cos(d);
                for (int i = 0; i < samples; i++)
                {
                    float psi = 2f * Mathf.PI * i / samples;
                    float aj = mid + swing * Mathf.Sin(psi);
                    float ak = mid + swing * Mathf.Sin(psi + d);
                    float d2 = aj * aj + ak * ak - 2f * aj * ak * cosD;
                    if (d2 < best) best = d2;
                }
            }
            return Mathf.Sqrt(Mathf.Max(0f, best));
        }

        /// <summary>One rail's centreline, sampled at a fixed pitch of ITS OWN arc length.</summary>
        sealed class Strand
        {
            public readonly int Index;
            public readonly float Lam, Phi;
            public readonly float[] Cuts;
            public readonly List<Vector3> Pts = new List<Vector3>();
            public readonly List<float> Arc = new List<float>();
            readonly float _mid, _swing, _L;
            readonly int _cycles;

            public int N => Pts.Count;

            public Strand(Spine spine, int index, float lam, float phi, float[] cuts,
                          float mid, float swing, int cycles, float spacing)
            {
                Index = index; Lam = lam; Phi = phi; Cuts = cuts;
                _mid = mid; _swing = swing; _cycles = cycles; _L = spine.L;

                // Sample at `spacing` of the strand's TRUE arc length, by walking the spine finely
                // and emitting a node every `spacing` of accumulated chord.
                //
                // The obvious sampler - step the spine by spacing / f, with f the constant-radius
                // arclength factor - is wrong wherever the RADIUS is moving, and on a breathing
                // strand the radius is moving EVERYWHERE (it was only the flare before). The
                // error would surface as a per-prism turn outside the pilot's budget, i.e. as a
                // rideability failure rather than as the sampling bug it is.
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
            /// The breathing radius. ONE closed-form sinusoid - no flare, no special case at a
            /// break.
            ///
            /// <para>The strand's radial phase IS its angular phase, and that identity is the
            /// whole reason the cable is provably clear of itself (see
            /// <see cref="ClosestPairSeparation"/>): it makes the angular separation between any
            /// two strands constant, so however the radii breathe they cannot approach beyond the
            /// closed-form bound. Give the radius its own phase and that theorem is gone.</para>
            ///
            /// <para>This is also the mode's mechanic rather than decoration: each strand spends
            /// part of the lap as the direct inner path and part spiralling out, so riding an
            /// outward-bound strand carries you out and an inward-bound one carries you in. The
            /// inward phase is 1.315x shorter than the outward one, which is the reason to change
            /// strands - and the launch gap is sized to give you time to.</para>
            /// </summary>
            public float RadiusAt(float s)
                => _mid + _swing * Mathf.Sin(2f * Mathf.PI * _cycles * s / _L + Phi);

            /// <summary>The strand's own direction of travel at a spine arc - a central
            /// difference, matching skein_budget.py's tangent(). A ring's axis is the direction
            /// the course FLOWS through its mouth, which on a breathing strand is not the spine's
            /// tangent: it carries the radial rate too.</summary>
            public Vector3 Tangent(Spine spine, float s, float h = 0.5f)
                => (Point(spine, s + h) - Point(spine, s - h)).normalized;

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
        /// ONE twist count for the whole cable - the two shells are gone, so there is one family.
        ///
        /// <para>Both bounds are evaluated at the OUTWARD extreme, which is where each binds:
        /// helix angle and arc factor both grow with radius, so a strand legal at its outward
        /// extreme is legal for the whole of its breath.</para>
        ///
        /// <para><c>psi &lt;= 45 deg</c> - past 45 a strand travels further AROUND the spine than
        /// ALONG it, and "spiralling out" stops reading as progress. This is the bound that
        /// BINDS. <c>f &lt;= 2.0</c> - an outward lane must stay at least 1.4x faster than FLYING
        /// the gate polyline, which at the shipped speeds would allow 3.30; very slack at the
        /// shipped w, and slacker since the grind doubled while the cruise rose only 30%.</para>
        ///
        /// <para>The readable speed advantage the old <c>psi_in &lt;= 25</c> bought BETWEEN the
        /// two shells is now bought WITHIN one strand for free: the inward phase is 1.315x
        /// shorter than the outward one.</para>
        /// </summary>
        static int SolveTwist(float L, float aMax)
        {
            int w = 1;
            while (HelixAngleDeg(aMax, LamForTurns(L, w + 1)) <= 45f
                   && ArclengthFactor(aMax, LamForTurns(L, w + 1)) <= 2f
                   && w < 512) w++;
            return w;
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
                if (arc >= spine.L - s.MinSegmentSpine) break;
                // Two breaks must not crowd, at ANY seed - the jitter is clamped by the
                // previous accepted cut rather than trusted to stay clear of it. (This used to
                // be the no-overlapping-flares rule; with the flare gone the constraint is
                // simply that a segment stays long enough to be worth riding.)
                if (arc > s.MinSegmentSpine &&
                    (outArcs.Count == 0 || arc - outArcs[outArcs.Count - 1] >= s.MinSegmentSpine))
                    outArcs.Add(arc);
            }
            return outArcs.ToArray();
        }

        /// <summary>
        /// ONE family of N strands, evenly phase-spread. phi_k is BOTH the angular phase and the
        /// radial phase - see <see cref="Strand.RadiusAt"/> for why that identity is load-bearing.
        ///
        /// <para>The even spread is what delivers full radial coverage: at any spine station the
        /// N radii are <c>M + S*sin(x + 2pi*k/N)</c>, which samples the whole band however x
        /// moves. The jitter is bounded so the spread stays near-even - a strand parked next to
        /// its neighbour would leave a radial hole on the far side of the ring.</para>
        /// </summary>
        static Strand[] BuildStrands(Spine spine, int seed, in SkeinCourseSettings s, int w)
        {
            float lam = LamForTurns(spine.L, w);
            var rng = new Rng(unchecked((int)((uint)seed * 747796405u + 2891336453u)));
            var strands = new Strand[s.StrandCount];
            for (int k = 0; k < s.StrandCount; k++)
            {
                float phi = 2f * Mathf.PI * (k + rng.Range(-PhaseJitter, PhaseJitter)) / s.StrandCount;
                strands[k] = new Strand(spine, k, lam, phi,
                                        CutArcs(spine, k, s.StrandCount, seed, s),
                                        s.MidRadius, s.SwingRadius, s.RadialCycles, s.PrismSpacing);
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
                float bestMiss = 0f, bestRange = -1f, bestArrival = 0f;
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
                    // FURTHEST, not nearest. EndAimMin guarantees a decision window EXISTS;
                    // preferring the nearest qualifying landing then spent it immediately, which
                    // is what made a launch read as shooting straight into the next segment.
                    // Taking the furthest inside the window uses the glide the vessel already
                    // carries (833 u above cruise) and gives the longest look at the cable.
                    if (rng > bestRange)
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
        /// THE RINGS MARCH DOWN THE COURSE; THE RANDOM PART IS WHICH STRAND EACH ONE SITS ON.
        ///
        /// <para>This replaces a walk that chased BREAKS - it hopped from one aimed transfer to
        /// the next, preferring those ahead of a cursor but falling back to those behind it,
        /// which meant the ring order could run BACKWARD along the spine. A race whose next
        /// objective is behind you is not a course, and the fallback existed only because the
        /// walk could otherwise starve.</para>
        ///
        /// <para>The march cannot starve, because it asks nothing of the breaks. Ring k sits at
        /// spine arc <c>k * SPACING</c>, so ring k+1 is always further down the cable than ring
        /// k and the sequence IS the course the bundle follows. SPACING is
        /// <c>GateLaps * L / (mid + 1)</c>, which closes exactly on the finish collar, so the
        /// race is a whole number of laps and the two collars share a point - safe by
        /// construction, since ordered gates make the finish uncrossable until its turn. Which
        /// STRAND carries ring k is a seeded draw that is never the previous ring's strand, so
        /// every ring is a strand change and there is a reason to be on all of them.</para>
        ///
        /// <para>The spacing is what buys the time to make that change: one rideable run plus the
        /// longest launch fits inside it (skein_budget.py's prove_gate_spacing asserts it), so a
        /// pilot always meets at least one aimed break between one ring and the next. Note the
        /// bound is the RUN and not the period - the break gap is never ridden across, it is the
        /// reason the pilot is flying.</para>
        /// </summary>
        static List<SkeinGate> WalkGates(Spine spine, Strand[] strands, List<Break> breaks,
                                         int seed, in SkeinCourseSettings s)
        {
            int mid = Mathf.Max(1, s.GateCount - 2);
            float spacing = Mathf.Max(1, s.GateLaps) * spine.L / (mid + 1);
            float mouth = s.GateMouth;
            var rng = new Rng(unchecked((int)((uint)seed * 2246822519u + 374761393u)));

            spine.FrameAtArc(0f, out var c0, out var t0, out _, out _);
            var gates = new List<SkeinGate> { new SkeinGate(c0, t0, s.CollarMouth, -1) };

            var placed = new List<Vector3> { c0 };
            float sep2 = s.GateSeparation * s.GateSeparation;
            int prev = -1;
            var order = new List<int>(strands.Length);

            for (int k = 1; k <= mid; k++)
            {
                float arc = (k * spacing) % spine.L;

                // A strand at random, never the one the previous ring was on - a ring you can
                // reach by holding the throttle is a ring that asks nothing.
                //
                // SEPARATION IS ENFORCED HERE, on the STRAND, because the strand is the free
                // variable. Even spacing along the SPINE is not even spacing in SPACE: the
                // trefoil passes close to itself, so two rings a lap apart in arc can be a few
                // hundred units apart in the world and one pass could thread both (measured:
                // 179.9 u against a 200 u floor, caught by the four-seed sweep). The draw simply
                // keeps drawing; only if no strand clears does the arc nudge, which is the last
                // resort because moving the arc is what breaks the even march.
                order.Clear();
                for (int i2 = 0; i2 < strands.Length; i2++) if (i2 != prev) order.Add(i2);
                int start = Mathf.Clamp((int)(rng.Unit() * order.Count), 0, order.Count - 1);

                int si = -1;
                Vector3 pos = default;
                for (int nudge = 0; nudge < GateNudges && si < 0; nudge++)
                {
                    float a = (arc + nudge * GateNudgeStep) % spine.L;
                    for (int t = 0; t < order.Count; t++)
                    {
                        int cand = order[(start + t) % order.Count];
                        Vector3 p = strands[cand].Point(spine, a);
                        bool clear = true;
                        for (int q = 0; q < placed.Count && clear; q++)
                            if ((p - placed[q]).sqrMagnitude < sep2) clear = false;
                        if (!clear) continue;
                        si = cand; pos = p; arc = a;
                        break;
                    }
                }
                if (si < 0)                       // nothing clears: take the draw
                {
                    si = order[start];
                    pos = strands[si].Point(spine, arc);
                }

                gates.Add(new SkeinGate(pos, strands[si].Tangent(spine, arc), mouth, si));
                placed.Add(pos);
                prev = si;
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
            int w = SolveTwist(spine.L, s.MaxRadius);
            var strands = BuildStrands(spine, seed, s, w);

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

            b.Gates = WalkGates(spine, strands, breaks, seed, s);
            if (b.Gates.Count != s.GateCount) return null;
            return b;
        }

    }
}
