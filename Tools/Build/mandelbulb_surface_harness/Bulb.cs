// OFFLINE REFERENCE ONLY — none of this ships.
//
// The Mandelbulb is evaluated exactly ONCE, here, to bake the spherical-harmonic basis
// the game reads. The runtime never touches a fractal: MandelbulbSurface.Reconstruct
// turns 169 floats into a height field and MandelbulbSurface.Growth walks it.
//
// It lives in C# rather than in the Python model for one reason — 16 ray-marched fields
// at 256x128 is ~500k rays, which is seconds here and minutes in interpreted Python with
// no numpy in this environment. The PYTHON model
// (Tools/Build/measure_mandelbulb_flora.py) independently transcribes the GROWTH RULE,
// which is the part that actually runs in the game; the table this file bakes is proven
// by re-baking it (verify_mandelbulb_flora_tables.py), not by transcription.
using System;

static class Bulb
{
    // ── the field ─────────────────────────────────────────────────────────────────
    // Triplex power map z -> z^n + c. juliaMode=false makes c the sample point (the
    // Mandelbulb proper); true makes c a constant (a Julia set of the same map), which
    // is the family the morph moves through.
    // mandel=true makes c the sample point PLUS the offset (the Mandelbulb proper, nudged);
    // false makes c the offset alone (a Julia set of the same map). Both families morph
    // through the same three numbers, which is the only property the runtime cares about.
    public static bool Mandel = true;

    public static bool Escapes(double px, double py, double pz, double cx, double cy, double cz,
                               double power, int iterations, double bailout, out double de)
    {
        if (Mandel) { cx += px; cy += py; cz += pz; }
        double x = px, y = py, z = pz;
        double dr = 1.0;
        double r = 0.0;
        for (int i = 0; i < iterations; i++)
        {
            r = Math.Sqrt(x * x + y * y + z * z);
            if (r > bailout) break;
            dr = Math.Pow(r, power - 1.0) * power * dr + 1.0;
            double theta = Math.Acos(Math.Max(-1.0, Math.Min(1.0, z / Math.Max(r, 1e-12))));
            double phi = Math.Atan2(y, x);
            double zr = Math.Pow(r, power);
            theta *= power; phi *= power;
            double st = Math.Sin(theta);
            x = zr * st * Math.Cos(phi) + cx;
            y = zr * st * Math.Sin(phi) + cy;
            z = zr * Math.Cos(theta) + cz;
        }
        de = 0.5 * Math.Log(Math.Max(r, 1e-12)) * r / Math.Max(dr, 1e-12);
        return r > bailout;
    }

    /// <summary>
    /// R(theta,phi): the outermost radius along each ray at which the set is reached.
    /// Marched inward with the distance estimator, then bisected. Rays that never hit
    /// land on `floor`, which is what produces the cliffs the class doc records — the
    /// fit does not converge across them and does not need to.
    /// </summary>
    public static float[] March(int w, int h, double power, double cx, double cy, double cz,
                                int iterations, double bailout, double start, double floor)
    {
        var r = new float[w * h];
        for (int j = 0; j < h; j++)
        {
            double th = (j + 0.5) / h * Math.PI;
            double st = Math.Sin(th), ct = Math.Cos(th);
            for (int i = 0; i < w; i++)
            {
                double ph = (double)i / w * 2.0 * Math.PI;
                double dx = st * Math.Cos(ph), dy = st * Math.Sin(ph), dz = ct;
                double t = start;
                double hit = -1.0;
                for (int s = 0; s < 512 && t > floor; s++)
                {
                    bool esc = Escapes(dx * t, dy * t, dz * t, cx, cy, cz, power, iterations, bailout, out double de);
                    if (!esc) { hit = t; break; }
                    double step = Math.Max(de * 0.8, 1e-3);
                    t -= Math.Min(step, 0.05);
                }
                if (hit < 0.0) { r[j * w + i] = (float)floor; continue; }
                double lo = hit, hi = Math.Min(start, hit + 0.05);
                for (int b = 0; b < 24; b++)
                {
                    double mid = 0.5 * (lo + hi);
                    if (Escapes(dx * mid, dy * mid, dz * mid, cx, cy, cz, power, iterations, bailout, out _)) hi = mid;
                    else lo = mid;
                }
                r[j * w + i] = (float)lo;
            }
        }
        return r;
    }

    /// <summary>
    /// Gaussian blur on the (theta,phi) lattice with the phi width scaled by 1/sin(theta),
    /// so the kernel is ISOTROPIC on the sphere rather than on the lattice. Separable.
    /// </summary>
    public static float[] Smooth(float[] src, int w, int h, double sigma)
    {
        if (sigma <= 1e-6) return (float[])src.Clone();
        double dTh = Math.PI / h, dPh = 2.0 * Math.PI / w;
        var tmp = new float[w * h];
        for (int j = 0; j < h; j++)
        {
            double th = (j + 0.5) / h * Math.PI;
            double st = Math.Max(Math.Sin(th), 0.05);
            double sPix = sigma / (dPh * st);
            int rad = (int)Math.Ceiling(sPix * 3.0);
            if (rad < 1) { Array.Copy(src, j * w, tmp, j * w, w); continue; }
            rad = Math.Min(rad, w / 2);
            var k = new double[2 * rad + 1];
            double sum = 0.0;
            for (int d = -rad; d <= rad; d++) { k[d + rad] = Math.Exp(-0.5 * d * d / (sPix * sPix)); sum += k[d + rad]; }
            for (int i = 0; i < w; i++)
            {
                double acc = 0.0;
                for (int d = -rad; d <= rad; d++) acc += k[d + rad] * src[j * w + ((i + d) % w + w) % w];
                tmp[j * w + i] = (float)(acc / sum);
            }
        }
        var outR = new float[w * h];
        {
            double sPix = sigma / dTh;
            int rad = Math.Max(1, Math.Min((int)Math.Ceiling(sPix * 3.0), h / 2));
            var k = new double[2 * rad + 1];
            double sum = 0.0;
            for (int d = -rad; d <= rad; d++) { k[d + rad] = Math.Exp(-0.5 * d * d / (sPix * sPix)); sum += k[d + rad]; }
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    double acc = 0.0, wt = 0.0;
                    for (int d = -rad; d <= rad; d++)
                    {
                        int jj = j + d;
                        if (jj < 0 || jj >= h) continue;   // clamp at the poles rather than wrap
                        acc += k[d + rad] * tmp[jj * w + i];
                        wt += k[d + rad];
                    }
                    outR[j * w + i] = (float)(acc / Math.Max(wt, 1e-9));
                }
        }
        return outR;
    }

    /// <summary>
    /// Separable SH fit: a DFT in phi per row, then a sin(theta)-weighted quadrature in
    /// theta against the SAME K(l,m)P(l,m) the shipped MandelbulbSurface uses, so the fit
    /// and the reconstruction can never disagree about a convention.
    /// </summary>
    public static float[] Fit(float[] field, int w, int h, int degree)
    {
        int stride = degree + 1;
        var norm = CosmicShore.Gameplay.MandelbulbSurface.NormTable(degree);
        var p = new double[stride * stride];
        var coeffs = new double[(degree + 1) * (degree + 1)];
        var cosM = new double[stride];
        var sinM = new double[stride];
        const double Root2 = 1.4142135623730951;
        double dTh = Math.PI / h, dPh = 2.0 * Math.PI / w;

        for (int j = 0; j < h; j++)
        {
            double th = (j + 0.5) / h * Math.PI;
            double wt = Math.Sin(th) * dTh * dPh;
            CosmicShore.Gameplay.MandelbulbSurface.LegendreTable(degree, Math.Cos(th), p);
            Array.Clear(cosM, 0, cosM.Length);
            Array.Clear(sinM, 0, sinM.Length);
            for (int i = 0; i < w; i++)
            {
                double ph = (double)i / w * 2.0 * Math.PI;
                double v = field[j * w + i];
                for (int m = 0; m <= degree; m++)
                {
                    cosM[m] += v * Math.Cos(m * ph);
                    sinM[m] += v * Math.Sin(m * ph);
                }
            }
            for (int l = 0; l <= degree; l++)
                for (int m = 0; m <= l; m++)
                {
                    double b = norm[l * stride + m] * p[l * stride + m] * wt;
                    if (m == 0) coeffs[l * (l + 1)] += b * cosM[0];
                    else
                    {
                        coeffs[l * (l + 1) + m] += Root2 * b * cosM[m];
                        coeffs[l * (l + 1) - m] += Root2 * b * sinM[m];
                    }
                }
        }
        var outC = new float[coeffs.Length];
        for (int i = 0; i < coeffs.Length; i++) outC[i] = (float)coeffs[i];
        return outC;
    }
}
