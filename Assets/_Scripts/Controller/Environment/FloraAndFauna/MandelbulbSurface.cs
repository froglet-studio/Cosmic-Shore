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

            // ── the surface's own critical points (the WATERSHED's seeds) ───────────
            //
            // A property of the SURFACE, not of the plant: every plant sharing an (element,
            // weights) pair shares it, so it is computed LAZILY on first access and cached
            // here beside the field. Nothing in the two free-walking species ever asks for
            // it, so they never pay for it.
            List<CriticalPoint> _critical;
            List<CriticalPoint> _saddles;

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
        // The WATERSHED species (Docs/ECOSYSTEM.md §47) grows the surface's own Morse–Smale
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
        /// belt defect §44.5 exists to forbid; farthest-point gives 7–8 of 8 at every prefix.
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
                                              // built on this rule (Docs/ECOSYSTEM.md §46).

            // ── THE FALL — the radial dive every species authors (Docs/ECOSYSTEM.md §47) ──
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

            // ── THE WATERSHED (Docs/ECOSYSTEM.md §47) ──
            public int SkeletonSeeds;       // 1 = seeds are the surface's SADDLES and lanes 0–3 are
                                            // their four separatrices; 0 = the free walk above.
            public float WalkStep;          // sampling step of the skeleton walk when > 0; 0 = StepSize.
                                            // §45 derives StepSize per element while a skeleton's cost
                                            // is fixed by the SURFACE, so the two are decoupled here
                                            // and LengthFactor is what makes them agree again.
            public float MinPersistence;    // an arm whose |R_end − R_start| is below this is dust
            public float GirthReference;    // the run length that earns full girth. 0 = MaxSteps/2 —
                                            // meaningless for a skeleton whose longest arm is 29
                                            // steps (§44's "a ceiling nothing reaches" defect).
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
            position = scratch.Dir * (scratch.Radius * (1f - a.Dive) + a.RadialOffset);
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

            int _seedIndex, _lane, _pendingIndex, _curveCount;

            public int CurvesTraced => _curveCount;
            public int SeedCount { get { EnsureSeeds(); return _seeds.Count; } }
            public int DivesSpent => _divesSpent;
            public bool Skeleton => _rules.SkeletonSeeds != 0;

            public Growth(Surface surface, in GrowthRules rules, int seed)
            {
                _surface = surface;
                _rules = rules;
                _rng = new Rng(seed * 2654435761u.GetHashCode() ^ 0x5bf03635);
                if (!Skeleton) EnsureSeeds();
            }

            void EnsureSeeds()
            {
                if (_seedsBuilt) return;
                _seedsBuilt = true;
                if (Skeleton) BuildSkeletonSeeds(); else BuildSeeds();
                int n = Mathf.Max(1, _seeds.Count);
                _lanePosition = new Vector3[n];
                _laneTangent = new Vector3[n];
                _laneTangentValid = new bool[n];
                _laneDead = new bool[n];
                for (int i = 0; i < _seeds.Count; i++) _lanePosition[i] = _seeds[i];

                // The Fall's owed set is STRIDED, never a prefix: BuildSeeds emits in Fibonacci
                // order, which is z-monotone, so a prefix would put every dive in a polar cap
                // (§44.5's defect for a new consumer). Same idiom BuildSeeds uses for `want`.
                _diveOwed = new bool[n];
                _divesSpent = 0;
                _diveQuota = Mathf.Min(Mathf.Max(0, _rules.DiveCount), _seeds.Count);
                if (_rules.DiveStepFraction > 0f && _diveQuota > 0)
                    for (int d = 0; d < _diveQuota; d++)
                        _diveOwed[(int)((long)d * _seeds.Count / _diveQuota)] = true;
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
            public bool TryNext(out PrismAddress address)
            {
                EnsureSeeds();
                while (_pendingIndex >= _pending.Count)
                {
                    if (!TraceNextCurve()) { address = default; return false; }
                }
                address = _pending[_pendingIndex++];
                return true;
            }

            float WalkStepSize => _rules.WalkStep > 0f ? _rules.WalkStep : _rules.StepSize;

            bool TraceNextCurve()
            {
                int lanes = Mathf.Max(1, _rules.LanesPerSeed);
                int guard = 0;
                while (_lane < lanes && guard++ < 4096)
                {
                    if (_seedIndex >= _seeds.Count) { _seedIndex = 0; _lane++; continue; }
                    int k = _seedIndex++;

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
                        Emit(points, TryDive(k, points));
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
                Emit(points, TryDive(k, points));
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
                AppendDive(_dive, end, tail.normalized, WalkStepSize);
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

            /// <summary>
            /// Lays a run's prisms, then its dive's. The girth keys on the BODY's run length
            /// only (a diving run is not promoted to a fatter class); the dive tapers on top
            /// of it, linearly in log r so the taper is scale-invariant like the spiral.
            /// </summary>
            void Emit(List<Sample> points, List<Vector3> dive)
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
                // the other direction. A species whose runs are structurally short (a skeleton
                // arm ends at the nearest extremum) authors GirthReference instead.
                float taper = _rules.GirthTaper <= 0f ? 1f : _rules.GirthTaper;
                float reference = _rules.GirthReference > 0f
                    ? Mathf.Max(2f, _rules.GirthReference)
                    : Mathf.Max(2f, _rules.MaxSteps * 0.5f);
                float u = Mathf.Clamp01((points.Count - 1) / reference);
                float girth = taper + (1f - taper) * u;
                // The twist accumulates along the RUN, so a long clean curve reads as a helix
                // and a short scrap as a single tilted plate. Converted here, once per curve,
                // rather than per prism.
                float twistPerStep = _rules.TwistDegreesPerStep * Mathf.Deg2Rad;

                int nSurface = points.Count;
                _pts.Clear();
                for (int i = 0; i < nSurface; i++) _pts.Add(points[i].Position);
                for (int i = 0; i < dive.Count; i++) _pts.Add(dive[i]);

                float gFloor = _rules.DiveGirthFloor <= 0f ? 1f : _rules.DiveGirthFloor;
                float lgLo = Mathf.Log(Mathf.Max(1e-6f, Mathf.Max(0.01f, _rules.DiveStopRadius)));
                float lgHi = Mathf.Log(Mathf.Max(1e-6f, points[nSurface - 1].Position.magnitude));
                float diveRoll0 = 0f;

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
                    bool isDive = dive.Count > 0 && i >= nSurface - 1;
                    float tanR, dv, off, g = girth, roll = i * twistPerStep;
                    if (isDive)
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

                    _pending.Add(new PrismAddress(
                        th, ph, off, dv,
                        Vector3.Dot(fwd, _frame.ETheta),
                        Vector3.Dot(fwd, _frame.EPhi), tanR,
                        len * Mathf.Max(0.05f, _rules.LengthFactor),
                        g, roll, _curveCount, _lane));
                }
            }
        }
    }
}
