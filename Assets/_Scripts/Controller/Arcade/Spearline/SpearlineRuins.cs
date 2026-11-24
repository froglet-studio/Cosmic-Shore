using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One rung of the garden ruins (SPEARLINE.md §2a).</summary>
    public struct SpearlineRuinSettings
    {
        public int PerLap;          // ruins a lap, one to a leg at most (0: none on this rung)
        public float ClearRadius;   // no standing ruin prism reaches nearer than this (u) to any leg's flown line
    }

    /// <summary>One ruin prism: where it stands, how it is turned (forward and up, as <c>Quaternion.LookRotation</c>
    /// takes them) and its size. A long prism is long along <see cref="Forward"/> (its scale's z).</summary>
    public readonly struct SpearlineRuinPart
    {
        public readonly Vector3 Position, Forward, Up, Scale;

        public SpearlineRuinPart(Vector3 position, Vector3 forward, Vector3 up, Vector3 scale)
        {
            Position = position;
            Forward = forward;
            Up = up;
            Scale = scale;
        }
    }

    /// <summary>A turning ruin: <see cref="Count"/> parts from <see cref="First"/> turn together about
    /// <see cref="Axis"/> through <see cref="Centre"/>. The parts hold the pose at angle zero.</summary>
    public readonly struct SpearlineRuinWheel
    {
        public readonly Vector3 Centre, Axis;
        public readonly float DegreesPerSecond;
        public readonly int First, Count;

        public SpearlineRuinWheel(Vector3 centre, Vector3 axis, float degreesPerSecond, int first, int count)
        {
            Centre = centre;
            Axis = axis;
            DegreesPerSecond = degreesPerSecond;
            First = first;
            Count = count;
        }
    }

    /// <summary>
    /// Spearline's garden ruins: what is left of old flora gardens, standing round the racing line between the rings of
    /// the hardest rung (Yash, 2026-11-24: "going through a ruins of old flora gardens", then "more thicker and wall /
    /// tunnel / 3d maze grid, moving obstacles"). Seven kinds, one to a leg, taking turns round the lap:
    /// a <b>pergola</b> (a row of broken arches), a <b>flower head</b> (a phyllotactic dish with its eye open and a bite
    /// out of it), <b>vines</b> (strands wound round the line), a <b>wall</b> (a broken wall across the leg with a
    /// doorway), a <b>tunnel</b> (a tube of plates with stones fallen out), a <b>maze</b> (a 3D lattice of bars) and a
    /// <b>wheel</b> (spokes and a rim that TURN about the line).
    ///
    /// <para>Each is laid round the leg's flown line (a Hermite curve through the two rings along their axes). The six
    /// standing kinds keep a clear lane down the middle, so a pilot on the line flies THROUGH the ruin and one who strays
    /// meets its prisms, at the game's own cost for a prism. The wheel alone crosses the lane: its spokes sweep it, and
    /// only its hub (<see cref="HubRadius"/>) and the gaps between the spokes are open.</para>
    ///
    /// <para>Pure: a function of the lap and the rung, so every peer lays the same ruins from the broadcast rings and
    /// the edit-mode tests hold it (<c>SpearlineRuinsTests</c>). Nothing here touches the vessel or the seals.</para>
    /// </summary>
    public static class SpearlineRuins
    {
        static readonly int[] PerLapByRung = { 0, 0, 0, 7 };

        /// <summary>The clear lane's radius: a little under the I4 mouth (66), so the lane a ring allows stays open.</summary>
        public const float LaneRadius = 60f;

        /// <summary>No ruin prism stands within this distance (u) of a ring's centre, past its mouth: the seal, its
        /// regrowth and the pilot's last look at the pane are left alone.</summary>
        public const float RingMargin = 120f;

        /// <summary>A leg shorter than this (u) carries no ruin: there is no room between its two rings.</summary>
        public const float MinLegLength = 380f;

        /// <summary>Points sampled along each leg's flown line for the clearance test.</summary>
        public const int LineSamples = 24;

        public const int Pergola = 0, FlowerHead = 1, Vines = 2, Wall = 3, Tunnel = 4, Maze = 5, Wheel = 6;
        public const int KindCount = 7;

        // How much ruin stands on a leg (raised by half on 2026-11-24, Yash: 3 arches, a 78 u dish, 2 strands before).
        public const int PergolaArches = 5, VineStrands = 3;
        public const float FlowerHeadDepth = 104f;

        /// <summary>The wheel: its spokes run from the hub out to the rim, and it turns this fast.</summary>
        public const float HubRadius = 30f, WheelRadius = 150f, WheelDegreesPerSecond = 25f;
        public const int WheelSpokes = 3;

        public static SpearlineRuinSettings ForIntensity(int intensity) => new()
        {
            PerLap = PerLapByRung[Mathf.Clamp(intensity, 1, 4) - 1],
            ClearRadius = LaneRadius,
        };

        /// <summary>The flown line of the leg from <paramref name="a"/> to <paramref name="b"/> at
        /// <paramref name="t"/> (0..1): a Hermite curve leaving and arriving along the rings' axes.</summary>
        public static Vector3 LinePoint(in RaceGate a, in RaceGate b, float t)
        {
            float len = (b.Position - a.Position).magnitude;
            Vector3 m0 = a.Axis * len, m1 = b.Axis * len;
            float t2 = t * t, t3 = t2 * t;
            return a.Position * (2f * t3 - 3f * t2 + 1f) + m0 * (t3 - 2f * t2 + t)
                 + b.Position * (-2f * t3 + 3f * t2) + m1 * (t3 - t2);
        }

        /// <summary>The direction of that line at <paramref name="t"/> (unit; the chord if the curve stalls).</summary>
        public static Vector3 LineDirection(in RaceGate a, in RaceGate b, float t)
        {
            float len = (b.Position - a.Position).magnitude;
            Vector3 m0 = a.Axis * len, m1 = b.Axis * len;
            float t2 = t * t;
            Vector3 d = a.Position * (6f * t2 - 6f * t) + m0 * (3f * t2 - 4f * t + 1f)
                      + b.Position * (-6f * t2 + 6f * t) + m1 * (3f * t2 - 2f * t);
            return d.sqrMagnitude > 1e-6f ? d.normalized : (b.Position - a.Position).normalized;
        }

        /// <summary>Which kind of ruin leg <paramref name="leg"/> carries. The kinds take turns round the lap, starting
        /// from a draw on the lap's first ring (<paramref name="lapOrigin"/>), so a lap of seven legs has every kind once.</summary>
        public static int KindOf(int leg, Vector3 lapOrigin)
        {
            int salt = Mathf.FloorToInt(Mathf.Abs(lapOrigin.x) + Mathf.Abs(lapOrigin.y) + Mathf.Abs(lapOrigin.z));
            return (leg + Mathf.FloorToInt(SpearlineSeals.Hash01(salt, 17, 3) * KindCount)) % KindCount;
        }

        /// <summary>
        /// Every ruin prism of <paramref name="lap"/> on this rung, appended to <paramref name="into"/>; the turning
        /// ones are also named in <paramref name="wheels"/>. The leg that closes the lap (into ring 0, where the field
        /// starts) carries none. A standing prism that would reach into any leg's lane or too near any ring is left out;
        /// a wheel is laid whole or not at all, and only where no other leg's lane passes through its sweep.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> lap, in SpearlineRuinSettings s, List<SpearlineRuinPart> into,
                               List<SpearlineRuinWheel> wheels)
        {
            int n = lap.Count;
            if (s.PerLap <= 0 || n < 3) return;
            int laid = 0;
            for (int leg = 0; leg < n - 1 && laid < s.PerLap; leg++)
            {
                var a = lap[leg];
                var b = lap[leg + 1];
                if ((b.Position - a.Position).magnitude < MinLegLength) continue;
                int salt = leg * 7919 + n;
                float t = 0.5f + (SpearlineSeals.Hash01(salt, 1, 0) - 0.5f) * 0.12f;
                Vector3 centre = LinePoint(a, b, t);
                Vector3 dir = LineDirection(a, b, t);
                SpearlineSeals.Basis(dir, out var u, out var v);
                int first = into.Count;
                int kind = KindOf(leg, lap[0].Position);
                if (kind == Wheel && !WheelFits(lap, leg, centre)) kind = Pergola;
                switch (kind)
                {
                    case Pergola: LayPergola(centre, dir, u, v, s.ClearRadius, salt, into); break;
                    case FlowerHead: LayFlowerHead(centre, dir, u, v, s.ClearRadius, salt, into); break;
                    case Vines: LayVines(centre, dir, u, v, s.ClearRadius, salt, into); break;
                    case Wall: LayWall(centre, dir, u, v, s.ClearRadius, salt, into); break;
                    case Tunnel: LayTunnel(centre, dir, u, v, s.ClearRadius, salt, into); break;
                    case Maze: LayMaze(centre, dir, u, v, salt, into); break;
                    default:
                        LayWheel(centre, dir, u, v, salt, into);
                        float sense = SpearlineSeals.Hash01(salt, 0, 71) < 0.5f ? -1f : 1f;
                        wheels?.Add(new SpearlineRuinWheel(centre, dir, sense * WheelDegreesPerSecond, first, into.Count - first));
                        break;
                }
                if (kind != Wheel) Clear(lap, s.ClearRadius, into, first);
                if (into.Count > first) laid++;
            }
        }

        // ── The standing kinds ───────────────────────────────────────────

        /// <summary>A row of arches round the lane, each with a fallen sector and a few stones gone.</summary>
        static void LayPergola(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, float clear, int salt, List<SpearlineRuinPart> into)
        {
            const float step = 14f;
            var scale = new Vector3(9f, 9f, 13f);
            for (int arch = 0; arch < PergolaArches; arch++)
            {
                float radius = clear + 18f + 10f * SpearlineSeals.Hash01(salt, arch, 11);
                Vector3 at = c + dir * ((arch - 0.5f * (PergolaArches - 1)) * 40f);
                float fallenAt = SpearlineSeals.Hash01(salt, arch, 12) * 2f * Mathf.PI;
                float fallenWidth = Mathf.Deg2Rad * (70f + 100f * SpearlineSeals.Hash01(salt, arch, 13));
                int count = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / step));
                for (int k = 0; k < count; k++)
                {
                    float angle = 2f * Mathf.PI * k / count;
                    if (Mathf.Abs(Wrap(angle - fallenAt)) < 0.5f * fallenWidth) continue;
                    if (SpearlineSeals.Hash01(salt, arch * 1000 + k, 14) < 0.12f) continue;
                    Vector3 radial = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    Vector3 along = v * Mathf.Cos(angle) - u * Mathf.Sin(angle);
                    into.Add(new SpearlineRuinPart(at + radial * radius, along, dir, scale));
                }
            }
        }

        /// <summary>A phyllotactic dish facing down the line: the eye is the lane, and a bite is gone from the rim.</summary>
        static void LayFlowerHead(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, float clear, int salt, List<SpearlineRuinPart> into)
        {
            const float golden = 2.39996323f, spread = 12f;
            var scale = new Vector3(11f, 11f, 5f);
            float inner = clear + 8f, outer = clear + FlowerHeadDepth;
            float biteAt = SpearlineSeals.Hash01(salt, 0, 21) * 2f * Mathf.PI;
            float biteWidth = Mathf.Deg2Rad * (60f + 70f * SpearlineSeals.Hash01(salt, 0, 22));
            int from = Mathf.CeilToInt(inner * inner / (spread * spread));
            int to = Mathf.FloorToInt(outer * outer / (spread * spread));
            for (int k = from; k <= to; k++)
            {
                float r = spread * Mathf.Sqrt(k);
                float angle = k * golden;
                // The bite takes the rim and narrows toward the eye.
                float rim = Mathf.Clamp01((r - inner) / (outer - inner));
                if (Mathf.Abs(Wrap(angle - biteAt)) < 0.5f * biteWidth * rim) continue;
                if (SpearlineSeals.Hash01(salt, k, 23) < 0.1f) continue;
                Vector3 radial = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                // A shallow dish, opening the way the course flows.
                Vector3 at = c + radial * r + dir * ((r - inner) * 0.45f - 16f);
                into.Add(new SpearlineRuinPart(at, (dir - radial * 0.45f).normalized, radial, scale));
            }
        }

        /// <summary>Strands wound round the lane, each broken into runs.</summary>
        static void LayVines(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, float clear, int salt, List<SpearlineRuinPart> into)
        {
            const float length = 150f, turns = 1.15f;
            var scale = new Vector3(8f, 8f, 13f);
            for (int strand = 0; strand < VineStrands; strand++)
            {
                float radius = clear + 12f + 12f * strand;
                float phase = SpearlineSeals.Hash01(salt, strand, 31) * 2f * Mathf.PI + 2f * Mathf.PI * strand / VineStrands;
                float sense = (strand & 1) == 0 ? 1f : -1f;
                float arc = Mathf.Sqrt(Mathf.Pow(2f * Mathf.PI * radius * turns, 2f) + length * length);
                int count = Mathf.Max(8, Mathf.RoundToInt(arc / 13.5f));
                for (int k = 0; k < count; k++)
                {
                    // Runs of about seven prisms; some runs are gone.
                    if (SpearlineSeals.Hash01(salt, strand * 100 + k / 7, 32) < 0.28f) continue;
                    float f = (k + 0.5f) / count;
                    float angle = phase + sense * 2f * Mathf.PI * turns * f;
                    Vector3 radial = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    Vector3 around = (v * Mathf.Cos(angle) - u * Mathf.Sin(angle)) * sense;
                    Vector3 tangent = (around * (2f * Mathf.PI * radius * turns) + dir * length).normalized;
                    into.Add(new SpearlineRuinPart(c + radial * radius + dir * ((f - 0.5f) * length), tangent, radial, scale));
                }
            }
        }

        /// <summary>A thick wall across the leg, with the lane as its doorway and a broken skyline.</summary>
        static void LayWall(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, float clear, int salt, List<SpearlineRuinPart> into)
        {
            const float stone = 20f;
            const int halfWide = 8, halfTall = 6;
            var scale = new Vector3(stone - 1f, stone - 1f, 14f);
            float door = clear + 0.75f * stone;
            for (int i = -halfWide; i <= halfWide; i++)
            {
                // The skyline: each column stands to its own height, lower toward the wall's ends.
                float ends = 1f - Mathf.Abs(i) / (halfWide + 1f);
                int top = Mathf.RoundToInt(halfTall * (0.35f + 0.65f * ends * (0.6f + 0.4f * SpearlineSeals.Hash01(salt, i, 41))));
                for (int j = -halfTall; j <= top; j++)
                {
                    float x = i * stone, y = j * stone;
                    if (x * x + y * y < door * door) continue;
                    if (SpearlineSeals.Hash01(salt, i * 100 + j, 42) < 0.14f) continue;
                    into.Add(new SpearlineRuinPart(c + u * x + v * y, dir, v, scale));
                }
            }
        }

        /// <summary>A tube of plates round the lane, with stones fallen out and a broken mouth at each end.</summary>
        static void LayTunnel(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, float clear, int salt, List<SpearlineRuinPart> into)
        {
            const float plate = 21f, length = 220f;
            var scale = new Vector3(plate - 1f, plate - 1f, 8f);
            float radius = clear + 10f;
            int around = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / plate));
            int rings = Mathf.RoundToInt(length / plate);
            for (int ring = 0; ring < rings; ring++)
            {
                float z = (ring - 0.5f * (rings - 1)) * plate;
                // More of the tube is gone toward its two mouths.
                float mouth = Mathf.Abs(ring - 0.5f * (rings - 1)) / (0.5f * rings);
                float gone = 0.1f + 0.35f * mouth * mouth;
                for (int k = 0; k < around; k++)
                {
                    if (SpearlineSeals.Hash01(salt, ring * 100 + k, 51) < gone) continue;
                    float angle = 2f * Mathf.PI * (k + 0.5f * (ring & 1)) / around;
                    Vector3 radial = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    into.Add(new SpearlineRuinPart(c + radial * radius + dir * z, radial, dir, scale));
                }
            }
        }

        /// <summary>A 3D lattice of long bars round the lane: a maze to a pilot who leaves the line. A third of the bars
        /// are gone, and <see cref="Clear"/> takes the ones that would cross the lane.</summary>
        static void LayMaze(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, int salt, List<SpearlineRuinPart> into)
        {
            const float cell = 64f;
            const int half = 2, deep = 3;   // nodes -2..2 across and up, 0..3 along
            var scale = new Vector3(8f, 8f, cell - 2f);
            float z0 = -0.5f * deep * cell;
            for (int i = -half; i <= half; i++)
            {
                for (int j = -half; j <= half; j++)
                {
                    for (int k = 0; k <= deep; k++)
                    {
                        Vector3 node = c + u * (i * cell) + v * (j * cell) + dir * (z0 + k * cell);
                        int id = (i + half) * 100 + (j + half) * 10 + k;
                        if (i < half && SpearlineSeals.Hash01(salt, id, 61) >= 0.33f)
                            into.Add(new SpearlineRuinPart(node + u * (0.5f * cell), u, dir, scale));
                        if (j < half && SpearlineSeals.Hash01(salt, id, 62) >= 0.33f)
                            into.Add(new SpearlineRuinPart(node + v * (0.5f * cell), v, dir, scale));
                        if (k < deep && SpearlineSeals.Hash01(salt, id, 63) >= 0.33f)
                            into.Add(new SpearlineRuinPart(node + dir * (0.5f * cell), dir, u, scale));
                    }
                }
            }
        }

        // ── The wheel (it turns) ─────────────────────────────────────────

        /// <summary>Spokes from the hub to the rim and a rim with stones gone, all in one plane across the line.</summary>
        static void LayWheel(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, int salt, List<SpearlineRuinPart> into)
        {
            const float spokeStone = 30f;
            var spoke = new Vector3(11f, 11f, spokeStone - 1f);
            int stones = Mathf.RoundToInt((WheelRadius - HubRadius) / spokeStone);
            float phase = SpearlineSeals.Hash01(salt, 0, 72) * 2f * Mathf.PI;
            for (int spokeIndex = 0; spokeIndex < WheelSpokes; spokeIndex++)
            {
                float angle = phase + 2f * Mathf.PI * spokeIndex / WheelSpokes;
                Vector3 radial = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                for (int k = 0; k < stones; k++)
                    into.Add(new SpearlineRuinPart(c + radial * (HubRadius + (k + 0.5f) * spokeStone), radial, dir, spoke));
            }
            const int rimStones = 26;
            var rim = new Vector3(9f, 9f, 2f * Mathf.PI * WheelRadius / rimStones - 2f);
            for (int k = 0; k < rimStones; k++)
            {
                if (SpearlineSeals.Hash01(salt, k, 73) < 0.25f) continue;
                float angle = 2f * Mathf.PI * k / rimStones;
                Vector3 radial = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                Vector3 along = v * Mathf.Cos(angle) - u * Mathf.Sin(angle);
                into.Add(new SpearlineRuinPart(c + radial * WheelRadius, along, dir, rim));
            }
        }

        /// <summary>May a wheel turn at <paramref name="centre"/> on leg <paramref name="leg"/>: clear of every ring, and
        /// with no OTHER leg's lane passing through the ball it sweeps?</summary>
        static bool WheelFits(IReadOnlyList<RaceGate> lap, int leg, Vector3 centre)
        {
            if (RingClearance(lap, centre) < RingMargin + WheelRadius) return false;
            int n = lap.Count;
            for (int other = 0; other < n; other++)
            {
                if (other == leg) continue;
                if (LegDistance(lap, other, centre) < WheelRadius + LaneRadius) return false;
            }
            return true;
        }

        /// <summary>Where <paramref name="part"/> of <paramref name="wheel"/> is after <paramref name="seconds"/>.</summary>
        public static void Pose(in SpearlineRuinPart part, in SpearlineRuinWheel wheel, float seconds,
                                out Vector3 position, out Vector3 forward, out Vector3 up)
        {
            float angle = wheel.DegreesPerSecond * seconds * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            position = wheel.Centre + Turn(part.Position - wheel.Centre, wheel.Axis, cos, sin);
            forward = Turn(part.Forward, wheel.Axis, cos, sin);
            up = Turn(part.Up, wheel.Axis, cos, sin);
        }

        /// <summary>Rodrigues: <paramref name="p"/> turned about the unit <paramref name="axis"/>.</summary>
        static Vector3 Turn(Vector3 p, Vector3 axis, float cos, float sin) =>
            p * cos + Vector3.Cross(axis, p) * sin + axis * (Vector3.Dot(axis, p) * (1f - cos));

        // ── Clearance ────────────────────────────────────────────────────

        /// <summary>Drop every part from <paramref name="first"/> on that reaches into a leg's lane or too near a ring.</summary>
        static void Clear(IReadOnlyList<RaceGate> lap, float clear, List<SpearlineRuinPart> parts, int first)
        {
            int keep = first;
            for (int i = first; i < parts.Count; i++)
            {
                if (!Stands(lap, clear, parts[i])) continue;
                parts[keep++] = parts[i];
            }
            parts.RemoveRange(keep, parts.Count - keep);
        }

        /// <summary>May a standing ruin prism be laid as <paramref name="part"/>: its whole length outside every leg's
        /// lane and clear of every ring?</summary>
        public static bool Stands(IReadOnlyList<RaceGate> lap, float clear, in SpearlineRuinPart part) =>
            PartLaneDistance(lap, part) >= clear && PartRingClearance(lap, part) >= RingMargin;

        /// <summary>How near <paramref name="part"/> comes to any leg's flown line, along its whole length.</summary>
        public static float PartLaneDistance(IReadOnlyList<RaceGate> lap, in SpearlineRuinPart part)
        {
            float best = float.MaxValue;
            int samples = LengthSamples(part);
            for (int k = 0; k < samples; k++)
                best = Mathf.Min(best, LaneDistance(lap, LengthPoint(part, k, samples)));
            return best;
        }

        /// <summary>How near <paramref name="part"/> comes to any ring's mouth, along its whole length.</summary>
        public static float PartRingClearance(IReadOnlyList<RaceGate> lap, in SpearlineRuinPart part)
        {
            float best = float.MaxValue;
            int samples = LengthSamples(part);
            for (int k = 0; k < samples; k++)
                best = Mathf.Min(best, RingClearance(lap, LengthPoint(part, k, samples)));
            return best;
        }

        static int LengthSamples(in SpearlineRuinPart part) => part.Scale.z <= 16f ? 1 : 1 + Mathf.CeilToInt(part.Scale.z / 10f);

        static Vector3 LengthPoint(in SpearlineRuinPart part, int k, int samples) =>
            samples == 1 ? part.Position : part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);

        /// <summary>How far <paramref name="p"/> is from the nearest leg's flown line (every leg, the closing one too).</summary>
        public static float LaneDistance(IReadOnlyList<RaceGate> lap, Vector3 p)
        {
            float best = float.MaxValue;
            for (int leg = 0; leg < lap.Count; leg++)
                best = Mathf.Min(best, LegDistance(lap, leg, p));
            return best;
        }

        /// <summary>How far <paramref name="p"/> is from the flown line of leg <paramref name="leg"/>.</summary>
        public static float LegDistance(IReadOnlyList<RaceGate> lap, int leg, Vector3 p)
        {
            var a = lap[leg];
            var b = lap[(leg + 1) % lap.Count];
            float best = float.MaxValue;
            Vector3 prev = a.Position;
            for (int k = 1; k <= LineSamples; k++)
            {
                Vector3 cur = LinePoint(a, b, k / (float)LineSamples);
                float d = SegmentDistanceSqr(p, prev, cur);
                if (d < best) best = d;
                prev = cur;
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>How far <paramref name="p"/> is past the nearest ring's mouth (distance to its centre less its radius).</summary>
        public static float RingClearance(IReadOnlyList<RaceGate> lap, Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < lap.Count; i++)
            {
                float d = (p - lap[i].Position).magnitude - lap[i].Radius;
                if (d < best) best = d;
            }
            return best;
        }

        static float SegmentDistanceSqr(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return (p - (a + ab * t)).sqrMagnitude;
        }

        /// <summary>An angle into [-pi, pi).</summary>
        static float Wrap(float a) => a - 2f * Mathf.PI * Mathf.Floor((a + Mathf.PI) / (2f * Mathf.PI));
    }
}
