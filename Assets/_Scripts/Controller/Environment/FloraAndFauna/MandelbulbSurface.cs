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
    /// unsampled. Nothing closes the surface into a skin: `Docs/ECOSYSTEM.md §50.2`
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

            // ── the surface's own critical points (the WATERSHED's seeds) ───────────
            //
            // A property of the SURFACE, not of the plant: every plant sharing an (element,
            // weights) pair shares it, so it is computed LAZILY on first access and cached
            // here beside the field. Nothing in the two free-walking species ever asks for
            // it, so they never pay for it.
            List<CriticalPoint> _critical;
            List<CriticalPoint> _saddles;
            List<CriticalPoint> _peaks;

            /// <summary>Every converged critical point this surface's detector found, in
            /// scan order, deduplicated on a 0.01 chord. Peaks and pits are MEASUREMENTS
            /// (the ring census); only the saddles are ever seeded.</summary>
            public IReadOnlyList<CriticalPoint> CriticalPoints()
                => _critical ??= FindCriticalPoints(this);

            /// <summary>The saddles alone, in FARTHEST-POINT order (see
            /// <see cref="FarthestPointOrder"/>) so every prefix of the list is spread over
            /// the whole sphere.</summary>
            public IReadOnlyList<CriticalPoint> Saddles()
            {
                if (_saddles != null) return _saddles;
                var all = CriticalPoints();
                var s = new List<CriticalPoint>(all.Count);
                for (int i = 0; i < all.Count; i++)
                    if (all[i].Kind == CriticalKind.Saddle) s.Add(all[i]);
                _saddles = FarthestPointOrder(s);
                return _saddles;
            }

            /// <summary>The PEAKS alone — the lobe tips — in FARTHEST-POINT order, the sibling
            /// of <see cref="Saddles"/> and the gasket's level-0 seeds. Cached beside the saddle
            /// list, so a species that never asks for it never pays for it. The peak SET is less
            /// stable across float widths than the saddle set (§53), which is why the gasket
            /// takes a PREFIX of this order: a marginal extra peak beyond K is invisible.</summary>
            public IReadOnlyList<CriticalPoint> Peaks()
            {
                if (_peaks != null) return _peaks;
                var all = CriticalPoints();
                var p = new List<CriticalPoint>(all.Count);
                for (int i = 0; i < all.Count; i++)
                    if (all[i].Kind == CriticalKind.Peak) p.Add(all[i]);
                _peaks = FarthestPointOrder(p);
                return _peaks;
            }

            /// <summary>
            /// The same bilinear sample in DOUBLE arithmetic, for the critical-point census.
            /// The census refines each point by Newton to 1e-7 and then ORDERS the saddles by
            /// comparing values that are exact ties in exact arithmetic (a symmetric surface's
            /// saddles come in orbits), so it has to be reproducible across two
            /// implementations to far better than float32 finite differences allow — measured,
            /// float32 disagreed with float64 on the saddle SET itself on two elements. In
            /// double, on the identical float32-stored field, both agree to ~1e-12.
            /// </summary>
            public double SampleD(double theta, double phi)
            {
                double x = phi / (2.0 * Math.PI) * W;
                double y = theta / Math.PI * H - 0.5;
                if (y < 0.0) y = 0.0;
                if (y > H - 1.0001) y = H - 1.0001;
                int i0 = (int)Math.Floor(x), j0 = (int)Math.Floor(y);
                double fx = x - i0, fy = y - j0;
                int ia = ((i0 % W) + W) % W, ib = ((i0 + 1) % W + W) % W;
                int ja = Math.Min(Math.Max(j0, 0), H - 1), jb = Math.Min(Math.Max(j0 + 1, 0), H - 1);
                double a = R[ja * W + ia] * (1.0 - fx) + R[ja * W + ib] * fx;
                double b = R[jb * W + ia] * (1.0 - fx) + R[jb * W + ib] * fx;
                return a * (1.0 - fy) + b * fy;
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

        // ── critical points ────────────────────────────────────────────────────────
        //
        // The WATERSHED species (Docs/ECOSYSTEM.md §53) grows the surface's own Morse–Smale
        // skeleton: every curve is a separatrix leaving a SADDLE of R(θ,φ) along one of its
        // Hessian eigen-directions and running uphill to a peak or downhill to a pit. The
        // detector below is the ONLY place the surface is differentiated twice, and its one
        // numerical constant is FROZEN in code — the critical-point SET moves with the stencil,
        // and a per-element stencil would make a differentiation parameter part of the
        // species' identity.

        public enum CriticalKind { Saddle = 0, Peak = 1, Pit = 2 }

        /// <summary>A converged critical point. Stored in DOUBLE — see <see cref="Surface.SampleD"/>;
        /// the walk reads the float views.</summary>
        public sealed class CriticalPoint
        {
            public CriticalKind Kind;
            public double Theta, Phi, Radius, Sharpness;
            public double DirX, DirY, DirZ;
            public double ValleyX, ValleyY, RidgeX, RidgeY;   // saddle only: the negative / positive eigen-directions

            public float ThetaF => (float)Theta;
            public float PhiF => (float)Phi;
            public Vector2 EValley => new Vector2((float)ValleyX, (float)ValleyY);
            public Vector2 ERidge => new Vector2((float)RidgeX, (float)RidgeY);
            public double Dot(CriticalPoint o) => DirX * o.DirX + DirY * o.DirY + DirZ * o.DirZ;
        }

        /// <summary>The height field's gradient in the point's own orthonormal frame
        /// (eθ, eφ) — the SAME 0.75-cell stencil <see cref="BuildFrame"/> uses, so a frame
        /// and its gradient agree. Its magnitude vanishes at a critical point.</summary>
        public static void Gradient(Surface s, double theta, double phi, out double gT, out double gP)
        {
            double hT = Math.PI / s.H * 0.75;
            double hP = 2.0 * Math.PI / s.W * 0.75;
            double sT = Math.Max(Math.Sin(theta), 1e-4);
            gT = (s.SampleD(theta + hT, phi) - s.SampleD(theta - hT, phi)) / (2.0 * hT);
            gP = (s.SampleD(theta, phi + hP) - s.SampleD(theta, phi - hP)) / (2.0 * hP) / sT;
        }

        /// <summary>The frozen Hessian stencil, in radians. Never a rule field.</summary>
        public const double HessianStencil = 0.01;

        /// <summary>
        /// Second derivatives in the orthonormal frame. The mixed term is ∂gφ/∂θ taken ONCE —
        /// the "other" mixed derivative (∂gθ/∂φ)/sinθ carries a (cosθ/sinθ)·gφ connection term
        /// that would leave a residual in a symmetrised average and is unbounded near the
        /// poles, where the Newton iterate is clamped.
        /// </summary>
        public static void Hessian(Surface s, double theta, double phi,
                                   out double htt, out double htp, out double hpp)
        {
            const double h = HessianStencil;
            Gradient(s, theta + h, phi, out double a1, out double b1);
            Gradient(s, theta - h, phi, out double a0, out double b0);
            Gradient(s, theta, phi + h, out _, out double b3);
            Gradient(s, theta, phi - h, out _, out double b2);
            Gradient(s, theta, phi, out double gT, out _);
            double sT = Math.Max(Math.Sin(theta), 1e-4);
            double ct = Math.Cos(theta);
            htt = (a1 - a0) / (2.0 * h);
            htp = (b1 - b0) / (2.0 * h);
            hpp = ((b3 - b2) / (2.0 * h)) / sT + (ct / sT) * gT;
        }

        /// <summary>Eigen-decomposition of the 2×2 symmetric Hessian: l1 ≥ l2.</summary>
        public static void Eigen(double htt, double htp, double hpp,
                                 out double l1, out double l2,
                                 out double v1x, out double v1y, out double v2x, out double v2y)
        {
            double tr = htt + hpp, det = htt * hpp - htp * htp;
            double r = Math.Sqrt(Math.Max(0.0, tr * tr * 0.25 - det));
            l1 = tr * 0.5 + r;
            l2 = tr * 0.5 - r;
            EigenVector(htt, htp, l1, out v1x, out v1y);
            EigenVector(htt, htp, l2, out v2x, out v2y);
        }

        static void EigenVector(double htt, double htp, double l, out double vx, out double vy)
        {
            vx = htp; vy = -(htt - l);
            if (Math.Abs(vx) < 1e-12 && Math.Abs(vy) < 1e-12) { vx = 1.0; vy = 0.0; return; }
            double m = Math.Sqrt(vx * vx + vy * vy);
            vx /= m; vy /= m;
        }

        /// <summary>Chord² under which two converged points are one point (chord 0.01).</summary>
        const double DedupeDot = 1.0 - 0.5 * 0.01 * 0.01;

        /// <summary>
        /// Scans a 2× lattice for local minima of |∇R| (the two polar rows excluded — the
        /// chart is singular there), refines each with a clamped Newton iteration, accepts
        /// those that converge, classifies them and deduplicates on a 0.01 chord, all IN
        /// SCAN ORDER so two implementations produce the same list.
        /// </summary>
        public static List<CriticalPoint> FindCriticalPoints(Surface s)
        {
            int w2 = 2 * s.W, h2 = 2 * s.H;
            var slope = new double[w2 * h2];
            for (int j = 0; j < h2; j++)
            {
                double th = (j + 0.5) / h2 * Math.PI;
                for (int i = 0; i < w2; i++)
                {
                    double ph = (double)i / w2 * 2.0 * Math.PI;
                    Gradient(s, th, ph, out double gT, out double gP);
                    slope[j * w2 + i] = Math.Sqrt(gT * gT + gP * gP);
                }
            }

            var found = new List<CriticalPoint>();
            for (int j = 1; j <= h2 - 2; j++)
                for (int i = 0; i < w2; i++)
                {
                    double v = slope[j * w2 + i];
                    bool minimum = true;
                    for (int dj = -1; dj <= 1 && minimum; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            if (dj == 0 && di == 0) continue;
                            int ii = ((i + di) % w2 + w2) % w2;
                            if (slope[(j + dj) * w2 + ii] <= v) { minimum = false; break; }
                        }
                    if (!minimum) continue;

                    double th = (j + 0.5) / h2 * Math.PI;
                    double ph = (double)i / w2 * 2.0 * Math.PI;
                    if (!RefineCriticalPoint(s, ref th, ref ph)) continue;

                    Hessian(s, th, ph, out double htt, out double htp, out double hpp);
                    double det = htt * hpp - htp * htp, tr = htt + hpp;
                    var kind = det < 0.0 ? CriticalKind.Saddle : (tr < 0.0 ? CriticalKind.Peak : CriticalKind.Pit);
                    double st = Math.Sin(th);
                    double dx = st * Math.Cos(ph), dy = st * Math.Sin(ph), dz = Math.Cos(th);
                    bool duplicate = false;
                    for (int q = 0; q < found.Count; q++)
                        if (dx * found[q].DirX + dy * found[q].DirY + dz * found[q].DirZ > DedupeDot) { duplicate = true; break; }
                    if (duplicate) continue;

                    Eigen(htt, htp, hpp, out double l1, out double l2,
                          out double v1x, out double v1y, out double v2x, out double v2y);
                    found.Add(new CriticalPoint
                    {
                        Kind = kind, Theta = th, Phi = ph, Radius = s.SampleD(th, ph),
                        DirX = dx, DirY = dy, DirZ = dz,
                        Sharpness = Math.Min(Math.Abs(l1), Math.Abs(l2)),
                        // l2 ≤ l1: at a saddle l2 < 0 < l1, so the VALLEY runs along v2.
                        ValleyX = v2x, ValleyY = v2y, RidgeX = v1x, RidgeY = v1y,
                    });
                }
            return found;
        }

        /// <summary>Clamped Newton on the gradient. Update ORDER is load-bearing for the
        /// mirror: θ moves first, and the φ step is divided by sin of the NEW θ.</summary>
        static bool RefineCriticalPoint(Surface s, ref double th, ref double ph)
        {
            for (int it = 0; it < 12; it++)
            {
                Gradient(s, th, ph, out double gT, out double gP);
                Hessian(s, th, ph, out double htt, out double htp, out double hpp);
                double det = htt * hpp - htp * htp;
                if (Math.Abs(det) < 1e-9) break;
                double dTh = -(hpp * gT - htp * gP) / det;
                double dPh = -(-htp * gT + htt * gP) / det;
                double n = Math.Sqrt(dTh * dTh + dPh * dPh);
                if (n > 0.05) { dTh *= 0.05 / n; dPh *= 0.05 / n; }
                th += dTh;
                ph += dPh / Math.Max(Math.Sin(th), 1e-4);
                th = Math.Min(Math.Max(th, 1e-3), Math.PI - 1e-3);
                ph = ((ph % (2.0 * Math.PI)) + 2.0 * Math.PI) % (2.0 * Math.PI);
                if (n < 1e-7) break;
            }
            Gradient(s, th, ph, out double fT, out double fP);
            return Math.Sqrt(fT * fT + fP * fP) <= 2e-3;
        }

        // Tolerances of the ordering comparator. The saddles of a symmetric surface come in
        // orbits whose sharpness and farthest-point distance are EXACT ties in exact
        // arithmetic and ε-apart in float, so an argmax with no tolerance is decided by
        // rounding and two implementations pick different, nearly antipodal first saddles
        // (measured). Within the tolerance the order falls through to (θ, φ), which two
        // converged transcriptions agree on.
        const double OrderSharpnessTolerance = 1e-6;
        const double OrderDistanceTolerance = 1e-6;
        const double OrderAngleTolerance = 1e-5;

        static int CompareAngles(CriticalPoint a, CriticalPoint b)
        {
            if (Math.Abs(a.Theta - b.Theta) > OrderAngleTolerance) return a.Theta < b.Theta ? -1 : 1;
            if (Math.Abs(a.Phi - b.Phi) > OrderAngleTolerance) return a.Phi < b.Phi ? -1 : 1;
            return 0;
        }

        /// <summary>
        /// Farthest-point ordering: the sharpest saddle first, then repeatedly the saddle
        /// farthest (on the sphere) from everything already taken. A MEASURED correction —
        /// sharpness-major put a plant's strongest quarter into 2 of 8 latitude bands, the
        /// belt defect §50.5 exists to forbid; farthest-point gives 7–8 of 8 at every prefix.
        /// </summary>
        public static List<CriticalPoint> FarthestPointOrder(List<CriticalPoint> saddles)
        {
            var order = new List<CriticalPoint>(saddles.Count);
            if (saddles.Count == 0) return order;
            double top = 0.0;
            for (int i = 0; i < saddles.Count; i++) top = Math.Max(top, saddles[i].Sharpness);
            double sTol = OrderSharpnessTolerance * Math.Max(top, 1e-12);

            var taken = new bool[saddles.Count];
            var dmin = new double[saddles.Count];
            int first = 0;
            for (int i = 1; i < saddles.Count; i++)
            {
                double ds = saddles[i].Sharpness - saddles[first].Sharpness;
                if (ds > sTol || (ds >= -sTol && CompareAngles(saddles[i], saddles[first]) < 0)) first = i;
            }
            taken[first] = true;
            order.Add(saddles[first]);
            for (int q = 0; q < saddles.Count; q++) dmin[q] = 1.0 - saddles[q].Dot(saddles[first]);

            while (order.Count < saddles.Count)
            {
                int best = -1;
                for (int i = 0; i < saddles.Count; i++)
                {
                    if (taken[i]) continue;
                    if (best < 0) { best = i; continue; }
                    double dd = dmin[i] - dmin[best];
                    if (dd > OrderDistanceTolerance) { best = i; continue; }
                    if (dd < -OrderDistanceTolerance) continue;
                    double ds = saddles[i].Sharpness - saddles[best].Sharpness;
                    if (ds > sTol) { best = i; continue; }
                    if (ds < -sTol) continue;
                    if (CompareAngles(saddles[i], saddles[best]) < 0) best = i;
                }
                taken[best] = true;
                order.Add(saddles[best]);
                for (int q = 0; q < saddles.Count; q++)
                    if (!taken[q]) dmin[q] = Math.Min(dmin[q], 1.0 - saddles[q].Dot(saddles[best]));
            }
            return order;
        }

        // ── the growth rule ────────────────────────────────────────────────────────

        public enum SteeringField { Contour = 0, Ascent = 1, Descent = 2, Azimuth = 3, Meridian = 4, Geodesic = 5 }

        /// <summary>
        /// Which curve family, how densely along it, how sparsely across it. Fields are
        /// APPENDED in groups and every field added after the first release defaults to 0,
        /// with 0 leaving the species that predate it BIT-IDENTICAL — the harness proves it.
        /// The declaration order IS the wire format (<c>Tools/Build/mandelbulb_surface_harness</c>),
        /// the model's <c>Rules.FIELDS</c> and the prefab layout: never reorder.
        /// </summary>
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
                                              // built on this rule (Docs/ECOSYSTEM.md §52).

            // ── THE FALL — the radial dive every species authors (Docs/ECOSYSTEM.md §53) ──
            // A curve the surface can no longer carry FALLS: from its last surface sample it
            // continues as a LOGARITHMIC SPIRAL toward the heart — a heading re-derived every
            // step at a constant angle ψ from the inward radial, and a step that is a fixed
            // fraction of its current radius — ending a few units short of the crystal. Which
            // seeds own a dive is strided; WHERE each one leaves is emergent.
            public int DiveCount;           // seeds that own a dive, strided over the seed list.
                                            // 0 = the whole mechanism is off.
            public float DiveStepFraction;  // f: step as a fraction of the current radius. 0 = off.
            public float DiveAngleDegrees;  // ψ: angle from the inward radial, clamped [5, 85]
            public float DiveStopRadius;    // where a dive stops, unit-sphere units (min 0.01)
            public int DiveMaxSteps;        // hard cap on dive prisms per curve (read as max(2, v))
            public float DiveSwirlDegrees;  // per-step precession about the ray: 0 = a planar spiral
            public float DiveStrideCeiling; // the dive's step ceiling in multiples of the walk step.
                                            // 0 = no ceiling. Without it the first prisms after the
                                            // release are f·r long — 13x a surface prism, measured.
            public float DiveGirthFloor;    // cross-section multiplier at the stop radius, tapered
                                            // linearly in log r from 1 at release. ≤ 0 = no taper.
            public float DiveAxisAlign;     // 0..1: how far each dive's tangential heading is turned
                                            // toward the AZIMUTHAL direction about the surface's
                                            // polar axis, so every dive winds about ONE axis and the
                                            // pole reads as a rosette. 0 = each dive keeps its own plane.
            public float DiveDescent;       // a run dives only if it DESCENDED at least this much
                                            // (R first − R last, surface units). 0 = any run.

            // ── THE WATERSHED (Docs/ECOSYSTEM.md §53) ──
            public int SkeletonSeeds;       // 1 = seeds are the surface's SADDLES and lanes 0–3 are
                                            // their four separatrices; 0 = the free walk above.
            public float WalkStep;          // sampling step of the skeleton walk when > 0; 0 = StepSize.
                                            // §51 derives StepSize per element while a skeleton's cost
                                            // is fixed by the SURFACE, so the two are decoupled here
                                            // and LengthFactor is what makes them agree again.
            public float MinPersistence;    // an arm whose |R_end − R_start| is below this is dust
            public float GirthReference;    // the run length that earns full girth. 0 = MaxSteps/2 —
                                            // meaningless for a skeleton whose longest arm is 29
                                            // steps (§50's "a ceiling nothing reaches" defect).

            // ── APOLLONIA — the spherical Apollonian gasket of rings (Docs/ECOSYSTEM.md §54) ──
            // Level 0 is the bulb's OWN LOBES: the surface's peaks in farthest-point order, each
            // given half the angle to its nearest neighbour so the big rings crown the lobes and
            // are mutually tangent by construction. Every later disc is the classic Apollonian
            // step — the disc inscribed in a curvilinear triangle of three mutually adjacent
            // discs, kept only if it overlaps nothing placed and clears the visibility floor.
            // Each disc is DRAWN as a ring of prisms around its small circle, lifted onto R(θ,φ),
            // so a big ring crossing three lobes comes out a scalloped star while a small ring
            // inside one lobe is a clean circle: the surface deforms the shared motif by exactly
            // how much of the bulb the motif spans. The whole block is unreachable while
            // GasketLevels == 0, and NO address field changed: a ring prism is verbatim the
            // shipped surface branch (radial lift in RadialOffset, tangential heading, TanR 0).
            public int GasketLevels;        // the master switch AND the number of size-OCTAVE lanes.
                                            // 0 = off: Growth takes the seed/trace/hop path, byte for byte.
            public int DiscSeeds;           // level-0 discs = the first K of the surface's PEAKS in
                                            // farthest-point order. ≤ 0 = every peak.
            public float DiscPad;           // adjacency SEARCH slack, radians: three discs bound a
                                            // curvilinear triangle when every pair has
                                            // angle(d_i,d_j) ≤ ρ_i + ρ_j + DiscPad. It widens which
                                            // TRIPLES are searched, never how tightly a child is
                                            // inscribed (measured: tangency gap 0.000 at 0.6, and
                                            // 0.09 finds no children at all). 0 = strict tangency.
            public float DiscMinRadius;     // the visibility floor and the recursion's real
                                            // terminator, radians. A SIZE rather than a level count
                                            // on purpose: stop the ladder where its rings stop
                                            // being visible. 0 = no floor.
            public float RingShrink;        // draw-time multiplier on every disc's ρ (never in the
                                            // packing), so tangent discs draw with a visible gap.
                                            // 0 is read as 1.
            public float RingFlatten;       // 0..1: how far a ring is pulled off the terrain onto
                                            // the sphere of its OWN mean radius. 0 rides R exactly
                                            // (maximum scalloping), 1 is a perfect circle (no bulb).
            public float RingGirthExponent; // cross-section allometry: Girth = (ρ/ρ_ref)^e with
                                            // ρ_ref the largest disc (COMPUTED, never authored).
                                            // 1 is strict homothety (the smallest rings are threads);
                                            // 0 = Girth 1 on every ring, which fails the volume-span
                                            // gate — stated so nobody authors 0 by accident.
            public int RingSamples;         // prisms per ring, the SAME at every scale (the
                                            // homothety). 0 = DERIVE from the reference ring and
                                            // this element's own §51 step (RingSamplesFor); > 0 =
                                            // that literal count (an escape hatch + a verifier control).
            public float GasketOctave;      // lane = floor(log2(ρ_ref/ρ) / GasketOctave), clamped to
                                            // [0, GasketLevels−1]. THE LANE IS THE SIZE OCTAVE, NOT
                                            // THE RECURSION LEVEL — measured, the recursion levels
                                            // are not monotone in ρ (Mass: level-1 median 0.270
                                            // against level-0's 0.260), so level-major truncation
                                            // does NOT lose the smallest rings and octave-major does.
                                            // 0 is read as 1.
            public float DiscRelaxRate;     // the inscribe relaxation's step rate. 0 = the frozen
                                            // 0.6. Exists ONLY so the verifier can perturb it as a
                                            // negative control; never author it.
            public float RingGirthFloor;    // floor on the allometric girth, so the finest octaves
                                            // stay readable at arena distance (measured: without it
                                            // Mass spent 41% of its prisms on 1% of the frame).
                                            // 0 = no floor.
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
            public readonly float Dive;           // depth below the surface as a SHELL FRACTION:
                                                  // 0 on the surface, 1 at the origin. A fraction
                                                  // rather than a lift so a morph moves a diving
                                                  // prism by (1 − Dive)·ΔR — the self-similar
                                                  // response — instead of shearing it off its canopy.
            public readonly float TanA, TanB;     // heading in this point's own (eθ, eφ)
            public readonly float TanR;           // heading's component along the RAY. 0 on every
                                                  // surface prism, whose heading is re-projected
                                                  // onto the tangent plane at pose time; non-zero
                                                  // on a dive prism, whose heading is absolute.
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

            public PrismAddress(float theta, float phi, float radialOffset, float dive,
                                float tanA, float tanB, float tanR,
                                float length, float girth, float roll,
                                int curve, int lane)
            {
                Theta = theta; Phi = phi; RadialOffset = radialOffset; Dive = dive;
                TanA = tanA; TanB = tanB; TanR = tanR;
                Length = length; Girth = girth; Roll = roll;
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
            Vector3 upBase;
            if (a.TanR != 0f)
            {
                // A DIVE prism is in free space and has no surface to hug: its heading is
                // taken whole in the spherical basis. Its `up` is derived from the RAY rather
                // than the normal — the normal passes arbitrarily close to a diving heading
                // (measured |sin| down to 0.03) and a plate hung off it tumbles, where the ray
                // sits ≥ cos(85°) off every legal dive heading by construction. The seam
                // between the last surface plate and the first dive plate is paid ONCE per
                // curve through Roll (see Emit), so the face is continuous across the release.
                f += a.TanR * scratch.Dir;
                float m = f.magnitude;
                forward = m > 1e-7f ? f / m : scratch.Dir;
                Vector3 w = scratch.Dir - forward * Vector3.Dot(scratch.Dir, forward);
                float m2 = w.magnitude;
                if (m2 > 1e-5f) upBase = w / m2;
                else
                {
                    w = n - forward * Vector3.Dot(n, forward);
                    upBase = w.sqrMagnitude > 1e-10f ? w.normalized : scratch.ETheta;
                }
            }
            else
            {
                f -= n * Vector3.Dot(f, n);
                float m = f.magnitude;
                forward = m > 1e-7f ? f / m : scratch.EPhi;
                upBase = n;
            }

            // HELICOIDAL TWIST: roll the prism about its OWN tangent. Rodrigues about a unit
            // axis that the vector is already perpendicular to reduces to one cos/sin blend
            // with the binormal, so this costs no cross product beyond the one it needs and
            // cannot drift off the frame. Roll 0 leaves `up` exactly on the normal, so a
            // species that authors no twist is bit-identical to before this existed.
            if (a.Roll != 0f)
            {
                Vector3 binormal = Vector3.Cross(forward, upBase);
                float cosRoll = Mathf.Cos(a.Roll), sinRoll = Mathf.Sin(a.Roll);
                up = upBase * cosRoll + binormal * sinRoll;
            }
            else
            {
                up = upBase;
            }

            // Radius × (1 − 0) is exact in IEEE, so a surface prism poses byte-identically.
            position = scratch.Dir * (scratch.Radius * (1f - a.Dive)) + n * a.RadialOffset;
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

            // The Fall: which seeds still owe a dive, and the dive itself as a SEPARATE list
            // from the run's samples — the lane bookkeeping reads `points` alone, so the dive
            // cannot restart a lane deep inside the plant (a mistake that would keep every
            // offline gate green while the species quietly collapsed).
            bool[] _diveOwed;
            int _diveQuota, _divesSpent;
            readonly List<Vector3> _dive = new(96);
            readonly List<Vector3> _pts = new(512);

            // The Watershed: the saddle list is the seed list, in farthest-point order. Built
            // LAZILY on the first TryNext rather than in the constructor, because the census is
            // ~0.4 M samples and `new Growth` runs on the frame a plant is planted.
            bool _seedsBuilt;
            IReadOnlyList<CriticalPoint> _saddles;

            // Apollonia: the disc set, resolved ONCE per plant in EnsureSeeds (lazy, like the
            // skeleton — the peak census and the O(n³) triple search must never run on the
            // planting frame). One seed per disc, same index, so every per-seed array fits.
            // `Parent` is the tangency parent — one of the three discs whose gap this one was
            // inscribed in, and therefore a disc it TOUCHES, so the bond a limb has to span is
            // the shortest the gasket offers. -1 on a level-0 disc, which crowns a peak and
            // reaches the crystal through the seed tree instead.
            struct Disc { public Vector3 Axis; public float Rho; public int Level, Lane, Parent; }
            readonly List<Disc> _discs = new();
            int[] _discOrder;              // indices into _discs, ρ-DESCENDING (the lay order)
            float _rhoRef;                 // the largest disc's ρ — computed, the girth reference
            int _ringSamples;              // N, resolved once per plant
            int _discCursor;               // walks _discOrder
            readonly List<Sample> _ringBody = new(160);
            readonly List<Vector3> _ringPts = new(160);
            readonly List<Vector3> _ringDirs = new(160);
            readonly List<float> _ringRadii = new(160);

            int _seedIndex, _lane, _pendingIndex, _curveCount;

            // ── THE PLANT'S SKELETON ─────────────────────────────────────────────────────────
            // A flora grows the way it withers, RUN BACKWARDS (the /flora skill §2): the crystal
            // first, then limbs out of the crystal, then limbs and plates out of limbs. Three
            // pieces carry that here, and none of them is geometry this species invented:
            //
            //   _seedParent / _seedOrder  a spanning tree over the SEEDS, rooted at the heart.
            //                             Lane 0 is walked in that order, so a seed's stem is
            //                             always laid after the seed it grows out of.
            //   _seedArrival[k]           the global index of the prism sitting AT seed k — where
            //                             its stem lands and its first curve begins. A child
            //                             seed's stem hangs off its parent's arrival prism.
            //   _laneAnchor[k]            the global index of the prism at the MIDPOINT of seed
            //                             k's last run, which is exactly the point `Hop` steps
            //                             across from. So lane L's curve hangs off lane L-1's
            //                             body over a bond one hop long — the gap the cage is
            //                             made of, now spanned by a limb instead of left open.
            //
            // The order is LANE-MAJOR and that is what makes the whole thing connected at every
            // tick rather than only when finished: every seed's lane 0 (its stem + first curve)
            // is laid before any seed's lane 1, so the plant is a spanning tree of stems the
            // moment the first lane completes and thickens from there.
            int[] _seedParent, _seedOrder, _seedArrival, _laneAnchor;
            // WHERE that arrival prism is. A stem must start at the prism it hangs off, not at
            // the seed's own point — for the two walking species and the Watershed those are the
            // same place (a curve starts AT its seed), but a gasket seed is a disc CENTRE while
            // its curve starts on the disc's RIM, so building the stem from the centre left the
            // first limb spanning the parent disc's whole radius with nothing in it. Measured by
            // measure_mandelbulb_flora.py's bond row at up to 15 strides; 1.6 after this.
            Vector3[] _seedArrivalPoint;
            readonly List<int> _pendingParent = new();
            readonly List<bool> _pendingConnector = new();
            static readonly List<Vector3> _noRise = new();
            readonly List<Vector3> _rise = new(96);
            readonly List<Sample> _stemRun = new(96);
            readonly List<Sample> _run = new(512);
            int _emitted;                 // prisms handed out so far — the global index base
            int _anchorForNextEmit = -1;  // what the next batch's first prism hangs off (-1 = heart)
            // Emit SKIPS a zero-length segment, so "the nth point" and "the nth prism" are not the
            // same number and cannot be related by arithmetic at the call site. These are set
            // before a batch and written back by it: the caller says which leading segments are
            // the connector and which point it wants marked, and gets the PRISM indices back.
            int _emitConnectorSegments, _emitMarkPoint;
            int _emitConnectorEnd = -1, _emitMarkedPrism = -1;

            public int CurvesTraced => _curveCount;
            public int SeedCount { get { EnsureSeeds(); return _seeds.Count; } }
            public int DivesSpent => _divesSpent;
            public bool Skeleton => _rules.SkeletonSeeds != 0;
            public bool Gasket => _rules.GasketLevels != 0;

            public Growth(Surface surface, in GrowthRules rules, int seed)
            {
                _surface = surface;
                _rules = rules;
                _rng = new Rng(seed * 2654435761u.GetHashCode() ^ 0x5bf03635);
                // The two species that read the surface's critical points build their seeds
                // LAZILY (first TryNext): the census is ~0.4 M samples and `new Growth` runs
                // on the frame a plant is planted.
                if (!Skeleton && !Gasket) EnsureSeeds();
            }

            void EnsureSeeds()
            {
                if (_seedsBuilt) return;
                _seedsBuilt = true;
                if (Gasket) BuildGasketSeeds();
                else if (Skeleton) BuildSkeletonSeeds();
                else BuildSeeds();
                int n = Mathf.Max(1, _seeds.Count);
                _lanePosition = new Vector3[n];
                _laneTangent = new Vector3[n];
                _laneTangentValid = new bool[n];
                _laneDead = new bool[n];
                for (int i = 0; i < _seeds.Count; i++) _lanePosition[i] = _seeds[i];
                // The plant's skeleton. Built here for every species, because every species has
                // seeds and none of them may start a patch in mid-air.
                BuildSeedTree();

                // The Fall's owed set is STRIDED, never a prefix: BuildSeeds emits in Fibonacci
                // order, which is z-monotone, so a prefix would put every dive in a polar cap
                // (§50.5's defect for a new consumer). Same idiom BuildSeeds uses for `want`.
                _diveOwed = new bool[n];
                _divesSpent = 0;
                if (Gasket)
                {
                    // The gasket strides over its LEVEL-0 discs in LAY order: a stride over all
                    // ~90 discs would put most dives on rings too small to release a readable
                    // spiral, and the largest rings are laid first, so the heart connection is
                    // never the thing the budget cuts.
                    var top = new List<int>(_discs.Count);
                    for (int i = 0; i < _discOrder.Length; i++)
                        if (_discs[_discOrder[i]].Level == 0) top.Add(_discOrder[i]);
                    _diveQuota = Mathf.Min(Mathf.Max(0, _rules.DiveCount), top.Count);
                    if (_rules.DiveStepFraction > 0f && _diveQuota > 0)
                        for (int d = 0; d < _diveQuota; d++)
                            _diveOwed[top[(int)((long)d * top.Count / _diveQuota)]] = true;
                    return;
                }
                _diveQuota = Mathf.Min(Mathf.Max(0, _rules.DiveCount), _seeds.Count);
                if (_rules.DiveStepFraction > 0f && _diveQuota > 0)
                    for (int d = 0; d < _diveQuota; d++)
                        // Over the SEED LIST, which is z-monotone and therefore spread by
                        // construction — NOT over the lay order. Striding the lay order was
                        // tried when these species started growing out of their crystal, on the
                        // reasoning that the plant is now laid in tree order: measured, it
                        // helped one species' theta spread and hurt another's, because Prim's
                        // order is spatially COHERENT and a stride over a nearest-neighbour walk
                        // is not a spread of anything. The generator's own order is the only one
                        // here with a spread argument behind it (§50.5).
                        _diveOwed[(int)((long)d * _seeds.Count / _diveQuota)] = true;
            }

            // ── APOLLONIA ─────────────────────────────────────────────────────────────

            const int GasketRelaxSteps = 24;
            const float GasketOverlapEps = 1e-4f;
            const int RingMinSamples = 16;      // below this a ring stops reading as a ring
            const int RingMaxSamples = 128;     // a cost ceiling, never reached at shipped steps
            const float GasketRelaxRateDefault = 0.6f;

            /// <summary>The gasket's seeds: one per disc, in disc-index order, plus the lay
            /// order (ρ descending), the girth reference and the ring sample count.
            /// No Rng draw anywhere in this species.</summary>
            void BuildGasketSeeds()
            {
                BuildGasket();
                _rhoRef = 1e-6f;
                for (int i = 0; i < _discs.Count; i++) _rhoRef = Mathf.Max(_rhoRef, _discs[i].Rho);

                // LAY ORDER: ρ DESCENDING, ties broken by (Level, index) so the key is TOTAL and
                // the result is the same under any sort algorithm. This is what makes "a
                // budget-stopped plant loses the smallest rings" exactly true.
                _discOrder = new int[_discs.Count];
                for (int i = 0; i < _discs.Count; i++) _discOrder[i] = i;
                var discs = _discs;
                Array.Sort(_discOrder, (a, b) =>
                {
                    int c = discs[b].Rho.CompareTo(discs[a].Rho);
                    if (c != 0) return c;
                    c = discs[a].Level.CompareTo(discs[b].Level);
                    return c != 0 ? c : a.CompareTo(b);
                });

                int lanes = Mathf.Max(1, _rules.GasketLevels);
                float oct = _rules.GasketOctave > 0f ? _rules.GasketOctave : 1f;
                for (int i = 0; i < _discs.Count; i++)
                {
                    var d = _discs[i];
                    float o = Mathf.Log(_rhoRef / Mathf.Max(d.Rho, 1e-9f), 2f) / oct;
                    d.Lane = Mathf.Clamp(Mathf.FloorToInt(o), 0, lanes - 1);
                    _discs[i] = d;
                    Spherical(d.Axis, out float th, out float ph);
                    BuildFrame(_surface, th, ph, ref _frame);
                    _seeds.Add(_frame.Position);
                }
                _ringSamples = RingSamplesFor();
            }

            /// <summary>Prisms per ring. The element's §51 step IS the prism's length on this
            /// family, so it sets how many strokes a ring is drawn with — measured against the
            /// REFERENCE ring so every ring of the plant shares one N, which is the homothety
            /// the species is named for. Space's long blades give a coarse polygon, Mass's
            /// bricks a fine one: the element's long axis spent as ring COARSENESS.</summary>
            int RingSamplesFor()
            {
                if (_rules.RingSamples > 0) return Mathf.Clamp(_rules.RingSamples, 3, RingMaxSamples);
                float circ = 2f * Mathf.PI * Mathf.Sin(_rhoRef) * _surface.MeanRadius;
                int n = Mathf.RoundToInt(circ / Mathf.Max(1e-6f, _rules.StepSize));
                return Mathf.Clamp(n, RingMinSamples, RingMaxSamples);
            }

            /// <summary>Level 0 from the surface's peaks, then the breadth-first Apollonian
            /// step. Deterministic: every ordering is a TOTAL key, never a sort's stability.</summary>
            void BuildGasket()
            {
                _discs.Clear();
                var peaks = _surface.Peaks();
                int want = _rules.DiscSeeds > 0 ? Mathf.Min(_rules.DiscSeeds, peaks.Count) : peaks.Count;
                var axes = new List<Vector3>(want);
                for (int i = 0; i < want; i++)
                    axes.Add(new Vector3((float)peaks[i].DirX, (float)peaks[i].DirY, (float)peaks[i].DirZ).normalized);
                for (int i = 0; i < axes.Count; i++)
                {
                    float nn = Mathf.PI / 3f;                          // lone-disc fallback
                    for (int j = 0; j < axes.Count; j++)
                        if (j != i) nn = Mathf.Min(nn, Angle(axes[i], axes[j]));
                    _discs.Add(new Disc { Axis = axes[i], Rho = 0.5f * nn, Level = 0, Parent = -1 });
                }

                int start = 0, count = _discs.Count;
                int levels = Mathf.Max(1, _rules.GasketLevels);
                var cand = new List<Disc>(256);
                var candOrder = new List<int>(256);
                for (int lvl = 1; lvl < levels; lvl++)
                {
                    cand.Clear();
                    int n = count;
                    for (int a = 0; a < n; a++)
                    for (int b = a + 1; b < n; b++)
                    {
                        if (!Adjacent(a, b)) continue;
                        for (int c = b + 1; c < n; c++)
                        {
                            // Past level 1 a triple must involve a disc created LAST level, or
                            // the same triples are re-found every level for nothing.
                            if (lvl > 1 && a < start && b < start && c < start) continue;
                            if (!Adjacent(a, c) || !Adjacent(b, c)) continue;
                            if (!Inscribe(a, b, c, out Vector3 x, out float rho)) continue;
                            if (rho < _rules.DiscMinRadius) continue;
                            // `a` is the lowest of the three indices, so it is the earliest disc
                            // created and therefore never later in the lay order than this child.
                            cand.Add(new Disc { Axis = x, Rho = rho, Level = lvl, Parent = a });
                        }
                    }
                    // Biggest gap first; ties fall through to the (a,b,c) enumeration index —
                    // a TOTAL key, because the bulb's symmetry puts children in orbits that
                    // share a ρ to the last bit, and .NET's List sort is not stable.
                    candOrder.Clear();
                    for (int i = 0; i < cand.Count; i++) candOrder.Add(i);
                    var cl = cand;
                    candOrder.Sort((p, q) =>
                    {
                        int c = cl[q].Rho.CompareTo(cl[p].Rho);
                        return c != 0 ? c : p.CompareTo(q);
                    });
                    start = count;
                    for (int i = 0; i < candOrder.Count; i++)
                    {
                        var x = cand[candOrder[i]];
                        bool clash = false;
                        for (int j = 0; j < count; j++)
                            if (Angle(x.Axis, _discs[j].Axis) < x.Rho + _discs[j].Rho - GasketOverlapEps)
                            { clash = true; break; }
                        if (clash) continue;
                        _discs.Add(x); count++;
                    }
                }
            }

            bool Adjacent(int a, int b)
                => Angle(_discs[a].Axis, _discs[b].Axis) <= _discs[a].Rho + _discs[b].Rho + _rules.DiscPad;

            static float Angle(Vector3 a, Vector3 b)
                => Mathf.Acos(Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f));

            /// <summary>The disc tangent internally to the curvilinear triangle (a,b,c): a
            /// fixed-iteration relaxation that EQUALISES f_i(x) = angle(x, d_i) − ρ_i. No Rng;
            /// measured across all four bakes it converges to a median tangency gap of 0.000.
            /// x is normalised once per iteration, which is transcription-load-bearing; the
            /// summation order (a, b, c) is NOT — measured, reversing it moves 65 of 255 disc
            /// rows in their last digits and changes no disc, lane, N or rho_ref.</summary>
            bool Inscribe(int ia, int ib, int ic, out Vector3 x, out float rho)
            {
                x = _discs[ia].Axis + _discs[ib].Axis + _discs[ic].Axis;
                rho = 0f;
                if (x.sqrMagnitude < 1e-12f) return false;
                x = x.normalized;
                float rate = _rules.DiscRelaxRate > 0f ? _rules.DiscRelaxRate : GasketRelaxRateDefault;
                for (int it = 0; it < GasketRelaxSteps; it++)
                {
                    float fa = Angle(x, _discs[ia].Axis) - _discs[ia].Rho;
                    float fb = Angle(x, _discs[ib].Axis) - _discs[ib].Rho;
                    float fc = Angle(x, _discs[ic].Axis) - _discs[ic].Rho;
                    float target = (fa + fb + fc) / 3f;
                    Vector3 step = Vector3.zero;
                    step += TangentToward(x, _discs[ia].Axis) * -(target - fa);   // moving AWAY from d_i raises f_i
                    step += TangentToward(x, _discs[ib].Axis) * -(target - fb);
                    step += TangentToward(x, _discs[ic].Axis) * -(target - fc);
                    x = x + step * rate;
                    if (x.sqrMagnitude < 1e-12f) return false;
                    x = x.normalized;
                }
                rho = Mathf.Min(Angle(x, _discs[ia].Axis) - _discs[ia].Rho,
                      Mathf.Min(Angle(x, _discs[ib].Axis) - _discs[ib].Rho,
                                Angle(x, _discs[ic].Axis) - _discs[ic].Rho));
                return true;
            }

            // the unit tangent at x pointing toward d, or zero when degenerate
            static Vector3 TangentToward(Vector3 x, Vector3 d)
            {
                Vector3 g = d - x * Vector3.Dot(d, x);
                return g.sqrMagnitude > 1e-14f ? g.normalized : Vector3.zero;
            }

            /// <summary>The ring of one disc, closed form: `samples` directions on the small
            /// circle of angular radius ρ about the axis, each lifted onto R(θ,φ) and pulled
            /// `flatten` of the way onto the ring's own mean radius. CLOSED — the first point
            /// is appended again, so the seam prism is emitted.</summary>
            void RingPoints(Vector3 d, float rho, int samples, float flatten, List<Vector3> into)
            {
                Vector3 e1 = Vector3.Cross(d, Vector3.forward);
                if (e1.sqrMagnitude < 1e-10f) e1 = Vector3.Cross(d, Vector3.right);
                e1 = e1.normalized;
                Vector3 e2 = Vector3.Cross(d, e1);

                float cr = Mathf.Cos(rho), sr = Mathf.Sin(rho);
                _ringDirs.Clear(); _ringRadii.Clear();
                float sum = 0f;
                for (int k = 0; k < samples; k++)
                {
                    float a = 2f * Mathf.PI * k / samples;
                    Vector3 u = (d * cr + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * sr).normalized;
                    Spherical(u, out float th, out float ph);
                    float r = _surface.Sample(th, ph);
                    _ringDirs.Add(u); _ringRadii.Add(r); sum += r;
                }
                float mean = sum / samples;
                float f = Mathf.Clamp01(flatten);
                into.Clear();
                for (int k = 0; k < samples; k++)
                    into.Add(_ringDirs[k] * (_ringRadii[k] * (1f - f) + mean * f));
                into.Add(into[0]);
            }

            /// <summary>One ring per call, in lay order. The lane IS the size octave.</summary>
            bool TraceNextRing()
            {
                while (_discCursor < _discOrder.Length)
                {
                    int idx = _discOrder[_discCursor++];
                    var disc = _discs[idx];
                    _lane = disc.Lane;
                    float shrink = _rules.RingShrink > 0f ? _rules.RingShrink : 1f;
                    RingPoints(disc.Axis, disc.Rho * shrink, _ringSamples, _rules.RingFlatten, _ringPts);

                    _ringBody.Clear();
                    for (int i = 0; i < _ringPts.Count; i++)
                        _ringBody.Add(new Sample { Position = _ringPts[i], Normal = Vector3.zero });

                    // Allometric girth off the disc's own ρ — a closed figure has no run length
                    // to taper on — floored so the finest octaves stay readable at range.
                    float girth = _rules.RingGirthExponent > 0f
                        ? Mathf.Pow(disc.Rho / _rhoRef, _rules.RingGirthExponent) : 1f;
                    girth = Mathf.Max(girth, Mathf.Clamp01(_rules.RingGirthFloor));

                    _curveCount++;
                    // A ring arrives over a connector like every other curve: a STEM from the
                    // disc it is inscribed against (a disc it touches, so the stem is short), or
                    // the TRUNK out of the crystal for the first crown. The ring is then a closed
                    // chain hanging off that arrival, which is what makes the gasket ONE object
                    // rather than a spray of hoops.
                    int saveLane = _lane;
                    _lane = 0;
                    // The STEP, not the walk step — the same substitution TryDive makes, and for
                    // the same reason: a ring's chord IS the element's step by construction
                    // (RingSamplesFor), and this species authors `WalkStep` at its do-nothing
                    // value to say that NOTHING HERE WALKS. Sampling the stem at the walk step
                    // made that claim false, which measure_mandelbulb_flora.py's inert-column
                    // probe reported the moment the stem existed.
                    int connectorSegments = PrepareConnector(idx, _ringBody, _rules.StepSize);
                    _lane = saveLane;
                    _emitMarkPoint = connectorSegments;
                    Emit(_rise, _run, TryDive(idx, _ringBody), girth);
                    _seedArrival[idx] = _emitConnectorEnd >= 0 ? _emitConnectorEnd : _emitted;
                    _seedArrivalPoint[idx] = _ringBody[0].Position;
                    _pendingIndex = 0;
                    return true;
                }
                return false;
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

            /// <summary>The Watershed's seeds: the surface's saddles, each one's frame
            /// position, in the surface's farthest-point order. No Rng draw.</summary>
            void BuildSkeletonSeeds()
            {
                _saddles = _surface.Saddles();
                for (int i = 0; i < _saddles.Count; i++)
                {
                    BuildFrame(_surface, _saddles[i].ThetaF, _saddles[i].PhiF, ref _frame);
                    _seeds.Add(_frame.Position);
                }
            }

            /// <summary>Next prism, or false when the rule has nothing left to lay.</summary>
            public bool TryNext(out PrismAddress address) => TryNext(out address, out _, out _);

            public bool TryNext(out PrismAddress address, out int parent)
                => TryNext(out address, out parent, out _);

            /// <summary>
            /// The next prism, and the global index of the prism it HANGS OFF (-1 = the heart).
            /// The parent is a property of the growth ORDER rather than of the address, which is
            /// why it is handed out here and never folded into <see cref="PrismAddress"/>: a
            /// prism's POSE must stay a pure function of its own address, or the pose table stops
            /// being provable one row at a time.
            /// </summary>
            public bool TryNext(out PrismAddress address, out int parent, out bool connector)
            {
                EnsureSeeds();
                while (_pendingIndex >= _pending.Count)
                {
                    if (!TraceNextCurve())
                    {
                        address = default; parent = -1; connector = false; return false;
                    }
                }
                parent = _pendingParent[_pendingIndex];
                connector = _pendingConnector[_pendingIndex];
                address = _pending[_pendingIndex++];
                _emitted++;
                return true;
            }

            float WalkStepSize => _rules.WalkStep > 0f ? _rules.WalkStep : _rules.StepSize;

            bool TraceNextCurve()
            {
                if (Gasket) return TraceNextRing();
                int lanes = Mathf.Max(1, _rules.LanesPerSeed);
                int guard = 0;
                while (_lane < lanes && guard++ < 4096)
                {
                    if (_seedIndex >= _seeds.Count) { _seedIndex = 0; _lane++; continue; }
                    // TREE order, not table order: a seed's stem grows out of the seed it hangs
                    // off, so its parent must already carry prisms. Prim's insertion order gives
                    // that by construction (see BuildSeedTree).
                    int k = _seedOrder[_seedIndex++];

                    if (Skeleton)
                    {
                        // Four separatrices per saddle and nothing else: a lane past the fourth
                        // ends the walk rather than falling into the hop, which would band off
                        // a seed list that holds saddle positions and would draw the Rng.
                        if (_lane >= 4) { _seedIndex = _seeds.Count; continue; }
                        if (TraceSeparatrix(k)) return true;
                        continue;
                    }

                    if (_laneDead[k]) continue;

                    var points = Trace(_lanePosition[k],
                                       _laneTangentValid[k] ? _laneTangent[k] : Vector3.zero,
                                       _laneTangentValid[k], _rules.Field, WalkStepSize);

                    if (points.Count >= Mathf.Max(2, _rules.MinRun))
                    {
                        _curveCount++;
                        // What this curve HANGS OFF. Lane 0 is the seed's first appearance, so it
                        // arrives over a connector — the TRUNK out of the heart for the root seed,
                        // a STEM from its parent seed for every other. Lane L>0 starts one Hop
                        // across from lane L-1's midpoint, which is a bond a limb can span, so it
                        // hangs off the prism that was sitting there.
                        int connectorSegments = PrepareConnector(k, points, WalkStepSize);
                        int mid = points.Count / 2;
                        _emitMarkPoint = connectorSegments + mid;
                        // TryDive is asked about POINTS, not about `_run`: its descent test
                        // compares the curve's first radius with its last, and a connector
                        // prefix would answer with the STEM's start instead — a different
                        // question, on a different curve, that changes which curves earn a Fall.
                        Emit(_rise, _run, TryDive(k, points));
                        if (_seedArrival[k] < 0)
                        {
                            _seedArrival[k] = _emitConnectorEnd >= 0 ? _emitConnectorEnd : _emitted;
                            _seedArrivalPoint[k] = points[0].Position;
                        }
                        if (_emitMarkedPrism >= 0) _laneAnchor[k] = _emitMarkedPrism;
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

            /// <summary>
            /// One arm of one saddle. The lanes are INTERLEAVED — valley+, ridge+, valley−,
            /// ridge− — so every budget prefix carries both a rise and a fall at every saddle:
            /// measured with the two valley lanes first, Time's budget ran out inside lane 2
            /// and the ridge net — the part that draws the silhouette — was never laid.
            /// </summary>
            bool TraceSeparatrix(int k)
            {
                var sad = _saddles[k];
                bool ascend = (_lane & 1) == 1;               // 0,2 = valley ; 1,3 = ridge
                int sign = _lane < 2 ? +1 : -1;
                var field = ascend ? SteeringField.Ascent : SteeringField.Descent;

                // Seed tangent = ±the saddle's Hessian eigen-direction, in the saddle's own
                // tangent basis. The gradient is ZERO at a saddle so the field is undefined
                // there; the existing haveSeedTangent branch of Trace takes the first step.
                BuildFrame(_surface, sad.ThetaF, sad.PhiF, ref _frame);
                Vector2 e = ascend ? sad.ERidge : sad.EValley;
                Vector3 tan = sign * (e.x * _frame.ETheta + e.y * _frame.EPhi);

                var points = Trace(_frame.Position, tan, true, field, WalkStepSize);
                if (points.Count < Mathf.Max(2, _rules.MinRun)) return false;                 // dust
                if (_rules.MinPersistence > 0f)
                {
                    float rise = Mathf.Abs(points[points.Count - 1].Position.magnitude - points[0].Position.magnitude);
                    if (rise < _rules.MinPersistence) return false;                             // dust
                }
                _curveCount++;
                // All four arms leave the SAME saddle, so the first arm laid is what the other
                // three hang off — and the saddle itself arrives over a connector, the trunk out
                // of the heart for the root saddle and a stem from its parent for the rest. A
                // saddle whose first arm was dust has no arrival prism yet, so the next arm to
                // survive pays for the connector instead; that is why the test is on
                // `_seedArrival[k]` rather than on the lane number.
                bool first = _seedArrival == null || _seedArrival[k] < 0;
                int connectorSegments = 0;
                if (first)
                {
                    int saveLane = _lane;
                    _lane = 0;                                   // PrepareConnector's "first appearance"
                    connectorSegments = PrepareConnector(k, points, WalkStepSize);
                    _lane = saveLane;
                }
                else
                {
                    _anchorForNextEmit = _seedArrival[k];
                    _rise.Clear();
                    _run.Clear(); _run.AddRange(points);
                    _emitConnectorSegments = 0;
                }
                Emit(_rise, _run, TryDive(k, points));
                if (first)
                {
                    _seedArrival[k] = _emitConnectorEnd >= 0 ? _emitConnectorEnd : _emitted;
                    _seedArrivalPoint[k] = points[0].Position;
                }
                _pendingIndex = 0;
                return true;
            }

            /// <summary>
            /// The Fall, if this seed still owes one and this run may take it. An owed seed
            /// keeps its flag until a dive is actually appended, so a seed whose first curves
            /// are dust (or did not descend) spends its dive on its first ELIGIBLE curve.
            /// </summary>
            List<Vector3> TryDive(int k, List<Sample> points)
            {
                _dive.Clear();
                if (_rules.DiveStepFraction <= 0f || !_diveOwed[k] || _divesSpent >= _diveQuota) return _dive;
                if (_rules.DiveDescent > 0f &&
                    points[0].Position.magnitude - points[points.Count - 1].Position.magnitude < _rules.DiveDescent)
                    return _dive;
                var end = points[points.Count - 1].Position;
                var tail = end - points[points.Count - 2].Position;
                if (tail.sqrMagnitude <= 1e-18f) return _dive;
                // A ring's chord IS the element's step by construction (RingSamplesFor), so
                // the gasket hands the dive its own chord rather than the walk step — which
                // keeps WalkStep genuinely inert on this species (measured: it was read here).
                AppendDive(_dive, end, tail.normalized, Gasket ? _rules.StepSize : WalkStepSize);
                if (_dive.Count > 0) { _diveOwed[k] = false; _divesSpent++; }
                return _dive;
            }

            /// <summary>
            /// The logarithmic spiral. The heading is RE-DERIVED each step at a constant angle
            /// ψ from the inward radial — a lerp toward the radial has no stable non-zero
            /// fixed point (it either escapes or degenerates to a radial stab), where the
            /// constant angle IS the definition of a log spiral, is exactly stable and drifts
            /// nowhere. ρ = sqrt(1 − 2f cos ψ + f²) &lt; 1 by the clamp on f, so it terminates.
            /// The final step is CLIPPED onto the stop sphere, never past it (a step through
            /// the origin lands a prism on the antipode with the wrong sign of TanR).
            /// </summary>
            void AppendDive(List<Vector3> into, Vector3 p, Vector3 t, float step)
            {
                into.Clear();
                float psi = Mathf.Clamp(_rules.DiveAngleDegrees, 5f, 85f) * Mathf.Deg2Rad;
                float cps = Mathf.Cos(psi), sps = Mathf.Sin(psi);
                float f = Mathf.Min(_rules.DiveStepFraction, 0.9f * 2f * cps);
                float stop = Mathf.Max(0.01f, _rules.DiveStopRadius);
                float align = Mathf.Clamp01(_rules.DiveAxisAlign);
                bool swirl = Mathf.Abs(_rules.DiveSwirlDegrees) > 0.01f;
                float swirlCos = 0f, swirlSin = 0f;
                if (swirl)
                {
                    float a = _rules.DiveSwirlDegrees * Mathf.Deg2Rad;
                    swirlCos = Mathf.Cos(a); swirlSin = Mathf.Sin(a);
                }
                int cap = Mathf.Max(2, _rules.DiveMaxSteps);
                for (int k = 0; k < cap; k++)
                {
                    float r = p.magnitude;
                    if (r <= stop) break;
                    Vector3 rhat = p / r;
                    Vector3 u = t - rhat * Vector3.Dot(t, rhat);          // the tangential heading, kept
                    if (u.sqrMagnitude < 1e-12f)
                    {
                        u = Vector3.Cross(rhat, Vector3.forward);
                        if (u.sqrMagnitude < 1e-12f) u = Vector3.Cross(rhat, Vector3.right);
                    }
                    u = u.normalized;
                    if (align > 0f)
                    {
                        // Turn the tangential heading toward the azimuthal direction about the
                        // surface's polar axis (+z), signed to agree with the incoming heading,
                        // so every dive of the plant winds about the same axis.
                        Vector3 az = Vector3.Cross(Vector3.forward, rhat);
                        if (az.sqrMagnitude > 1e-12f)
                        {
                            az = az.normalized;
                            // The sign follows the incoming heading, with a DEAD BAND: an arm
                            // that leaves a saddle on one of the surface's mirror planes runs
                            // exactly meridionally, its azimuthal component is a rounding
                            // residual, and a bare sign test would let two implementations wind
                            // the same dive opposite ways (measured on Space's first arm).
                            if (Vector3.Dot(az, u) < -1e-3f) az = -az;
                            Vector3 blended = u * (1f - align) + az * align;
                            if (blended.sqrMagnitude > 1e-12f) u = blended.normalized;
                        }
                    }
                    t = -rhat * cps + u * sps;                             // EXACTLY ψ from the inward radial
                    if (swirl)
                        t = (t * swirlCos + Vector3.Cross(rhat, t) * swirlSin
                             + rhat * (Vector3.Dot(rhat, t) * (1f - swirlCos))).normalized;
                    float s = f * r;
                    if (_rules.DiveStrideCeiling > 0f) s = Mathf.Min(s, step * _rules.DiveStrideCeiling);
                    Vector3 q = p + t * s;
                    float rq = q.magnitude;
                    if (rq < stop)
                    {
                        q *= stop / Mathf.Max(rq, 1e-9f);
                        into.Add(q);
                        break;
                    }
                    p = q;
                    into.Add(p);
                }
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

            List<Sample> Trace(Vector3 start, Vector3 seedTangent, bool haveSeedTangent,
                               SteeringField field, float step)
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
                    bool haveField = TryFieldDirection(n, field, out Vector3 fd);

                    if (haveT)
                    {
                        Vector3 ahead = t - n * Vector3.Dot(t, n);
                        if (ahead.sqrMagnitude < 1e-16f) break;
                        ahead = ahead.normalized;
                        if (haveField)
                        {
                            if (IsBipolar(field) && Vector3.Dot(fd, ahead) < 0f) fd = -fd;
                            // A separatrix is PURE gradient flow by construction, not by the
                            // authored mix happening to be 1 (measured: the "inert" columns
                            // moved the Watershed until this was made structural).
                            want = Skeleton ? fd : Vector3.Lerp(ahead, fd, Mathf.Clamp01(_rules.FieldMix));
                        }
                        else want = ahead;
                        if (want.sqrMagnitude < 1e-16f) break;
                        want = Skeleton ? want.normalized
                                        : Vector3.Lerp(want.normalized, t, Mathf.Clamp01(_rules.Momentum));
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

                    Vector3 q = _frame.Position + t * step;
                    Spherical(q, out theta, out phi);
                }
                return _scratch;
            }

            // Ascent and Descent are UNIPOLAR: they must never be flipped to agree with the
            // running tangent, or half the arms of a species seeded ALONG them run the flow
            // backwards with every gate still green. A new unipolar field belongs here.
            static bool IsBipolar(SteeringField f)
                => f != SteeringField.Ascent && f != SteeringField.Descent;

            bool TryFieldDirection(Vector3 n, SteeringField field, out Vector3 dir)
            {
                Vector3 v;
                switch (field)
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
                if (!Skeleton && Mathf.Abs(_rules.SwirlDegrees) > 0.01f)   // a separatrix has no swirl
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

            /// <summary>
            /// Lays a run's prisms, then its dive's. The girth keys on the BODY's run length
            /// only (a diving run is not promoted to a fatter class); the dive tapers on top
            /// of it, linearly in log r so the taper is scale-invariant like the spiral.
            /// </summary>
            void Emit(List<Sample> points, List<Vector3> dive, float girthOverride = 0f)
                => Emit(_noRise, points, dive, girthOverride);

            /// <summary>
            /// One curve becomes prisms — and the chain of them is the plant's SKELETON, so this
            /// is also where a prism learns what it hangs off.
            ///
            /// <para><b>The RISE is the Fall run backwards</b> (Docs/ECOSYSTEM.md §53): free-space
            /// points laid BEFORE the surface run, carrying the curve out of the heart to the
            /// point where the surface walk begins, exactly as <c>dive</c> carries it back in
            /// afterwards. They are the same log spiral in the same family — the rise is literally
            /// <c>AppendDive</c>'s output reversed — so nothing new has to be tuned, gated or
            /// proven about its shape, and the two halves of a curve that both rises and falls
            /// cannot disagree.</para>
            ///
            /// <para>Within a batch every prism's parent is its predecessor, because a curve IS a
            /// chain; the batch's FIRST prism hangs off <c>_anchorForNextEmit</c> — the global
            /// index of an already-laid prism, or -1 for the heart itself. That is the whole of
            /// the growth law's clause (b) (every prism hangs off something that already exists);
            /// clause (a) — ONE connected object at every tick — is what the seed tree and the
            /// lane-major order buy, and what <c>connected_prefixes</c> asserts.</para>
            /// </summary>
            void Emit(List<Vector3> rise, List<Sample> points, List<Vector3> dive,
                      float girthOverride = 0f)
            {
                _pending.Clear();
                _pendingParent.Clear();
                _pendingConnector.Clear();
                _pendingIndex = 0;
                _emitConnectorEnd = -1;
                _emitMarkedPrism = -1;
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
                // the other direction. A species whose runs are structurally short (a skeleton
                // arm ends at the nearest extremum) authors GirthReference instead.
                float taper = _rules.GirthTaper <= 0f ? 1f : _rules.GirthTaper;
                float reference = _rules.GirthReference > 0f
                    ? Mathf.Max(2f, _rules.GirthReference)
                    : Mathf.Max(2f, _rules.MaxSteps * 0.5f);
                float u = Mathf.Clamp01((points.Count - 1) / reference);
                // A ring's girth is a function of its ρ, not of a run length; both walking
                // species pass nothing and take the identical expression.
                float girth = girthOverride > 0f ? girthOverride : taper + (1f - taper) * u;
                // The twist accumulates along the RUN, so a long clean curve reads as a helix
                // and a short scrap as a single tilted plate. Converted here, once per curve,
                // rather than per prism.
                float twistPerStep = _rules.TwistDegreesPerStep * Mathf.Deg2Rad;

                int nSurface = points.Count;
                int nRise = rise.Count;
                _pts.Clear();
                for (int i = 0; i < nRise; i++) _pts.Add(rise[i]);
                for (int i = 0; i < nSurface; i++) _pts.Add(points[i].Position);
                for (int i = 0; i < dive.Count; i++) _pts.Add(dive[i]);

                float gFloor = _rules.DiveGirthFloor <= 0f ? 1f : _rules.DiveGirthFloor;
                float lgLo = Mathf.Log(Mathf.Max(1e-6f, Mathf.Max(0.01f, _rules.DiveStopRadius)));
                float lgHi = Mathf.Log(Mathf.Max(1e-6f, points[nSurface - 1].Position.magnitude));
                float diveRoll0 = 0f;
                // The rise pays the SAME seam the Fall does, and pays it UP FRONT: its prisms are
                // emitted BEFORE the transition that defines the angle, so it cannot be computed
                // where the dive's is. Same expression, same meaning — the signed angle about the
                // shared heading between the plate hung off the RAY (free space) and the plate hung
                // off the NORMAL (the surface), so the face is continuous where the curve arrives.
                float riseRoll0 = 0f;
                if (nRise > 0)
                {
                    Vector3 ra = _pts[nRise - 1], rb = points[0].Position;
                    Vector3 rd = rb - ra;
                    if (rd.sqrMagnitude > 1e-14f)
                    {
                        Vector3 rfwd = rd.normalized;
                        Spherical((ra + rb) * 0.5f, out float rth, out float rph);
                        BuildFrame(_surface, rth, rph, ref _frame);
                        Vector3 upRay = _frame.Dir - rfwd * Vector3.Dot(_frame.Dir, rfwd);
                        Vector3 upSurf = _frame.Normal - rfwd * Vector3.Dot(_frame.Normal, rfwd);
                        if (upRay.sqrMagnitude > 1e-10f && upSurf.sqrMagnitude > 1e-10f)
                        {
                            upRay = upRay.normalized; upSurf = upSurf.normalized;
                            riseRoll0 = Mathf.Atan2(Vector3.Dot(Vector3.Cross(upRay, upSurf), rfwd),
                                                    Vector3.Dot(upRay, upSurf));
                        }
                    }
                }

                for (int i = 0; i + 1 < _pts.Count; i++)
                {
                    Vector3 a = _pts[i], b = _pts[i + 1];
                    Vector3 delta = b - a;
                    float len = delta.magnitude;
                    if (len < 1e-7f) continue;
                    Vector3 fwd = delta / len;
                    Vector3 centre = (a + b) * 0.5f;

                    Spherical(centre, out float th, out float ph);
                    BuildFrame(_surface, th, ph, ref _frame);
                    float cm = centre.magnitude;

                    // The TRANSITION prism (last surface point -> first dive point) is a dive
                    // prism: its heading genuinely leaves the surface.
                    bool isDive = dive.Count > 0 && i >= nRise + nSurface - 1;
                    bool isRise = i < nRise;
                    float tanR, dv, off, g = girth, roll = i * twistPerStep;
                    if (isRise)
                    {
                        // Free space, exactly as a dive prism is: the plate hangs off the RAY it
                        // shares with its surface point rather than off a surface normal it is
                        // nowhere near, and it thins toward the heart on the same log ramp.
                        tanR = Vector3.Dot(fwd, _frame.Dir);
                        dv = 1f - cm / Mathf.Max(1e-6f, _frame.Radius);
                        off = 0f;
                        float wr = lgHi <= lgLo ? 1f
                                 : Mathf.Clamp01((Mathf.Log(Mathf.Max(1e-6f, cm)) - lgLo) / (lgHi - lgLo));
                        g = girth * (gFloor + (1f - gFloor) * wr);
                        roll += riseRoll0;
                    }
                    else if (isDive)
                    {
                        tanR = Vector3.Dot(fwd, _frame.Dir);
                        dv = 1f - cm / Mathf.Max(1e-6f, _frame.Radius);
                        off = 0f;
                        float w = lgHi <= lgLo ? 1f
                                : Mathf.Clamp01((Mathf.Log(Mathf.Max(1e-6f, cm)) - lgLo) / (lgHi - lgLo));
                        g = girth * (gFloor + (1f - gFloor) * w);
                        if (i == nSurface - 1)
                        {
                            // Pay the SEAM once per curve: Pose hangs a dive plate off the RAY, the
                            // surface plate it follows hangs off the NORMAL, and the signed angle
                            // between the two about the shared heading is added to every dive
                            // prism's roll so the face is continuous across the release. Stored
                            // in Roll, which the address already carries and Pose already applies.
                            Vector3 upRay = _frame.Dir - fwd * Vector3.Dot(_frame.Dir, fwd);
                            Vector3 upSurf = _frame.Normal - fwd * Vector3.Dot(_frame.Normal, fwd);
                            if (upRay.sqrMagnitude > 1e-10f && upSurf.sqrMagnitude > 1e-10f)
                            {
                                upRay = upRay.normalized; upSurf = upSurf.normalized;
                                diveRoll0 = Mathf.Atan2(Vector3.Dot(Vector3.Cross(upRay, upSurf), fwd),
                                                        Vector3.Dot(upRay, upSurf));
                            }
                        }
                        roll += diveRoll0;
                    }
                    else
                    {
                        // Radial lift: centre and its surface point are on the same ray.
                        tanR = 0f; dv = 0f; off = cm - _frame.Radius;
                    }

                    // A curve IS a chain, so a prism's parent is its predecessor in this batch;
                    // the batch's first hangs off whatever the caller anchored it to.
                    _pendingParent.Add(_pending.Count == 0
                                       ? _anchorForNextEmit
                                       : _emitted + _pending.Count - 1);
                    // A CONNECTOR prism — a trunk's rise or a stem — rather than one of the
                    // curve's own. Handed out beside the parent for the same reason: it is a
                    // property of the growth ORDER, not of the address, and a consumer needs
                    // it because a LIMB has to yield to a ribbon it crosses where a ribbon
                    // crossing a ribbon is simply what a cage is.
                    _pendingConnector.Add(i < _emitConnectorSegments);
                    int here = _emitted + _pending.Count;
                    if (i < _emitConnectorSegments) _emitConnectorEnd = here;
                    if (_emitMarkedPrism < 0 && i >= _emitMarkPoint) _emitMarkedPrism = here;
                    _pending.Add(new PrismAddress(
                        th, ph, off, dv,
                        Vector3.Dot(fwd, _frame.ETheta),
                        Vector3.Dot(fwd, _frame.EPhi), tanR,
                        len * Mathf.Max(0.05f, _rules.LengthFactor),
                        g, roll, _curveCount, _lane));
                }
                // A request is for ONE batch. Left standing, a stale split would make the next
                // curve's prisms read as somebody's connector.
                _emitConnectorSegments = 0;
                _emitMarkPoint = int.MaxValue;
            }

            /// <summary>
            /// Decide what a curve hangs off, and build the run that gets it there.
            ///
            /// <para>Returns the number of leading SEGMENTS that are connector rather than curve —
            /// what <c>Emit</c> needs in order to hand back the index of the prism that ARRIVES at
            /// the seed, which is what the seed's later lanes and its child seeds hang off.</para>
            /// </summary>
            int PrepareConnector(int k, List<Sample> points, float step)
            {
                _rise.Clear();
                _stemRun.Clear();

                if (_laneAnchor[k] >= 0)
                {
                    // A later lane is one Hop across from this seed's previous run. That hop IS
                    // the bond; the prism at its origin was marked when that run was emitted.
                    _anchorForNextEmit = _laneAnchor[k];
                    _run.Clear(); _run.AddRange(points);
                    _emitConnectorSegments = 0;
                    return 0;
                }

                // THIS SEED'S FIRST APPEARANCE. Usually lane 0 — but a seed whose early curves
                // were all dust makes its first appearance on a later lane, and the test has to be
                // "have I laid anything yet" rather than "is this lane 0" or that seed starts a
                // second plant. Measured on the first cut: 3-33 roots per plant, every one of them
                // a seed whose tree parent laid nothing.
                int from = AttachmentSeed(k);
                if (from >= 0)
                {
                    BuildStem(_seedArrivalPoint[from], points[0].Position, step);
                    _anchorForNextEmit = _seedArrival[from];
                }
                else
                {
                    // THE TRUNK. Exactly one curve per plant takes this branch — the first to
                    // survive, when nothing else is standing yet — and it is the only prism in
                    // the plant whose parent is the crystal itself.
                    Vector3 t0 = _laneTangentValid[k] ? _laneTangent[k] : Vector3.zero;
                    BuildTrunk(points[0].Position, t0, step);
                    _anchorForNextEmit = -1;
                }

                _run.Clear();
                _run.AddRange(_stemRun);
                _run.AddRange(points);
                int connectorSegments = _rise.Count + _stemRun.Count;
                _emitConnectorSegments = connectorSegments;
                return connectorSegments;
            }

            /// <summary>
            /// Which already-standing seed this one grows out of.
            ///
            /// <para>Its TREE parent when that parent is standing — for the walking species the
            /// nearest seed by great-circle distance, for Apollonia the disc it is literally
            /// inscribed against — because that is the shortest bond the species knows about and
            /// the one that carries meaning. When it is not standing (its curves were dust, or
            /// the budget stopped before it), the NEAREST seed that is: a plant may not start a
            /// second component just because one branch failed, and proximity is the only thing
            /// left that keeps the replacement bond short.</para>
            ///
            /// <para>-1 only while nothing at all is standing, which happens exactly once per
            /// plant and is what the trunk is for.</para>
            /// </summary>
            int AttachmentSeed(int k)
            {
                if (_seedArrival == null) return -1;
                int parent = _seedParent != null && k < _seedParent.Length ? _seedParent[k] : -1;
                if (parent >= 0 && parent < _seedArrival.Length && _seedArrival[parent] >= 0) return parent;

                Vector3 me = k < _seeds.Count ? _seeds[k] : Vector3.up;
                Vector3 md = me.sqrMagnitude > 1e-18f ? me.normalized : Vector3.up;
                int best = -1; float bestCos = -2f;
                for (int i = 0; i < _seedArrival.Length && i < _seeds.Count; i++)
                {
                    if (i == k || _seedArrival[i] < 0) continue;
                    Vector3 d = _seeds[i];
                    if (d.sqrMagnitude < 1e-18f) continue;
                    float c = Vector3.Dot(md, d.normalized);
                    // Tolerant + lowest index, for the reason SeedCostEpsilon records: the
                    // fallback runs over a symmetric seed set too.
                    if (c > bestCos + SeedCostEpsilon) { bestCos = c; best = i; }
                }
                return best;
            }

            // ── THE SEED TREE ────────────────────────────────────────────────────────────────

            /// <summary>
            /// A spanning tree over the seeds, rooted at the one the plant's TRUNK climbs to.
            ///
            /// <para>Prim's algorithm on great-circle distance between the seeds' own directions,
            /// which is the right metric because every seed lies on the same star-shaped surface:
            /// two seeds close in angle are close on the membrane, and the stem between them is
            /// short. The root is seed 0 — for the walking species the first point the
            /// well-spread generator emits, for the Watershed the first saddle in farthest-point
            /// order, for Apollonia the largest disc — so in every case the trunk climbs to the
            /// most prominent feature the species knows about rather than to an arbitrary one.</para>
            ///
            /// <para><b>Prim's insertion order IS a valid growth order</b>: it only ever admits a
            /// seed whose parent is already in the tree, so <c>_seedOrder</c> needs no separate
            /// sort and `parent appears earlier than child` holds by construction rather than by
            /// assertion. That is the same property the Borromean table gets from its hop
            /// ordering (Docs/ECOSYSTEM.md §49) and the reason neither species needs a topological
            /// pass it could get wrong.</para>
            /// </summary>
            /// <summary>
            /// How close two seed-tree costs have to be before the INDEX decides.
            ///
            /// <para>A Mandelbulb is highly symmetric, so its seeds come in ORBITS whose
            /// members sit at distances that are equal to the last bit — measured on Space's
            /// Watershed, four saddles were exactly 0.847114625 from the tree so far, and which
            /// one Prim admitted first was then decided by float WIDTH: the shipped float32 and
            /// the offline float64 model picked different ones and grew visibly different plants
            /// from the same seed. That is the same trap Apollonia's lay order records
            /// (Docs/ECOSYSTEM.md §54: every ordering is a TOTAL key), met from a second
            /// direction — and it is invisible to every statistical gate, because both plants
            /// are perfectly good plants.
            ///
            /// <para>1e-5 is far above float32's ~1e-7 resolution on a unit dot product and far
            /// below any real gap the seed sets produce (the nearest genuine competitor in that
            /// measurement was 0.14 away), so it separates "the same distance" from "a different
            /// distance" rather than papering over one.</para>
            /// </summary>
            const float SeedCostEpsilon = 1e-5f;

            void BuildSeedTree()
            {
                if (Gasket) { BuildGasketTree(); return; }
                int n = _seeds.Count;
                _seedParent = new int[n];
                _seedOrder = new int[n];
                _seedArrival = new int[n];
                _laneAnchor = new int[n];
                _seedArrivalPoint = new Vector3[n];
                for (int i = 0; i < n; i++) { _seedParent[i] = -1; _seedArrival[i] = -1; _laneAnchor[i] = -1; }
                if (n == 0) return;

                var dir = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    var v = _seeds[i];
                    dir[i] = v.sqrMagnitude > 1e-18f ? v.normalized : Vector3.up;
                }

                var inTree = new bool[n];
                var best = new float[n];
                var bestFrom = new int[n];
                for (int i = 0; i < n; i++) { best[i] = float.MaxValue; bestFrom[i] = 0; }

                inTree[0] = true;
                _seedOrder[0] = 0;
                _seedParent[0] = -1;
                for (int i = 1; i < n; i++)
                {
                    best[i] = -Vector3.Dot(dir[0], dir[i]);   // monotone in great-circle distance
                    bestFrom[i] = 0;
                }

                for (int placed = 1; placed < n; placed++)
                {
                    int pick = -1; float pickCost = float.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        // Strictly cheaper by more than the epsilon, or tied and earlier in the
                        // table. A bare `<` makes the winner of a symmetry tie a property of
                        // float width (see SeedCostEpsilon).
                        if (inTree[i]) continue;
                        if (pick >= 0 && best[i] >= pickCost - SeedCostEpsilon) continue;
                        pick = i; pickCost = best[i];
                    }
                    // Degenerate seed sets (coincident directions) can leave every candidate at
                    // the same cost; take the first free index rather than stalling.
                    if (pick < 0) { for (int i = 0; i < n && pick < 0; i++) if (!inTree[i]) pick = i; }
                    if (pick < 0) break;

                    inTree[pick] = true;
                    _seedParent[pick] = bestFrom[pick];
                    _seedOrder[placed] = pick;
                    for (int i = 0; i < n; i++)
                    {
                        if (inTree[i]) continue;
                        float c = -Vector3.Dot(dir[pick], dir[i]);
                        // Must IMPROVE by more than the epsilon: a tie keeps the parent already
                        // recorded, which is the earlier seed and therefore the same on both
                        // implementations.
                        if (c < best[i] - SeedCostEpsilon) { best[i] = c; bestFrom[i] = pick; }
                    }
                }
            }

            /// <summary>
            /// The gasket's tree is the GASKET, which is the whole reason this species is the
            /// self-similar one: a child disc is inscribed in the gap between three discs it
            /// touches, so it already knows what it grows out of and the plant's skeleton is the
            /// tangency graph rather than anything imposed on it. Only the level-0 crowns need a
            /// rule, and theirs is the cheapest one that cannot get the order wrong — each
            /// attaches to the NEAREST crown laid before it, so the first (the largest ρ, which
            /// is where the trunk climbs) roots the plant and every later one is a short hop from
            /// a crown that already exists.
            /// </summary>
            void BuildGasketTree()
            {
                int n = _seeds.Count;
                _seedParent = new int[n];
                _seedOrder = new int[n];
                _seedArrival = new int[n];
                _laneAnchor = new int[n];
                _seedArrivalPoint = new Vector3[n];
                for (int i = 0; i < n; i++) { _seedParent[i] = -1; _seedArrival[i] = -1; _laneAnchor[i] = -1; }
                if (n == 0 || _discOrder == null) return;

                var placedCrowns = new List<int>(16);
                for (int r = 0; r < _discOrder.Length; r++)
                {
                    int d = _discOrder[r];
                    _seedOrder[r] = d;
                    if (d >= n) continue;
                    if (_discs[d].Level != 0) { _seedParent[d] = _discs[d].Parent; continue; }

                    int best = -1; float bestCos = -2f;
                    for (int i = 0; i < placedCrowns.Count; i++)
                    {
                        float c = Vector3.Dot(_discs[d].Axis, _discs[placedCrowns[i]].Axis);
                        // The crowns sit on the bulb's PEAKS, which are the most symmetric points
                        // it has, so this search is the most tie-prone of the three.
                        if (c > bestCos + SeedCostEpsilon) { bestCos = c; best = placedCrowns[i]; }
                    }
                    _seedParent[d] = best;                    // -1 for the first crown: the trunk
                    placedCrowns.Add(d);
                }
            }

            /// <summary>
            /// THE TRUNK — the Fall run backwards, from the heart out to the root seed.
            /// <c>AppendDive</c> walks a log spiral from a surface point down to the heart; the
            /// same points read the other way are a climb out of it. Reusing the descent verbatim
            /// is the whole point: the rise cannot acquire a shape, a stride ceiling or a winding
            /// the Fall does not already have, and a curve that both rises and falls is provably
            /// one family of curve rather than two that have to be kept in step.
            /// </summary>
            void BuildTrunk(Vector3 seedPoint, Vector3 seedTangent, float step)
            {
                AppendDive(_dive, seedPoint, seedTangent, step);
                _rise.Clear();
                // Reversed, and WITHOUT the seed itself: the curve's own first point is the seed,
                // and a duplicated point is a zero-length segment Emit would drop anyway.
                for (int i = _dive.Count - 1; i >= 0; i--) _rise.Add(_dive[i]);
                _dive.Clear();
            }

            /// <summary>
            /// A STEM — the surface path from one seed to the next, sampled at the walk step.
            /// It is a slerp of the two directions evaluated ON the membrane, so every point of
            /// it is a genuine surface point and its prisms are ordinary plates rather than
            /// free-space ones. The final point is left OFF: the curve that follows starts at the
            /// seed, and the stem's last segment is what arrives there.
            /// </summary>
            void BuildStem(Vector3 from, Vector3 to, float step)
            {
                _stemRun.Clear();
                Vector3 a = from.sqrMagnitude > 1e-18f ? from.normalized : Vector3.up;
                Vector3 b = to.sqrMagnitude > 1e-18f ? to.normalized : Vector3.up;
                float cos = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);
                float ang = Mathf.Acos(cos);
                float arc = ang * Mathf.Max(1e-4f, (from.magnitude + to.magnitude) * 0.5f);
                // +1 rather than a ceiling, and no `Vector3.Slerp`: both of those are engine
                // calls this file would then be proved against a STUB of, and a stub that
                // differs from Unity in its last bits is a divergence no gate here could see.
                // The great-circle interpolation is three lines of the arithmetic already in
                // this file, so the harness proves the shipped expression itself.
                int steps = Mathf.Clamp((int)(arc / Mathf.Max(1e-4f, step)) + 1, 1, 256);
                float sinAng = Mathf.Sin(ang);
                for (int i = 0; i < steps; i++)
                {
                    float t = (float)i / steps;
                    Vector3 d = sinAng > 1e-5f
                        ? a * (Mathf.Sin((1f - t) * ang) / sinAng) + b * (Mathf.Sin(t * ang) / sinAng)
                        : a * (1f - t) + b * t;                       // antipodal-safe / near-parallel
                    if (d.sqrMagnitude < 1e-18f) continue;
                    d = d.normalized;
                    Spherical(d, out float th, out float ph);
                    BuildFrame(_surface, th, ph, ref _frame);
                    _stemRun.Add(new Sample { Position = _frame.Dir * _frame.Radius, Normal = _frame.Normal });
                }
            }
        }
    }
}
