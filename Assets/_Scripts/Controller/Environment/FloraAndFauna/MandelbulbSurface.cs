using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Mandelbulb flora's growth rule, in full, with no Unity wiring — so the
    /// offline verifier can COMPILE AND RUN it rather than read it
    /// (<c>Tools/Build/mandelbulb_surface_harness/</c>). <see cref="MandelbulbFlora"/>
    /// only consumes what this produces.
    ///
    /// ── The surface ──────────────────────────────────────────────────────────────
    /// The fractal is never evaluated at runtime. Offline, the Mandelbulb's outer
    /// surface is ray-marched into a spherical height field R(θ,φ) and fitted to
    /// SPHERICAL HARMONICS; the shipped table is those coefficients. Three facts make
    /// that the right representation and all three are measurements, not taste:
    ///
    ///  • The distance estimator's GRADIENT is unusable as an orientation at this
    ///    scale — measured 48° of swing between surface points 0.013 apart (bulb
    ///    radius ~1), so curves traced against it die instantly on any turn gate.
    ///    A height field's normal is analytic and smooth, and its smoothing σ is an
    ///    explicit dial: the same walk turns 7.5°/step at σ=0.5 and 0.7° at σ=6.
    ///  • The FIT does not converge and does not need to. R has cliffs where the march
    ///    jumps sheets, so SH power at degree 32 is comparable to degree 8 — the bulb
    ///    is not reproducible at any practical degree. It does not have to be: the
    ///    plant needs a surface with the bulb's CHARACTER, and degree 12 (169 floats)
    ///    grows one that is visually of a piece with the true field's.
    ///  • The surface's response to the Julia constant is LINEAR over a useful basin,
    ///    so three modes explain 100.0% of the variance across 56 sampled fields with
    ///    the fourth eigenvalue 278x smaller. Those three modes ARE ∂R/∂c, which is why
    ///    four ray-marches recover the same 3-space that fifty-six do (99.2–100.0%
    ///    overlap). A whole plant is therefore the shared basis plus THREE FLOATS.
    ///
    /// ── The prisms ───────────────────────────────────────────────────────────────
    /// Curves are traced ON that surface — dense along the curve, sparse across it —
    /// and a prism is one step of one curve. Curves that cannot be followed cleanly
    /// (too much turn, out of the radius band, over the roughness limit) are ABANDONED
    /// rather than plated, which is what leaves the dust and the singularities
    /// unsampled. Nothing closes the surface into a skin: `Docs/ECOSYSTEM.md §44.2`
    /// records the four closed-surface candidates that were built and rejected,
    /// because a closed crust of a solid form reads as that solid at any resolution.
    ///
    /// ── The address ──────────────────────────────────────────────────────────────
    /// A prism is stamped ONCE with a <see cref="PrismAddress"/>: where it sits on the
    /// SPHERE, its heading in that point's own tangent basis, its lift, and its size.
    /// Pose is then a pure function of (address, surface), so moving the surface moves
    /// every prism with no per-prism state — which is what makes the optional morph
    /// possible. Addressing by EMISSION INDEX instead does not work and was measured:
    /// nudging c by 0.0002 moves the median prism 23% of the bulb and 97% of them
    /// further than their own body length, because tracing is sequential and every
    /// discrete decision in it reshuffles. The same nudge moves a (θ,φ) address by
    /// 0.00002.
    ///
    /// LENGTH IS AUTHORED, never derived from the address pair. Deriving it keeps the
    /// chain welded but lets prisms stretch 20x under a morph, and a prism whose length
    /// is a function of the morph has a VOLUME that is too — which lands straight on
    /// the cell's phase ladder. Authored length holds the plant's volume exactly
    /// constant under any morph, at the cost of ~4 percentage points of extra gap
    /// between neighbours across the safe basin.
    /// </summary>
    public static class MandelbulbSurface
    {
        // ── spherical harmonics ────────────────────────────────────────────────────

        /// <summary>Orthonormal real-SH normalisation K(l,m), flattened l*(L+1)+m.</summary>
        public static float[] NormTable(int degree)
        {
            var k = new float[(degree + 1) * (degree + 1)];
            for (int l = 0; l <= degree; l++)
            {
                double r = 1.0;
                for (int m = 0; m <= l; m++)
                {
                    if (m > 0) r /= (double)(l + m) * (l - m + 1);
                    k[l * (degree + 1) + m] = (float)Math.Sqrt((2 * l + 1) / (4.0 * Math.PI) * r);
                }
            }
            return k;
        }

        /// <summary>Associated Legendre P(l,m)(x) for all l &lt;= degree, flattened l*(L+1)+m.</summary>
        public static void LegendreTable(int degree, double x, double[] outP)
        {
            int stride = degree + 1;
            double s = Math.Sqrt(Math.Max(0.0, 1.0 - x * x));
            outP[0] = 1.0;
            for (int m = 0; m <= degree; m++)
            {
                if (m > 0) outP[m * stride + m] = outP[(m - 1) * stride + (m - 1)] * -(2 * m - 1) * s;
                if (m < degree) outP[(m + 1) * stride + m] = x * (2 * m + 1) * outP[m * stride + m];
                for (int l = m + 2; l <= degree; l++)
                    outP[l * stride + m] =
                        ((2 * l - 1) * x * outP[(l - 1) * stride + m] - (l + m - 1) * outP[(l - 2) * stride + m]) / (l - m);
            }
        }

        /// <summary>
        /// Reconstructs coefficients onto a (w x h) equirectangular lattice. Done ONCE
        /// per (element, weights) and shared by every plant of that element — a curve
        /// step then costs bilinear samples rather than a 169-term sum.
        /// </summary>
        public static float[] Reconstruct(int degree, float[] coeffs, int w, int h)
        {
            int stride = degree + 1;
            var norm = NormTable(degree);
            var p = new double[stride * stride];
            var cm = new double[stride];
            var sm = new double[stride];
            var outR = new float[w * h];
            const double Root2 = 1.4142135623730951;
            for (int j = 0; j < h; j++)
            {
                double th = (j + 0.5) / h * Math.PI;
                LegendreTable(degree, Math.Cos(th), p);
                Array.Clear(cm, 0, cm.Length);
                Array.Clear(sm, 0, sm.Length);
                for (int l = 0; l <= degree; l++)
                    for (int m = 0; m <= l; m++)
                    {
                        double b = norm[l * stride + m] * p[l * stride + m];
                        if (m == 0) cm[0] += b * coeffs[l * (l + 1)];
                        else
                        {
                            cm[m] += Root2 * b * coeffs[l * (l + 1) + m];
                            sm[m] += Root2 * b * coeffs[l * (l + 1) - m];
                        }
                    }
                for (int i = 0; i < w; i++)
                {
                    double ph = (double)i / w * 2.0 * Math.PI;
                    double v = cm[0];
                    for (int m = 1; m <= degree; m++) v += cm[m] * Math.Cos(m * ph) + sm[m] * Math.Sin(m * ph);
                    outR[j * w + i] = (float)v;
                }
            }
            return outR;
        }

        /// <summary>
        /// mean + w·modes, in COEFFICIENT space (three floats select a plant). Linear,
        /// so composing here and reconstructing once is identical to reconstructing
        /// each mode and blending lattices, at a quarter of the work.
        /// </summary>
        public static float[] Compose(float[][] basis, float w0, float w1, float w2)
        {
            int n = basis[0].Length;
            var outC = new float[n];
            for (int i = 0; i < n; i++)
                outC[i] = basis[0][i] + w0 * basis[1][i] + w1 * basis[2][i] + w2 * basis[3][i];
            return outC;
        }

        // ── the live surface ───────────────────────────────────────────────────────

        /// <summary>One reconstructed height field, sampled bilinearly (φ wraps, θ clamps).</summary>
        public sealed class Surface
        {
            public readonly int W, H;
            public readonly float[] R;
            public readonly float MeanRadius;

            public Surface(float[] r, int w, int h)
            {
                R = r; W = w; H = h;
                double s = 0.0;
                for (int i = 0; i < r.Length; i++) s += r[i];
                MeanRadius = (float)(s / Math.Max(1, r.Length));
            }

            public float Sample(float theta, float phi)
            {
                float x = phi / (2f * Mathf.PI) * W;
                float y = theta / Mathf.PI * H - 0.5f;
                if (y < 0f) y = 0f;
                if (y > H - 1.0001f) y = H - 1.0001f;
                int i0 = Mathf.FloorToInt(x), j0 = Mathf.FloorToInt(y);
                float fx = x - i0, fy = y - j0;
                int ia = ((i0 % W) + W) % W, ib = ((i0 + 1) % W + W) % W;
                int ja = Mathf.Clamp(j0, 0, H - 1), jb = Mathf.Clamp(j0 + 1, 0, H - 1);
                float a = R[ja * W + ia] * (1f - fx) + R[ja * W + ib] * fx;
                float b = R[jb * W + ia] * (1f - fx) + R[jb * W + ib] * fx;
                return a * (1f - fy) + b * fy;
            }
        }

        /// <summary>The local frame of the surface r = R(θ,φ) at one point.</summary>
        public struct Frame
        {
            public Vector3 Position, Normal, Ascent, Dir, ETheta, EPhi;
            public float Radius, Slope, SinTheta;
        }

        public static void BuildFrame(Surface s, float theta, float phi, ref Frame f)
        {
            float hT = Mathf.PI / s.H * 0.75f;
            float hP = 2f * Mathf.PI / s.W * 0.75f;
            float st = Mathf.Sin(theta), ct = Mathf.Cos(theta);
            float cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
            float sT = Mathf.Max(st, 1e-4f);

            float r = s.Sample(theta, phi);
            float rT = (s.Sample(theta + hT, phi) - s.Sample(theta - hT, phi)) / (2f * hT);
            float rP = (s.Sample(theta, phi + hP) - s.Sample(theta, phi - hP)) / (2f * hP);

            var d = new Vector3(st * cp, st * sp, ct);
            var eT = new Vector3(ct * cp, ct * sp, -st);
            var eP = new Vector3(-sp, cp, 0f);

            Vector3 sTh = rT * d + r * eT;
            Vector3 sPh = rP * d + (r * sT) * eP;
            Vector3 n = Vector3.Cross(sTh, sPh);
            float m = n.magnitude;
            n = m > 1e-12f ? n / m : d;
            if (Vector3.Dot(n, d) < 0f) n = -n;

            Vector3 a = rT * eT + (rP / sT) * eP;
            a -= n * Vector3.Dot(a, n);
            float slope = a.magnitude;
            a = slope > 1e-9f ? a / slope : eP;

            f.Position = d * r;
            f.Normal = n;
            f.Ascent = a;
            f.Dir = d;
            f.ETheta = eT;
            f.EPhi = eP;
            f.Radius = r;
            f.Slope = slope;
            f.SinTheta = sT;
        }

        // ── the growth rule ────────────────────────────────────────────────────────

        public enum SteeringField { Contour = 0, Ascent = 1, Descent = 2, Azimuth = 3, Meridian = 4, Geodesic = 5 }

        /// <summary>Which curve family, how densely along it, how sparsely across it.</summary>
        [Serializable]
        public struct GrowthRules
        {
            public SteeringField Field;
            public float SwirlDegrees;      // rotates the field in the tangent plane
            public float FieldMix;          // field vs. straight ahead
            public float Momentum;          // tangent smoothing
            public float StepSize;          // along the curve
            public int MaxSteps;            // per curve
            public int LanesPerSeed;        // hops across
            public float LaneGap;           // across-curve pitch — with StepSize, the anisotropy
            public float HopSeek;           // search for the next crest after a hop
            public float HopJitter;
            public int SeedCount;
            public float SeedSpreadDegrees; // minimum angle between seeds
            public float MaxTurnDegrees;    // abandon above this (what becomes dust)
            public float RadiusMin, RadiusMax;
            public int MinRun;              // discard curves shorter than this
            public float LengthFactor;      // prism length as a multiple of the step
            public float GirthTaper;        // cross-section of the SHORTEST run vs the longest
            public float TwistDegreesPerStep; // HELICOIDAL roll about the curve's own tangent,
                                              // accumulated step by step along a run. 0 = a flat
                                              // ribbon; the dial that separates the two species
                                              // built on this rule (Docs/ECOSYSTEM.md §46).
        }

        /// <summary>
        /// One prism, addressed on the sphere. Pose is a pure function of (this,
        /// surface); nothing here is a world position, which is what lets the surface
        /// move underneath a grown plant.
        /// </summary>
        public readonly struct PrismAddress
        {
            public readonly float Theta, Phi;
            public readonly float RadialOffset;   // lift off the surface, ALONG THE RAY
            public readonly float TanA, TanB;     // heading in this point's own (eθ, eφ)
            public readonly float Length;         // AUTHORED — never derived from the surface
            public readonly float Girth;          // cross-section multiplier — the plant's
                                                  // texture scales, one per lane generation
            public readonly float Roll;           // HELICOIDAL twist about the curve's own
                                                  // tangent, in RADIANS, accumulated from the
                                                  // start of this run. Stored rather than
                                                  // recomputed because the address is the whole
                                                  // of a prism's identity — a pose that had to
                                                  // ask "how far along its curve am I?" would
                                                  // need the curve to still exist.
            public readonly int Curve, Lane;

            public PrismAddress(float theta, float phi, float radialOffset,
                                float tanA, float tanB, float length, float girth, float roll,
                                int curve, int lane)
            {
                Theta = theta; Phi = phi; RadialOffset = radialOffset;
                TanA = tanA; TanB = tanB; Length = length; Girth = girth; Roll = roll;
                Curve = curve; Lane = lane;
            }
        }

        /// <summary>
        /// Resolves an address against a surface. The lift is RADIAL because the laid
        /// prism and its surface point sit on the same ray by construction — storing it
        /// along the NORMAL leaves the tangential difference behind and was measured at
        /// 2.4 prism lengths on the worst prism of one preset.
        /// </summary>
        public static void Pose(Surface s, in PrismAddress a, ref Frame scratch,
                                out Vector3 position, out Vector3 forward, out Vector3 up)
        {
            BuildFrame(s, a.Theta, a.Phi, ref scratch);
            Vector3 n = scratch.Normal;
            Vector3 f = a.TanA * scratch.ETheta + a.TanB * scratch.EPhi;
            f -= n * Vector3.Dot(f, n);
            float m = f.magnitude;
            forward = m > 1e-7f ? f / m : scratch.EPhi;

            // HELICOIDAL TWIST: roll the prism about its OWN tangent. Rodrigues about a unit
            // axis that the vector is already perpendicular to reduces to one cos/sin blend
            // with the binormal, so this costs no cross product beyond the one it needs and
            // cannot drift off the frame. Roll 0 leaves `up` exactly on the normal, so a
            // species that authors no twist is bit-identical to before this existed.
            if (a.Roll != 0f)
            {
                Vector3 binormal = Vector3.Cross(forward, n);
                float cosRoll = Mathf.Cos(a.Roll), sinRoll = Mathf.Sin(a.Roll);
                up = n * cosRoll + binormal * sinRoll;
            }
            else
            {
                up = n;
            }

            position = scratch.Dir * (scratch.Radius + a.RadialOffset);
        }

        // ── deterministic rng (no UnityEngine.Random: a plant must grow the same on
        //    every peer, and flora are simulated per-peer) ───────────────────────────
        sealed class Rng
        {
            uint _s;
            public Rng(int seed) { _s = (uint)seed; if (_s == 0u) _s = 1u; }
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>
        /// Lazy growth: traces one curve at a time and hands back its prisms one at a
        /// time, so a plant reaching its budget costs no frame a visible hitch — flora
        /// grow incrementally anyway.
        /// </summary>
        public sealed class Growth
        {
            readonly Surface _surface;
            readonly GrowthRules _rules;
            readonly Rng _rng;
            readonly List<Vector3> _seeds = new();
            readonly List<PrismAddress> _pending = new();
            Frame _frame;

            // Per-seed lane state. The traversal is LANE-MAJOR — every seed lays its lane 0
            // before any seed lays its lane 1 — so a plant that stops at its budget is the
            // whole bulb drawn thinly rather than two seeds' worth of it drawn fully. Seed
            // -major traversal was measured: at 6,000 prisms it spent the entire budget on
            // two seeds and grew a belt around one band of the surface.
            Vector3[] _lanePosition;
            Vector3[] _laneTangent;
            bool[] _laneTangentValid;
            bool[] _laneDead;

            int _seedIndex, _lane, _pendingIndex, _curveCount;

            public int CurvesTraced => _curveCount;
            public int SeedCount => _seeds.Count;

            public Growth(Surface surface, in GrowthRules rules, int seed)
            {
                _surface = surface;
                _rules = rules;
                _rng = new Rng(seed * 2654435761u.GetHashCode() ^ 0x5bf03635);
                BuildSeeds();
                int n = Mathf.Max(1, _seeds.Count);
                _lanePosition = new Vector3[n];
                _laneTangent = new Vector3[n];
                _laneTangentValid = new bool[n];
                _laneDead = new bool[n];
                for (int i = 0; i < _seeds.Count; i++) _lanePosition[i] = _seeds[i];
            }

            /// <summary>
            /// Seeds spread over the WHOLE sphere. The Fibonacci sequence walks z from +1
            /// to -1 monotonically, so taking a PREFIX of it — which is what an NMS that
            /// stops at `want` does — yields a polar CAP, not a spread. Measured: 78% of one
            /// plant's prisms landed in the top eighth of the sphere by area and nothing at
            /// all below the equator. The NMS therefore runs over every candidate and the
            /// result is STRIDED down to `want`, which keeps the spread global.
            /// </summary>
            void BuildSeeds()
            {
                float minCos = Mathf.Cos(Mathf.Clamp(_rules.SeedSpreadDegrees, 1f, 179f) * Mathf.Deg2Rad);
                int want = Mathf.Max(1, _rules.SeedCount);
                int probe = Mathf.Max(want * 4, 256);
                float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
                var dirs = new List<Vector3>(probe);
                var kept = new List<int>(probe);
                for (int i = 0; i < probe; i++)
                {
                    float z = 1f - (i + 0.5f) / probe * 2f;
                    float theta = Mathf.Acos(Mathf.Clamp(z, -1f, 1f));
                    float phi = (ga * i) % (2f * Mathf.PI);
                    if (phi < 0f) phi += 2f * Mathf.PI;
                    float st = Mathf.Sin(theta);
                    var d = new Vector3(st * Mathf.Cos(phi), st * Mathf.Sin(phi), Mathf.Cos(theta));
                    bool tooClose = false;
                    for (int q = 0; q < dirs.Count; q++)
                        if (Vector3.Dot(d, dirs[q]) > minCos) { tooClose = true; break; }
                    if (tooClose) continue;
                    dirs.Add(d);
                    kept.Add(i);
                }
                int take = Mathf.Min(want, dirs.Count);
                for (int k = 0; k < take; k++)
                {
                    int i = (int)((long)k * dirs.Count / take);
                    var d = dirs[i];
                    float theta = Mathf.Acos(Mathf.Clamp(d.z, -1f, 1f));
                    float phi = Mathf.Atan2(d.y, d.x);
                    if (phi < 0f) phi += 2f * Mathf.PI;
                    BuildFrame(_surface, theta, phi, ref _frame);
                    _seeds.Add(_frame.Position);
                }
            }

            /// <summary>Next prism, or false when the rule has nothing left to lay.</summary>
            public bool TryNext(out PrismAddress address)
            {
                while (_pendingIndex >= _pending.Count)
                {
                    if (!TraceNextCurve()) { address = default; return false; }
                }
                address = _pending[_pendingIndex++];
                return true;
            }

            bool TraceNextCurve()
            {
                int lanes = Mathf.Max(1, _rules.LanesPerSeed);
                int guard = 0;
                while (_lane < lanes && guard++ < 4096)
                {
                    if (_seedIndex >= _seeds.Count) { _seedIndex = 0; _lane++; continue; }
                    int k = _seedIndex++;
                    if (_laneDead[k]) continue;

                    var points = Trace(_lanePosition[k],
                                       _laneTangentValid[k] ? _laneTangent[k] : Vector3.zero,
                                       _laneTangentValid[k]);

                    if (points.Count >= Mathf.Max(2, _rules.MinRun))
                    {
                        _curveCount++;
                        Emit(points);
                        int mid = points.Count / 2;
                        int nxt = Mathf.Min(points.Count - 1, mid + 1);
                        Vector3 tv = points[nxt].Position - points[mid].Position;
                        _laneTangent[k] = tv.sqrMagnitude > 1e-18f ? tv.normalized : Vector3.right;
                        _laneTangentValid[k] = true;
                        _lanePosition[k] = Hop(points[mid].Position, points[mid].Normal, _laneTangent[k]);
                        _pendingIndex = 0;
                        return true;
                    }

                    // A curve the rule could not follow is DUST: it is not plated, and the
                    // lane still advances past it so the plant does not stall on a bad seed.
                    var p0 = _lanePosition[k];
                    var n0 = p0.sqrMagnitude > 1e-12f ? p0.normalized : Vector3.up;
                    var b0 = Vector3.Cross(n0, Vector3.forward);
                    if (b0.sqrMagnitude < 1e-12f) b0 = Vector3.Cross(n0, Vector3.right);
                    var hopped = Hop(p0, n0, b0.normalized);
                    // Two dead hops in a row from one seed means the lane has walked off the
                    // band it was seeded in; retire it rather than spending guard budget.
                    if ((hopped - p0).sqrMagnitude < 1e-10f) _laneDead[k] = true;
                    _lanePosition[k] = hopped;
                }
                return false;
            }

            struct Sample { public Vector3 Position, Normal; }

            static void Spherical(Vector3 p, out float theta, out float phi)
            {
                float r = p.magnitude;
                if (r < 1e-9f) { theta = 0f; phi = 0f; return; }
                theta = Mathf.Acos(Mathf.Clamp(p.z / r, -1f, 1f));
                phi = Mathf.Atan2(p.y, p.x);
                if (phi < 0f) phi += 2f * Mathf.PI;
            }

            readonly List<Sample> _scratch = new(512);

            List<Sample> Trace(Vector3 start, Vector3 seedTangent, bool haveSeedTangent)
            {
                _scratch.Clear();
                float maxTurnCos = Mathf.Cos(Mathf.Clamp(_rules.MaxTurnDegrees, 1f, 179f) * Mathf.Deg2Rad);
                float rMin = Mathf.Min(_rules.RadiusMin, _rules.RadiusMax);
                float rMax = Mathf.Max(_rules.RadiusMin, _rules.RadiusMax);
                Spherical(start, out float theta, out float phi);
                Vector3 t = Vector3.zero;
                bool haveT = false;

                for (int i = 0; i < Mathf.Max(2, _rules.MaxSteps); i++)
                {
                    BuildFrame(_surface, theta, phi, ref _frame);
                    if (_frame.Radius < rMin || _frame.Radius > rMax) break;

                    Vector3 n = _frame.Normal;
                    Vector3 want;
                    bool haveField = TryFieldDirection(n, out Vector3 fd);

                    if (haveT)
                    {
                        Vector3 ahead = t - n * Vector3.Dot(t, n);
                        if (ahead.sqrMagnitude < 1e-16f) break;
                        ahead = ahead.normalized;
                        if (haveField)
                        {
                            if (IsBipolar(_rules.Field) && Vector3.Dot(fd, ahead) < 0f) fd = -fd;
                            want = Vector3.Lerp(ahead, fd, Mathf.Clamp01(_rules.FieldMix));
                        }
                        else want = ahead;
                        if (want.sqrMagnitude < 1e-16f) break;
                        want = Vector3.Lerp(want.normalized, t, Mathf.Clamp01(_rules.Momentum));
                        want -= n * Vector3.Dot(want, n);
                        if (want.sqrMagnitude < 1e-16f) break;
                        want = want.normalized;
                    }
                    else if (haveSeedTangent)
                    {
                        Vector3 a = seedTangent - n * Vector3.Dot(seedTangent, n);
                        if (a.sqrMagnitude < 1e-16f) return _scratch;
                        want = a.normalized;
                    }
                    else want = haveField ? fd : _frame.EPhi;

                    if (haveT && Vector3.Dot(t, want) < maxTurnCos) break;   // -> dust

                    t = want;
                    haveT = true;
                    _scratch.Add(new Sample { Position = _frame.Position, Normal = n });

                    Vector3 q = _frame.Position + t * _rules.StepSize;
                    Spherical(q, out theta, out phi);
                }
                return _scratch;
            }

            static bool IsBipolar(SteeringField f)
                => f != SteeringField.Ascent && f != SteeringField.Descent;

            bool TryFieldDirection(Vector3 n, out Vector3 dir)
            {
                Vector3 v;
                switch (_rules.Field)
                {
                    case SteeringField.Contour: v = Vector3.Cross(n, _frame.Ascent); break;
                    case SteeringField.Ascent: v = _frame.Ascent; break;
                    case SteeringField.Descent: v = -_frame.Ascent; break;
                    case SteeringField.Azimuth: v = new Vector3(-_frame.Position.y, _frame.Position.x, 0f); break;
                    case SteeringField.Meridian: v = _frame.ETheta; break;
                    default: dir = Vector3.zero; return false;
                }
                v -= n * Vector3.Dot(v, n);
                if (v.sqrMagnitude < 1e-16f) { dir = Vector3.zero; return false; }
                v = v.normalized;
                if (Mathf.Abs(_rules.SwirlDegrees) > 0.01f)
                {
                    float ang = _rules.SwirlDegrees * Mathf.Deg2Rad;
                    float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                    v = v * c + Vector3.Cross(n, v) * s + n * (Vector3.Dot(n, v) * (1f - c));
                    v = v.normalized;
                }
                dir = v;
                return true;
            }

            Vector3 Hop(Vector3 p, Vector3 n, Vector3 t)
            {
                Vector3 b = Vector3.Cross(n, t);
                b = b.sqrMagnitude < 1e-16f ? Vector3.right : b.normalized;
                Vector3 dir = _rng.Next() < 0.5f ? -b : b;
                if (_rules.HopJitter > 0f)
                {
                    float j = _rules.HopJitter * (_rng.Next() * 2f - 1f);
                    dir = (dir + b * j);
                    dir = dir.sqrMagnitude < 1e-16f ? b : dir.normalized;
                }

                Vector3 q = p + dir * _rules.LaneGap;
                Spherical(q, out float th, out float ph);
                BuildFrame(_surface, th, ph, ref _frame);
                Vector3 best = _frame.Position;

                if (_rules.HopSeek > 0f)
                {
                    float bestR = _frame.Radius;
                    float span = _rules.LaneGap * _rules.HopSeek;
                    for (int i = 1; i <= 4; i++)
                        for (int sgn = -1; sgn <= 1; sgn += 2)
                        {
                            Vector3 c = best + dir * (span * i / 4f * sgn);
                            Spherical(c, out float ct, out float cp2);
                            BuildFrame(_surface, ct, cp2, ref _frame);
                            if (_frame.Radius > bestR) { bestR = _frame.Radius; best = _frame.Position; }
                        }
                }
                return best;
            }

            void Emit(List<Sample> points)
            {
                _pending.Clear();
                _pendingIndex = 0;
                // A curve's cross-section is a function of HOW FAR IT GOT: a long clean run is
                // structure and a short scrap is detail, so one plant carries several scales of
                // texture and the finest of them sit where the surface is hardest to follow.
                //
                // It keys on the RUN rather than on the lane counter, and that is a correction
                // rather than a preference: a lane-indexed taper is spread over LanesPerSeed
                // generations, a plant stops at its live-prism budget long before it reaches the
                // last of them, and the measured prism-volume span was 1.4x — i.e. the dial was
                // very nearly inert on every plant that actually grows.
                // The reference run is HALF the step ceiling, not the whole of it: a curve that
                // hits MaxSteps is one the surface let run forever, and keying full girth on
                // that leaves every ordinary run near the taper floor — measured, it cost 4x of
                // a plant's volume and left the span at 1.4x, which is the dial being inert in
                // the other direction.
                float taper = _rules.GirthTaper <= 0f ? 1f : _rules.GirthTaper;
                float reference = Mathf.Max(2f, _rules.MaxSteps * 0.5f);
                float u = Mathf.Clamp01((points.Count - 1) / reference);
                float girth = taper + (1f - taper) * u;
                // The twist accumulates along the RUN, so a long clean curve reads as a helix
                // and a short scrap as a single tilted plate. Converted here, once per curve,
                // rather than per prism.
                float twistPerStep = _rules.TwistDegreesPerStep * Mathf.Deg2Rad;
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    Vector3 a = points[i].Position, b = points[i + 1].Position;
                    Vector3 delta = b - a;
                    float len = delta.magnitude;
                    if (len < 1e-7f) continue;
                    Vector3 fwd = delta / len;
                    Vector3 centre = (a + b) * 0.5f;

                    Spherical(centre, out float th, out float ph);
                    BuildFrame(_surface, th, ph, ref _frame);
                    // Radial lift: centre and its surface point are on the same ray.
                    float off = centre.magnitude - _frame.Radius;
                    _pending.Add(new PrismAddress(
                        th, ph, off,
                        Vector3.Dot(fwd, _frame.ETheta),
                        Vector3.Dot(fwd, _frame.EPhi),
                        len * Mathf.Max(0.05f, _rules.LengthFactor),
                        girth, i * twistPerStep, _curveCount, _lane));
                }
            }
        }
    }
}
