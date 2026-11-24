using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One intensity's cut of Decoy Run (<see cref="DecoyRunCourse.ForIntensity"/>).</summary>
    public struct DecoyRunSettings
    {
        public int RingCount;
        public float InnerRadius;            // the nucleus shell: no leg passes inside it (+ CoreMargin)
        public int Group;                    // rings a cycle is cut for: 1 a door, 2 a skewer (one zip), 3 a skewer and a corner (two zips)
        public bool TiltEachGroup;           // every group after the first lies on its own flat, rolled about the hinge
        public float SkewerLegMin, SkewerLegMax;   // a group's first leg: its two rings on one straight line
        public float SkewerBendMax;                // degrees the skewer's second ring sits off the first's line
        public float CornerLegMin, CornerLegMax;   // a group's second leg (I4): the hairpin the second zip doubles back through
        public float CornerBendMin, CornerBendMax;
        public float LinkLegMin, LinkLegMax;       // the leg from one group to the next
        public float LinkBendMin, LinkBendMax;
        public float TiltMin, TiltMax;             // degrees the next flat rolls about the hinge
        public float RingRadius;
        public int StartTimeLevel;           // the card's StartingElements: 5 gives each trigger its own decoy
    }

    /// <summary>
    /// Decoy Run, the JACKS' Time race (DECOY_RUN.md). The Jacks fly at the Rhino rig's plain 50 u/s, so the race is their
    /// Time card and their take-off: herding, the hull holds still while the jacks run on its board (208 u/s); taking off,
    /// the hull JUMPS to halfway between them (a teleport: it threads nothing, it puts the hull on a line); netting, a
    /// trigger flies it fast to a decoy (any distance in 0.35-1.2 s), and that flight threads what it passes. One cycle is
    /// therefore a take-off onto a line and one zip down it (a second zip with Time 5), and the rings are cut in GROUPS a
    /// cycle can take: the skill is putting two thumbs where their midpoint lines the hull up and a decoy ends the line.
    ///
    /// <list type="number">
    /// <item><b>I1 Doors:</b> single rings on one flat, wide mouths, the turns between them hard. Run the jacks past the
    /// ring (one lays the decoy), centre them behind it, take off, zip.</item>
    /// <item><b>I2 Skewers:</b> rings in pairs on one straight line: a zip laid down the line threads both. Line up the
    /// pair, lay the decoy past the second.</item>
    /// <item><b>I3 Tilt:</b> skewers again, each on its own FLAT, rolled 25-45 degrees about the line the last ring was
    /// flown along: roll the hull (one fish up, one down) to lay the board onto the next flat, then line up.</item>
    /// <item><b>I4 Knots:</b> Time 5 from the start (the second decoy): a skewer and then a HAIRPIN, the third ring
    /// 125-155 degrees back beside the skewer, so the second zip doubles back from the first decoy through it (and the
    /// jack that lays the second decoy has a short run); flats rolled up to 55 degrees, the smallest mouths.</item>
    /// </list>
    ///
    /// <para><b>The flats are a function of the broadcast rings</b> (<see cref="FlatNormal"/>): flat 0 is the cell's
    /// equator (the start line lies on it and every pilot starts with its board on it, <see cref="StartPoses"/>), and flat
    /// k contains the last ring of flat k-1 and its axis, so the ring after the hinge decides the roll. The chalk dots
    /// that mark each flat (<see cref="ChalkFor"/>) follow from the rings too, so nothing new is replicated.</para>
    /// </summary>
    public static class DecoyRunCourse
    {
        // ── The vessel this course is cut for (read back off JacksConfig.asset by DecoyRunCourseTests) ──

        /// <summary>`dogSpeed` x `labScale` (520 x 0.4): a jack's run on the board, u/s.</summary>
        public const float JackSpeed = 208f;

        /// <summary>`travelSpeed`, `travelMinSeconds`, `travelMaxSeconds`: the fast travel to a decoy.</summary>
        public const float TravelSpeed = 900f, TravelMinSeconds = 0.35f, TravelMaxSeconds = 1.2f;

        /// <summary>`teleportGuard`: the next travel waits for this one to land plus this.</summary>
        public const float TravelGuard = 0.6f;

        /// <summary><c>PilotSwap.CooldownSeconds</c>: between take-off and landing, either way.</summary>
        public const float SwapSeconds = 0.75f;

        /// <summary>The Rhino rig's plain speed (`DefaultThrottleScaler` 50 on Jacks.prefab, no ramp).</summary>
        public const float FlightSpeed = 50f;

        // ── The course's own constants ───────────────────────────────────

        /// <summary>Where every ring sits, from the cell centre.</summary>
        public const float ShellMin = 600f, ShellMax = 1050f;

        /// <summary>The start: ring 0 on flat 0, flown along +X. Flat 0 is the plane <see cref="StartFlatHeight"/> above the
        /// cell's centre, square to its up: a disc about 830 u across that never meets the nucleus, so a dogleg can bend
        /// hard anywhere on it (the equator's 620-1050 u annulus cannot hold a right-angle pair of 300-460 u legs).</summary>
        public const float StartFlatHeight = 640f;
        public static readonly Vector3 StartRing = new(0f, StartFlatHeight, -700f);
        public static readonly Vector3 StartAxis = Vector3.right;
        public static readonly Vector3 StartFlatNormal = Vector3.up;

        /// <summary>The pilots line up abreast this far behind ring 0, this far apart, all on the equator.</summary>
        public const float StartStandoff = 260f, StartSpacing = 34f;

        /// <summary>No leg passes closer than this outside the nucleus shell.</summary>
        public const float CoreMargin = 60f;

        /// <summary>Ring centres keep this far apart, and every leg clears every ring but its own two by a mouth and this.</summary>
        public const float MinRingSeparation = 170f, RingClearance = 25f;

        /// <summary>How far past a ring (past the second, on a skewer) a decoy is laid, so the zip to it lands clear.</summary>
        public const float Overshoot = 45f;

        /// <summary>How far behind the next ring's mouth, on its line, the AI's take-off lands.</summary>
        public const float JumpLead = 70f;

        /// <summary>A ring is on the board when its centre is within this share of its radius of the board's plane.</summary>
        public const float OnBoardShare = 0.45f;

        /// <summary>The chalk: a small super-shielded block every this many units along a leg of a flat.</summary>
        public const float ChalkSpacing = 48f, ChalkSize = 5f, ChalkLane = 30f;

        /// <summary>The gate races' nucleus fallback (RaceCourseSource.DefaultInnerRadiusFallback; the tests hold the two equal).</summary>
        public const float DefaultInnerRadius = 480f;

        const int CandidatesPerStep = 64;
        const int Attempts = 128;

        public static DecoyRunSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new DecoyRunSettings
            {
                RingCount = 12,
                InnerRadius = DefaultInnerRadius,
                Group = new[] { 1, 2, 2, 3 }[i],
                TiltEachGroup = i >= 2,
                SkewerLegMin = new[] { 0f, 300f, 300f, 280f }[i],
                SkewerLegMax = new[] { 0f, 440f, 420f, 400f }[i],
                SkewerBendMax = new[] { 0f, 4f, 4f, 4f }[i],
                CornerLegMin = new[] { 0f, 0f, 0f, 260f }[i],
                CornerLegMax = new[] { 0f, 0f, 0f, 380f }[i],
                CornerBendMin = new[] { 0f, 0f, 0f, 125f }[i],
                CornerBendMax = new[] { 0f, 0f, 0f, 155f }[i],
                LinkLegMin = new[] { 380f, 360f, 340f, 320f }[i],
                LinkLegMax = new[] { 560f, 540f, 520f, 480f }[i],
                LinkBendMin = new[] { 30f, 50f, 50f, 40f }[i],
                LinkBendMax = new[] { 75f, 100f, 100f, 90f }[i],
                TiltMin = new[] { 0f, 0f, 25f, 30f }[i],
                TiltMax = new[] { 0f, 0f, 45f, 55f }[i],
                RingRadius = new[] { 72f, 62f, 56f, 48f }[i],
                StartTimeLevel = new[] { 0, 0, 0, 5 }[i],
            };
        }

        /// <summary>The flat ring <paramref name="ring"/> lies on: 0 for every ring at I1-I2, its group at I3-I4.</summary>
        public static int FlatOf(int ring, in DecoyRunSettings s) => s.TiltEachGroup ? ring / Mathf.Max(1, s.Group) : 0;

        /// <summary>Ring <paramref name="ring"/> is the first of its group.</summary>
        public static bool StartsGroup(int ring, in DecoyRunSettings s) => ring % Mathf.Max(1, s.Group) == 0;

        /// <summary>Ring <paramref name="ring"/> and the one after it are a SKEWER: two rings on one straight line, so one zip
        /// threads both (the first two rings of every group from I2).</summary>
        public static bool StartsSkewer(int ring, in DecoyRunSettings s) =>
            s.Group >= 2 && StartsGroup(ring, s) && ring + 1 < s.RingCount;

        // ── Timing ───────────────────────────────────────────────────────

        /// <summary>One fast travel of <paramref name="distance"/> u (the shipped clamp).</summary>
        public static float TravelSeconds(float distance) =>
            Mathf.Clamp(distance / TravelSpeed, TravelMinSeconds, TravelMaxSeconds);

        // ── The start ────────────────────────────────────────────────────

        /// <summary>
        /// The spawn poses, cell-local: abreast on flat 0, <see cref="StartStandoff"/> behind ring 0, all flying +X with
        /// the cell's up as their up, so every pilot's board IS flat 0 from the first frame. Every slot is the same distance
        /// behind the start line's plane; they are spread across it, slot 0 at one end.
        /// </summary>
        public static Pose[] StartPoses(int count)
        {
            count = Mathf.Max(1, count);
            var poses = new Pose[count];
            Vector3 across = Vector3.Cross(StartAxis, StartFlatNormal).normalized;   // in the equator, square to the axis
            Vector3 line = StartRing - StartAxis * StartStandoff;
            var facing = Quaternion.LookRotation(StartAxis, StartFlatNormal);
            for (int i = 0; i < count; i++)
                poses[i] = new Pose(line + across * ((i - 0.5f * (count - 1)) * StartSpacing), facing);
            return poses;
        }

        // ── The walk ─────────────────────────────────────────────────────

        /// <summary>The course's rings, cell-local, or null when no attempt fits. Ring k's axis is the direction its leg
        /// arrives from, so a leg flown centre to centre threads it square; a skewer's first ring faces down the skewer.</summary>
        public static List<RaceGate> Generate(int seed, in DecoyRunSettings s)
        {
            var points = new List<Vector3>();
            var axes = new List<Vector3>();
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var rng = new RaceCourseGeometry.Rng(unchecked(seed * 7919 + attempt * 104729 + 23));
                if (!TryWalk(ref rng, s, points, axes)) continue;
                var rings = new List<RaceGate>(points.Count);
                for (int i = 0; i < points.Count; i++) rings.Add(new RaceGate(points[i], axes[i], s.RingRadius));
                return rings;
            }
            return null;
        }

        static bool TryWalk(ref RaceCourseGeometry.Rng rng, in DecoyRunSettings s, List<Vector3> p, List<Vector3> a)
        {
            p.Clear();
            a.Clear();
            p.Add(StartRing);
            a.Add(StartAxis);
            Vector3 normal = StartFlatNormal;
            for (int k = 1; k < s.RingCount; k++)
            {
                bool link = StartsGroup(k, s);
                bool placed = false;
                for (int c = 0; c < CandidatesPerStep && !placed; c++)
                {
                    Vector3 n = normal;
                    if (link && s.TiltEachGroup)
                    {
                        // the next flat rolls about the hinge: the last ring's line, so the hull flown out along it is on both
                        float tilt = rng.Range(s.TiltMin, s.TiltMax) * (rng.Unit() < 0.5f ? -1f : 1f);
                        n = RaceCourseGeometry.RotateAbout(normal, a[k - 1], tilt * Mathf.Deg2Rad);
                    }
                    // a group's legs: the skewer's (all but straight on), then the corner's (I4)
                    bool skewer = !link && StartsGroup(k - 1, s);
                    float bend = link ? rng.Range(s.LinkBendMin, s.LinkBendMax)
                               : skewer ? rng.Range(0f, s.SkewerBendMax) : rng.Range(s.CornerBendMin, s.CornerBendMax);
                    float leg = link ? rng.Range(s.LinkLegMin, s.LinkLegMax)
                              : skewer ? rng.Range(s.SkewerLegMin, s.SkewerLegMax) : rng.Range(s.CornerLegMin, s.CornerLegMax);
                    Vector3 d = RaceCourseGeometry.RotateAbout(a[k - 1], n, bend * Mathf.Deg2Rad * (rng.Unit() < 0.5f ? -1f : 1f));
                    Vector3 next = p[k - 1] + d * leg;
                    if (!Fits(s, p, next)) continue;
                    p.Add(next);
                    a.Add(d);
                    // a skewer's two rings face down its line, so the one zip that threads both crosses both square
                    if (skewer) a[k - 1] = d;
                    normal = n;
                    placed = true;
                }
                if (!placed) return false;
            }
            return Valid(s, ToRings(p, a, s.RingRadius), out _);
        }

        static List<RaceGate> ToRings(List<Vector3> p, List<Vector3> a, float radius)
        {
            var rings = new List<RaceGate>(p.Count);
            for (int i = 0; i < p.Count; i++) rings.Add(new RaceGate(p[i], a[i], radius));
            return rings;
        }

        static bool Fits(in DecoyRunSettings s, List<Vector3> p, Vector3 next)
        {
            float r = next.magnitude;
            if (r < ShellMin || r > ShellMax) return false;
            if (DistanceToSegment(Vector3.zero, p[p.Count - 1], next) < s.InnerRadius + CoreMargin) return false;
            for (int i = 0; i < p.Count - 1; i++)
                if ((p[i] - next).magnitude < MinRingSeparation) return false;
            // the new leg clears every older ring, and no older leg passes through the new ring
            float clear = s.RingRadius + RingClearance;
            for (int i = 0; i < p.Count - 1; i++)
                if (DistanceToSegment(p[i], p[p.Count - 1], next) < clear) return false;
            for (int i = 1; i < p.Count - 1; i++)
                if (DistanceToSegment(next, p[i - 1], p[i]) < clear) return false;
            return true;
        }

        /// <summary>The course the tests hold: inside the shell, clear of the core, rings apart, legs clear of other rings,
        /// every flat's rings on their flat, and every flat after the first containing its hinge.</summary>
        public static bool Valid(in DecoyRunSettings s, IReadOnlyList<RaceGate> rings, out string why)
        {
            why = null;
            if (rings == null || rings.Count != s.RingCount) { why = "wrong ring count"; return false; }
            if ((rings[0].Position - StartRing).sqrMagnitude > 1e-4f) { why = "ring 0 is not on the start line"; return false; }
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
            }
            for (int i = 0; i < rings.Count; i++)
            {
                Vector3 n = FlatNormal(rings, i, s);
                Vector3 origin = rings[FirstOfFlat(i, s)].Position;
                if (Mathf.Abs(Vector3.Dot(rings[i].Position - origin, n)) > 1f) { why = $"ring {i} is off its flat"; return false; }
                if (Mathf.Abs(Vector3.Dot(rings[i].Axis, n)) > 0.02f) { why = $"ring {i}'s axis leaves its flat"; return false; }
            }
            return true;
        }

        static int FirstOfFlat(int ring, in DecoyRunSettings s)
        {
            int flat = FlatOf(ring, s);
            int i = ring;
            while (i > 0 && FlatOf(i - 1, s) == flat) i--;
            return i;
        }

        /// <summary>
        /// The normal of the flat ring <paramref name="ring"/> lies on, from the rings alone (every peer derives the same):
        /// flat 0's up (the cell's) for flat 0; for flat k, the plane through the last ring of flat k-1 that holds its axis (the hinge) and
        /// the first ring of flat k. Signed to lean the way the equator's up does, so a pilot reads "up" consistently.
        /// </summary>
        public static Vector3 FlatNormal(IReadOnlyList<RaceGate> rings, int ring, in DecoyRunSettings s)
        {
            int first = FirstOfFlat(ring, s);
            if (FlatOf(ring, s) == 0 || first == 0) return StartFlatNormal;
            var hinge = rings[first - 1];
            Vector3 n = RaceCourseGeometry.SafeNormalize(Vector3.Cross(hinge.Axis, rings[first].Position - hinge.Position), StartFlatNormal);
            Vector3 prev = FlatNormal(rings, first - 1, s);
            return Vector3.Dot(n, prev) < 0f ? -n : n;
        }

        // ── The chalk: each flat drawn as two dotted rails along its legs ─

        /// <summary>
        /// The chalk dots, cell-local or world (as the rings are): two dotted RAILS either side of the start line and every
        /// leg, on that leg's flat (a leg into a new flat lies on the new one), <see cref="ChalkLane"/> outside the mouth of
        /// the ring the leg runs to. So the herding view shows the flat as a lane, and no dot sits where a hull flies: a dot
        /// is dropped when it comes within a ring's radius of any leg or of any ring.
        /// </summary>
        public static void ChalkFor(IReadOnlyList<RaceGate> rings, in DecoyRunSettings s, float startStandoff, List<Vector3> dots)
        {
            dots.Clear();
            if (rings == null || rings.Count == 0) return;
            Vector3 start = rings[0].Position - rings[0].Axis * startStandoff;
            for (int i = 0; i < rings.Count; i++)
            {
                Vector3 from = i == 0 ? start : rings[i - 1].Position, to = rings[i].Position;
                Vector3 leg = to - from;
                float len = leg.magnitude;
                if (len < 1f) continue;
                Vector3 side = RaceCourseGeometry.SafeNormalize(Vector3.Cross(FlatNormal(rings, i, s), leg / len), Vector3.right);
                float lane = rings[i].Radius + ChalkLane;
                int n = Mathf.FloorToInt(len / ChalkSpacing);
                for (int k = 1; k < n; k++)
                {
                    Vector3 mid = from + leg * (k / (float)n);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        Vector3 at = mid + side * (sgn * lane);
                        if (ClearOfFlight(rings, start, at)) dots.Add(at);
                    }
                }
            }
        }

        static bool ClearOfFlight(IReadOnlyList<RaceGate> rings, Vector3 start, Vector3 at)
        {
            for (int j = 0; j < rings.Count; j++)
            {
                float keep = rings[j].Radius + ChalkSize * 2f;
                if ((at - rings[j].Position).sqrMagnitude < keep * keep) return false;
                Vector3 from = j == 0 ? start : rings[j - 1].Position;
                if (DistanceToSegment(at, from, rings[j].Position) < rings[j].Radius + ChalkSize) return false;
            }
            return true;
        }

        // ── The AI's cycle (the same verbs a person has) ─────────────────

        /// <summary>A leg from <paramref name="from"/> to <paramref name="to"/> threads <paramref name="ring"/>: it passes the
        /// centre inside the mouth with the hull's own margin, and it crosses the ring's plane.</summary>
        public static bool Threads(Vector3 from, Vector3 to, in RaceGate ring, float margin = 8f)
        {
            if (DistanceToSegment(ring.Position, from, to) > ring.Radius - margin) return false;
            float a = Vector3.Dot(from - ring.Position, ring.Axis), b = Vector3.Dot(to - ring.Position, ring.Axis);
            return (a < 0f && b > 0f) || (a > 0f && b < 0f);
        }

        /// <summary>The rings from <paramref name="next"/> on that lie on a board through <paramref name="hull"/> with normal
        /// <paramref name="up"/>, in order, up to <paramref name="max"/>, and never past the end of their flat.</summary>
        public static int RingsOnBoard(Vector3 hull, Vector3 up, IReadOnlyList<RaceGate> rings, int next, int count, int max,
                                       in DecoyRunSettings s)
        {
            int n = 0;
            int flat = next < count ? FlatOf(next, s) : -1;
            for (int i = next; i < count && n < max; i++)
            {
                if (FlatOf(i, s) != flat) break;
                if (Mathf.Abs(Vector3.Dot(rings[i].Position - hull, up)) > rings[i].Radius * OnBoardShare) break;
                n++;
            }
            return n;
        }

        static Vector3 OnBoard(Vector3 p, Vector3 hull, Vector3 up) => p - up * Vector3.Dot(p - hull, up);

        /// <summary>
        /// One cycle for the rings from <paramref name="next"/> on the board, at most <paramref name="maxRings"/>, flown in
        /// <paramref name="zips"/> fast travels (1; 2 with Time 5). The TAKE-OFF threads nothing: it is a jump
        /// (<c>VesselTransformer.SetPose</c>, a teleport, which a gate race never credits), so it only puts the hull on
        /// the line: <see cref="JumpLead"/> behind the next ring, on its axis (or on the skewer's line). Zip 1 flies from
        /// there to decoy 0, <see cref="Overshoot"/> past the ring (past both, on a skewer); zip 2 flies on from decoy 0
        /// through the ring after to decoy 1. Every point is laid on the board. The jacks' ends are chosen so their
        /// midpoint is the take-off and the longer run is as short as the samples find. Returns the rings the zips thread
        /// (0: no plan), and the seconds the jacks run.
        /// </summary>
        public static int PlanCycle(Vector3 hull, Vector3 up, IReadOnlyList<RaceGate> rings, int next, int count, int zips,
                                    int maxRings, in DecoyRunSettings s, out JacksRacePlan plan, out float runSeconds)
        {
            plan = JacksRacePlan.Jump(hull, hull);
            runSeconds = 0f;
            maxRings = Mathf.Min(maxRings, count - next);
            if (maxRings <= 0 || zips <= 0) return 0;

            // zip 1: a door, or both rings of a skewer
            var first = rings[next];
            Vector3 c0 = OnBoard(first.Position, hull, up);
            int take = StartsSkewer(next, s) && maxRings >= 2 ? 2 : 1;
            Vector3 c1 = take == 2 ? OnBoard(rings[next + 1].Position, hull, up) : c0;
            Vector3 dir = RaceCourseGeometry.SafeNormalize(take == 2 ? c1 - c0 : OnBoard(c0 + first.Axis, hull, up) - c0, first.Axis);
            Vector3 m = c0 - dir * JumpLead;
            Vector3 d0 = c1 + dir * Overshoot;
            for (int j = 0; j < take; j++)
                if (!Threads(m, d0, rings[next + j])) return 0;
            int made = take;

            // zip 2 (Time 5): on from decoy 0 through the next ring
            bool second = zips >= 2 && made < maxRings;
            Vector3 d1 = d0;
            if (second)
            {
                var ring = rings[next + made];
                Vector3 c = OnBoard(ring.Position, hull, up);
                d1 = c + RaceCourseGeometry.SafeNormalize(c - d0, ring.Axis) * Overshoot;
                if (Threads(d0, d1, ring)) made++;
                else second = false;
            }

            // The jacks' ends are a = m + v and b = m - v. Jack 0 lays decoy 0 on its way; with two zips jack 1 lays decoy 1.
            // v is sampled in the board over the span of the decoys' offsets, and the plan with the shorter longest run wins.
            Vector3 u0 = d0 - m, u1 = second ? m - d1 : Vector3.Cross(up, u0);
            float best = float.MaxValue;
            for (int ia = 0; ia <= 10; ia++)
                for (int ib = -5; ib <= 5; ib++)
                {
                    Vector3 v = u0 * (ia / 10f) + u1 * (ib / (second ? 5f : 10f));
                    Vector3 ea = m + v, eb = m - v;
                    float run0 = (d0 - hull).magnitude + (ea - d0).magnitude;
                    float run1 = second ? (d1 - hull).magnitude + (eb - d1).magnitude : (eb - hull).magnitude;
                    float run = Mathf.Max(run0, run1);
                    if (run >= best) continue;
                    best = run;
                    plan = new JacksRacePlan
                    {
                        Jack0Via = d0, Jack0To = ea,
                        Jack1Via = second ? d1 : eb, Jack1To = eb,
                        Jack0Lays = 0,
                        Jack1Lays = second ? 1 : -1,
                        FirstTravel = 0,
                        SecondTravel = second ? 1 : -1,
                    };
                }
            runSeconds = best / JackSpeed;
            return made;
        }

        /// <summary>How many rings one cycle is cut to take from <paramref name="next"/> with <paramref name="zips"/> fast
        /// travels: a door is one ring, a skewer two, and a second zip one more; never past the end of the flat.</summary>
        public static int RingsACycleTakes(int next, int zips, in DecoyRunSettings s)
        {
            int count = s.RingCount;
            if (next >= count) return 0;
            int n = StartsSkewer(next, s) ? 2 : 1;
            if (zips >= 2 && next + n < count) n++;
            int flat = FlatOf(next, s), end = next;
            while (end < count && FlatOf(end, s) == flat) end++;
            return Mathf.Min(n, end - next);
        }

        /// <summary>
        /// The seconds one cycle costs a pilot who acts the moment it can: the run (at least the swap's cooldown), the
        /// take-off (instant), each travel and the guard between two, and the swap back (its cooldown runs from the
        /// take-off). What the harness sums; the Unity AI does the same steps.
        /// </summary>
        public static float CycleSeconds(float runSeconds, Vector3 jumpEnd, Vector3 decoy0, Vector3 decoy1, int zips)
        {
            float t = Mathf.Max(runSeconds, SwapSeconds);   // landing to take-off
            float flying = TravelSeconds((decoy0 - jumpEnd).magnitude);
            if (zips >= 2) flying += TravelGuard + TravelSeconds((decoy1 - decoy0).magnitude);
            return t + Mathf.Max(flying, SwapSeconds);       // take-off to the next landing
        }

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
