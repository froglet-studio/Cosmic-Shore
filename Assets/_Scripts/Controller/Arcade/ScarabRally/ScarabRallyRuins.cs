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
    /// <para><b>It is an open chain of 24 rings, not a lap of eight.</b> Every leg is asked for a ruin, and at most
    /// <see cref="SpearlineRuinSettings.PerLap"/> (seven) of them are laid, spread along the whole chain, so the ruins
    /// do not all stand on the first third of the race and the prism count stays Spearline's.</para>
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
        /// Every candidate ruin prism of <paramref name="course"/> on this rung into <paramref name="parts"/>, the
        /// turning ones named in <paramref name="wheels"/>, and for each part whether Scarab Rally lays it
        /// (<paramref name="laid"/>, the same length as <paramref name="parts"/>). The lists are cleared first. A part
        /// that is not laid keeps its place in the list, so a wheel's <c>First</c> and <c>Count</c> stay true.
        /// <paramref name="field"/> is the course's Blast Run prisms (null or empty: none).
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
            settings.PerLap = course.Count;     // every leg is a candidate; the budget is applied below
            SpearlineRuins.Lay(course, settings, parts, wheels);
            if (parts.Count == 0) return;

            int legs = course.Count;
            var first = new int[legs];
            var count = new int[legs];
            for (int i = 0; i < parts.Count; i++)
            {
                int leg = parts[i].Leg;
                if (count[leg]++ == 0) first[leg] = i;
            }

            // 1. The sight line from each ring to the next stays open; a wheel sweeps it, so no wheel.
            for (int i = 0; i < parts.Count; i++)
                laid.Add(WheelOf(wheels, i) < 0 && !ReachesSightLine(course, intensity, parts[i]));

            // 2. Cut the field out of every ruin. A leg's ruin is tested against the field prisms near it only.
            var near = new List<int>(256);
            for (int leg = 0; leg < legs; leg++)
            {
                if (count[leg] == 0) continue;
                int end = first[leg] + count[leg];
                Near(parts, first[leg], end, field, near);
                if (near.Count == 0) continue;
                for (int i = first[leg]; i < end; i++) laid[i] = laid[i] && !ReachesField(parts[i], field, near);
            }

            // 3. A ruin that the field left in scraps is not laid.
            var standing = new List<int>(legs);
            for (int leg = 0; leg < legs; leg++)
            {
                int left = 0;
                for (int i = first[leg]; i < first[leg] + count[leg]; i++)
                    if (laid[i]) left++;
                if (left >= MinRuinPrisms) standing.Add(leg);
            }

            // 4. The budget: at most `budget` ruins, spread evenly along the chain.
            var keep = new bool[legs];
            if (standing.Count <= budget)
                for (int k = 0; k < standing.Count; k++) keep[standing[k]] = true;
            else
                for (int k = 0; k < budget; k++)
                    keep[standing[budget == 1 ? 0 : k * (standing.Count - 1) / (budget - 1)]] = true;
            for (int i = 0; i < parts.Count; i++) laid[i] = laid[i] && keep[parts[i].Leg];
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
