// Shared plain-C# helpers for the two THREAT FLORA cores (SnapTrapCore, PhysarumCore) - no UnityEngine,
// so the same file compiles and RUNS headless in Tools/Build/threat_flora_harness. Docs/THREAT_FLORA.md.
using System;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A small deterministic RNG (xorshift128+ with a Box-Muller gaussian). The cores never touch
    /// UnityEngine.Random or System.Random so a seeded run is bit-identical on every runtime the
    /// harness and the editor use.
    /// </summary>
    public struct ThreatRng
    {
        ulong _s0, _s1;
        float _spare;
        bool _hasSpare;

        public ThreatRng(ulong seed)
        {
            // splitmix64 seeding: any seed (including 0) gives a well-mixed non-zero state
            ulong z = seed + 0x9E3779B97F4A7C15UL;
            _s0 = Mix(ref z);
            _s1 = Mix(ref z);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
            _spare = 0f;
            _hasSpare = false;
        }

        static ulong Mix(ref ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            ulong x = z;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        public ulong NextULong()
        {
            ulong s1 = _s0, s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return _s1 + s0;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / (1UL << 24));

        public float Range(float lo, float hi) => lo + (hi - lo) * NextFloat();

        public int Range(int lo, int hiExclusive) =>
            hiExclusive <= lo ? lo : lo + (int)(NextULong() % (ulong)(hiExclusive - lo));

        /// <summary>Standard normal (Box-Muller, the spare cached).</summary>
        public float Gaussian()
        {
            if (_hasSpare) { _hasSpare = false; return _spare; }
            float u1 = Math.Max(NextFloat(), 1e-7f), u2 = NextFloat();
            float r = MathF.Sqrt(-2f * MathF.Log(u1));
            float a = 2f * MathF.PI * u2;
            _spare = r * MathF.Sin(a);
            _hasSpare = true;
            return r * MathF.Cos(a);
        }

        public Vector3 GaussianVector() => new Vector3(Gaussian(), Gaussian(), Gaussian());

        public Vector3 OnUnitSphere()
        {
            Vector3 d = GaussianVector();
            float n = d.Length();
            return n < 1e-6f ? Vector3.UnitZ : d / n;
        }
    }

    /// <summary>
    /// The GROVE: where a threat flora community lives. Either a BALL (the research's 450 u grove,
    /// Tools/Ecology/flora/harness.py GROVE_C / GROVE_R) or a SECTOR OF A SPHERICAL SHELL around the
    /// cell's centre - the shape the Swarm cell gives it, between the outer swarm band and the membrane,
    /// so the grove never sits on a swarm's band (Docs/THREAT_FLORA.md §4).
    /// </summary>
    public struct ThreatGroveShape
    {
        /// <summary>0 = ball (Centre, OuterRadius), 1 = shell sector (around CellCentre).</summary>
        public int Kind;
        public Vector3 Centre;          // ball centre; for a sector, the sector's mid point (derived)
        public Vector3 CellCentre;      // sector only
        public Vector3 Axis;            // sector only: unit direction from the cell centre to the grove
        public float InnerRadius;       // sector only: shell inner radius from the cell centre
        public float OuterRadius;       // ball radius, or the shell's outer radius
        public float CosHalfAngle;      // sector only

        public const int Ball = 0;
        public const int ShellSector = 1;

        public static ThreatGroveShape MakeBall(Vector3 centre, float radius) => new ThreatGroveShape
        {
            Kind = Ball, Centre = centre, OuterRadius = radius, Axis = Vector3.UnitZ, CosHalfAngle = -1f,
        };

        public static ThreatGroveShape MakeShellSector(Vector3 cellCentre, Vector3 axis, float inner, float outer,
            float halfAngleDegrees)
        {
            Vector3 a = axis.LengthSquared() < 1e-9f ? Vector3.UnitX : Vector3.Normalize(axis);
            return new ThreatGroveShape
            {
                Kind = ShellSector, CellCentre = cellCentre, Axis = a, InnerRadius = inner, OuterRadius = outer,
                CosHalfAngle = MathF.Cos(halfAngleDegrees * MathF.PI / 180f),
                Centre = cellCentre + a * (0.5f * (inner + outer)),
            };
        }

        /// <summary>True when p is inside the grove, shrunk by margin on every face.</summary>
        public bool Contains(Vector3 p, float margin = 0f)
        {
            if (Kind == Ball) return (p - Centre).LengthSquared() < Sq(OuterRadius - margin);
            Vector3 d = p - CellCentre;
            float r = d.Length();
            if (r < InnerRadius + margin || r > OuterRadius - margin || r < 1e-6f) return false;
            // the angular margin, measured as an arc at this radius
            float cos = Vector3.Dot(d / r, Axis);
            float half = MathF.Acos(Math.Clamp(CosHalfAngle, -1f, 1f)) - margin / r;
            return half > 0f && cos > MathF.Cos(half);
        }

        /// <summary>
        /// p moved just clear of the grove (grown by clearance on every face), keeping its distance from the cell
        /// centre: a sector point swings sideways out of the cone, a ball point is pushed out radially. A point already
        /// clear comes back unchanged. Used to keep another creature's home off the grove (round 11e's thief nest).
        /// </summary>
        public Vector3 PushOutside(Vector3 p, float clearance)
        {
            if (!Contains(p, -clearance)) return p;
            if (Kind == Ball)
            {
                Vector3 o = p - Centre;
                Vector3 dir = o.LengthSquared() < 1e-9f ? Vector3.UnitX : Vector3.Normalize(o);
                return Centre + dir * (OuterRadius + clearance + 1f);
            }
            Vector3 d = p - CellCentre;
            float r = d.Length();
            Vector3 side = d - Axis * Vector3.Dot(d, Axis);
            if (side.LengthSquared() < 1e-6f) ThreatFloraMath.Frame(Axis, out side, out _);
            side = Vector3.Normalize(side);
            float angle = MathF.Acos(Math.Clamp(CosHalfAngle, -1f, 1f)) + (clearance + 1f) / r;
            return CellCentre + Vector3.Normalize(Axis * MathF.Cos(angle) + side * MathF.Sin(angle)) * r;
        }

        /// <summary>The axis-aligned box that holds the grove (for the physarum grid).</summary>
        public void Bounds(out Vector3 min, out Vector3 max)
        {
            if (Kind == Ball)
            {
                min = Centre - new Vector3(OuterRadius);
                max = Centre + new Vector3(OuterRadius);
                return;
            }
            // sample the sector's surface densely; pad by one percent of the outer radius
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            float half = MathF.Acos(Math.Clamp(CosHalfAngle, -1f, 1f));
            Vector3 up = MathF.Abs(Axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 n = Vector3.Normalize(Vector3.Cross(Axis, up)), b = Vector3.Cross(Axis, n);
            for (int i = 0; i <= 16; i++)
            for (int j = 0; j < 32; j++)
            for (int k = 0; k < 2; k++)
            {
                float th = half * i / 16f, ph = 2f * MathF.PI * j / 32f;
                Vector3 d = MathF.Cos(th) * Axis + MathF.Sin(th) * (MathF.Cos(ph) * n + MathF.Sin(ph) * b);
                Vector3 p = CellCentre + d * (k == 0 ? InnerRadius : OuterRadius);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 pad = new Vector3(OuterRadius * 0.01f);
            min -= pad;
            max += pad;
        }

        /// <summary>Pull p back inside the grove (shrunk by margin) - radially for a sector.</summary>
        public Vector3 Clamp(Vector3 p, float margin)
        {
            if (Contains(p, margin)) return p;
            if (Kind == Ball)
            {
                Vector3 d = p - Centre;
                float r = d.Length();
                float lim = Math.Max(0f, OuterRadius - margin);
                return r < 1e-6f ? Centre : Centre + d * (lim / r);
            }
            Vector3 q = p - CellCentre;
            float rr = q.Length();
            Vector3 dir = rr < 1e-6f ? Axis : q / rr;
            float half = MathF.Acos(Math.Clamp(CosHalfAngle, -1f, 1f));
            float rad = Math.Clamp(rr, InnerRadius + margin, OuterRadius - margin);
            float maxAng = Math.Max(0f, half - margin / Math.Max(rad, 1f));
            float ang = MathF.Acos(Math.Clamp(Vector3.Dot(dir, Axis), -1f, 1f));
            if (ang > maxAng)
            {
                Vector3 perp = dir - Axis * Vector3.Dot(dir, Axis);
                float pl = perp.Length();
                perp = pl < 1e-6f ? Vector3.Zero : perp / pl;
                dir = MathF.Cos(maxAng) * Axis + MathF.Sin(maxAng) * perp;
            }
            return CellCentre + dir * rad;
        }

        /// <summary>A point uniform in volume inside the grove (shrunk by margin), by rejection.</summary>
        public Vector3 Sample(ref ThreatRng rng, float margin = 0f)
        {
            Bounds(out Vector3 lo, out Vector3 hi);
            for (int k = 0; k < 4096; k++)
            {
                var p = new Vector3(rng.Range(lo.X, hi.X), rng.Range(lo.Y, hi.Y), rng.Range(lo.Z, hi.Z));
                if (Contains(p, margin)) return p;
            }
            return Centre;
        }

        static float Sq(float x) => x * x;
    }

    /// <summary>The vessel input every threat core reads: where each vessel is now and where it was.</summary>
    public struct ThreatVessel
    {
        public Vector3 Position;
        public Vector3 Previous;
        public float Radius;
    }

    public static class ThreatFloraMath
    {
        /// <summary>Orthonormal (n, b) perpendicular to unit axis a - research flora/snaptrap.py _frame.</summary>
        public static void Frame(Vector3 a, out Vector3 n, out Vector3 b)
        {
            Vector3 up = MathF.Abs(a.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            n = Vector3.Cross(a, up);
            float l = n.Length();
            n = l < 1e-9f ? Vector3.UnitX : n / l;
            b = Vector3.Cross(a, n);
        }

        /// <summary>Distance from point p to segment a-b (research harness.seg_point_dist).</summary>
        public static float SegmentPointDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float l2 = ab.LengthSquared();
            float t = l2 < 1e-12f ? 0f : Math.Clamp(Vector3.Dot(p - a, ab) / l2, 0f, 1f);
            return (a + ab * t - p).Length();
        }

        public static float Deg2Rad(float d) => d * (MathF.PI / 180f);

        /// <summary>Rotate unit vector cur toward unit vector tgt by at most maxRadians (research heliotropism).</summary>
        public static Vector3 TurnToward(Vector3 cur, Vector3 tgt, float maxRadians)
        {
            float ang = MathF.Acos(Math.Clamp(Vector3.Dot(cur, tgt), -1f, 1f));
            float k = Math.Min(1f, maxRadians / Math.Max(ang, 1e-6f));
            Vector3 v = cur + (tgt - cur) * k;
            float l = v.Length();
            return l < 1e-9f ? cur : v / l;
        }
    }
}
