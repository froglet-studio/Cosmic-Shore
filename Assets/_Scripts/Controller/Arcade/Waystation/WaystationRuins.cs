using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Something of a Drop Tower a ruin must stand clear of (a wall tile, a lantern segment), as a ball.</summary>
    public readonly struct WaystationRuinObstacle
    {
        public readonly Vector3 Centre;
        public readonly float Reach;

        public WaystationRuinObstacle(Vector3 centre, float reach)
        {
            Centre = centre;
            Reach = reach;
        }
    }

    /// <summary>
    /// Waystation's garden ruins (WAYSTATION.md, "Garden ruins"): Spearline's seven ruin kinds
    /// (<see cref="SpearlineRuins"/>, called here and never copied) stood round Waystation's racing line on the hardest
    /// rung. Decoration: plain prisms with the game's own rules. Nothing here reads or writes a vessel.
    ///
    /// <para><b>What this class adds to the generator, and why.</b> Spearline's course is a lap of rings flown ring to
    /// ring. Waystation's is an open chain with Drop Towers, so three things are Waystation's own:</para>
    /// <list type="bullet">
    /// <item><b>The racing line.</b> A tower ring is not flown to: the pilot cruises level to the PRESS point and falls
    /// along the ring's axis. <see cref="RacingLine"/> states that as gates - the press point twice (once on the heading
    /// the pilot arrives on, once on the fall line), then the ring - so every tower leg is a straight line and the
    /// generator's clear lane lies where the hull really goes. A press point has the generator's ring margin too.</item>
    /// <item><b>The towers.</b> No ruin prism stands within <see cref="WallClear"/> of a tower wall tile or a lantern
    /// post, and a wheel turns only where its whole sweep is clear of them.</item>
    /// <item><b>Which legs.</b> The generator lays the first legs it meets; a chain would get all its ruins at the
    /// start. <see cref="Lay"/> lays every leg that has room and keeps at most <see cref="PerCourse"/>, each kind
    /// once, spread along the chain, the longest leg (the long last drop) first.</item>
    /// </list>
    ///
    /// <para>Pure: a function of the broadcast course and the rung, so every peer stands the same ruins
    /// (<c>WaystationRuinsTests</c>, run headless by <c>Tools/Build/waystation_tower_harness</c>).</para>
    /// </summary>
    public static class WaystationRuins
    {
        /// <summary>Ruins a course carries at most: one of each kind.</summary>
        public const int PerCourse = SpearlineRuins.KindCount;

        /// <summary>No ruin prism reaches nearer than this (u) to a tower wall tile or a lantern segment: the hull's
        /// radius and a margin, so a landing beside a plate meets the plate and nothing else.</summary>
        public const float WallClear = 14f;

        /// <summary>A leg whose ruin was cut down to fewer prisms than this carries none: it would not read as a ruin.</summary>
        public const int MinParts = 12;

        /// <summary>A gap between two rings this long (u) or longer is crossed by the Drop (the controller's
        /// aiFoldMinDistance, and where a person drops too): its clear lane is the straight chord.</summary>
        public const float DropGap = 700f;

        /// <summary>How finely the autopilot reads a leg's flown line when it steers back to it.</summary>
        const int LaneSamples = 32;

        /// <summary>
        /// The line a pilot flies, as gates: every course ring, and before each tower ring its press point twice (radius
        /// 0), first on the heading the pilot arrives on and then on the fall line. Both tower legs are then straight.
        /// A gap of <see cref="DropGap"/> or more between two rings is crossed by the Drop, a straight fall, so each of its
        /// ends is given twice too (radius 0, on the chord) and the lane the ruins keep is the chord, not a curve.
        /// </summary>
        public static void RacingLine(IReadOnlyList<RaceGate> course, int intensity, List<RaceGate> into)
        {
            into.Clear();
            var s = WaystationTowerSettings.ForIntensity(intensity);
            for (int i = 0; i < course.Count; i++)
            {
                if (i >= 1 && WaystationTower.IsTowerRing(course[i], s))
                {
                    Vector3 press = WaystationTower.PressOf(course, i, s);
                    into.Add(new RaceGate(press, course[i - 1].Axis, 0f));
                    into.Add(new RaceGate(press, course[i].Axis, 0f));
                }
                bool longGap = i >= 1 && !WaystationTower.IsTowerRing(course[i], s)
                               && (course[i].Position - course[i - 1].Position).magnitude >= DropGap;
                if (longGap)
                {
                    Vector3 chord = (course[i].Position - course[i - 1].Position).normalized;
                    into.Add(new RaceGate(course[i - 1].Position, chord, 0f));
                    into.Add(new RaceGate(course[i].Position, chord, 0f));
                }
                into.Add(course[i]);
            }
        }

        /// <summary>
        /// Every ruin prism of <paramref name="course"/> on this rung, appended to <paramref name="into"/>, in the
        /// course's own frame; the turning ones are also named in <paramref name="wheels"/>. A part's
        /// <see cref="SpearlineRuinPart.Leg"/> is its ruin's place along the chain (0, 1, 2...), which is what the
        /// controller colours by. Nothing below the rung Spearline's own table gives ruins to (I4).
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> course, int intensity, List<SpearlineRuinPart> into,
                               List<SpearlineRuinWheel> wheels)
        {
            var settings = SpearlineRuins.ForIntensity(intensity);
            if (settings.PerLap <= 0 || course == null || course.Count < 3) return;

            var line = new List<RaceGate>(course.Count * 2);
            RacingLine(course, intensity, line);
            settings.PerLap = line.Count;   // every leg with room; the pick below keeps PerCourse of them
            var all = new List<SpearlineRuinPart>(2048);
            var turning = new List<SpearlineRuinWheel>(4);
            SpearlineRuins.Lay(line, settings, all, turning);
            if (all.Count == 0) return;

            var obstacles = new List<WaystationRuinObstacle>(2560);
            TowerObstacles(course, intensity, obstacles);

            // One candidate per leg that still reads as a ruin once the towers have taken their share.
            var candidates = new List<Candidate>(16);
            var keep = new bool[all.Count];
            for (int first = 0; first < all.Count;)
            {
                int end = first;
                while (end < all.Count && all[end].Leg == all[first].Leg) end++;
                int wheel = WheelOf(turning, first);
                int kept = 0;
                if (wheel >= 0)
                {
                    float sweep = SpearlineRuins.WheelRadius + Reach(all[first].Scale);
                    if (Nearest(obstacles, turning[wheel].Centre, sweep + WallClear) >= sweep + WallClear)
                    {
                        for (int i = first; i < end; i++) keep[i] = true;
                        kept = end - first;
                    }
                }
                else
                {
                    for (int i = first; i < end; i++)
                    {
                        keep[i] = PartObstacleDistance(obstacles, all[i]) >= WallClear;
                        if (keep[i]) kept++;
                    }
                }
                if (kept >= MinParts)
                {
                    int leg = all[first].Leg;
                    int kind = wheel >= 0 ? SpearlineRuins.Wheel : SpearlineRuins.KindOf(leg, line[0].Position);
                    if (wheel < 0 && kind == SpearlineRuins.Wheel) kind = SpearlineRuins.Pergola;   // the generator's fallback
                    float length = (line[leg + 1].Position - line[leg].Position).magnitude;
                    candidates.Add(new Candidate(leg, kind, first, end, wheel, length));
                }
                first = end;
            }

            var picked = Pick(candidates);
            picked.Sort((a, b) => a.Leg.CompareTo(b.Leg));
            for (int ordinal = 0; ordinal < picked.Count; ordinal++)
            {
                var c = picked[ordinal];
                int start = into.Count;
                for (int i = c.First; i < c.End; i++)
                    if (keep[i]) into.Add(all[i].OnLeg(ordinal));
                if (c.Wheel >= 0)
                {
                    var w = turning[c.Wheel];
                    wheels?.Add(new SpearlineRuinWheel(w.Centre, w.Axis, w.DegreesPerSecond, start, into.Count - start));
                }
            }
        }

        readonly struct Candidate
        {
            public readonly int Leg, Kind, First, End, Wheel;
            public readonly float Length;

            public Candidate(int leg, int kind, int first, int end, int wheel, float length)
            {
                Leg = leg;
                Kind = kind;
                First = first;
                End = end;
                Wheel = wheel;
                Length = length;
            }
        }

        /// <summary>At most <see cref="PerCourse"/> candidates, no kind twice: the longest leg first, then whichever
        /// leg of an unused kind lies farthest along the chain from every leg already picked (the earliest on a tie).</summary>
        static List<Candidate> Pick(List<Candidate> candidates)
        {
            var picked = new List<Candidate>(PerCourse);
            var used = new bool[SpearlineRuins.KindCount];
            while (picked.Count < PerCourse)
            {
                int best = -1;
                float bestScore = -1f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    if (used[c.Kind]) continue;
                    float score;
                    if (picked.Count == 0) score = c.Length;
                    else
                    {
                        score = float.MaxValue;
                        foreach (var p in picked) score = Mathf.Min(score, Mathf.Abs(c.Leg - p.Leg));
                    }
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                if (best < 0) break;
                used[candidates[best].Kind] = true;
                picked.Add(candidates[best]);
            }
            return picked;
        }

        static int WheelOf(List<SpearlineRuinWheel> wheels, int part)
        {
            for (int w = 0; w < wheels.Count; w++)
                if (part >= wheels[w].First && part < wheels[w].First + wheels[w].Count) return w;
            return -1;
        }

        // ── The towers ───────────────────────────────────────────────────

        /// <summary>Every tower wall tile and lantern segment as a ball: its centre (xyz) and its reach (w).</summary>
        public static void TowerObstacles(IReadOnlyList<RaceGate> course, int intensity, List<WaystationRuinObstacle> into)
        {
            into.Clear();
            var tiles = new List<WaystationTowerTile>(2048);
            WaystationTower.Walls(course, intensity, tiles);
            foreach (var tile in tiles) into.Add(Ball(tile.Position, Reach(tile.Scale)));
            var lanterns = new List<WaystationLantern>(512);
            WaystationTower.Lanterns(course, intensity, lanterns);
            foreach (var lantern in lanterns) into.Add(Ball(lantern.Position, Reach(lantern.Scale)));
        }

        // ── The autopilot and the ruins (2026-11-25) ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// The first ruin prism the straight path <paramref name="from"/> to <paramref name="to"/> passes within
        /// <paramref name="clearance"/> of its centre, or -1. Prisms behind the start or past the end do not count.
        /// </summary>
        public static int FirstOnPath(IReadOnlyList<Vector3> prisms, Vector3 from, Vector3 to, float clearance)
        {
            Vector3 path = to - from;
            float length = path.magnitude;
            if (prisms == null || length < 1e-3f) return -1;
            Vector3 dir = path / length;
            int first = -1;
            float firstAlong = float.MaxValue;
            float clear2 = clearance * clearance;
            for (int i = 0; i < prisms.Count; i++)
            {
                Vector3 d = prisms[i] - from;
                float along = Vector3.Dot(d, dir);
                if (along < 0f || along > length || along >= firstAlong) continue;
                if ((d - dir * along).sqrMagnitude >= clear2) continue;
                first = i;
                firstAlong = along;
            }
            return first;
        }

        /// <summary>
        /// The leg of <paramref name="line"/> (<see cref="RacingLine"/>) a pilot flies first on its way to course ring
        /// <paramref name="ring"/>: the cruise to the press point on a tower, the chord on a long gap, else ring to ring.
        /// False for the first ring (no leg leads to it) and past the course's end.
        /// </summary>
        public static bool LaneLegTo(IReadOnlyList<RaceGate> line, int ring, out RaceGate legStart, out RaceGate legEnd)
        {
            legStart = legEnd = default;
            if (line == null || ring < 1) return false;
            int rings = 0;
            for (int j = 0; j < line.Count; j++)
            {
                if (line[j].Radius <= 0f) continue;
                if (rings++ != ring - 1) continue;
                for (int k = j; k + 1 < line.Count; k++)
                {
                    if ((line[k + 1].Position - line[k].Position).sqrMagnitude < 1e-4f)
                    {
                        if (line[k + 1].Radius > 0f) return false;
                        continue;
                    }
                    legStart = line[k];
                    legEnd = line[k + 1];
                    return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// Where an autopilot at <paramref name="hull"/> steers when a ruin stands between it and its
        /// <paramref name="goal"/>: back into the lane the ruins keep clear, the flown line of the leg it is on
        /// (<paramref name="legStart"/> to <paramref name="legEnd"/>, two gates of <see cref="RacingLine"/>; clear by
        /// <see cref="SpearlineRuins.LaneRadius"/>). It tries points on that line from <paramref name="lookahead"/> ahead
        /// of the hull back to abreast of it and takes the first it can reach in a straight line; with none, it steps
        /// round the blocking prism on the side away from it. False when the next <paramref name="lookahead"/> of the
        /// way to the goal is clear, and the caller's own steering stands. Everything in one frame.
        /// </summary>
        public static bool TrySteerRound(IReadOnlyList<Vector3> prisms, Vector3 hull, Vector3 goal, in RaceGate legStart,
                                         in RaceGate legEnd, float clearance, float lookahead, out Vector3 target)
        {
            target = goal;
            Vector3 toGoal = goal - hull;
            float distance = toGoal.magnitude;
            if (distance < 1e-3f) return false;
            Vector3 dir = toGoal / distance;
            int blocking = FirstOnPath(prisms, hull, hull + dir * Mathf.Min(distance, lookahead), clearance);
            if (blocking < 0) return false;

            float legLength = (legEnd.Position - legStart.Position).magnitude;
            if (legLength > 1e-3f)
            {
                int abreast = 0;
                float best = float.MaxValue;
                for (int k = 0; k <= LaneSamples; k++)
                {
                    float d = (SpearlineRuins.LinePoint(legStart, legEnd, k / (float)LaneSamples) - hull).sqrMagnitude;
                    if (d < best) { best = d; abreast = k; }
                }
                int ahead = Mathf.Max(1, Mathf.CeilToInt(lookahead * LaneSamples / legLength));
                for (int step = 2; step >= 0; step--)
                {
                    int k = Mathf.Min(LaneSamples, abreast + (ahead * step + 1) / 2);
                    Vector3 onLane = SpearlineRuins.LinePoint(legStart, legEnd, k / (float)LaneSamples);
                    if ((onLane - hull).sqrMagnitude < 1f) continue;
                    if (FirstOnPath(prisms, hull, onLane, clearance) >= 0) continue;
                    target = onLane;
                    return true;
                }
            }
            Vector3 toPrism = prisms[blocking] - hull;
            Vector3 aside = toPrism - dir * Vector3.Dot(toPrism, dir);
            Vector3 away = aside.sqrMagnitude > 1e-4f ? -aside.normalized : RaceCourseGeometry.Perpendicular(dir);
            target = prisms[blocking] + away * (clearance * 2f);
            return true;
        }

        static WaystationRuinObstacle Ball(Vector3 p, float reach) => new(p, reach);

        /// <summary>The radius of the ball round a prism of this size.</summary>
        static float Reach(Vector3 scale) => 0.5f * scale.magnitude;

        /// <summary>The gap (u) between <paramref name="part"/> and the nearest obstacle ball, along the part's whole
        /// length: under zero when they overlap.</summary>
        public static float PartObstacleDistance(List<WaystationRuinObstacle> obstacles, in SpearlineRuinPart part)
        {
            float cross = 0.5f * Mathf.Sqrt(part.Scale.x * part.Scale.x + part.Scale.y * part.Scale.y);
            int samples = 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            float best = float.MaxValue;
            for (int k = 0; k < samples; k++)
            {
                Vector3 p = part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);
                best = Mathf.Min(best, Nearest(obstacles, p, cross + WallClear) - cross);
            }
            return best;
        }

        /// <summary>The gap (u) from <paramref name="p"/> to the nearest obstacle ball's surface. <paramref name="enough"/>
        /// is only a hint: anything at or past it may be returned as soon as it is known not to matter.</summary>
        static float Nearest(List<WaystationRuinObstacle> obstacles, Vector3 p, float enough)
        {
            float best = float.MaxValue;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                float dx = p.x - o.Centre.x, dy = p.y - o.Centre.y, dz = p.z - o.Centre.z;
                float limit = best + o.Reach;
                if (best < float.MaxValue && dx * dx + dy * dy + dz * dz >= limit * limit) continue;
                float d = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - o.Reach;
                if (d < best) best = d;
                if (best < 0f && enough > 0f) return best;
            }
            return best;
        }
    }
}
