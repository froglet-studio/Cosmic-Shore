using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One intensity's cut of Breach (<see cref="BreachCourse.ForIntensity"/>).</summary>
    public struct BreachSettings
    {
        public int RingCount;
        public float InnerRadius;              // the nucleus shell: no leg passes inside it (+ CoreMargin)
        public float MinLeg, MaxLeg;           // ring to ring, u
        public float MinBend, MaxBend;         // degrees off the ring's axis, an ordinary leg
        public int SharpEvery;                 // every Nth leg turns hard (0: none)
        public float SharpMin, SharpMax;       // degrees, a hard leg
        public float SharpLegMin, SharpLegMax; // a hard leg is short: the turn is most of it
        public float RingRadius;
        public int WallEvery;                  // every Nth ring has a wall before it (0: none)...
        public int WallPhase;                  // ...the rings whose index leaves this remainder (never ring 0 or 1)
        public float WallMargin;               // the wall's radius past the ring's: how far out the hull swings to round it
        public float WallAhead;                // how far before its ring the wall stands: the room to swing back in
    }

    /// <summary>
    /// Breach, the ORCA's Time race (BREACH.md). The Orca is a heavy four-seat hull: it cruises at about 30 u/s and turns
    /// at about 46 degrees a second, and the helm's Time card is a BOOST TANK: 3 s of boost at five times the speed
    /// (about 115 u/s), refilling a quarter of a second each second it is not used. A full tank crosses 345 u against 90
    /// at cruise, so the race is WHERE IN EACH LEG the helm burns the tank, and at five times the speed the hull turns
    /// round a circle three and a half times as wide.
    ///
    /// <list type="number">
    /// <item><b>I1 Open water:</b> gentle bends. Burn on every straight; learn the tank (empty is fine, full is waste).</item>
    /// <item><b>I2 Hard turns:</b> every other leg turns 90-120 degrees, and it is short (160-220 u). Burn into one and the hull swings
    /// so wide it overshoots the ring: coast the turn, burn out of it.</item>
    /// <item><b>I3 Walls:</b> every other ring has a WALL of danger prisms standing square across its line 110 u before
    /// it, 16 u wider than the ring and rounded on the inside of the turn. A danger prism stops the hull dead. Swing round the wall's edge and back into the ring:
    /// at cruise the hull turns that dog-leg easily, burning it cannot. Or let the crew's guns breach it first: they
    /// shoot hostile prisms, so a wall met at cruise is often open by the time the hull is there.</item>
    /// <item><b>I4 Breach:</b> hard turns and walls in turn (a wall on the ring after each hard one), with the smallest
    /// rings and the widest (22 u), nearest (90 u) walls.</item>
    /// </list>
    ///
    /// <para><b>The walls are a function of the broadcast rings</b> (<see cref="WallsFor"/>): which rings are walled is
    /// the ring's index, and the wall stands on the ring's axis. Nothing new is replicated. The vessel kit is
    /// untouched: the tank, the shield, the guns and the missiles are the shipped crew.</para>
    /// </summary>
    public static class BreachCourse
    {
        // ── The hull this course is cut for (read back off Orca.prefab and OrcaCrewConfig.asset by BreachCourseTests) ──

        /// <summary>`DefaultMinimumSpeed`, `DefaultThrottleScaler` and `boostMultiplier` (Orca.prefab).</summary>
        public const float MinimumSpeed = 8.5f, ThrottleScaler = 21.25f, BoostMultiplier = 5f;

        /// <summary>`PitchScaler` / `YawScaler` and `RotationThrottleScaler` (Orca.prefab): degrees a second at full stick.</summary>
        public const float TurnScaler = 44f, RotationThrottleScaler = 0.055f;

        /// <summary>`boostTank` (s) and `boostRefill` at rest (tank s a second) (OrcaCrewConfig.asset).</summary>
        public const float BoostTank = 3f, BoostRefill = 0.25f;

        /// <summary>A danger prism stops the hull (`SparrowVesselChangeSpeedByPrism`: 0.5 x 3, clamped) and the throttle
        /// recovers over `speedModifierDuration` x `dangerSlowDurationMultiplier` s.</summary>
        public const float DangerRecovery = 3f;

        public static float CruiseSpeed => MinimumSpeed + ThrottleScaler;
        public static float BoostSpeed => MinimumSpeed + ThrottleScaler * BoostMultiplier;
        public static float TurnRate(float speed) => speed * RotationThrottleScaler + TurnScaler;

        // ── The course's own constants ───────────────────────────────────

        /// <summary>Where every ring sits, from the cell centre.</summary>
        public const float ShellMin = 560f, ShellMax = 1050f;

        /// <summary>Ring 0, its axis, and the start line behind it.</summary>
        public static readonly Vector3 StartRing = new(0f, 0f, -820f);
        public static readonly Vector3 StartAxis = Vector3.right;
        public const float StartStandoff = 200f, StartSpacing = 40f;

        public const float CoreMargin = 60f, MinRingSeparation = 160f, RingClearance = 25f;

        /// <summary>The wall: danger prisms on a hex grid square to its ring's axis, this far apart, each this wide and thick.</summary>
        public const float WallSpacing = 9f, WallPrismSize = 7f, WallPrismDepth = 1.5f;

        /// <summary>The line round a wall passes its edge this far out (the hull's own width and a little).</summary>
        public const float WallClear = 10f;

        /// <summary>The gate races' nucleus fallback (RaceCourseSource.DefaultInnerRadiusFallback).</summary>
        public const float DefaultInnerRadius = 480f;

        const int CandidatesPerStep = 64;
        const int Attempts = 128;

        public static BreachSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new BreachSettings
            {
                RingCount = 16,
                InnerRadius = DefaultInnerRadius,
                MinLeg = 300f,
                MaxLeg = 380f,
                MinBend = 15f,
                MaxBend = new[] { 40f, 40f, 40f, 40f }[i],
                SharpEvery = new[] { 0, 2, 0, 3 }[i],
                SharpMin = 90f,
                SharpMax = new[] { 0f, 120f, 0f, 120f }[i],
                SharpLegMin = 160f,
                SharpLegMax = 220f,
                RingRadius = new[] { 70f, 64f, 60f, 54f }[i],
                WallEvery = new[] { 0, 0, 2, 3 }[i],
                WallPhase = 1,
                WallMargin = new[] { 0f, 0f, 16f, 22f }[i],
                WallAhead = new[] { 0f, 0f, 110f, 90f }[i],
            };
        }

        /// <summary>The leg into ring <paramref name="ring"/> turns hard off ring <paramref name="ring"/>-1's axis.</summary>
        public static bool IsSharp(int ring, in BreachSettings s) => s.SharpEvery > 0 && ring >= 1 && ring % s.SharpEvery == 0;

        /// <summary>Ring <paramref name="ring"/> has a wall before it: at I3 rings 3, 5, 7...; at I4 rings 4, 7, 10, 13, the
        /// ring after each hard turn's, so the hull comes out of the hard turn, reads the wall down a full leg, and leaves
        /// it on an ordinary one.</summary>
        public static bool IsWalled(int ring, in BreachSettings s) =>
            s.WallEvery > 0 && ring >= 2 && ring % s.WallEvery == s.WallPhase;

        // ── The start ────────────────────────────────────────────────────

        public static Pose[] StartPoses(int count)
        {
            count = Mathf.Max(1, count);
            var poses = new Pose[count];
            Vector3 across = Vector3.Cross(StartAxis, Vector3.up).normalized;
            Vector3 line = StartRing - StartAxis * StartStandoff;
            var facing = Quaternion.LookRotation(StartAxis, Vector3.up);
            for (int i = 0; i < count; i++)
                poses[i] = new Pose(line + across * ((i - 0.5f * (count - 1)) * StartSpacing), facing);
            return poses;
        }

        // ── The walk ─────────────────────────────────────────────────────

        public static List<RaceGate> Generate(int seed, in BreachSettings s)
        {
            var p = new List<Vector3>();
            var a = new List<Vector3>();
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var rng = new RaceCourseGeometry.Rng(unchecked(seed * 7919 + attempt * 104729 + 71));
                if (!TryWalk(ref rng, s, p, a)) continue;
                var rings = new List<RaceGate>(p.Count);
                for (int i = 0; i < p.Count; i++) rings.Add(new RaceGate(p[i], a[i], s.RingRadius));
                if (Valid(s, rings, out _)) return rings;
            }
            return null;
        }

        static bool TryWalk(ref RaceCourseGeometry.Rng rng, in BreachSettings s, List<Vector3> p, List<Vector3> a)
        {
            p.Clear();
            a.Clear();
            p.Add(StartRing);
            a.Add(StartAxis);
            for (int k = 1; k < s.RingCount; k++)
            {
                bool sharp = IsSharp(k, s);
                float legMin = sharp ? s.SharpLegMin : s.MinLeg, legMax = sharp ? s.SharpLegMax : s.MaxLeg;
                float bendMin = sharp ? s.SharpMin : s.MinBend, bendMax = sharp ? s.SharpMax : s.MaxBend;
                Vector3 prev = a[k - 1];
                Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(prev, Vector3.up), Vector3.forward);
                Vector3 e2 = Vector3.Cross(prev, e1);
                bool placed = false;
                for (int c = 0; c < CandidatesPerStep && !placed; c++)
                {
                    float bend = rng.Range(bendMin, bendMax);
                    float phi = rng.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 about = e1 * Mathf.Cos(phi) + e2 * Mathf.Sin(phi);
                    Vector3 d = RaceCourseGeometry.RotateAbout(prev, about, bend * Mathf.Deg2Rad);
                    Vector3 next = p[k - 1] + d * rng.Range(legMin, legMax);
                    if (!Fits(s, p, a, next, d)) continue;
                    p.Add(next);
                    a.Add(d);
                    placed = true;
                }
                if (!placed) return false;
            }
            return true;
        }

        static bool Fits(in BreachSettings s, List<Vector3> p, List<Vector3> a, Vector3 next, Vector3 axis)
        {
            int k = p.Count;   // the ring being placed
            Vector3 last = p[k - 1];
            float wallR = s.RingRadius + s.WallMargin;
            // the new leg keeps clear of every wall laid so far
            for (int j = 1; j < k; j++)
            {
                if (!IsWalled(j, s)) continue;
                if (j == k - 1 && Vector3.Dot(axis, a[j]) > 0.2f) continue;   // the leg out of a walled ring, on away from the wall behind it
                if (DistanceToSegment(p[j] - a[j] * s.WallAhead, last, next) < wallR + RingClearance) return false;
            }
            // the new ring's own wall keeps clear of every leg and ring so far, with room after the ring before
            if (IsWalled(k, s))
            {
                Vector3 c = next - axis * s.WallAhead;
                if ((c - last).magnitude < wallR + WallLeadIn) return false;
                for (int j = 1; j < k; j++)
                    if (DistanceToSegment(c, p[j - 1], p[j]) < wallR + RingClearance) return false;
                for (int j = 0; j < k - 1; j++)
                    if ((p[j] - c).magnitude < wallR + s.RingRadius + RingClearance) return false;
            }
            float r = next.magnitude;
            if (r < ShellMin || r > ShellMax) return false;
            if (DistanceToSegment(Vector3.zero, p[p.Count - 1], next) < s.InnerRadius + CoreMargin) return false;
            for (int i = 0; i < p.Count - 1; i++)
                if ((p[i] - next).magnitude < MinRingSeparation) return false;
            float clear = s.RingRadius + RingClearance;
            for (int i = 0; i < p.Count - 1; i++)
                if (DistanceToSegment(p[i], p[p.Count - 1], next) < clear) return false;
            for (int i = 1; i < p.Count - 1; i++)
                if (DistanceToSegment(next, p[i - 1], p[i]) < clear) return false;
            return true;
        }

        // ── The walls ────────────────────────────────────────────────────

        /// <summary>Ring <paramref name="k"/>'s wall: its centre, on the ring's axis <see cref="BreachSettings.WallAhead"/>
        /// before it.</summary>
        public static Vector3 WallCentre(IReadOnlyList<RaceGate> rings, int k, in BreachSettings s) =>
            rings[k].Position - rings[k].Axis * s.WallAhead;

        /// <summary>The wall's radius: the ring's and <see cref="BreachSettings.WallMargin"/>.</summary>
        public static float WallRadius(in RaceGate g, in BreachSettings s) => g.Radius + s.WallMargin;

        /// <summary>The side the line rounds ring <paramref name="k"/>'s wall on: the INSIDE of the turn into the leg (away
        /// from where the leg before carried the hull), so the line bends the way the hull is already turning.</summary>
        public static Vector3 RoundSide(IReadOnlyList<RaceGate> rings, int k)
        {
            var g = rings[k];
            Vector3 outward = Vector3.ProjectOnPlane(rings[k - 1].Axis, g.Axis);
            Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(g.Axis, Vector3.up), Vector3.forward);
            return outward.sqrMagnitude > 1e-4f ? -outward.normalized : e1;
        }

        /// <summary>The point the line passes ring <paramref name="k"/>'s wall at: <see cref="WallClear"/> past its edge, on
        /// <see cref="RoundSide"/>.</summary>
        public static Vector3 RoundPoint(IReadOnlyList<RaceGate> rings, int k, in BreachSettings s) =>
            WallCentre(rings, k, s) + RoundSide(rings, k) * (WallRadius(rings[k], s) + WallClear);

        /// <summary>True once <paramref name="hull"/> is past ring <paramref name="k"/>'s wall (or the ring has none).</summary>
        public static bool PastWall(Vector3 hull, IReadOnlyList<RaceGate> rings, int k, in BreachSettings s) =>
            !IsWalled(k, s) || Vector3.Dot(hull - WallCentre(rings, k, s), rings[k].Axis) > 0f;

        /// <summary>Every wall prism: points on a hex grid in each wall's plane within its radius, each with its ring (for
        /// the prism's facing).</summary>
        public static void WallsFor(IReadOnlyList<RaceGate> rings, in BreachSettings s, List<(Vector3 position, int ring)> wall)
        {
            wall.Clear();
            if (rings == null) return;
            for (int k = 1; k < rings.Count; k++)
            {
                if (!IsWalled(k, s)) continue;
                var g = rings[k];
                Vector3 c = WallCentre(rings, k, s);
                Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(g.Axis, Vector3.up), Vector3.forward);
                Vector3 e2 = Vector3.Cross(g.Axis, e1);
                float reach = WallRadius(g, s);
                int n = Mathf.CeilToInt(reach / WallSpacing) + 1;
                float rowH = WallSpacing * 0.8660254f;
                for (int row = -n; row <= n; row++)
                for (int col = -n; col <= n; col++)
                {
                    float x = (col + ((row & 1) != 0 ? 0.5f : 0f)) * WallSpacing, y = row * rowH;
                    if (x * x + y * y > reach * reach) continue;
                    wall.Add((c + e1 * x + e2 * y, k));
                }
            }
        }

        // ── Validity ─────────────────────────────────────────────────────

        /// <summary>The course the tests hold: inside the shell, clear of the core, rings apart, legs clear of other rings,
        /// every hard leg as hard as its level says, every wall after an ordinary leg with room before it, and no wall in
        /// any other leg's way.</summary>
        public static bool Valid(in BreachSettings s, IReadOnlyList<RaceGate> rings, out string why)
        {
            why = null;
            if (rings == null || rings.Count != s.RingCount) { why = "wrong ring count"; return false; }
            float clear = s.RingRadius + RingClearance;
            for (int i = 0; i < rings.Count; i++)
            {
                float r = rings[i].Position.magnitude;
                if (r < ShellMin - 1f || r > ShellMax + 1f) { why = $"ring {i} outside the shell ({r:F0})"; return false; }
                for (int j = 0; j < i; j++)
                    if ((rings[i].Position - rings[j].Position).magnitude < MinRingSeparation - 1f) { why = $"rings {j} and {i} too close"; return false; }
                if (i == 0) continue;
                if (DistanceToSegment(Vector3.zero, rings[i - 1].Position, rings[i].Position) < s.InnerRadius + CoreMargin - 1f)
                { why = $"leg {i} cuts the core"; return false; }
                for (int j = 0; j < rings.Count; j++)
                    if (j != i && j != i - 1 && DistanceToSegment(rings[j].Position, rings[i - 1].Position, rings[i].Position) < clear - 1f)
                    { why = $"leg {i} passes through ring {j}"; return false; }
                float bend = Vector3.Angle(rings[i - 1].Axis, rings[i].Axis);
                if (IsSharp(i, s) && bend < s.SharpMin - 0.5f) { why = $"hard leg {i} turns only {bend:F0}"; return false; }
                if (!IsSharp(i, s) && bend > s.MaxBend + 0.5f) { why = $"leg {i} turns {bend:F0}"; return false; }
                if (IsWalled(i, s))
                {
                    if (IsSharp(i, s)) { why = $"wall {i} follows a hard leg"; return false; }
                    Vector3 c = WallCentre(rings, i, s);
                    float wr = WallRadius(rings[i], s);
                    if ((c - rings[i - 1].Position).magnitude < wr + WallLeadIn) { why = $"wall {i} stands too near ring {i - 1}"; return false; }
                    if (IsSharp(i + 1, s)) { why = $"a hard leg leaves wall {i}'s ring"; return false; }
                    for (int j = 1; j < rings.Count; j++)
                    {
                        if (j == i) continue;
                        if (j == i + 1 && Vector3.Dot(rings[j].Axis, rings[i].Axis) > 0.2f) continue;   // the leg out, on away from the wall
                        if (DistanceToSegment(c, rings[j - 1].Position, rings[j].Position) < wr + RingClearance)
                        { why = $"wall {i} stands in leg {j}"; return false; }
                    }
                    for (int j = 0; j < rings.Count; j++)
                        if (j != i && (rings[j].Position - c).magnitude < wr + rings[j].Radius + RingClearance)
                        { why = $"wall {i} stands by ring {j}"; return false; }
                }
            }
            return true;
        }

        // ── The helm's burn: where in a leg the tank is spent ────────────

        /// <summary>A helm's reaction: the hull flies on this long before a correction lands. The walls are cut so that the
        /// dog-leg round one is a cruise turn after it and not a burning one (BreachCourseTests).</summary>
        public const float ReactionSeconds = 0.3f;

        /// <summary>How far sideways a hull at <paramref name="speed"/> can move while it covers <paramref name="forward"/>
        /// on one full-stick arc (its turn circle's radius when the arc is a quarter or more).</summary>
        public static float ArcReach(float speed, float forward)
        {
            float r = speed / (TurnRate(speed) * Mathf.Deg2Rad);
            return forward >= r ? r : r - Mathf.Sqrt(r * r - forward * forward);
        }

        /// <summary>A wall stands at least this far past the ring before it (beyond its own radius).</summary>
        public const float WallLeadIn = 120f;

        /// <summary>The point the helm flies for next: the line round ring <paramref name="k"/>'s wall until the hull is
        /// past it, then the ring's centre.</summary>
        public static Vector3 AimPoint(Vector3 hull, IReadOnlyList<RaceGate> rings, int k, in BreachSettings s) =>
            PastWall(hull, rings, k, s) ? rings[k].Position : RoundPoint(rings, k, s);

        /// <summary>The turn the hull makes at ring <paramref name="k"/> onto the next leg, in degrees (0 at the last ring).</summary>
        public static float TurnAfter(IReadOnlyList<RaceGate> rings, int k) =>
            k + 1 < rings.Count ? Vector3.Angle(rings[k].Axis, rings[k + 1].Position - rings[k].Position) : 0f;

        /// <summary>Burn only with the nose this close to the aim point.</summary>
        public const float BurnAlignDeg = 8f;

        /// <summary>A turn at a ring harder than this is coasted: the burn stops this far out, so the hull reaches the ring
        /// at cruise and turns round the tight circle.</summary>
        public const float HardTurnDeg = 70f, HardTurnStop = 150f;

        /// <summary>A wall is coasted: the burn stops this far from the point the line rounds it, and does not start again
        /// until the hull is through the ring (the dog-leg back in is a cruise turn).</summary>
        public const float WallStop = 180f;

        /// <summary>A burn starts only with this much in the tank. The helm's tank refills only while the burn is let go
        /// (OrcaCrew.StepHelm: a burn held on an empty tank spends each frame's refill as it comes), so a helm that holds
        /// the burn on empty holds the tank at empty.</summary>
        public const float MinBurnStart = 1f;

        /// <summary>
        /// The burn an AI helm (or a reading pilot) holds: with the nose on its aim point, not into a hard turn at the ring
        /// (coast the last <see cref="HardTurnStop"/>), and not round a wall (coast from <see cref="WallStop"/> out until
        /// through the ring). The tank itself is the vessel's: a burn with an empty tank is no burn.
        /// </summary>
        public static bool BurnWanted(Vector3 hull, Vector3 forward, IReadOnlyList<RaceGate> rings, int next, in BreachSettings s,
            float tank = BoostTank, bool burning = false)
        {
            if (rings == null || next < 0 || next >= rings.Count) return false;
            if (!burning && tank < MinBurnStart) return false;   // let the tank refill: held on empty, it never does
            Vector3 aim = AimPoint(hull, rings, next, s);
            Vector3 to = aim - hull;
            float d = to.magnitude;
            if (d < 1f) return false;
            float off = Vector3.Angle(forward, to);
            if (off > BurnAlignDeg) return false;
            if (TurnAfter(rings, next) > HardTurnDeg && d < HardTurnStop) return false;
            if (IsWalled(next, s) && (PastWall(hull, rings, next, s) || d < WallStop)) return false;
            return true;
        }

        // ── Geometry ─────────────────────────────────────────────────────

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-6f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
