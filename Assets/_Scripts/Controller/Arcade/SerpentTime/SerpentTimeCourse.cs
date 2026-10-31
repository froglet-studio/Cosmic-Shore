using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent's circuit: a switchback ladder of rings far apart with hairpin bends (<see cref="Build"/>), and the
    /// SERPENT's flight model it is flown with (Serpent.prefab and ConsumeBoostAction.asset; SerpentTimeCourseTests
    /// reads them back).
    ///
    /// <para><b>Speed is pellets.</b> Cruise is <c>10 + 50</c> = 60 u/s. A burning pellet sets the
    /// boost multiplier to 3; n overlapping pellets to <c>1 + 2n</c> (3 / 5 / 7 / 9x), so speed is
    /// <c>50 x multiplier + 10</c>: 160, 260, 360, 460 u/s. The turn rate GROWS with speed
    /// (<c>0.4 x speed + 80</c> deg/s), so the circle grows only from 33 u cruising to ~100 u on four
    /// pellets - the Serpent can carry a stack through most corners. What it cannot carry is the
    /// tank: four pellets, 3 s each, one back every ~3.6 s. A lap is ~5,000 u; burning all four at
    /// once covers ~1,400 u of it. The ladder tightens the mouths and the presentation, so a stack
    /// arriving at 460 u/s has to be aimed.</para>
    /// </summary>
    public static class SerpentTimeCourse
    {
        // ── The vessel this course is cut for (SerpentTimeCourseTests reads each back off its asset) ──

        /// <summary>`DefaultMinimumSpeed` on Serpent.prefab.</summary>
        public const float MinimumSpeed = 10f;

        /// <summary>`DefaultThrottleScaler` on Serpent.prefab.</summary>
        public const float ThrottleScaler = 50f;

        /// <summary>`RotationThrottleScaler` on Serpent.prefab: 0.4 deg/s more turn per u/s.</summary>
        public const float RotationThrottleScaler = 0.4f;

        /// <summary>The smaller of `PitchScaler` and `YawScaler` on Serpent.prefab.</summary>
        public const float TurnScaler = 80f;

        /// <summary>`boostMultiplier` on ConsumeBoostAction.asset - ONE pellet's multiplier.</summary>
        public const float PelletMultiplier = 3f;

        /// <summary>`boostDuration` on ConsumeBoostAction.asset - one pellet's burn, s.</summary>
        public const float PelletBurnSeconds = 3f;

        /// <summary>Pellets the tank holds: `maxAmount` 1 / `resourceCost` 0.25.</summary>
        public const int TankPellets = 4;

        /// <summary>Speed with <paramref name="pellets"/> burning at once (0..4): 60, 160, 260, 360, 460.</summary>
        public static float SpeedWithPellets(int pellets)
        {
            int n = Mathf.Clamp(pellets, 0, TankPellets);
            float multiplier = n == 0 ? 1f : 1f + (PelletMultiplier - 1f) * n;
            return ThrottleScaler * multiplier + MinimumSpeed;
        }

        public static float TurnRateDegPerSec(float speed) => speed * RotationThrottleScaler + TurnScaler;

        public static float CornerRadiusAt(float speed) => speed / (TurnRateDegPerSec(speed) * Mathf.Deg2Rad);

        /// <summary>The circle on a full stack (~100 u) - the number the floors sit against.</summary>
        public static float FullStackRadius => CornerRadiusAt(SpeedWithPellets(TankPellets));

        /// <summary>The cruise circle (~33 u).</summary>
        public static float CruiseRadius => CornerRadiusAt(SpeedWithPellets(0));

        // ── The course: a switchback ladder (2026-10-31) ────────────────────────────────────────
        // Shombith: "no shapes just rings spaced out a lot from each other and hairpin bends". Headlong's circle put eight
        // rings ~612 u apart with at most two sharp corners; this lays the rings on two BANKS on opposite sides of the cell,
        // alternating, each bank's rings a short step apart sideways. Every leg crosses the cell (about 1,000 u), and the
        // next ring is just beside the last one on the bank you came from, so every turn but the two at the ladder's ends is
        // a HAIRPIN (150 - 170 degrees); the two ends close the lap with a sweeping return. Legs stay clear of the nucleus
        // and rings inside the shell (relaxed toward a narrower ladder until they do), and a ring faces the leg that
        // arrives at it, so you fly straight in and turn round after it.

        /// <summary>The rings' mouths at I1-I4, Breakwater's port sizes.</summary>
        public static float RingRadiusFor(int intensity) => new[] { 72f, 60f, 50f, 42f }[Mathf.Clamp(intensity, 1, 4) - 1];

        /// <summary>Degrees between neighbouring rings of a bank at I1-I4: the smaller, the sharper the hairpin.</summary>
        public static float LateralStepDegrees(int intensity) => new[] { 26f, 23f, 20f, 17f }[Mathf.Clamp(intensity, 1, 4) - 1];

        /// <summary>Degrees between the two banks, seen from the cell's centre: how far each leg crosses.</summary>
        public const float BankSpanDegrees = 76f;

        /// <summary>A leg keeps at least this multiple of the shell's inner radius from the centre (the nucleus).</summary>
        public const float NucleusClearance = 1.1f;

        /// <summary>Two rings' centres stay at least two mouths plus this apart.</summary>
        public const float RingGap = 60f;

        const float OuterMargin = 60f;
        const float RadialJitter = 70f;
        const float BankJitterDegrees = 4f;
        const float LateralJitterDegrees = 2.5f;
        const int RelaxSteps = 40;

        /// <summary>
        /// One lap of <paramref name="gateCount"/> rings (rounded up to an even number, at least 4) in cell-local
        /// coordinates, from <paramref name="seed"/>. Deterministic: the server builds it, the start line builds the same
        /// one early, and every peer is sent the result.
        /// </summary>
        public static List<RaceGate> Build(int seed, int intensity, int gateCount, float inner, float outer)
        {
            int n = Mathf.Max(4, gateCount + (gateCount & 1));
            float radius = RingRadiusFor(intensity);
            var rng = new RaceCourseGeometry.Rng(seed);

            // A random frame for the ladder: a = the middle of it, e1 = across (bank to bank), e2 = along a bank.
            Vector3 a = RaceCourseGeometry.SafeNormalize(new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f)), Vector3.up);
            Vector3 t = RaceCourseGeometry.SafeNormalize(new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f)), Vector3.right);
            Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(a, t), RaceCourseGeometry.Perpendicular(a));
            Vector3 e2 = Vector3.Cross(a, e1);

            var jitter = new Vector3[n];
            for (int k = 0; k < n; k++) jitter[k] = new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f));

            float onShell = outer - radius - OuterMargin;
            float clear = inner * NucleusClearance;
            float span = BankSpanDegrees, step = LateralStepDegrees(intensity);
            var points = new Vector3[n];
            for (int attempt = 0; attempt <= RelaxSteps; attempt++)
            {
                int half = n / 2;
                for (int k = 0; k < n; k++)
                {
                    int bank = k & 1, rung = k >> 1;
                    float lateral = (rung - (half - 1) * 0.5f + (bank == 1 ? 0.5f : 0f) - 0.25f) * step
                                    + jitter[k].y * LateralJitterDegrees;
                    float across = (bank == 0 ? -span : span) * 0.5f + jitter[k].x * BankJitterDegrees;
                    float r = onShell - Mathf.Abs(jitter[k].z) * RadialJitter;
                    float p = across * Mathf.Deg2Rad, l = lateral * Mathf.Deg2Rad;
                    Vector3 dir = Mathf.Cos(l) * (Mathf.Cos(p) * a + Mathf.Sin(p) * e1) + Mathf.Sin(l) * e2;
                    points[k] = dir * r;
                }
                if (IsLegal(points, clear, 2f * radius + RingGap)) break;
                span *= 0.96f;
                step *= 0.97f;
            }

            var gates = new List<RaceGate>(n);
            for (int k = 0; k < n; k++)
            {
                Vector3 arriving = points[k] - points[(k - 1 + n) % n];
                gates.Add(new RaceGate(points[k], RaceCourseGeometry.SafeNormalize(arriving, a), radius));
            }
            return gates;
        }

        static bool IsLegal(Vector3[] points, float clear, float separation)
        {
            int n = points.Length;
            for (int i = 0; i < n; i++)
            {
                if (DistanceFromCentre(points[i], points[(i + 1) % n]) < clear) return false;
                for (int j = i + 1; j < n; j++)
                    if ((points[i] - points[j]).sqrMagnitude < separation * separation) return false;
            }
            return true;
        }

        /// <summary>How close the leg from <paramref name="a"/> to <paramref name="b"/> comes to the cell's centre.</summary>
        public static float DistanceFromCentre(Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(-Vector3.Dot(a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t).magnitude;
        }

        /// <summary>The turn a pilot makes at ring <paramref name="i"/>, in degrees (0 straight on, 180 straight back).</summary>
        public static float TurnDegrees(List<RaceGate> lap, int i)
        {
            int n = lap.Count;
            Vector3 arriving = lap[i].Position - lap[(i - 1 + n) % n].Position;
            Vector3 leaving = lap[(i + 1) % n].Position - lap[i].Position;
            return Vector3.Angle(arriving, leaving);
        }

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 8;
    }
}
