using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One intensity's cut of Perch Run (<see cref="PerchRunCourse.ForIntensity"/>).</summary>
    public struct PerchRunSettings
    {
        public int RingCount;
        public float InnerRadius;              // the nucleus shell: no leg passes inside it (+ CoreMargin)
        public float MinLeg, MaxLeg;           // ring to ring, u
        public float MinBend, MaxBend;         // degrees off the ring's axis at a plain bend
        public int Period;                     // the roles repeat every Period rings (0: every ring plain)
        public int HairpinSlot;                // ring k with k % Period == HairpinSlot turns a hairpin after it (-1 none)
        public int BlockerSlot;                // the leg into ring k with k % Period == BlockerSlot has a BLOCKER on it (-1 none)
        public int DoubleSlot;                 // ring k with k % Period == DoubleSlot and the ring after it are a DOUBLE (-1 none)
        public float HairpinMin, HairpinMax;   // degrees, the hairpin's turn
        public float CoralSize;                // a coral head's cube side, u
        public float RingRadius;
    }

    /// <summary>A coral head: one block a dart stops on.</summary>
    public readonly struct Coral
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;   // square to the line it stands on: a face toward the dart
        public readonly float Size;
        public readonly int Ring;        // the ring whose leg it blocks

        public Coral(Vector3 position, Quaternion rotation, float size, int ring)
        {
            Position = position;
            Rotation = rotation;
            Size = size;
            Ring = ring;
        }
    }

    /// <summary>
    /// Perch Run, the GOBY's Time race (PERCH_RUN.md). The Goby cruises on its own flight and DARTS: hold to charge a
    /// length (140-600 u over 0.8 s), let go, and it flies there in one second, stopping on the first block on its line
    /// (Time 5: it phases through). So the race is the dart's two numbers, its LINE and its LENGTH, and the course is
    /// rings with CORAL - single blocks the dart stops on - that turn those two numbers into decisions:
    ///
    /// <list type="number">
    /// <item><b>I1 Darts:</b> rings in open water. Aim, charge to just past the ring, dart, turn while it reloads.</item>
    /// <item><b>I2 Hairpins:</b> every other ring turns back 105-140 degrees, after a short leg in (300-340 u) and
    /// before a long leg out (510-540 u). A full charge overshoots the mouth by 260 u or more and the way back is past a
    /// dart's reach: two darts. Charge to just past the mouth (the reach ring the charge draws) and one dart turns it.</item>
    /// <item><b>I3 Blockers:</b> plus a coral head on the straight line of every fourth leg, 62% of the way along: a
    /// dart at the ring's centre stops on it. Dart at the far RIM instead (a line inside the mouth that clears the head)
    /// and it never touches. Hairpins every fourth ring.</item>
    /// <item><b>I4 Reef:</b> plus DOUBLES: two rings straight on down one line, close enough that one full dart threads
    /// both, if the line is found; the smallest mouths and heads.</item>
    /// </list>
    ///
    /// <para><b>The coral is a function of the broadcast rings</b> (<see cref="CoralFor"/>): a blocker stands on its leg,
    /// chosen by index; so nothing new is replicated. A gate ring counts a
    /// crossing either way, so a Goby that overshoots can come back through. The vessel is untouched: the dart, its
    /// charge, its stop on a block, the perches and the Pistol are the shipped kit.</para>
    /// </summary>
    public static class PerchRunCourse
    {
        // ── The vessel this course is cut for (read back off GobyPistolConfig.asset by PerchRunCourseTests) ──

        /// <summary>`dartRange` / `dartMinRange`: a full and an untouched charge.</summary>
        public const float DartRange = 600f, DartMinRange = 140f;

        /// <summary>`dartChargeSeconds`, `dartSeconds`, `dartCooldown`.</summary>
        public const float DartChargeSeconds = 0.8f, DartSeconds = 1f, DartCooldown = 1.2f;

        /// <summary>`hullRadius`: the dart's line is this wide.</summary>
        public const float HullRadius = 3f;

        /// <summary>`aiCruiseSpeed`, `aiTurnRate` (the seat's AI); `perchSeconds`; `islandMinLength`; `islandQueue`.</summary>
        public const float AiCruiseSpeed = 100f, AiTurnRate = 90f, PerchSeconds = 1.5f, IslandMinLength = 900f;
        public const int IslandQueue = 3;

        // ── The course's own constants ───────────────────────────────────

        /// <summary>Where every ring sits, from the cell centre.</summary>
        public const float ShellMin = 600f, ShellMax = 1050f;

        /// <summary>Ring 0, its axis, and the start line behind it.</summary>
        public static readonly Vector3 StartRing = new(0f, 0f, -820f);
        public static readonly Vector3 StartAxis = Vector3.right;
        public const float StartStandoff = 300f, StartSpacing = 30f;

        /// <summary>No leg passes closer than this outside the nucleus shell.</summary>
        public const float CoreMargin = 60f;

        /// <summary>Ring centres keep this far apart; a leg clears every other ring by its radius plus this.</summary>
        public const float MinRingSeparation = 170f, RingClearance = 25f;

        /// <summary>A blocker stands this far along its leg.</summary>
        public const float BlockerAt = 0.62f;

        /// <summary>Every line that is not a coral head's own clears it by this much more than its half-diagonal and the
        /// hull, so no dart stops by accident.</summary>
        public const float CoralClearance = 8f;

        /// <summary>A dart is aimed this far past the ring.</summary>
        public const float Overshoot = 40f;

        /// <summary>A rim shot threads its ring this far inside the mouth.</summary>
        public const float RimMargin = 6f;

        /// <summary>The gate races' nucleus fallback (RaceCourseSource.DefaultInnerRadiusFallback).</summary>
        public const float DefaultInnerRadius = 480f;

        const int CandidatesPerStep = 64;
        const int Attempts = 128;

        public static PerchRunSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new PerchRunSettings
            {
                RingCount = 16,
                InnerRadius = DefaultInnerRadius,
                MinLeg = new[] { 300f, 300f, 320f, 340f }[i],
                MaxLeg = new[] { 500f, 520f, 540f, 560f }[i],
                MinBend = new[] { 15f, 15f, 20f, 20f }[i],
                MaxBend = new[] { 40f, 40f, 45f, 50f }[i],
                Period = new[] { 0, 2, 4, 5 }[i],
                HairpinSlot = new[] { -1, 1, 3, 4 }[i],
                BlockerSlot = new[] { -1, -1, 1, 1 }[i],
                DoubleSlot = new[] { -1, -1, -1, 2 }[i],
                HairpinMin = new[] { 0f, 105f, 105f, 110f }[i],
                HairpinMax = new[] { 0f, 140f, 140f, 150f }[i],
                CoralSize = new[] { 34f, 34f, 30f, 26f }[i],
                RingRadius = new[] { 72f, 66f, 60f, 54f }[i],
            };
        }

        static bool Slot(int ring, int slot, in PerchRunSettings s) => s.Period > 0 && slot >= 0 && ring >= 1 && ring % s.Period == slot;

        /// <summary>Ring <paramref name="ring"/> is followed by a hairpin.</summary>
        public static bool IsHairpin(int ring, in PerchRunSettings s) => Slot(ring, s.HairpinSlot, s) && ring + 1 < s.RingCount;

        /// <summary>The leg into ring <paramref name="ring"/> has a blocker on it.</summary>
        public static bool HasBlocker(int ring, in PerchRunSettings s) => Slot(ring, s.BlockerSlot, s) && ring >= 2;

        /// <summary>Ring <paramref name="ring"/> and the one after it are a DOUBLE: the second straight on down the first's
        /// line, close enough that one full dart threads both.</summary>
        public static bool StartsDouble(int ring, in PerchRunSettings s) => Slot(ring, s.DoubleSlot, s) && ring + 1 < s.RingCount;

        /// <summary>A double's second leg, and the short leg into its first ring, so a full dart from the ring before
        /// reaches past both: 300 + 240 + the overshoot fits the 600 u dart.</summary>
        public const float DoubleLegMin = 200f, DoubleLegMax = 240f, DoubleInMin = 240f, DoubleInMax = 300f;

        /// <summary>The legs round a hairpin: short in (so a full charge overshoots a long way) and long out (so the
        /// overshoot costs a second dart).</summary>
        public const float HairpinInMin = 300f, HairpinInMax = 340f, HairpinOutMin = 510f, HairpinOutMax = 540f;

        /// <summary>A head's rotation: a face square to <paramref name="line"/>, rolled to the world's up (every peer the same).</summary>
        public static Quaternion Facing(Vector3 line) =>
            Quaternion.LookRotation(line, Mathf.Abs(Vector3.Dot(line, Vector3.up)) < 0.95f ? Vector3.up : Vector3.forward);

        /// <summary>A coral head's half-diagonal: the farthest its cube reaches from its centre.</summary>
        public static float HalfDiagonal(float size) => size * 0.8660254f;

        // ── The start ────────────────────────────────────────────────────

        /// <summary>The spawn poses, cell-local: abreast, <see cref="StartStandoff"/> behind ring 0, facing through it.</summary>
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

        /// <summary>The course's rings, cell-local, or null when no attempt fits. Ring k's axis is the direction its leg
        /// arrives from.</summary>
        public static List<RaceGate> Generate(int seed, in PerchRunSettings s)
        {
            var p = new List<Vector3>();
            var a = new List<Vector3>();
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var rng = new RaceCourseGeometry.Rng(unchecked(seed * 7919 + attempt * 104729 + 31));
                if (!TryWalk(ref rng, s, p, a)) continue;
                var rings = new List<RaceGate>(p.Count);
                for (int i = 0; i < p.Count; i++) rings.Add(new RaceGate(p[i], a[i], s.RingRadius));
                if (Valid(s, rings, out _)) return rings;
            }
            return null;
        }

        static bool TryWalk(ref RaceCourseGeometry.Rng rng, in PerchRunSettings s, List<Vector3> p, List<Vector3> a)
        {
            p.Clear();
            a.Clear();
            p.Add(StartRing);
            a.Add(StartAxis);
            for (int k = 1; k < s.RingCount; k++)
            {
                bool hairpin = IsHairpin(k - 1, s);
                bool second = StartsDouble(k - 1, s);
                float legMin = second ? DoubleLegMin : hairpin ? HairpinOutMin : StartsDouble(k, s) ? DoubleInMin : IsHairpin(k, s) ? HairpinInMin : s.MinLeg;
                float legMax = second ? DoubleLegMax : hairpin ? HairpinOutMax : StartsDouble(k, s) ? DoubleInMax : IsHairpin(k, s) ? HairpinInMax : s.MaxLeg;
                bool placed = false;
                Vector3 prev = a[k - 1];
                Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(prev, Vector3.up), Vector3.forward);
                Vector3 e2 = Vector3.Cross(prev, e1);
                for (int c = 0; c < CandidatesPerStep && !placed; c++)
                {
                    // a double's second ring sits straight on down the line from the ring before the double
                    float bend = second ? 0f : hairpin ? rng.Range(s.HairpinMin, s.HairpinMax) : rng.Range(s.MinBend, s.MaxBend);
                    float phi = rng.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 about = e1 * Mathf.Cos(phi) + e2 * Mathf.Sin(phi);
                    Vector3 d = RaceCourseGeometry.RotateAbout(prev, about, bend * Mathf.Deg2Rad);
                    Vector3 next = p[k - 1] + d * rng.Range(legMin, legMax);
                    if (!Fits(s, p, next)) continue;
                    p.Add(next);
                    a.Add(d);
                    placed = true;
                }
                if (!placed) return false;
            }
            return true;
        }

        static bool Fits(in PerchRunSettings s, List<Vector3> p, Vector3 next)
        {
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

        // ── The coral ────────────────────────────────────────────────────

        /// <summary>The coral heads, cell-local or world (as the rings are): a blocker on every blocked leg,
        /// <see cref="BlockerAt"/> of the way along, a face square to the leg.</summary>
        public static void CoralFor(IReadOnlyList<RaceGate> rings, in PerchRunSettings s, List<Coral> coral)
        {
            coral.Clear();
            if (rings == null) return;
            for (int k = 0; k < rings.Count; k++)
            {
                if (!HasBlocker(k, s)) continue;
                Vector3 leg = RaceCourseGeometry.SafeNormalize(rings[k].Position - rings[k - 1].Position, rings[k].Axis);
                coral.Add(new Coral(Vector3.Lerp(rings[k - 1].Position, rings[k].Position, BlockerAt), Facing(leg), s.CoralSize, k));
            }
        }

        /// <summary>The course the tests hold: inside the shell, clear of the core, rings apart, legs clear of other rings
        /// and of every coral head but their own, and every blocked leg passable by a rim shot from the ring before.</summary>
        public static bool Valid(in PerchRunSettings s, IReadOnlyList<RaceGate> rings, out string why)
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
            }
            var coral = new List<Coral>();
            CoralFor(rings, s, coral);
            Vector3 start = rings[0].Position - rings[0].Axis * StartStandoff;
            for (int i = 0; i < rings.Count; i++)
            {
                Vector3 from = i == 0 ? start : rings[i - 1].Position;
                foreach (var c in coral)
                {
                    if (c.Ring == i) continue;   // a leg's own blocker sits on it
                    float keep = HalfDiagonal(c.Size) + HullRadius + CoralClearance;
                    if (DistanceToSegment(c.Position, from, rings[i].Position) < keep) { why = $"leg {i} runs into a coral head (ring {c.Ring})"; return false; }
                }
            }
            foreach (var c in coral)
                if (!TryRimShot(rings[c.Ring - 1].Position, rings, c.Ring, coral, out _))
                { why = $"blocker on leg {c.Ring} has no rim shot"; return false; }
            for (int i = 0; i < coral.Count; i++)
                for (int j = 0; j < coral.Count; j++)
                    if (i != j && (coral[i].Position - coral[j].Position).magnitude < coral[i].Size * 2f)
                    { why = "two coral heads touch"; return false; }
            return true;
        }

        // ── The dart's line against the coral ─────────────────────────────

        /// <summary>How far along the line from <paramref name="from"/> (unit <paramref name="dir"/>, up to
        /// <paramref name="length"/>) a hull of <see cref="HullRadius"/> first meets a coral head, or false. The heads are
        /// cubes laid at their <see cref="Coral.Rotation"/>, widened by the hull.</summary>
        public static bool FirstCoral(Vector3 from, Vector3 dir, float length, IReadOnlyList<Coral> coral, out float along)
        {
            along = float.MaxValue;
            for (int i = 0; i < coral.Count; i++)
            {
                float half = 0.5f * coral[i].Size + HullRadius;
                var q = coral[i].Rotation;
                Vector3 ex = q * Vector3.right, ey = q * Vector3.up, ez = q * Vector3.forward, o = from - coral[i].Position;
                var lo = new Vector3(Vector3.Dot(o, ex), Vector3.Dot(o, ey), Vector3.Dot(o, ez));
                var ld = new Vector3(Vector3.Dot(dir, ex), Vector3.Dot(dir, ey), Vector3.Dot(dir, ez));
                if (RayBox(lo, ld, half, length, out float t) && t < along) along = t;
            }
            return along < float.MaxValue;
        }

        static bool RayBox(Vector3 o, Vector3 d, float half, float length, out float t)
        {
            float tMin = 0f, tMax = length;
            t = 0f;
            // a hull already against the head (a dart that stopped on it) is blocked only by a line that goes on into it
            if (Mathf.Abs(o.x) <= half && Mathf.Abs(o.y) <= half && Mathf.Abs(o.z) <= half) return Vector3.Dot(d, o) < 0f;
            for (int ax = 0; ax < 3; ax++)
            {
                float oa = ax == 0 ? o.x : ax == 1 ? o.y : o.z, da = ax == 0 ? d.x : ax == 1 ? d.y : d.z;
                if (Mathf.Abs(da) < 1e-6f) { if (oa < -half || oa > half) return false; continue; }
                float t1 = (-half - oa) / da, t2 = (half - oa) / da;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax) return false;
            }
            t = tMin;
            return true;
        }

        /// <summary>A line from <paramref name="from"/> that threads ring <paramref name="ring"/> at least
        /// <see cref="RimMargin"/> inside its mouth and meets no coral head before it is past the ring: the centre if that
        /// is clear, else the nearest rim point that is (24 directions round the mouth, 6 radii). The point returned is on
        /// the ring's plane.</summary>
        public static bool TryRimShot(Vector3 from, IReadOnlyList<RaceGate> rings, int ring, IReadOnlyList<Coral> coral, out Vector3 through)
        {
            var g = rings[ring];
            Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(g.Axis, Vector3.up), Vector3.forward);
            Vector3 e2 = Vector3.Cross(g.Axis, e1);
            float reach = g.Radius - HullRadius - RimMargin;
            for (int ri = 0; ri <= 6; ri++)
            {
                float o = reach * ri / 6f;
                for (int di = 0; di < (ri == 0 ? 1 : 24); di++)
                {
                    float ang = di * (Mathf.PI * 2f / 24f);
                    through = g.Position + (e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang)) * o;
                    Vector3 to = through - from;
                    float len = to.magnitude;
                    if (len < 1f) continue;
                    Vector3 dir = to / len;
                    if (Mathf.Abs(Vector3.Dot(dir, g.Axis)) < 0.2f) continue;   // a line that grazes the mouth edge-on is no shot
                    if (FirstCoral(from, dir, len + HullRadius + 2f, coral, out _)) continue;
                    return true;
                }
            }
            through = g.Position;
            return false;
        }

        // ── The AI's dart (the same two numbers a person picks) ──────────

        /// <summary>
        /// The AI's next dart from <paramref name="hull"/> at ring <paramref name="next"/>: the line (a rim shot when the
        /// centre is blocked) and the length. In order: a DOUBLE - one line that threads this ring and the next and
        /// meets no coral first; else this ring alone. Either way charged to <see cref="Overshoot"/> past the last ring it
        /// threads, clamped to the charge's range: no further, so a hairpin after it is one dart.
        /// Length 0: no line clears (cruise: the Goby's own flight passes through coral).
        /// </summary>
        public static Vector3 PlanDart(Vector3 hull, IReadOnlyList<RaceGate> rings, int next, IReadOnlyList<Coral> coral, out float length)
        {
            length = 0f;
            var g = rings[next];
            if (next + 1 < rings.Count && TryRimShot(hull, rings, next + 1, coral, out Vector3 far))
            {
                Vector3 d2 = far - hull;
                float l2 = d2.magnitude;
                Vector3 u = d2 / Mathf.Max(1e-3f, l2);
                if (l2 + Overshoot <= DartRange && Threads(hull, far, g, HullRadius + RimMargin))
                {
                    length = Mathf.Clamp(l2 + Overshoot, DartMinRange, DartRange);
                    return far + u * Overshoot;
                }
            }
            if (!TryRimShot(hull, rings, next, coral, out Vector3 through)) return g.Position;
            Vector3 to = through - hull;
            float dist = to.magnitude;
            Vector3 dir = to / Mathf.Max(1e-3f, dist);
            Vector3 aim = through + dir * Overshoot;
            length = Mathf.Clamp(dist + Overshoot, DartMinRange, DartRange);
            return aim;
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-6f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>A segment threads a ring's mouth (either way across its plane) with <paramref name="margin"/> to spare.</summary>
        public static bool Threads(Vector3 from, Vector3 to, in RaceGate ring, float margin = 4f)
        {
            float a = Vector3.Dot(from - ring.Position, ring.Axis), b = Vector3.Dot(to - ring.Position, ring.Axis);
            if (a * b > 0f || Mathf.Approximately(a, b)) return false;
            Vector3 x = Vector3.Lerp(from, to, a / (a - b));
            return (x - ring.Position).magnitude <= ring.Radius - margin;
        }
    }
}
