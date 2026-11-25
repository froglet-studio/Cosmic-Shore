using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slip Line's ruins at I4 (SLIP_LINE.md §3a): Spearline's garden ruins (<see cref="SpearlineRuins"/>, called, not
    /// copied) standing round the line flown ring to ring. Decoration (Yash, 2026-11-24): plain prisms with the game's
    /// own rules, nothing here reads or writes a vessel.
    ///
    /// <para>Two things are Slip Line's own. <b>The lap it hands the generator</b> (<see cref="RaceLineLap"/>): Slip
    /// Line's flown line is the STRAIGHT ring-to-ring line (the one the course test holds clear of every rail), and a
    /// ring's axis is the leg INTO it, so the generator's curve between two real rings would bow off that line. Each
    /// ring is therefore given twice, once facing the leg in and once facing the leg out, which makes every leg's lane
    /// the straight line; the start line is a gate too, so the run-up to ring 0 is a lane. The course has more long
    /// legs than a lap has ruins, so the legs that carry none are cut in two (too short for a ruin), which spreads the
    /// seven ruins over the whole course, and the chosen legs are spaced so the kinds still take turns.
    /// <b>The rails</b> (<see cref="KeepOffRails"/>): a rail leans up to 25 degrees off its leg and so leaves the lane;
    /// every ruin prism within <see cref="RailClearance"/> of a rail is left out, and a wheel that would sweep a rail
    /// is not laid.</para>
    ///
    /// <para>Pure: a function of the rings, the rails and the rung, so every peer lays the same ruins from the
    /// broadcast rings (<c>SlipLineRuinsTests</c>).</para>
    /// </summary>
    public static class SlipLineRuins
    {
        /// <summary>No ruin prism's centre line comes nearer than this (u) to a rail: room to meet the rail nose along
        /// it (the slip's contact radius is 5.5) and to see it against the ruin.</summary>
        public const float RailClearance = 40f;

        /// <summary>A wheel sweeps a rail that passes within this (u) of its plane.</summary>
        public const float WheelSlab = 20f;

        const float RailStep = 5f;

        /// <summary>Every ruin prism of the course on this rung into <paramref name="parts"/> (the turning ones named
        /// in <paramref name="wheels"/>), clear of every leg's flown line, every ring and every rail.
        /// <paramref name="lap"/> is scratch: it is left holding the lap the generator was given.</summary>
        public static void Lay(IReadOnlyList<RaceGate> rings, IReadOnlyList<Rail> rails, int intensity,
                               List<RaceGate> lap, List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels)
        {
            parts.Clear();
            wheels.Clear();
            lap.Clear();
            var settings = SpearlineRuins.ForIntensity(intensity);
            if (settings.PerLap <= 0 || rings == null || rings.Count < 3) return;
            RaceLineLap(rings, settings.PerLap, lap);
            SpearlineRuins.Lay(lap, settings, parts, wheels);
            KeepOffRails(rails, parts, wheels);
        }

        /// <summary>
        /// The lap <see cref="SpearlineRuins.Lay"/> is given for Slip Line's <paramref name="rings"/>: the start line,
        /// then every ring twice (facing the leg in, then the leg out), so each leg's lane is the straight line. Of the
        /// legs long enough for a ruin, <paramref name="ruins"/> are kept whole, spread evenly down the course; the
        /// others get a gate at their middle. A kept leg's index is padded (a repeated gate, a leg of no length) so
        /// consecutive ruins are consecutive kinds.
        /// </summary>
        public static void RaceLineLap(IReadOnlyList<RaceGate> rings, int ruins, List<RaceGate> lap)
        {
            lap.Clear();
            int n = rings.Count;
            int eligible = 0;
            for (int j = 0; j + 1 < n; j++)
                if ((rings[j + 1].Position - rings[j].Position).magnitude >= SpearlineRuins.MinLegLength) eligible++;

            lap.Add(new RaceGate(SlipLineCourse.LegStart(rings, 0), rings[0].Axis, 0f));
            int seen = 0, kept = 0, firstKept = -1;
            for (int j = 0; j < n; j++)
            {
                var ring = rings[j];
                var facingIn = new RaceGate(ring.Position, SlipLineCourse.LegDirection(rings, j), ring.Radius);
                lap.Add(facingIn);
                if (j + 1 == n) break;

                Vector3 chord = rings[j + 1].Position - ring.Position;
                Vector3 dir = SlipLineCourse.LegDirection(rings, j + 1);
                bool longLeg = chord.magnitude >= SpearlineRuins.MinLegLength;
                bool keep = longLeg && Takes(seen, eligible, ruins);
                if (longLeg) seen++;
                if (keep)
                {
                    // the leg's index is the index of the gate it leaves from, which is added next
                    if (firstKept < 0) firstKept = lap.Count;
                    else while ((lap.Count - firstKept) % SpearlineRuins.KindCount != kept % SpearlineRuins.KindCount) lap.Add(facingIn);
                    kept++;
                }
                lap.Add(new RaceGate(ring.Position, dir, ring.Radius));
                if (longLeg && !keep) lap.Add(new RaceGate(ring.Position + 0.5f * chord, dir, 0f));
            }
        }

        /// <summary>Is the <paramref name="index"/>th of <paramref name="of"/> long legs one of the
        /// <paramref name="take"/> that carry a ruin (evenly spread; all of them when there are no more than that)?</summary>
        static bool Takes(int index, int of, int take) =>
            of <= take || (index + 1) * take / of > index * take / of;

        /// <summary>Drop every standing part that comes within <see cref="RailClearance"/> of a rail, and every wheel
        /// (whole) that would sweep one. The order is kept, and the wheels are re-pointed at their parts.</summary>
        public static void KeepOffRails(IReadOnlyList<Rail> rails, List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels)
        {
            if (rails == null || rails.Count == 0) return;
            int keep = 0, wheel = 0, keptWheels = 0;
            for (int i = 0; i < parts.Count;)
            {
                if (wheel < wheels.Count && wheels[wheel].First == i)
                {
                    var w = wheels[wheel++];
                    if (!SweepsARail(rails, w))
                    {
                        wheels[keptWheels++] = new SpearlineRuinWheel(w.Centre, w.Axis, w.DegreesPerSecond, keep, w.Count);
                        for (int k = 0; k < w.Count; k++) parts[keep++] = parts[i + k];
                    }
                    i += w.Count;
                    if (w.Count > 0) continue;
                }
                if (RailDistance(rails, parts[i]) >= RailClearance) parts[keep++] = parts[i];
                i++;
            }
            parts.RemoveRange(keep, parts.Count - keep);
            wheels.RemoveRange(keptWheels, wheels.Count - keptWheels);
        }

        /// <summary>How near <paramref name="part"/> comes to any rail, along its whole length.</summary>
        public static float RailDistance(IReadOnlyList<Rail> rails, in SpearlineRuinPart part)
        {
            float best = float.MaxValue;
            int samples = part.Scale.z <= 16f ? 1 : 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            for (int k = 0; k < samples; k++)
            {
                Vector3 p = samples == 1
                    ? part.Position
                    : part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);
                for (int r = 0; r < rails.Count; r++)
                    best = Mathf.Min(best, SlipLineCourse.DistanceToSegment(p, rails[r].Start, rails[r].End));
            }
            return best;
        }

        /// <summary>Does a rail pass through the disc <paramref name="wheel"/> turns in (hub and all)?</summary>
        public static bool SweepsARail(IReadOnlyList<Rail> rails, in SpearlineRuinWheel wheel)
        {
            float reach = SpearlineRuins.WheelRadius + RailClearance;
            for (int r = 0; r < rails.Count; r++)
            {
                var rail = rails[r];
                int steps = Mathf.Max(1, Mathf.CeilToInt(rail.Length / RailStep));
                for (int k = 0; k <= steps; k++)
                {
                    Vector3 d = Vector3.Lerp(rail.Start, rail.End, k / (float)steps) - wheel.Centre;
                    float along = Vector3.Dot(d, wheel.Axis);
                    if (Mathf.Abs(along) > WheelSlab) continue;
                    if ((d - wheel.Axis * along).magnitude < reach) return true;
                }
            }
            return false;
        }
    }
}
