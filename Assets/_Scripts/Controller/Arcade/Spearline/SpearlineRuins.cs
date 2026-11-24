using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One rung of the garden ruins (SPEARLINE.md §2a).</summary>
    public struct SpearlineRuinSettings
    {
        public int PerLap;          // ruins a lap, one to a leg at most (0: none on this rung)
        public float ClearRadius;   // no ruin prism's centre stands nearer than this (u) to any leg's flown line
    }

    /// <summary>One ruin prism: where it stands, how it is turned (forward and up, as <c>Quaternion.LookRotation</c>
    /// takes them) and its size.</summary>
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

    /// <summary>
    /// Spearline's garden ruins: what is left of old flora gardens, standing round the racing line between the rings of
    /// the hardest rung (Yash, 2026-11-24: "going through a ruins of old flora gardens"). Three kinds, one to a leg:
    /// a <b>pergola</b> (a row of broken arches), a <b>flower head</b> (a phyllotactic dish with its eye open and a bite
    /// out of it) and <b>vines</b> (two strands wound round the line, with gaps). Each is laid round the leg's flown
    /// line (a Hermite curve through the two rings along their axes) with a clear lane down the middle, so a pilot on
    /// the line flies THROUGH the ruin and one who strays meets its prisms, at the game's own cost for a prism.
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

        public const int KindCount = 3;

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

        /// <summary>Which kind of ruin leg <paramref name="leg"/> carries: 0 pergola, 1 flower head, 2 vines. Neighbouring
        /// legs differ.</summary>
        public static int KindOf(int leg, Vector3 ringPosition)
        {
            int salt = Mathf.FloorToInt(Mathf.Abs(ringPosition.x) + Mathf.Abs(ringPosition.y) + Mathf.Abs(ringPosition.z));
            return (leg + Mathf.FloorToInt(SpearlineSeals.Hash01(salt, 17, 3) * KindCount)) % KindCount;
        }

        /// <summary>
        /// Every ruin prism of <paramref name="lap"/> on this rung, appended to <paramref name="into"/>. The leg that
        /// closes the lap (into ring 0, where the field starts) carries none. A prism that would stand in any leg's
        /// lane or too near any ring is left out, so the result always holds both clearances.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> lap, in SpearlineRuinSettings s, List<SpearlineRuinPart> into)
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
                switch (KindOf(leg, a.Position))
                {
                    case 0: Pergola(centre, dir, u, v, s.ClearRadius, salt, into); break;
                    case 1: FlowerHead(centre, dir, u, v, s.ClearRadius, salt, into); break;
                    default: Vines(centre, dir, u, v, s.ClearRadius, salt, into); break;
                }
                Clear(lap, s.ClearRadius, into, first);
                if (into.Count > first) laid++;
            }
        }

        // ── The three kinds ──────────────────────────────────────────────

        /// <summary>A row of three arches round the lane, each with a fallen sector and a few stones gone.</summary>
        static void Pergola(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, float clear, int salt, List<SpearlineRuinPart> into)
        {
            const float step = 13f;
            var scale = new Vector3(5f, 5f, 12f);
            for (int arch = 0; arch < 3; arch++)
            {
                float radius = clear + 16f + 10f * SpearlineSeals.Hash01(salt, arch, 11);
                Vector3 at = c + dir * ((arch - 1) * 46f);
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
        static void FlowerHead(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, float clear, int salt, List<SpearlineRuinPart> into)
        {
            const float golden = 2.39996323f, spread = 12f;
            var scale = new Vector3(9f, 9f, 2.5f);
            float inner = clear + 6f, outer = clear + 78f;
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

        /// <summary>Two strands wound round the lane, each broken into runs.</summary>
        static void Vines(Vector3 c, Vector3 dir, Vector3 u, Vector3 v, float clear, int salt, List<SpearlineRuinPart> into)
        {
            const float length = 150f, turns = 1.15f;
            var scale = new Vector3(4.5f, 4.5f, 12f);
            for (int strand = 0; strand < 2; strand++)
            {
                float radius = clear + 10f + 14f * strand;
                float phase = SpearlineSeals.Hash01(salt, strand, 31) * 2f * Mathf.PI + Mathf.PI * strand;
                float sense = strand == 0 ? 1f : -1f;
                float arc = Mathf.Sqrt(Mathf.Pow(2f * Mathf.PI * radius * turns, 2f) + length * length);
                int count = Mathf.Max(8, Mathf.RoundToInt(arc / 12.5f));
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

        // ── Clearance ────────────────────────────────────────────────────

        /// <summary>Drop every part from <paramref name="first"/> on that stands in a leg's lane or too near a ring.</summary>
        static void Clear(IReadOnlyList<RaceGate> lap, float clear, List<SpearlineRuinPart> parts, int first)
        {
            int keep = first;
            for (int i = first; i < parts.Count; i++)
            {
                if (!Stands(lap, clear, parts[i].Position)) continue;
                parts[keep++] = parts[i];
            }
            parts.RemoveRange(keep, parts.Count - keep);
        }

        /// <summary>May a ruin prism stand at <paramref name="p"/>: outside every leg's lane and clear of every ring?</summary>
        public static bool Stands(IReadOnlyList<RaceGate> lap, float clear, Vector3 p) =>
            LaneDistance(lap, p) >= clear && RingClearance(lap, p) >= RingMargin;

        /// <summary>How far <paramref name="p"/> is from the nearest leg's flown line (every leg, the closing one too).</summary>
        public static float LaneDistance(IReadOnlyList<RaceGate> lap, Vector3 p)
        {
            float best = float.MaxValue;
            int n = lap.Count;
            for (int leg = 0; leg < n; leg++)
            {
                var a = lap[leg];
                var b = lap[(leg + 1) % n];
                Vector3 prev = a.Position;
                for (int k = 1; k <= LineSamples; k++)
                {
                    Vector3 cur = LinePoint(a, b, k / (float)LineSamples);
                    float d = SegmentDistanceSqr(p, prev, cur);
                    if (d < best) best = d;
                    prev = cur;
                }
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
