using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skim Race's garden ruins (SKIMRACE.md §5b): the SAME ruins Spearline lays round its racing line at I4
    /// (<see cref="SpearlineRuins"/>: seven kinds, a clear lane down the flown line, nothing at a gate), laid round
    /// Skim Race's ribbon. This file adds no ruin of its own. It turns the ribbon into the gates Spearline's generator
    /// reads, and decides which of the generator's ruins this course keeps, because Skim Race's course is not
    /// Spearline's:
    ///
    /// <para><b>Gates.</b> A gate stands at every crystal marker of the ribbon (a crystal appears near each), facing
    /// along the ribbon, as wide as the shell a crystal is placed on (<see cref="CrystalShell"/>).</para>
    ///
    /// <para><b>Seven ruins, spread round the lap.</b> The ribbon has many more legs than Spearline's lap (25 against
    /// 7), so every leg is offered to the generator and seven are kept: spread evenly round the lap, each of a
    /// different kind where the course allows it.</para>
    ///
    /// <para><b>Two flown lines.</b> A pilot rides the ribbon to skim it, or cuts straight from one crystal to the
    /// next. A standing ruin prism keeps <see cref="RibbonKeep"/> off both. The turning wheel is laid whole or not at
    /// all, and only where both lines pass through its open hub.</para>
    ///
    /// <para><b>Nothing in the nucleus.</b> No ruin prism stands within <see cref="CoreRadius"/> of the cell's centre,
    /// where the ribbon's five passes cross.</para>
    ///
    /// <para>Pure: a function of the ribbon and the rung, so every peer lays the same ruins from the authored track
    /// (<c>SkimRaceRuinsTests</c>). Nothing here touches a vessel, a crystal or the ribbon.</para>
    /// </summary>
    public static class SkimRaceRuins
    {
        /// <summary>The radius of a gate: a crystal is placed on a shell this far (u) from its anchor
        /// (<c>NetworkCrystalManager.anchorJitterRadius</c>), so the generator's ring margin starts outside it.</summary>
        public const float CrystalShell = 35f;

        /// <summary>u a standing ruin prism keeps off the ribbon and off the straight line between two crystals,
        /// measured as Spearline measures its lane (from the prism's centre line).</summary>
        public const float RibbonKeep = SpearlineRuins.LaneRadius;

        /// <summary>No ruin prism within this distance (u) of the cell's centre: the radius inside which the track's
        /// generator counts a pass as threading the nucleus (<c>NUCLEUS_R</c>).</summary>
        public const float CoreRadius = 330f;

        /// <summary>A leg keeps its ruin only with at least this many prisms left standing: fewer does not read as a
        /// ruin.</summary>
        public const int MinParts = 24;

        /// <summary>A flown line passes a wheel through its hub when it is within this distance (u) of the wheel's
        /// axis: the hub (<see cref="SpearlineRuins.HubRadius"/>) less the reach of a ribbon block (15 u) and a margin.</summary>
        public const float HubOpen = 12f;

        /// <summary>A flown line is in a wheel's plane within this distance (u) along its axis, and clear of its rim
        /// this far (u) outside it.</summary>
        public const float WheelPlaneKeep = 20f, WheelRimKeep = 15f;

        /// <summary>Spacing (u) of the points a straight crystal-to-crystal line is sampled at: the ribbon's own.</summary>
        const float ChordStep = 12f;

        /// <summary>The gates of one lap: one at every marked block of <paramref name="ribbon"/>, facing along it.</summary>
        public static void Gates(IReadOnlyList<Vector3> ribbon, IReadOnlyList<bool> marker, List<RaceGate> into)
        {
            into.Clear();
            int n = ribbon.Count;
            for (int i = 0; i < n; i++)
            {
                if (!marker[i]) continue;
                Vector3 along = ribbon[(i + 1) % n] - ribbon[(i - 1 + n) % n];
                if (along.sqrMagnitude < 1e-6f) continue;
                into.Add(new RaceGate(ribbon[i], along.normalized, CrystalShell));
            }
        }

        /// <summary>
        /// The ruins of one lap of <paramref name="ribbon"/> (its blocks in lay order, closed) on this rung. Every
        /// list is cleared first. <paramref name="parts"/> holds every prism the generator offers and
        /// <paramref name="laid"/> (the same length) says which of them Skim Race lays; a part that is not laid keeps
        /// its place, so a wheel's <c>First</c> and <c>Count</c> stay true. <paramref name="core"/> is the cell's centre.
        /// </summary>
        public static void Lay(IReadOnlyList<Vector3> ribbon, IReadOnlyList<bool> marker, Vector3 core, int intensity,
                               List<RaceGate> gates, List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels,
                               List<bool> laid)
        {
            gates.Clear();
            parts.Clear();
            wheels.Clear();
            laid.Clear();
            var rung = SpearlineRuins.ForIntensity(intensity);
            if (rung.PerLap <= 0 || ribbon == null || marker == null || ribbon.Count != marker.Count) return;
            Gates(ribbon, marker, gates);
            if (gates.Count < 3) return;

            int want = rung.PerLap;
            rung.PerLap = gates.Count;   // offer every leg; seven are kept below
            SpearlineRuins.Lay(gates, rung, parts, wheels);
            if (parts.Count == 0) return;

            var lines = FlownLines(ribbon, gates);
            int legs = gates.Count - 1;
            var first = new int[legs];
            var count = new int[legs];
            var wheelOf = new int[legs];
            for (int leg = 0; leg < legs; leg++) wheelOf[leg] = -1;
            for (int i = 0; i < parts.Count; i++)
            {
                int leg = parts[i].Leg;
                if (count[leg]++ == 0) first[leg] = i;
            }
            for (int w = 0; w < wheels.Count; w++) wheelOf[parts[wheels[w].First].Leg] = w;

            var fits = new bool[parts.Count];
            var state = new int[legs];   // 0 not looked at, 1 keeps a ruin, -1 does not
            var taken = new bool[legs];
            var kindUsed = new bool[SpearlineRuins.KindCount];
            var slotFilled = new bool[want];
            var near = new List<int>(256);

            // Pass 0 gives every slot a kind no other slot has, never beside another ruin; pass 1 lets a kind come
            // twice; pass 2 fills what is left with any leg that keeps a ruin.
            for (int pass = 0; pass < 3; pass++)
            {
                for (int slot = 0; slot < want; slot++)
                {
                    if (slotFilled[slot]) continue;
                    float ideal = (slot + 0.5f) * legs / want - 0.5f;
                    int best = -1;
                    float bestGap = float.MaxValue;
                    for (int leg = 0; leg < legs; leg++)
                    {
                        if (taken[leg] || count[leg] == 0) continue;
                        if (pass == 0 && kindUsed[KindOnLeg(gates, wheelOf, leg)]) continue;
                        if (pass < 2 && ((leg > 0 && taken[leg - 1]) || (leg + 1 < legs && taken[leg + 1]))) continue;
                        float gap = Mathf.Abs(leg - ideal);
                        if (gap >= bestGap) continue;
                        if (state[leg] == 0)
                            state[leg] = Keeps(parts, wheels, wheelOf[leg], first[leg], count[leg], lines, core, near, fits) ? 1 : -1;
                        if (state[leg] < 0) continue;
                        best = leg;
                        bestGap = gap;
                    }
                    if (best < 0) continue;
                    taken[best] = true;
                    slotFilled[slot] = true;
                    kindUsed[KindOnLeg(gates, wheelOf, best)] = true;
                }
            }

            for (int i = 0; i < parts.Count; i++) laid.Add(taken[parts[i].Leg] && fits[i]);
        }

        /// <summary>The kind of ruin the generator laid on <paramref name="leg"/>: its turn's kind, or a pergola where
        /// the turn was a wheel that did not fit.</summary>
        static int KindOnLeg(IReadOnlyList<RaceGate> gates, int[] wheelOf, int leg)
        {
            if (wheelOf[leg] >= 0) return SpearlineRuins.Wheel;
            int kind = SpearlineRuins.KindOf(leg, gates[0].Position);
            return kind == SpearlineRuins.Wheel ? SpearlineRuins.Pergola : kind;
        }

        /// <summary>Every point a pilot flies: the ribbon's blocks, and the straight line from each gate to the next
        /// (the closing one too) sampled at the ribbon's spacing.</summary>
        public static List<Vector3> FlownLines(IReadOnlyList<Vector3> ribbon, IReadOnlyList<RaceGate> gates)
        {
            var lines = new List<Vector3>(ribbon.Count * 2);
            for (int i = 0; i < ribbon.Count; i++) lines.Add(ribbon[i]);
            for (int g = 0; g < gates.Count; g++)
            {
                Vector3 a = gates[g].Position, b = gates[(g + 1) % gates.Count].Position;
                int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / ChordStep));
                for (int k = 1; k < steps; k++) lines.Add(Vector3.Lerp(a, b, k / (float)steps));
            }
            return lines;
        }

        /// <summary>Does the ruin of one leg (<paramref name="count"/> parts from <paramref name="first"/>) keep enough
        /// of itself on this course? Fills <paramref name="fits"/> for its parts.</summary>
        static bool Keeps(List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels, int wheel, int first, int count,
                          List<Vector3> lines, Vector3 core, List<int> near, bool[] fits)
        {
            if (wheel >= 0)
            {
                bool turns = WheelTurnsFree(wheels[wheel], lines, core);
                for (int i = first; i < first + count; i++) fits[i] = turns;
                return turns;
            }

            Vector3 centre = Vector3.zero;
            for (int i = first; i < first + count; i++) centre += parts[i].Position;
            centre /= count;
            float reach = 0f;
            for (int i = first; i < first + count; i++)
                reach = Mathf.Max(reach, (parts[i].Position - centre).magnitude + 0.5f * parts[i].Scale.z);
            reach += RibbonKeep + 40f;
            near.Clear();
            for (int p = 0; p < lines.Count; p++)
                if ((lines[p] - centre).sqrMagnitude < reach * reach) near.Add(p);

            int standing = 0;
            for (int i = first; i < first + count; i++)
            {
                fits[i] = Clearance(parts[i], lines, near) >= RibbonKeep && CoreClearance(parts[i], core) >= CoreRadius;
                if (fits[i]) standing++;
            }
            return standing >= MinParts;
        }

        /// <summary>How far a standing <paramref name="part"/>'s centre line is from the nearest of
        /// <paramref name="lines"/> (<see cref="FlownLines"/>), along its whole length.</summary>
        public static float Clearance(in SpearlineRuinPart part, IReadOnlyList<Vector3> lines) => Clearance(part, lines, null);

        static float Clearance(in SpearlineRuinPart part, IReadOnlyList<Vector3> lines, List<int> only)
        {
            float best = float.MaxValue;
            int samples = Samples(part);
            int n = only?.Count ?? lines.Count;
            for (int k = 0; k < samples; k++)
            {
                Vector3 at = Point(part, k, samples);
                for (int j = 0; j < n; j++)
                {
                    float d = (lines[only != null ? only[j] : j] - at).sqrMagnitude;
                    if (d < best) best = d;
                }
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>How far a standing <paramref name="part"/>'s surface is from <paramref name="core"/>.</summary>
        public static float CoreClearance(in SpearlineRuinPart part, Vector3 core)
        {
            float best = float.MaxValue;
            int samples = Samples(part);
            for (int k = 0; k < samples; k++)
                best = Mathf.Min(best, (Point(part, k, samples) - core).magnitude);
            return best - HalfThick(part);
        }

        /// <summary>May <paramref name="wheel"/> turn here: its whole sweep outside the nucleus, and every flown line
        /// that crosses its plane inside the rim doing so through the hub?</summary>
        public static bool WheelTurnsFree(in SpearlineRuinWheel wheel, IReadOnlyList<Vector3> lines, Vector3 core)
        {
            if ((wheel.Centre - core).magnitude < CoreRadius + SpearlineRuins.WheelRadius) return false;
            float rim = SpearlineRuins.WheelRadius + WheelRimKeep;
            for (int p = 0; p < lines.Count; p++)
            {
                Vector3 rel = lines[p] - wheel.Centre;
                float along = Vector3.Dot(rel, wheel.Axis);
                if (Mathf.Abs(along) > WheelPlaneKeep) continue;
                float outward = (rel - wheel.Axis * along).magnitude;
                if (outward > HubOpen && outward < rim) return false;
            }
            return true;
        }

        static float HalfThick(in SpearlineRuinPart part) => 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);

        static int Samples(in SpearlineRuinPart part) => Mathf.Max(2, 1 + Mathf.CeilToInt(part.Scale.z / 10f));

        static Vector3 Point(in SpearlineRuinPart part, int k, int samples) =>
            part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);
    }
}
