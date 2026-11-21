// Hierarchical ecology (round 11f, Docs/ECOLOGY_LOD.md): the deterministic sampler every macro count moves by.
// Pure C# (no UnityEngine): compiled and RUN headless by Tools/Build/ecology_lod_harness.
//
// The research (Tools/Ecology/hierarchy/macro.py) moves every count by a numpy Binomial / Poisson / multinomial /
// multivariate-hypergeometric draw, so "a region of 3 grazers behaves like 3 grazers". This is the same set of draws
// on one xoshiro256** stream: deterministic per seed (the macro state is what a server would replicate), no
// allocation, and every draw returns an INTEGER that is exactly inside its support - so a count can never go
// negative or exceed what it is drawn from, which is half of the conservation gate.
using System;

namespace CosmicShore.Gameplay
{
    public sealed class EcologyRng
    {
        ulong _s0, _s1, _s2, _s3;
        double _spare; bool _hasSpare;

        public EcologyRng(ulong seed) => Reseed(seed);

        /// <summary>Restart the stream (tests: one starting state, then per-seed runs - research make()).</summary>
        public void Reseed(ulong seed)
        {
            _hasSpare = false;
            // splitmix64 expands the seed (xoshiro must not start all-zero)
            ulong x = seed + 0x9E3779B97F4A7C15UL;
            _s0 = Mix(ref x); _s1 = Mix(ref x); _s2 = Mix(ref x); _s3 = Mix(ref x);
        }

        static ulong Mix(ref ulong x)
        {
            ulong z = (x += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        public ulong NextU64()
        {
            ulong r = Rotl(_s1 * 5, 7) * 9, t = _s1 << 17;
            _s2 ^= _s0; _s3 ^= _s1; _s1 ^= _s2; _s0 ^= _s3; _s2 ^= t; _s3 = Rotl(_s3, 45);
            return r;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double Uniform() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);

        public double Uniform(double a, double b) => a + (b - a) * Uniform();

        /// <summary>Uniform integer in [0, n).</summary>
        public int Int(int n) => n <= 1 ? 0 : (int)Math.Min(n - 1, (long)(Uniform() * n));

        public double Normal()
        {
            if (_hasSpare) { _hasSpare = false; return _spare; }
            double u1 = 1.0 - Uniform(), u2 = Uniform();
            double r = Math.Sqrt(-2.0 * Math.Log(u1)), th = 2.0 * Math.PI * u2;
            _spare = r * Math.Sin(th); _hasSpare = true;
            return r * Math.Cos(th);
        }

        public double Normal(double mean, double sd) => mean + sd * Normal();

        /// <summary>Poisson(lambda). Inversion below 30, a clamped rounded normal above (the macro's kill counts are
        /// small; above 30 the relative error of the normal is under 2%).</summary>
        public long Poisson(double lambda)
        {
            if (!(lambda > 0)) return 0;
            if (lambda < 30)
            {
                double L = Math.Exp(-lambda), p = 1.0; long k = 0;
                do { k++; p *= Uniform(); } while (p > L);
                return k - 1;
            }
            long v = (long)Math.Round(lambda + Math.Sqrt(lambda) * Normal());
            return Math.Max(0, v);
        }

        /// <summary>Binomial(n, p), exactly inside [0, n]. Bernoulli sum for small n, inversion while n·min(p,1-p) is
        /// small, otherwise a rounded normal clamped to the support.</summary>
        public long Binomial(long n, double p)
        {
            if (n <= 0 || !(p > 0)) return 0;
            if (p >= 1) return n;
            if (p > 0.5) return n - Binomial(n, 1 - p);
            if (n <= 24)
            {
                long k = 0;
                for (long i = 0; i < n; i++) if (Uniform() < p) k++;
                return k;
            }
            double mu = n * p;
            if (mu < 12)
            {
                // inversion on the pmf recurrence
                double q = 1 - p, s = p / q, f = Math.Pow(q, n), u = Uniform(), cdf = f;
                long k = 0;
                while (u > cdf && k < n) { f *= s * (n - k) / (k + 1); k++; cdf += f; if (f < 1e-300) break; }
                return k;
            }
            long v = (long)Math.Round(mu + Math.Sqrt(mu * (1 - p)) * Normal());
            return Math.Min(n, Math.Max(0, v));
        }

        /// <summary>Hypergeometric: how many of <paramref name="k"/> draws without replacement land in a group of
        /// <paramref name="good"/> out of good + <paramref name="bad"/>. Exactly inside its support.</summary>
        public long Hypergeometric(long good, long bad, long k)
        {
            long N = good + bad;
            if (k <= 0 || good <= 0) return 0;
            if (k >= N) return good;
            if (bad <= 0) return k;
            long lo = Math.Max(0, k - bad), hi = Math.Min(k, good);
            if (k <= 64)
            {
                long g = good, t = N, got = 0;
                for (long i = 0; i < k; i++) { if (Uniform() * t < g) { got++; g--; } t--; }
                return got;
            }
            double p = (double)good / N, mu = k * p, var = k * p * (1 - p) * (N - k) / Math.Max(1.0, N - 1.0);
            long v = (long)Math.Round(mu + Math.Sqrt(Math.Max(var, 0)) * Normal());
            return Math.Min(hi, Math.Max(lo, v));
        }

        /// <summary>Multivariate hypergeometric: split <paramref name="k"/> draws over the groups in
        /// <paramref name="counts"/> (written to <paramref name="outK"/>). Sum is exactly k (k is clamped to the total).</summary>
        public void MultiHypergeometric(ReadOnlySpan<long> counts, long k, Span<long> outK)
        {
            long rest = 0;
            for (int i = 0; i < counts.Length; i++) rest += counts[i];
            k = Math.Min(k, rest);
            for (int i = 0; i < counts.Length; i++)
            {
                rest -= counts[i];
                long d = i == counts.Length - 1 ? k : Hypergeometric(counts[i], rest, k);
                outK[i] = d; k -= d;
            }
        }

        /// <summary>Multinomial(n, p) over p.Length categories; p need not be normalised. Sum is exactly n.</summary>
        public void Multinomial(long n, ReadOnlySpan<double> p, Span<long> outK)
        {
            double rest = 0;
            for (int i = 0; i < p.Length; i++) rest += Math.Max(0, p[i]);
            for (int i = 0; i < p.Length; i++)
            {
                double pi = Math.Max(0, p[i]);
                long d = i == p.Length - 1 || rest <= 0 ? n : Binomial(n, Math.Min(1.0, pi / rest));
                outK[i] = d; n -= d; rest -= pi;
            }
        }

        // ── the Normal CDF and pdf the cohort tails integrate (scipy.special.ndtr in the research) ──

        const double InvSqrt2Pi = 0.39894228040143267794;

        public static double Phi(double x) => InvSqrt2Pi * Math.Exp(-0.5 * x * x);

        /// <summary>Standard normal CDF via erfc (W. J. Cody's rational approximation; |error| &lt; 1e-14).</summary>
        public static double Ndtr(double x)
        {
            if (double.IsPositiveInfinity(x)) return 1.0;
            if (double.IsNegativeInfinity(x)) return 0.0;
            double z = x / 1.4142135623730951;
            return z < 0 ? 0.5 * Erfc(-z) : 1.0 - 0.5 * Erfc(z);
        }

        /// <summary>erfc for z &gt;= 0 (Numerical Recipes' Chebyshev erfccheb, relative error ~1.2e-16).</summary>
        static double Erfc(double z)
        {
            if (z > 27) return 0.0;
            double t = 2.0 / (2.0 + z), ty = 4.0 * t - 2.0, d = 0.0, dd = 0.0, tmp;
            for (int j = Cof.Length - 1; j > 0; j--) { tmp = d; d = ty * d - dd + Cof[j]; dd = tmp; }
            return t * Math.Exp(-z * z + 0.5 * (Cof[0] + ty * d) - dd);
        }

        static readonly double[] Cof =
        {
            -1.3026537197817094, 6.4196979235649026e-1, 1.9476473204185836e-2, -9.561514786808631e-3,
            -9.46595344482036e-4, 3.66839497852761e-4, 4.2523324806907e-5, -2.0278578112534e-5, -1.624290004647e-6,
            1.303655835580e-6, 1.5626441722e-8, -8.5238095915e-8, 6.529054439e-9, 5.059343495e-9, -9.91364156e-10,
            -2.27365122e-10, 9.6467911e-11, 2.394038e-12, -6.886027e-12, 8.94487e-13, 3.13092e-13, -1.12708e-13,
            3.81e-16, 7.106e-15, -1.523e-15, -9.4e-17, 1.21e-16, -2.8e-17,
        };
    }
}
