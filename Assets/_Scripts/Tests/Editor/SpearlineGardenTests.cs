using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Spearline's race garden (SPEARLINE.md §2b), one test per placement rule: the rungs trade help for cost (I1 helps
    /// only, I4 costs most), a fuel hedge stands outside the hull's path and inside a stroke, only on legs into unsealed
    /// rings; a pillar stands across the line only on legs into sealed rings and a short sidestep clears it; nothing
    /// stands at a ring, in another leg's lane or on a ruin; the lay is a pure function of the lap. The same file runs
    /// headless through <c>Tools/Build/spearline_harness</c>.
    /// </summary>
    public class SpearlineGardenTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 42, 101, 102, 103 };

        static List<RaceGate> Lap(int intensity, int seed)
        {
            var settings = HeadlongCircuitSettings.ForIntensity(intensity);
            settings.RingRadius = SpearlineSeals.ForIntensity(intensity).MouthRadius;
            settings.InnerRadius = 480f;
            settings.OuterRadius = 1080f;
            settings.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, settings);
        }

        static List<SpearlineGardenPart> Garden(int intensity, int seed, out List<RaceGate> lap, out bool[] sealedRings)
        {
            lap = Lap(intensity, seed);
            sealedRings = SpearlineSeals.SealedRings(lap, SpearlineSeals.ForIntensity(intensity));
            var parts = new List<SpearlineGardenPart>();
            SpearlineGarden.Lay(lap, sealedRings, SpearlineGarden.ForIntensity(intensity), parts);
            return parts;
        }

        static int Pillars(List<SpearlineGardenPart> parts)
        {
            var legs = new HashSet<int>();
            foreach (var part in parts) if (part.Pillar) legs.Add(part.Leg);
            return legs.Count;
        }

        static bool Costs(in SpearlineGardenPart part) => part.Pillar || part.Kind != PrismKind.Plain;

        [Test]
        public void I1_only_helps_and_I4_costs_most()
        {
            foreach (int seed in Seeds)
            {
                foreach (var part in Garden(1, seed, out _, out _))
                    Assert.IsFalse(Costs(part), $"seed {seed}: I1 has a cost piece");
                var i4 = Garden(4, seed, out _, out _);
                Assert.Greater(Pillars(i4), 0, $"seed {seed}: I4 has no pillar");
                Assert.IsTrue(i4.Exists(p => p.Kind == PrismKind.Danger), $"seed {seed}: I4 has no danger");
            }
        }

        [Test]
        public void Help_falls_and_cost_rises_rung_by_rung()
        {
            // Help: plain hedge prisms (free fuel). Cost: pillars a lap, and the share of a hedge that is armoured or
            // danger (fuel that costs a stroke more, or slows the hull).
            var help = new float[5];
            var pillars = new float[5];
            var hard = new float[5];
            var hedge = new float[5];
            foreach (int seed in Seeds)
            {
                for (int i = 1; i <= 4; i++)
                {
                    var parts = Garden(i, seed, out _, out _);
                    pillars[i] += Pillars(parts);
                    foreach (var part in parts)
                    {
                        if (part.Pillar) continue;
                        hedge[i]++;
                        if (part.Kind == PrismKind.Plain) help[i]++;
                        else hard[i]++;
                    }
                }
            }
            for (int i = 2; i <= 4; i++)
            {
                Assert.Less(help[i], help[i - 1], $"fuel does not fall from I{i - 1} to I{i}");
                Assert.Greater(pillars[i], pillars[i - 1], $"pillars do not rise from I{i - 1} to I{i}");
                Assert.GreaterOrEqual(hard[i] / hedge[i], hard[i - 1] / hedge[i - 1], $"hedges soften from I{i - 1} to I{i}");
            }
            Assert.Greater(hard[4] / hedge[4], hard[3] / hedge[3], "I4's hedges are no harder than I3's");
        }

        [Test]
        public void A_hedge_stands_outside_the_hulls_path_and_inside_a_stroke()
        {
            for (int i = 1; i <= 4; i++)
            {
                foreach (int seed in Seeds)
                {
                    var parts = Garden(i, seed, out var lap, out _);
                    foreach (var part in parts)
                    {
                        if (part.Pillar) continue;
                        float d = SpearlineRuins.LegDistance(lap, part.Leg, part.Position);
                        Assert.GreaterOrEqual(d, SpearlineGarden.HedgeInner - 1f, $"I{i} seed {seed}: a hedge stands in the hull's path");
                        Assert.LessOrEqual(d, SpearlineGarden.HedgeOuter + 2.5f, $"I{i} seed {seed}: a hedge is out of a stroke's reach");
                    }
                }
            }
            // The hull and an Easy AI's lane error pass clear of the nearest hedge prism's edge.
            Assert.Greater(SpearlineGarden.HedgeInner - 0.5f * SpearlineGarden.HedgeScale.x,
                           SpearlineSeals.HullReach + 10f - 4f);
        }

        [Test]
        public void Hedges_only_on_legs_into_unsealed_rings_and_pillars_only_into_sealed_ones()
        {
            for (int i = 1; i <= 4; i++)
            {
                foreach (int seed in Seeds)
                {
                    var parts = Garden(i, seed, out var lap, out var sealedRings);
                    foreach (var part in parts)
                    {
                        Assert.Less(part.Leg, lap.Count - 1, "the closing leg carries nothing");
                        Assert.AreEqual(part.Pillar, sealedRings[part.Leg + 1], $"I{i} seed {seed} leg {part.Leg}");
                    }
                    Assert.LessOrEqual(Pillars(parts), SpearlineGarden.ForIntensity(i).Pillars);
                }
            }
        }

        [Test]
        public void A_pillar_stands_across_the_line_and_a_short_sidestep_clears_it()
        {
            for (int i = 2; i <= 4; i++)
            {
                foreach (int seed in Seeds)
                {
                    var parts = Garden(i, seed, out var lap, out _);
                    var nearest = new Dictionary<int, float>();
                    foreach (var part in parts)
                    {
                        if (!part.Pillar) continue;
                        float d = SpearlineRuins.LegDistance(lap, part.Leg, part.Position);
                        nearest[part.Leg] = nearest.TryGetValue(part.Leg, out float was) ? Mathf.Min(was, d) : d;
                    }
                    foreach (var pair in nearest)
                        Assert.LessOrEqual(pair.Value, SpearlineGarden.PillarScale.x,
                            $"I{i} seed {seed}: the pillar on leg {pair.Key} does not cross the line");
                }
            }
            // Every pillar prism lies within its radius of the pillar's axis, so a sidestep of radius + a prism clears it,
            // well inside the lane the ruins keep open.
            Assert.Less(SpearlineGarden.PillarRadius + SpearlineGarden.PillarScale.x, SpearlineRuins.LaneRadius);
        }

        [Test]
        public void Nothing_stands_at_a_ring_or_in_another_legs_lane()
        {
            for (int i = 1; i <= 4; i++)
            {
                foreach (int seed in Seeds)
                {
                    var parts = Garden(i, seed, out var lap, out _);
                    foreach (var part in parts)
                    {
                        Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lap, part.Position), SpearlineGarden.RingMargin - 0.01f,
                            $"I{i} seed {seed}: a garden prism stands at a ring");
                        for (int other = 0; other < lap.Count; other++)
                            if (other != part.Leg)
                                Assert.GreaterOrEqual(SpearlineRuins.LegDistance(lap, other, part.Position),
                                                      SpearlineGarden.OtherLaneClear - 0.01f, $"I{i} seed {seed}: in leg {other}'s lane");
                    }
                }
            }
        }

        [Test]
        public void No_garden_prism_stands_on_a_ruin()
        {
            foreach (int seed in Seeds)
            {
                var garden = Garden(4, seed, out var lap, out _);
                var ruins = new List<SpearlineRuinPart>();
                SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(4), ruins, null);
                foreach (var g in garden)
                    foreach (var r in ruins)
                        Assert.Greater((g.Position - r.Position).magnitude, 8f, $"seed {seed}: a garden prism stands on a ruin");
            }
        }

        [Test]
        public void Every_rung_has_fuel_and_the_lay_is_a_pure_function_of_the_lap()
        {
            for (int i = 1; i <= 4; i++)
            {
                foreach (int seed in Seeds)
                {
                    var first = Garden(i, seed, out _, out _);
                    var second = Garden(i, seed, out _, out _);
                    Assert.IsTrue(first.Exists(p => !p.Pillar), $"I{i} seed {seed}: no fuel hedge");
                    Assert.AreEqual(first.Count, second.Count);
                    for (int k = 0; k < first.Count; k++)
                    {
                        Assert.AreEqual(first[k].Position, second[k].Position);
                        Assert.AreEqual(first[k].Kind, second[k].Kind);
                    }
                    Assert.LessOrEqual(first.Count, 900, $"I{i} seed {seed}: over the prism budget");
                }
            }
        }
    }
}
