using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The line a skim racer holds when it is not going anywhere else: for each face of the ribbon,
    /// the path that asks the least of the pilot's stick at full speed while keeping the hull clear
    /// of the plates and staying in skimming reach of them wherever that is cheap — solved once per
    /// route and shared by every racer on it (it is a property of the ribbon and the hull).
    ///
    /// <para><b>What it minimises is the STICK, not the curvature.</b> A Squirrel turns at
    /// <c>k</c> = 1.5 / s times the rotation between where it points and where the stick has
    /// commanded it, so holding a turn costs <c>v κ</c> of stick and STARTING one costs
    /// <c>(v² / k) dκ/ds</c> more — at 300 u/s the second term is 200 times the first per unit of
    /// curvature change. The ribbon is a Catmull-Rom spline through the waypoints, whose curvature
    /// JUMPS at every knot, so the centre line asks for up to 3.5 rad/s there against the
    /// 2.09 rad/s the stick has. A line that only straightens bends (the first cut) leaves those
    /// jumps where they are. This one spreads them: it minimises the sum of both terms, squared, at
    /// the design speed.</para>
    ///
    /// <para><b>The skim band is soft, the plates are hard.</b> At full speed the boost is held
    /// with 12 % skim duty, so a line may leave the skimmer's reach for a few tenths of a second
    /// to enter a dip high, touch its apex low and leave it high — which is exactly what it takes
    /// to spread a curvature jump that bends toward the plates. Clearing the plates (with the
    /// hull at any roll) is a hard bound; staying in reach is a quadratic penalty.</para>
    ///
    /// <para><b>How.</b> Accelerated projected gradient (FISTA) on a cyclic grid of
    /// <see cref="GridStep"/> u, each iterate clamped back into the hard bounds — a convex problem,
    /// so the answer does not depend on where it starts. Evaluated between grid points with a
    /// Catmull-Rom spline. Pure; compiled and raced by Tools/Build/squirrel_ai_harness.</para>
    /// </summary>
    public sealed class SkimRacingLine
    {
        const float GridStep = 24f;
        const int Iterations = 4000;

        readonly SkimRoute _route;
        readonly int _n;
        readonly float _step;
        // [face][i]: face 0 = over the plates, face 1 = under them.
        readonly float[][] _a = new float[2][];
        readonly float[][] _b = new float[2][];

        /// <param name="nominalHeight">Where the line starts, and where it rests wherever nothing
        /// bends.</param>
        /// <param name="lateralLimit">How far across the plate (u) the line may cut.</param>
        /// <param name="hullReach">Radius a hull at any roll reaches from its centre line.</param>
        /// <param name="clearance">Kept between that reach and the plates.</param>
        /// <param name="skimRadius">The skimmer's radius.</param>
        /// <param name="along">Half-length (u) over which a marker's bigger shell is cleared.</param>
        /// <param name="designSpeed">The speed the stick demand is minimised for: the pilot's top
        /// speed.</param>
        /// <param name="followRate">The ship's rotation follow rate <c>k</c>.</param>
        /// <param name="bandWeight">Penalty, in (rad/s)² per grid point per u², for leaving the
        /// skimmer's reach.</param>
        public SkimRacingLine(SkimRoute route, float nominalHeight, float lateralLimit,
                              float hullReach, float clearance, float skimRadius, float along,
                              float designSpeed, float followRate, float bandWeight)
        {
            _route = route ?? throw new ArgumentNullException(nameof(route));
            _n = Mathf.Max(16, Mathf.RoundToInt(route.Length / GridStep));
            _step = route.Length / _n;
            int n = _n;

            var c = new Vector3[n];
            var right = new Vector3[n];
            var up = new Vector3[n];
            var lo = new float[n];      // hard: |b| at least this (clear of the plates)
            var hi = new float[n];      // soft: |b| at most this (in skimming reach)
            var hiHard = new float[n];  // hard: never further than this
            var lat = new float[n];     // hard: |a| at most this
            for (int i = 0; i < n; i++)
            {
                float s = i * _step;
                route.Frame(s, out c[i], out _, out right[i], out up[i]);
                // The clearance at a grid point is the tightest over the stretch it stands for.
                float hh = 0f;
                for (float d = -_step * 0.5f; d <= _step * 0.5f + 0.01f; d += 4f)
                {
                    route.Envelope(s + d, along, out _, out float h);
                    hh = Mathf.Max(hh, h);
                }
                // The plate itself, bridged across the 2.8 u gap between consecutive shells (a bare
                // gap would read as no plate at all and drop the skim band to the centre line).
                route.Envelope(s, 4f, out float plateW, out float plateH);
                lo[i] = hh + hullReach + clearance + 0.3f;
                hi[i] = Mathf.Max(lo[i], plateH + skimRadius - 0.75f);
                hiHard[i] = hi[i] + 8f;
                lat[i] = Mathf.Max(0f, Mathf.Min(lateralLimit, plateW - 3f));
            }

            float v = Mathf.Max(designSpeed, 1f);
            float k = Mathf.Max(0.05f, followRate);
            float h1 = _step, h2 = h1 * h1, h3 = h2 * h1;
            float w3 = (v * v / k / h3) * (v * v / k / h3);   // onset term, per (third difference)²
            float w2 = (v / h2) * (v / h2);                   // turn term, per (second difference)²
            float lambda = Mathf.Max(0f, bandWeight);
            float lip = 2f * w3 * 64f + 2f * w2 * 16f + 2f * lambda;
            float stepSize = 1f / lip;

            var p = new Vector3[n];
            var q3 = new Vector3[n];
            var q2 = new Vector3[n];
            var ya = new float[n];
            var yb = new float[n];
            var pa = new float[n];
            var pb = new float[n];

            for (int face = 0; face < 2; face++)
            {
                float sign = face == 0 ? 1f : -1f;
                var a = new float[n];
                var b = new float[n];
                for (int i = 0; i < n; i++)
                {
                    a[i] = 0f;
                    b[i] = sign * Mathf.Clamp(nominalHeight, lo[i], hi[i]);
                    ya[i] = a[i];
                    yb[i] = b[i];
                }

                float t = 1f;
                for (int it = 0; it < Iterations; it++)
                {
                    // Gradient at the extrapolated point y.
                    for (int i = 0; i < n; i++) p[i] = c[i] + right[i] * ya[i] + up[i] * yb[i];
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 pm = p[Wrap(i - 1)], p0 = p[i], p1 = p[Wrap(i + 1)], p2 = p[Wrap(i + 2)];
                        q3[i] = -pm + 3f * p0 - 3f * p1 + p2;
                        q2[i] = pm - 2f * p0 + p1;
                    }
                    for (int i = 0; i < n; i++)
                    {
                        pa[i] = a[i];
                        pb[i] = b[i];
                        Vector3 g = 2f * w3 * (-q3[Wrap(i + 1)] + 3f * q3[i] - 3f * q3[Wrap(i - 1)] + q3[Wrap(i - 2)])
                                  + 2f * w2 * (q2[Wrap(i - 1)] - 2f * q2[i] + q2[Wrap(i + 1)]);
                        float ga = Vector3.Dot(g, right[i]);
                        float gb = Vector3.Dot(g, up[i]);
                        float over = sign * yb[i] - hi[i];
                        if (over > 0f) gb += 2f * lambda * over * sign;

                        float na = ya[i] - stepSize * ga;
                        float nb = yb[i] - stepSize * gb;
                        a[i] = Mathf.Clamp(na, -lat[i], lat[i]);
                        b[i] = sign * Mathf.Clamp(sign * nb, lo[i], hiHard[i]);
                    }
                    float tNext = 0.5f * (1f + Mathf.Sqrt(1f + 4f * t * t));
                    float mom = (t - 1f) / tNext;
                    t = tNext;
                    for (int i = 0; i < n; i++)
                    {
                        ya[i] = a[i] + mom * (a[i] - pa[i]);
                        yb[i] = b[i] + mom * (b[i] - pb[i]);
                    }
                }
                _a[face] = a;
                _b[face] = b;
            }
        }

        int Wrap(int i) => ((i % _n) + _n) % _n;

        /// <summary>The line's offset in the plates' frame (right, up) at arc <paramref name="s"/> on
        /// <paramref name="face"/> (0 = over, 1 = under), Catmull-Rom between grid points.</summary>
        public void Offset(int face, float s, out float a, out float b)
        {
            float local = _route.Wrap(s) / _step;
            int i = Mathf.FloorToInt(local);
            float t = local - i;
            var fa = _a[face];
            var fb = _b[face];
            a = CatmullRom(fa[Wrap(i - 1)], fa[Wrap(i)], fa[Wrap(i + 1)], fa[Wrap(i + 2)], t);
            b = CatmullRom(fb[Wrap(i - 1)], fb[Wrap(i)], fb[Wrap(i + 1)], fb[Wrap(i + 2)], t);
        }

        static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
