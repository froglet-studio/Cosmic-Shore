using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Switchback's I4 garden ruins (<see cref="SwitchbackRuins"/>, SWITCHBACK.md "The I4 ruins"): Spearline's ruins
    /// laid round the gauntlet course. Swept over the shipped shell, 40 seeds and the picker's fixed I4 seed: none
    /// below I4, the generator's lane and ring margin hold, the flown route (ring, keys, ring) stays open, no ruin
    /// prism stands in a Blast Run prism, the count stays inside its budget, and the lay is deterministic.
    /// Headless outside Unity with the Blast Run harness's shim (SWITCHBACK.md "The I4 ruins").
    /// </summary>
    public class SwitchbackRuinsTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Gates = 26;
        const int Seeds = 40;
        const float ClearRadius = Inner / 1.22f + 30f;

        /// <summary>Ruin prisms a course, at most (measured worst 2026-11-24: see SWITCHBACK.md "The I4 ruins").</summary>
        const int PrismBudget = 450;

        static List<RaceGate> Course(int intensity, int seed)
        {
            var s = SwitchbackCourseSettings.ForIntensity(intensity);
            s.GateCount = Gates;
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            s.FirstGateDistance = 620f;
            return SwitchbackCourse.Generate(seed, s);
        }

        static IEnumerable<int> SeedList()
        {
            for (int s = 1; s <= Seeds; s++) yield return s;
            yield return SwitchbackBlastRun.FixedSeed(4);
        }

        static List<SpearlineRuinPart> Lay(List<RaceGate> course, int intensity, out SwitchbackFeatureLayout layout)
        {
            layout = SwitchbackBlastRun.Layout(course, intensity, Vector3.zero, ClearRadius, true);
            var parts = new List<SpearlineRuinPart>();
            SwitchbackRuins.Lay(course, intensity, layout, parts);
            return parts;
        }

        static int RuinCount(List<SpearlineRuinPart> parts)
        {
            var legs = new HashSet<int>();
            foreach (var part in parts) legs.Add(part.Leg);
            return legs.Count;
        }

        [Test]
        public void NoRuinsBelowI4()
        {
            for (int intensity = 1; intensity <= 3; intensity++)
                foreach (int seed in SeedList())
                {
                    var course = Course(intensity, seed);
                    if (course == null) continue;
                    Assert.AreEqual(0, Lay(course, intensity, out _).Count, $"I{intensity} seed {seed}");
                }
        }

        [Test]
        public void I4LaysRuinsOnEveryCourse_InsideTheBudget()
        {
            int courses = 0, total = 0, fewest = int.MaxValue;
            foreach (int seed in SeedList())
            {
                var course = Course(4, seed);
                if (course == null) continue;
                var parts = Lay(course, 4, out _);
                int ruins = RuinCount(parts);
                courses++;
                total += ruins;
                fewest = Mathf.Min(fewest, ruins);
                Assert.LessOrEqual(ruins, SwitchbackRuins.PerCourse, $"seed {seed}: ruins");
                Assert.LessOrEqual(parts.Count, PrismBudget, $"seed {seed}: ruin prisms");
            }
            Assert.Greater(courses, Seeds / 2, "courses generated");
            Assert.GreaterOrEqual(fewest, 3, "the poorest course still has ruins");
            Assert.GreaterOrEqual(total, 4 * courses, "four ruins a course on average");
        }

        [Test]
        public void EveryRuinIsWholeSpreadAndOfItsOwnKind()
        {
            foreach (int seed in SeedList())
            {
                var course = Course(4, seed);
                if (course == null) continue;
                var parts = Lay(course, 4, out _);
                var perLeg = new SortedDictionary<int, int>();
                foreach (var part in parts) perLeg[part.Leg] = perLeg.TryGetValue(part.Leg, out int n) ? n + 1 : 1;
                int last = -SwitchbackRuins.LegGap, kinds = 0;
                foreach (var pair in perLeg)
                {
                    Assert.GreaterOrEqual(pair.Value, SwitchbackRuins.MinParts, $"seed {seed} leg {pair.Key}: prisms");
                    Assert.GreaterOrEqual(pair.Key - last, SwitchbackRuins.LegGap, $"seed {seed} leg {pair.Key}: gap");
                    last = pair.Key;
                    int kind = SpearlineRuins.KindOf(pair.Key, course[0].Position);
                    if (kind == SpearlineRuins.Wheel) kind = SpearlineRuins.Pergola;
                    Assert.AreEqual(0, kinds & (1 << kind), $"seed {seed} leg {pair.Key}: kind {kind} laid twice");
                    kinds |= 1 << kind;
                }
            }
        }

        [Test]
        public void TheGeneratorsLaneAndRingMarginHold()
        {
            foreach (int seed in SeedList())
            {
                var course = Course(4, seed);
                if (course == null) continue;
                foreach (var part in Lay(course, 4, out _))
                {
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(course, part), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed} leg {part.Leg}: lane");
                    Assert.GreaterOrEqual(SpearlineRuins.PartRingClearance(course, part), SpearlineRuins.RingMargin - 0.01f,
                        $"seed {seed} leg {part.Leg}: ring margin");
                }
            }
        }

        [Test]
        public void TheFlownRouteAndTheGauntletStayClear()
        {
            foreach (int seed in SeedList())
            {
                var course = Course(4, seed);
                if (course == null) continue;
                var parts = Lay(course, 4, out var layout);
                var route = SwitchbackRuins.Route(course, layout);
                foreach (var part in parts)
                {
                    Assert.GreaterOrEqual(SwitchbackRuins.PartRouteDistance(part, route), SwitchbackRuins.RouteLane - 0.01f,
                        $"seed {seed} leg {part.Leg}: route lane");
                    Assert.GreaterOrEqual(SwitchbackRuins.PartPrismDistance(part, layout), SwitchbackRuins.PrismClearance - 0.01f,
                        $"seed {seed} leg {part.Leg}: stands in a Blast Run prism");
                }
            }
        }

        [Test]
        public void TheRouteRunsThroughEveryKey()
        {
            var course = Course(4, SwitchbackBlastRun.FixedSeed(4));
            var layout = SwitchbackBlastRun.Layout(course, 4, Vector3.zero, ClearRadius, true);
            var route = SwitchbackRuins.Route(course, layout);
            Assert.AreEqual(course.Count - 1 + layout.Stations.Count, route.Count, "a segment a leg, and one more a key");
            foreach (var station in layout.Stations)
            {
                bool found = false;
                foreach (var segment in route) found |= (segment.b - station.Key).sqrMagnitude < 1e-6f;
                Assert.IsTrue(found, $"gate {station.Gate} stage {station.Stage}: key on the route");
            }
        }

        [Test]
        public void TheLayIsDeterministic()
        {
            var course = Course(4, SwitchbackBlastRun.FixedSeed(4));
            var a = Lay(course, 4, out _);
            var b = Lay(course, 4, out _);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Scale, b[i].Scale);
                Assert.AreEqual(a[i].Leg, b[i].Leg);
            }
        }
    }
}
