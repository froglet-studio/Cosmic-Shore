using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Scarab Rally's garden ruins (SCARABRALLY.md, "The ruins"): the SAME ruins Spearline lays round its racing line at
    /// I4 (<see cref="SpearlineRuins"/>: seven kinds, a clear lane down the flown line, nothing at a ring), laid from
    /// Scarab Rally's rings. This file adds no ruin of its own. It decides which of Spearline's prisms this course
    /// lays, because the course differs from Spearline's lap in two ways:
    ///
    /// <para><b>It is an open chain of 24 rings, not a lap of eight.</b> At most
    /// <see cref="SpearlineRuinSettings.PerLap"/> (seven) ruins are laid, on legs spread along the whole chain, so the
    /// ruins do not all stand on the first third of the race and the prism count stays Spearline's.</para>
    ///
    /// <para><b>Its I4 legs already carry Switchback's block fields.</b> A ruin prism that would reach within
    /// <see cref="FieldKeep"/> of a field prism is left out, and a ruin with fewer than <see cref="MinRuinPrisms"/>
    /// prisms left is not laid at all (scraps do not read as a ruin).</para>
    ///
    /// <para><b>The next ring stays in sight</b> (Garrett, 2026-11-14, SCARABRALLY.md "I4 keeps the sight line").
    /// Spearline's lane follows the curved line a pilot flies, and a wall across it can still stand in the STRAIGHT
    /// line from one ring to the next. So no ruin prism reaches into the sight bore the field itself keeps
    /// (<see cref="SwitchbackBlastRun.SightBore"/>), and the turning wheel, which sweeps the line, is never laid here.</para>
    ///
    /// <para>Pure: a function of the rings, the rung and the field, so every peer lays the same ruins from the
    /// broadcast course (<c>ScarabRallyRuinsTests</c>). Nothing here touches a vessel or changes the field.</para>
    /// </summary>
    public static class ScarabRallyRuins
    {
        /// <summary>SwitchbackController's NucleusMargin (private there): the race lays the field out again with the
        /// same clear radius, so the field the ruins are cut round is the field that stands. ScarabRallyRuinsTests reads
        /// Switchback's source to hold the two equal.</summary>
        public const float FieldNucleusMargin = 30f;

        /// <summary>u a ruin prism's surface keeps off a block-field prism's bounding ball.</summary>
        public const float FieldKeep = 6f;

        /// <summary>A ruin with fewer prisms than this left after the field is cut out of it is not laid.</summary>
        public const int MinRuinPrisms = 24;

        /// <summary>
        /// The ruin prisms of <paramref name="course"/> on this rung into <paramref name="parts"/>, in leg order, and
        /// for each part whether Scarab Rally lays it (<paramref name="laid"/>, the same length as
        /// <paramref name="parts"/>; a part the field or the sight line cuts out stays in the list, not laid). Only the
        /// ruins that stand are listed. <paramref name="wheels"/> is cleared and left empty: no wheel turns here.
        /// <paramref name="field"/> is the course's Blast Run prisms (null or empty: none).
        ///
        /// <para>Spearline's lane check is the cost (every raw prism against every leg), so a ruin is asked for only on
        /// the legs that may keep one: the budget's places, spread evenly along the legs long enough for a ruin, and
        /// the nearest untried leg where a place's ruin does not stand.</para>
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> course, int intensity, IReadOnlyList<SwitchbackFeaturePrism> field,
                               List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels, List<bool> laid)
        {
            parts.Clear();
            wheels.Clear();
            laid.Clear();
            if (course == null || course.Count < 3) return;
            var settings = SpearlineRuins.ForIntensity(intensity);
            int budget = settings.PerLap;
            if (budget <= 0) return;
            settings.PerLap = 1;                // one leg a call: the leg the course is turned to

            // The legs long enough for a ruin (Spearline's own floor; the closing leg of a lap does not exist here).
            var eligible = new List<int>(course.Count);
            for (int leg = 0; leg + 1 < course.Count; leg++)
                if ((course[leg + 1].Position - course[leg].Position).magnitude >= settings.MinLegLength) eligible.Add(leg);
            if (eligible.Count == 0) return;

            var turned = new TurnedCourse(course);
            var tried = new bool[eligible.Count];
            var near = new List<int>(256);
            var ruins = new List<(int leg, int first, int count)>(budget);
            for (int k = 0; k < budget; k++)
            {
                // The budget: at most `budget` ruins, spread evenly along the chain.
                int place = budget == 1 ? 0 : k * (eligible.Count - 1) / (budget - 1);
                // The nearest untried leg to that place whose ruin stands.
                for (int step = 0; step < 2 * eligible.Count; step++)
                {
                    int e = place + ((step & 1) == 0 ? step / 2 : -(step + 1) / 2);
                    if (e < 0 || e >= eligible.Count || tried[e]) continue;
                    tried[e] = true;
                    int first = parts.Count;
                    if (!LayLeg(course, turned, intensity, settings, eligible[e], field, parts, wheels, laid, near)) continue;
                    ruins.Add((eligible[e], first, parts.Count - first));
                    break;
                }
            }
            InLegOrder(ruins, parts, laid);
        }

        /// <summary>One leg's ruin, appended to <paramref name="parts"/> and <paramref name="laid"/>; false, and nothing
        /// appended, where it does not stand (no prism, a wheel, or scraps once the field and the sight line are cut out).</summary>
        static bool LayLeg(IReadOnlyList<RaceGate> course, TurnedCourse turned, int intensity, in SpearlineRuinSettings settings,
                           int leg, IReadOnlyList<SwitchbackFeaturePrism> field, List<SpearlineRuinPart> parts,
                           List<SpearlineRuinWheel> wheels, List<bool> laid, List<int> near)
        {
            int first = parts.Count, firstWheel = wheels.Count;
            // The same rings and the same lanes, counted from this leg: Spearline lays its first leg and stops.
            turned.Start = leg;
            SpearlineRuins.Lay(turned, settings, parts, wheels);
            int left = 0;
            // A leg whose every prism Spearline cleared away lets the lay run on to the next leg: not this leg's ruin.
            if (parts.Count > first && parts[first].Leg == 0)
            {
                // 1. The sight line from each ring to the next stays open; a wheel sweeps it, so no wheel.
                for (int i = first; i < parts.Count; i++)
                {
                    parts[i] = parts[i].OnLeg(leg);
                    laid.Add(WheelOf(wheels, i) < 0 && !ReachesSightLine(course, intensity, parts[i]));
                }
                // 2. Cut the field out of the ruin. It is tested against the field prisms near it only.
                Near(parts, first, parts.Count, field, near);
                for (int i = first; i < parts.Count; i++)
                {
                    if (near.Count > 0) laid[i] = laid[i] && !ReachesField(parts[i], field, near);
                    if (laid[i]) left++;
                }
            }
            // 3. A ruin that the field left in scraps is not laid.
            if (left >= MinRuinPrisms) return true;
            parts.RemoveRange(first, parts.Count - first);
            laid.RemoveRange(first, laid.Count - first);
            wheels.RemoveRange(firstWheel, wheels.Count - firstWheel);
            return false;
        }

        /// <summary>The standing ruins, put in the order of their legs (they were laid in the order of the budget's
        /// places). No wheel stands, so no wheel's part numbers move.</summary>
        static void InLegOrder(List<(int leg, int first, int count)> ruins, List<SpearlineRuinPart> parts, List<bool> laid)
        {
            ruins.Sort((x, y) => x.leg.CompareTo(y.leg));
            var p = new List<SpearlineRuinPart>(parts.Count);
            var l = new List<bool>(laid.Count);
            foreach (var (_, first, count) in ruins)
            {
                p.AddRange(parts.GetRange(first, count));
                l.AddRange(laid.GetRange(first, count));
            }
            parts.Clear();
            parts.AddRange(p);
            laid.Clear();
            laid.AddRange(l);
        }

        /// <summary>The course counted from ring <see cref="Start"/> and wrapping: the same rings and, to
        /// <see cref="SpearlineRuins"/>, the same legs (it wraps the last ring to the first itself), with the wanted
        /// leg first.</summary>
        sealed class TurnedCourse : IReadOnlyList<RaceGate>
        {
            readonly IReadOnlyList<RaceGate> _course;
            public int Start;
            public TurnedCourse(IReadOnlyList<RaceGate> course) => _course = course;
            public int Count => _course.Count;
            public RaceGate this[int index] => _course[(index + Start) % _course.Count];

            public IEnumerator<RaceGate> GetEnumerator()
            {
                for (int i = 0; i < Count; i++) yield return this[i];
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>How many ruins (legs with a laid prism) a lay has.</summary>
        public static int RuinCount(IReadOnlyList<SpearlineRuinPart> parts, IReadOnlyList<bool> laid)
        {
            int ruins = 0, last = -1;
            for (int i = 0; i < parts.Count; i++)
            {
                if (!laid[i] || parts[i].Leg == last) continue;
                last = parts[i].Leg;
                ruins++;
            }
            return ruins;
        }

        static int WheelOf(List<SpearlineRuinWheel> wheels, int part)
        {
            for (int w = 0; w < wheels.Count; w++)
                if (part >= wheels[w].First && part < wheels[w].First + wheels[w].Count) return w;
            return -1;
        }

        /// <summary>The field prisms that could touch parts <paramref name="from"/>..<paramref name="to"/>: those
        /// inside the parts' bounding ball, grown by the largest reach either side can have.</summary>
        static void Near(List<SpearlineRuinPart> parts, int from, int to, IReadOnlyList<SwitchbackFeaturePrism> field,
                         List<int> into)
        {
            into.Clear();
            if (field == null || field.Count == 0) return;
            Vector3 centre = Vector3.zero;
            for (int i = from; i < to; i++) centre += parts[i].Position;
            centre /= to - from;
            float radius = 0f;
            for (int i = from; i < to; i++)
                radius = Mathf.Max(radius, (parts[i].Position - centre).magnitude + Largest(parts[i].Scale));
            for (int f = 0; f < field.Count; f++)
                if ((field[f].Position - centre).magnitude < radius + Largest(field[f].Scale) + FieldKeep) into.Add(f);
        }

        static float Largest(Vector3 scale) => Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

        /// <summary>The ball a field prism is kept clear by: half its largest side, and the keep.</summary>
        static float Ball(in SwitchbackFeaturePrism prism) => 0.5f * Largest(prism.Scale) + FieldKeep;

        /// <summary>Does a standing <paramref name="part"/> come within <see cref="FieldKeep"/> of one of the
        /// <paramref name="near"/> field prisms, anywhere along its length (its half thickness counted)?</summary>
        public static bool ReachesField(in SpearlineRuinPart part, IReadOnlyList<SwitchbackFeaturePrism> field,
                                        IReadOnlyList<int> near)
        {
            float half = 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
            int samples = 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            for (int n = 0; n < near.Count; n++)
            {
                var prism = field[near[n]];
                float reach = half + Ball(prism);
                for (int k = 0; k < samples; k++)
                {
                    float along = samples == 1 ? 0f : (k / (float)(samples - 1) - 0.5f) * part.Scale.z;
                    if ((part.Position + part.Forward * along - prism.Position).magnitude < reach) return true;
                }
            }
            return false;
        }

        /// <summary>Does <paramref name="part"/> reach into the sight bore of any leg: the straight line from one ring to
        /// the next, as wide as the field keeps it (<see cref="SwitchbackBlastRun.SightBore"/>, no gauntlet), anywhere
        /// along the part's length (its half thickness counted)?</summary>
        public static bool ReachesSightLine(IReadOnlyList<RaceGate> course, int intensity, in SpearlineRuinPart part)
        {
            float half = 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
            int samples = 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            for (int leg = 0; leg + 1 < course.Count; leg++)
            {
                var a = course[leg];
                var b = course[leg + 1];
                float reach = half + SwitchbackBlastRun.SightBore(intensity, false, b.Radius, a.Radius);
                for (int k = 0; k < samples; k++)
                {
                    float along = samples == 1 ? 0f : (k / (float)(samples - 1) - 0.5f) * part.Scale.z;
                    if (SwitchbackFeatures.DistanceToSegment(part.Position + part.Forward * along, a.Position, b.Position) < reach)
                        return true;
                }
            }
            return false;
        }
    }
}
