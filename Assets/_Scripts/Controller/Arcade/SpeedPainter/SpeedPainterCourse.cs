using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SpeedPainter's circuit and its prism MASS CLUSTERS, stated against the ANGLER (Angler.prefab, AnglerConfig.asset;
    /// SpeedPainterCourseTests reads them back). The solver is <see cref="HeadlongCircuit"/>, shared.
    ///
    /// <para><b>The Angler's speed is its school.</b> It cruises at 40 u/s. Everything faster is its own kit
    /// (ANGLER.md §1, §1f, §1m): hold RT and the group whose turn it is balls up at the nose while the brush paints what
    /// lies along it; let go and that group is THROWN at the paint at <see cref="SlingSpeed"/>; the Time dash covers
    /// <see cref="DashFrac"/> of the way to a group in <see cref="DashTime"/>, and the next dash goes to the OTHER group.
    /// The Angler has two groups, so it can paint two places: that is the whole mode.</para>
    ///
    /// <para><b>Two clusters a leg: paint your path.</b> Every leg (the run from one ring to the next) carries up to two
    /// clusters of neutral prisms, one for each group (Shombith, 2026-11-01: "it can paint at two places where there is
    /// prism mass clusters and the fauna will go there to eat it ... he can in a way paint its path and zoom towards
    /// it"):</para>
    /// <list type="bullet">
    /// <item><b>The near cluster</b> (<see cref="ClusterRole.Near"/>) sits on the leg, <see cref="NearBefore"/> short
    /// of the next ring (half the leg on a short one).</item>
    /// <item><b>The far cluster</b> (<see cref="ClusterRole.Far"/>) sits <see cref="FarPast"/> PAST the ring, on the
    /// ring's own line (its axis), so painting through the mouth paints it.</item>
    /// </list>
    /// <para>So a leg is: paint near, throw group A; paint far, throw group B; dash to A; dash to B - and the second dash
    /// runs THROUGH the ring (its landing point, 75% of the way from the near cluster to the far one, is past the ring's
    /// plane whenever <c>0.25 x (NearBefore + FarPast) &lt; FarPast</c>, which the constants hold and the tests assert).
    /// Creatures eat what they reach, so a cluster grows back one prism at a time
    /// (<see cref="RegrowSeconds"/>), and every racer on every lap has mass to paint.</para>
    ///
    /// <para><b>The ladder is where the paint is.</b></para>
    /// <list type="table">
    /// <item><b>I1 Two strokes:</b> both clusters on every leg, on the line, the biggest clusters, wide mouths.</item>
    /// <item><b>I2 Zigzag:</b> both clusters on every leg, pushed to opposite sides of the line, so the path zigzags
    /// and each stroke has to be aimed; the A-to-B dash still crosses the mouth (asserted).</item>
    /// <item><b>I3 One stroke:</b> only the far cluster: the other group's throw is a plain sling along the nose (~316
    /// u) or a rival's trail (any enemy prism paints).</item>
    /// <item><b>I4 Sparse:</b> the far cluster zigzagged on every leg, the near one on every second leg, the smallest
    /// clusters, the slowest regrowth and the tightest mouths.</item>
    /// </list>
    /// <para>Every function is pure and draws no random numbers, so every peer lays the same prisms from the same
    /// broadcast course.</para>
    /// </summary>
    public static class SpeedPainterCourse
    {
        // ── The vessel this course is cut for (SpeedPainterCourseTests reads each back off its asset) ──

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

        // ── The clusters ───────────────────────────────────────────────────────

        /// <summary>Which of a leg's two clusters: the one on the leg, or the one past the ring.</summary>
        public enum ClusterRole
        {
            Near = 0,
            Far = 1,
        }

        /// <summary>u the near cluster sits short of the ring it leads to, along the ring's axis (at most half the leg).</summary>
        public const float NearBefore = 300f;

        /// <summary>u the far cluster sits past the ring, along the ring's axis.</summary>
        public const float FarPast = 150f;

        /// <summary>The size of one cluster prism, u.</summary>
        public static readonly Vector3 ClusterPrismScale = new(3.5f, 3.5f, 3.5f);

        /// <summary>u a cluster prism keeps outside every ring's rim, so no cluster ever plugs a mouth.</summary>
        public const float MouthClearance = 20f;

        /// <summary>Prisms in one cluster, per intensity: enough for four racers' thrown groups (half a school each).</summary>
        public static int ClusterPrisms(int intensity) => new[] { 30, 26, 22, 18 }[Clamp(intensity) - 1];

        /// <summary>A cluster's radius, u: a ball of prisms about one prism apart, well inside the 3-degree brush at
        /// any range a pilot throws from (26 u at 500 u).</summary>
        public static float ClusterRadius(int intensity) => 2.1f * Mathf.Sqrt(ClusterPrisms(intensity));

        /// <summary>Seconds between two prisms growing back into one cluster that is short.</summary>
        public static float RegrowSeconds(int intensity) => new[] { 1.0f, 1.25f, 1.75f, 2.5f }[Clamp(intensity) - 1];

        /// <summary>Does leg <paramref name="leg"/> (the run INTO ring <paramref name="leg"/>) carry this cluster?</summary>
        public static bool HasCluster(int leg, ClusterRole role, int intensity)
        {
            int i = Clamp(intensity);
            if (role == ClusterRole.Far) return true;
            return i switch
            {
                3 => false,
                4 => leg % 2 == 0,
                _ => true,
            };
        }

        /// <summary>How far a cluster sits off the line, as a share of the ring's mouth radius. I2 and I4 push the near
        /// cluster to one side and the far one to the other (the side alternates leg by leg), so the leg zigzags.</summary>
        public static float SideMouths(int intensity) => Clamp(intensity) switch
        {
            2 => 1.2f,
            4 => 1.4f,
            _ => 0f,
        };

        /// <summary>Which side a cluster leans to: near and far opposite, flipping every leg. The same on every peer.</summary>
        public static float SideSign(int leg, ClusterRole role) =>
            ((leg % 2 == 0) ^ (role == ClusterRole.Far)) ? 1f : -1f;

        /// <summary>
        /// Where leg <paramref name="leg"/>'s cluster of <paramref name="role"/> sits: on the ring's OWN line (its axis,
        /// the direction the course flows through the mouth), <see cref="NearBefore"/> before it (at most half the leg)
        /// or <see cref="FarPast"/> past it, pushed <see cref="SideMouths"/> mouths sideways. The axis, not the chord from
        /// the previous ring: the circuit presents a ring up to 76 degrees off that chord, and clusters on the chord would
        /// draw a line that crosses the ring's plane outside its mouth (measured: up to 4 mouths off at I4). On the axis
        /// the near-to-far line threads the mouth by construction.
        /// </summary>
        public static Vector3 ClusterCentre(IReadOnlyList<RaceGate> rings, int leg, ClusterRole role, int intensity)
        {
            int n = rings.Count;
            Vector3 here = rings[leg].Position, prev = rings[(leg - 1 + n) % n].Position;
            Vector3 axis = rings[leg].Axis;
            Vector3 side = RaceCourseGeometry.Perpendicular(axis) * SideSign(leg, role);
            float offset = SideMouths(intensity) * rings[leg].Radius;
            if (role == ClusterRole.Far)
                return here + axis * FarPast + side * offset;
            float back = Mathf.Min(NearBefore, 0.5f * Vector3.Distance(here, prev));
            return here - axis * back + side * offset;
        }

        /// <summary>Slot <paramref name="k"/> of a cluster of <paramref name="count"/> prisms: a golden-spiral ball, no
        /// random state, so a prism that grows back grows into the same place on every peer.</summary>
        public static Vector3 SlotOffset(int k, int count, float radius)
        {
            float y = 1f - 2f * (k + 0.5f) / count;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = k * 2.39996323f;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r) * radius;
        }

        /// <summary>One cluster: which leg, which role, where, and the slot positions that survive the mouth and nucleus
        /// clearances.</summary>
        public readonly struct Cluster
        {
            public readonly int Leg;
            public readonly ClusterRole Role;
            public readonly Vector3 Centre;
            public readonly Vector3[] Slots;

            public Cluster(int leg, ClusterRole role, Vector3 centre, Vector3[] slots)
            {
                Leg = leg;
                Role = role;
                Centre = centre;
                Slots = slots;
            }
        }

        /// <summary>
        /// Every cluster of one lap of <paramref name="rings"/> (closed: ring i's leg comes from ring i-1). A slot that
        /// would sit within <see cref="MouthClearance"/> of any ring's rim, or inside <paramref name="clearRadius"/> of
        /// <paramref name="centre"/> (the nucleus), is dropped; a cluster left with no slot is dropped.
        /// </summary>
        public static List<Cluster> Clusters(IReadOnlyList<RaceGate> rings, int intensity,
                                             Vector3 centre = default, float clearRadius = 0f)
        {
            var clusters = new List<Cluster>(16);
            if (rings == null || rings.Count < 3) return clusters;
            int n = rings.Count, count = ClusterPrisms(intensity);
            float radius = ClusterRadius(intensity);
            var slots = new List<Vector3>(count);
            for (int leg = 0; leg < n; leg++)
            {
                for (int r = 0; r < 2; r++)
                {
                    var role = (ClusterRole)r;
                    if (!HasCluster(leg, role, intensity)) continue;
                    Vector3 at = ClusterCentre(rings, leg, role, intensity);
                    slots.Clear();
                    for (int k = 0; k < count; k++)
                    {
                        Vector3 p = at + SlotOffset(k, count, radius);
                        if ((p - centre).sqrMagnitude < clearRadius * clearRadius) continue;
                        if (InAnyMouth(rings, p)) continue;
                        slots.Add(p);
                    }
                    if (slots.Count > 0) clusters.Add(new Cluster(leg, role, at, slots.ToArray()));
                }
            }
            return clusters;
        }

        /// <summary>True when <paramref name="p"/> lies within <see cref="MouthClearance"/> of a ring's rim sphere.</summary>
        public static bool InAnyMouth(IReadOnlyList<RaceGate> rings, Vector3 p)
        {
            for (int g = 0; g < rings.Count; g++)
                if ((p - rings[g].Position).magnitude < rings[g].Radius + MouthClearance) return true;
            return false;
        }

        /// <summary>Where a dash from <paramref name="from"/> toward a group sitting at <paramref name="to"/> lands
        /// (<see cref="AnglerCommandMath.DashPoint"/>'s rule: <see cref="DashFrac"/> of the way).</summary>
        public static Vector3 DashLanding(Vector3 from, Vector3 to) => from + (to - from) * DashFrac;

        /// <summary>
        /// Where the straight line from <paramref name="from"/> to <paramref name="to"/> crosses the plane of
        /// <paramref name="ring"/>, as a distance from the ring's centre; false when the segment does not reach the
        /// plane. The tests use it to hold "the near-to-far dash runs through the mouth".
        /// </summary>
        public static bool CrossesPlaneAt(Vector3 from, Vector3 to, in RaceGate ring, out float missFromCentre)
        {
            missFromCentre = float.PositiveInfinity;
            float a = Vector3.Dot(from - ring.Position, ring.Axis), b = Vector3.Dot(to - ring.Position, ring.Axis);
            if (a > 0f || b < 0f || Mathf.Approximately(a, b)) return false;
            float t = a / (a - b);
            missFromCentre = Vector3.Distance(from + (to - from) * t, ring.Position);
            return true;
        }

        /// <summary>
        /// The shipped circuit per intensity: Thresher Time's corner profiles (the Angler's corners cost nothing - its
        /// speed is the throw, not the hull), floors far above the 48 u cruise circle, and the mouths that tighten.
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Clamp(intensity);
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
                AngularSpread = new[] { 1.2f, 3.0f, 2.0f, 4.0f }[i - 1],
                LateralPerturbation = new[] { 120f, 160f, 215f, 260f }[i - 1],
                RingRadius = new[] { 72f, 60f, 48f, 38f }[i - 1],
                AxisJitterDegrees = new[] { 18f, 24f, 34f, 42f }[i - 1],
                MaxPresentDegrees = new[] { 45f, 60f, 70f, 76f }[i - 1],
            };
        }

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 8;

        /// <summary>The circuit's base circle - Headlong's octagon at 800, mid-shell.</summary>
        public const float BaseRadius = 800f;

        static int Clamp(int intensity) => Mathf.Clamp(intensity, 1, 4);
    }
}
