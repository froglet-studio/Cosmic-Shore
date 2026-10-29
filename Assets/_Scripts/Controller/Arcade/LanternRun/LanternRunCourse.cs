using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lantern Run's circuit and its LANTERNS, stated against the ANGLER (Angler.prefab, AnglerConfig.asset;
    /// LanternRunCourseTests reads them back). The solver is <see cref="HeadlongCircuit"/>, shared.
    ///
    /// <para><b>The Angler's speed is its school.</b> It cruises at 40 u/s on a flat 48 deg/s turn. Everything faster
    /// is the LEAPFROG (ANGLER.md §1, §1f, §1m): hold RT and the group whose turn it is balls up at the nose while the
    /// brush paints whatever lies along it; let go and that group is THROWN at the paint at <see cref="SlingSpeed"/>,
    /// homing; press the Time dash and the hull covers <see cref="DashFrac"/> of the way to a group in
    /// <see cref="DashTime"/>. Paint something far ahead and the school streams there at 150 u/s, and every dash closes
    /// three quarters of the gap: the hull moves at the school's pace, ~4x its cruise.</para>
    ///
    /// <para><b>A lantern is something to paint.</b> An empty race cell has no prism to throw at, so every lantern ring
    /// hangs a small cluster of plain neutral prisms <see cref="LanternDistance"/> PAST its mouth, on the line the
    /// pilot arrives on. Paint the lantern THROUGH the ring and the dash line runs through the mouth: lining the throw up
    /// is the skill. Unpainted, a throw flies along the nose for 3 s and decays (~316 u): the slow way.</para>
    ///
    /// <para><b>The ladder is the lanterns.</b> I1: a lantern at every ring, on the line, wide mouths. I2: every ring,
    /// but each lantern sits off to one side, so the line through the mouth has to be flown before the throw. I3: a
    /// lantern at every second ring; the rest are plain throws or a rival's trail (any enemy prism paints). I4: every
    /// third ring, off-line, the tightest mouths.</para>
    /// </summary>
    public static class LanternRunCourse
    {
        // ── The vessel this course is cut for ──────────────────────────────────

        /// <summary>`DefaultThrottleScaler` on Angler.prefab: cruise, u/s.</summary>
        public const float CruiseSpeed = 40f;

        /// <summary>The smaller of `PitchScaler` / `YawScaler` on Angler.prefab (flat: `RotationThrottleScaler` 0).</summary>
        public const float TurnScaler = 48f;

        /// <summary>`slingSpeed` on AnglerConfig.asset: a thrown creature's pace, u/s (x1 -> x2 with Time).</summary>
        public const float SlingSpeed = 150f;

        /// <summary>`dashFrac` on AnglerConfig.asset.</summary>
        public const float DashFrac = 0.75f;

        /// <summary>`dashTime` on AnglerConfig.asset, s.</summary>
        public const float DashTime = 0.35f;

        /// <summary>`paintCone` on AnglerConfig.asset: the brush's half-angle, degrees.</summary>
        public const float PaintCone = 3f;

        /// <summary>`paintRange` on AnglerConfig.asset, u.</summary>
        public const float PaintRange = 3200f;

        /// <summary>The cruise circle, ~48 u.</summary>
        public static float CruiseRadius => CruiseSpeed / (TurnScaler * Mathf.Deg2Rad);

        // ── The lanterns ───────────────────────────────────────────────────────

        /// <summary>u past the ring's mouth, along the arrival line.</summary>
        public const float LanternDistance = 140f;

        /// <summary>Prisms in one lantern: enough for four racers' thrown groups (half a school each).</summary>
        public const int LanternPrisms = 18;

        /// <summary>The lantern's radius, u: well inside the brush at any range a pilot throws from (3 degrees is 26 u at 500 u).</summary>
        public const float LanternRadius = 9f;

        public static readonly Vector3 LanternPrismScale = new(3.5f, 3.5f, 3.5f);

        /// <summary>u a lantern prism keeps outside every ring's rim.</summary>
        public const float MouthClearance = 20f;

        /// <summary>Which rings carry a lantern: every ring at I1-I2, every second at I3, every third at I4.</summary>
        public static int LanternEvery(int intensity) => new[] { 1, 1, 2, 3 }[Mathf.Clamp(intensity, 1, 4) - 1];

        /// <summary>The sideways offset of a lantern off the arrival line, as a share of the ring's mouth radius: 0 on the
        /// line (I1, I3), 1.4 mouths to the side (I2, I4) - the straight line from the last ring through this one's centre
        /// then paints nothing, so the pilot flies the line THROUGH the mouth TO the lantern before throwing.</summary>
        public static float LanternOffsetMouths(int intensity) => new[] { 0f, 1.4f, 0f, 1.4f }[Mathf.Clamp(intensity, 1, 4) - 1];

        public static bool HasLantern(int ringIndex, int intensity) => ringIndex % LanternEvery(intensity) == 0;

        /// <summary>
        /// The lanterns for one lap of <paramref name="rings"/> (closed: ring i's arrival line is from ring i-1).
        /// Deterministic and pure: every peer lays the same prisms from the same broadcast course. A prism that would sit
        /// in a mouth, or inside <paramref name="clearRadius"/> of <paramref name="centre"/>, is dropped.
        /// </summary>
        public static List<SwitchbackFeaturePrism> Lanterns(IReadOnlyList<RaceGate> rings, int intensity,
                                                            Vector3 centre = default, float clearRadius = 0f)
        {
            var prisms = new List<SwitchbackFeaturePrism>(LanternPrisms * 8);
            if (rings == null || rings.Count < 3) return prisms;
            int n = rings.Count;
            for (int i = 0; i < n; i++)
            {
                if (!HasLantern(i, intensity)) continue;
                Vector3 at = LanternCentre(rings, i, intensity);
                for (int k = 0; k < LanternPrisms; k++)
                {
                    // A golden-spiral ball: the same on every peer, no random state.
                    float y = 1f - 2f * (k + 0.5f) / LanternPrisms;
                    float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    float a = k * 2.39996323f;
                    Vector3 p = at + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r) * LanternRadius;
                    if ((p - centre).sqrMagnitude < clearRadius * clearRadius) continue;
                    bool inMouth = false;
                    for (int g = 0; g < n && !inMouth; g++)
                        inMouth = (p - rings[g].Position).magnitude < rings[g].Radius + MouthClearance;
                    if (inMouth) continue;
                    prisms.Add(new SwitchbackFeaturePrism(p, Quaternion.identity, LanternPrismScale, PrismKind.Plain));
                }
            }
            return prisms;
        }

        /// <summary>Where ring <paramref name="i"/>'s lantern hangs: <see cref="LanternDistance"/> past its mouth along the
        /// arrival line, pushed <see cref="LanternOffsetMouths"/> mouths sideways (a side the same on every peer).</summary>
        public static Vector3 LanternCentre(IReadOnlyList<RaceGate> rings, int i, int intensity)
        {
            int n = rings.Count;
            Vector3 here = rings[i].Position, prev = rings[(i - 1 + n) % n].Position;
            Vector3 arrive = here - prev;
            arrive = arrive.sqrMagnitude > 1e-4f ? arrive.normalized : rings[i].Axis;
            Vector3 side = RaceCourseGeometry.Perpendicular(arrive) * (i % 2 == 0 ? 1f : -1f);
            return here + arrive * LanternDistance + side * (LanternOffsetMouths(intensity) * rings[i].Radius);
        }

        /// <summary>
        /// The shipped circuit per intensity: Thresher Time's corner profiles (the Angler's corners cost nothing - its
        /// speed is the throw, not the hull), floors far above the 48 u cruise circle, and the mouths that tighten.
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new HeadlongCircuitSettings
            {
                GateCount = GatesPerLap,
                BaseRadius = BaseRadius,
                CornerProfile = new[]
                {
                    new[] { 125f,  90f,  60f,  40f,  25f,  12f,   5f,   3f },
                    new[] { 110f,  90f,  60f,  40f,  25f,  18f,  12f,   5f },
                    new[] { 150f, 135f,  60f,   6f,   4f,   3f,   2f,   1f },
                    new[] { 140f, 135f,  70f,   6f,   4f,   3f,   2f,   1f },
                }[i - 1],
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 180f, 140f, 120f, 110f }[i - 1],
                RadialSwing = 0.42f,
                AngularSpread = new[] { 1.2f, 4.0f, 2.0f, 4.0f }[i - 1],
                LateralPerturbation = new[] { 120f, 170f, 215f, 260f }[i - 1],
                RingRadius = new[] { 72f, 58f, 46f, 36f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                MaxPresentDegrees = new[] { 50f, 70f, 72f, 76f }[i - 1],
            };
        }

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 8;

        /// <summary>The circuit's base circle - Headlong's octagon at 800, mid-shell.</summary>
        public const float BaseRadius = 800f;
    }
}
