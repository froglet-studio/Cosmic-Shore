using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Headlong's stampede ladder (<see cref="HeadlongHedges"/>) on the shipped circuit, 400 seeds a level: the hedges
    /// each level promises stand, they never crowd a ring or another leg, a thorn's window is open and reachable, the
    /// stampede hedges stand at the costing corners' exits, the crystals sit inside the tightest apexes clear of every
    /// hedge, and the prism budget holds. Runs offline in Tools/Build/headlong_course_harness.
    /// </summary>
    public class HeadlongHedgesTests
    {
        const int Seeds = 400;
        const float Inner = 480f, Outer = 1080f;

        static List<RaceGate> Circuit(int intensity, int seed)
        {
            var s = HeadlongCircuitSettings.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        static List<HeadlongHedges.Hedge> Hedges(List<RaceGate> c, int intensity)
        {
            var into = new List<HeadlongHedges.Hedge>();
            HeadlongHedges.Place(c, intensity, into);
            return into;
        }

        [Test]
        public void Every_level_stands_the_hedges_it_promises()
        {
            for (int i = 1; i <= 4; i++)
            {
                var counts = new List<int>();
                var dangers = new List<int>();
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var h = Hedges(Circuit(i, seed), i);
                    counts.Add(h.Count);
                    dangers.Add(h.FindAll(x => x.Danger).Count);
                }
                counts.Sort();
                dangers.Sort();
                int median = counts[Seeds / 2], medianDanger = dangers[Seeds / 2];
                Assert.GreaterOrEqual(median, HeadlongHedges.HedgesPerLap(i) - 1,
                    $"I{i}: the median lap stands {median} hedges, the ladder promises {HeadlongHedges.HedgesPerLap(i)}");
                Assert.AreEqual(HeadlongHedges.DangerPerLap(i), medianDanger, $"I{i}: median danger hedges");
            }
        }

        [Test]
        public void Each_level_asks_one_new_thing()
        {
            // I1 plain only; I2 adds crystals; I3 adds thorns (with windows); I4 trades them for stampede hedges and Time 5.
            Assert.AreEqual(0, HeadlongHedges.DangerPerLap(1));
            Assert.AreEqual(0, HeadlongHedges.CrystalsPerLap(1));
            Assert.Greater(HeadlongHedges.CrystalsPerLap(2), 0);
            Assert.AreEqual(0, HeadlongHedges.DangerPerLap(2));
            Assert.Greater(HeadlongHedges.DangerPerLap(3), 0);
            Assert.AreEqual(0, HeadlongHedges.StartingTimeLevel(3));
            Assert.AreEqual(5, HeadlongHedges.StartingTimeLevel(4), "I4 starts at Time 5, where Momentum holds");
            for (int seed = 1; seed <= 50; seed++)
            {
                foreach (var h in Hedges(Circuit(3, seed), 3))
                    if (h.Danger) Assert.AreEqual(HeadlongHedges.Kind.Thorn, h.Kind);
                foreach (var h in Hedges(Circuit(4, seed), 4))
                    if (h.Danger) Assert.AreEqual(HeadlongHedges.Kind.Stampede, h.Kind);
            }
        }

        [Test]
        public void No_hedge_crowds_a_ring_or_another_leg()
        {
            for (int i = 1; i <= 4; i++)
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Circuit(i, seed);
                int n = c.Count;
                foreach (var h in Hedges(c, i))
                {
                    Assert.GreaterOrEqual(h.Radius, (h.Kind == HeadlongHedges.Kind.Stampede
                        ? HeadlongHedges.MinStampedeRadius : HeadlongHedges.MinHedgeRadius) - 1e-3f);
                    Vector3 a = c[h.Leg].Position, b = c[(h.Leg + 1) % n].Position;
                    float len = Vector3.Distance(a, b);
                    // Out of every mouth's approach: a pilot lining up a ring is never also meeting a hedge.
                    Assert.GreaterOrEqual(h.AlongLeg, 120f, $"I{i} seed {seed}: a hedge in a ring's approach");
                    Assert.LessOrEqual(h.AlongLeg, len - 120f, $"I{i} seed {seed}: a hedge in a ring's approach");
                    for (int other = 0; other < n; other++)
                    {
                        if (other == h.Leg) continue;
                        float d = HeadlongHedges.DistanceToSegment(h.Centre, c[other].Position, c[(other + 1) % n].Position);
                        Assert.GreaterOrEqual(d, h.Radius + 20f,   // half the shipped margin: a hull on that leg never grazes it

                            $"I{i} seed {seed}: hedge on leg {h.Leg} reaches leg {other}");
                    }
                    for (int g = 0; g < n; g++)
                        if (g != h.Leg && g != (h.Leg + 1) % n)   // its own two rings: RingClearance along the leg, above
                        Assert.GreaterOrEqual(Vector3.Distance(h.Centre, c[g].Position),
                            h.Radius + c[g].Radius + 20f, $"I{i} seed {seed}: ring {g}");
                }
            }
        }

        [Test]
        public void The_line_through_the_rings_meets_every_hedge()
        {
            // A hedge you can fly past without touching is not a hedge: the straight line from ring to ring crosses
            // every one inside its disc.
            for (int i = 1; i <= 4; i++)
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Circuit(i, seed);
                foreach (var h in Hedges(c, i))
                {
                    bool crosses = HeadlongHedges.Crosses(h, c[h.Leg].Position, c[(h.Leg + 1) % c.Count].Position,
                                                          out _, out _);
                    Assert.IsTrue(crosses, $"I{i} seed {seed}: the line misses the hedge on leg {h.Leg}");
                }
            }
        }

        [Test]
        public void A_thorn_window_is_open_framed_and_off_the_line()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Circuit(3, seed);
                foreach (var h in Hedges(c, 3))
                {
                    if (h.Kind != HeadlongHedges.Kind.Thorn) continue;
                    Assert.AreEqual(HeadlongHedges.WindowRadius, h.WindowRadius);
                    float off = Vector3.Distance(h.Window, h.Centre);
                    Assert.Greater(off, h.WindowRadius, "the window must be off the line, so taking it is a jink");
                    Assert.LessOrEqual(off + h.WindowRadius, h.Radius - HeadlongHedges.Pitch + 1e-2f, "inside the hedge");
                    Assert.AreEqual(0f, Vector3.Dot(h.Window - h.Centre, h.Normal), 1e-2f, "in the hedge's plane");
                    var parts = new List<HeadlongHedges.Part>();
                    HeadlongHedges.Build(h, parts);
                    foreach (var p in parts)
                    {
                        float w = Vector3.Distance(p.Position, h.Window);
                        Assert.GreaterOrEqual(w, h.WindowRadius, "no slab in the window");
                        if (w < h.WindowRadius + HeadlongHedges.Pitch) Assert.AreEqual(PrismKind.Plain, p.Kind, "plain frame");
                    }
                }
            }
        }

        [Test]
        public void Stampede_hedges_stand_just_past_the_costing_corners()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Circuit(4, seed);
                var tight = HeadlongHedges.TightestCorners(c);
                var stampedes = Hedges(c, 4).FindAll(h => h.Kind == HeadlongHedges.Kind.Stampede);
                foreach (var h in stampedes)
                {
                    int rank = tight.IndexOf(h.Leg);
                    Assert.Less(rank, HeadlongHedges.DangerPerLap(4), $"seed {seed}: a stampede hedge off a costing corner");
                    int n = c.Count;
                    float r = RaceCourseGeometry.CornerRadius(c[(h.Leg - 1 + n) % n].Position, c[h.Leg].Position, c[(h.Leg + 1) % n].Position);
                    float len = Vector3.Distance(c[h.Leg].Position, c[(h.Leg + 1) % n].Position);
                    float want = Mathf.Clamp(HeadlongHedges.StampedeAlongExit(r), HeadlongHedges.RingClearance, len - HeadlongHedges.RingClearance);
                    Assert.AreEqual(want, h.AlongLeg, 1e-2f, $"seed {seed}: where a perfect exit reaches Momentum");
                    // A perfect exit reaches Momentum before it (a slow one does not): the hedge asks for the corner.
                    float exit = HeadlongCircuitSettings.FastestSpeedForCorner(r);
                    float atHedge = Mathf.Min(HeadlongCircuitSettings.RhinoTopSpeed,
                        Mathf.Sqrt(exit * exit + 2f * HeadlongHedges.StampedeAcceleration * h.AlongLeg));
                    if (h.AlongLeg > HeadlongHedges.RingClearance + 1e-2f && h.AlongLeg < len - HeadlongHedges.RingClearance - 1e-2f)
                        Assert.GreaterOrEqual(atHedge, HeadlongHedges.MomentumSpeed - 1e-2f, $"seed {seed}");
                }
            }
        }

        [Test]
        public void Crystals_sit_inside_the_tightest_apexes_clear_of_every_hedge()
        {
            for (int i = 1; i <= 4; i++)
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Circuit(i, seed);
                var h = Hedges(c, i);
                var crystals = new List<Vector3>();
                HeadlongHedges.PlaceCrystals(c, i, h, crystals);
                Assert.AreEqual(HeadlongHedges.CrystalsPerLap(i), crystals.Count, $"I{i}");
                foreach (var x in crystals)
                {
                    foreach (var hedge in h)
                    {
                        float s = Vector3.Dot(x - hedge.Centre, hedge.Normal);
                        bool inDisc = (x - hedge.Centre - hedge.Normal * s).magnitude < hedge.Radius;
                        if (inDisc) Assert.GreaterOrEqual(Mathf.Abs(s), HeadlongHedges.CrystalHedgeClearance - 1e-2f,
                            $"I{i} seed {seed}: a crystal on a hedge's face");
                    }
                    foreach (var g in c)
                        Assert.Greater(Vector3.Distance(x, g.Position), g.Radius, $"I{i} seed {seed}: a crystal in a mouth");
                    Assert.Less(x.magnitude, Outer, "inside the cell");
                }
            }
        }

        [Test]
        public void The_prism_budget_holds()
        {
            // Under Switchback's I2 total (TotalCap 2400) and far under its gauntlet (5100); one hedge, a thorn at its
            // widest, under Breakwater's worst 200 u neighbourhood (568).
            for (int i = 1; i <= 4; i++)
            {
                int worst = 0, worstHedge = 0;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    int total = 0;
                    foreach (var h in Hedges(Circuit(i, seed), i))
                    {
                        var parts = new List<HeadlongHedges.Part>();
                        HeadlongHedges.Build(h, parts);
                        total += parts.Count;
                        worstHedge = Mathf.Max(worstHedge, parts.Count);
                    }
                    worst = Mathf.Max(worst, total);
                }
                Assert.LessOrEqual(worst, 2200, $"I{i}: {worst} prisms a lap");
                Assert.LessOrEqual(worstHedge, 380, $"I{i}: {worstHedge} prisms in one hedge");
            }
        }

        [Test]
        public void The_stampede_is_a_pure_function_of_the_course()
        {
            for (int i = 1; i <= 4; i++)
            {
                var a = Hedges(Circuit(i, 7), i);
                var b = Hedges(Circuit(i, 7), i);
                Assert.AreEqual(a.Count, b.Count);
                for (int k = 0; k < a.Count; k++)
                {
                    Assert.AreEqual(a[k].Centre, b[k].Centre);
                    Assert.AreEqual(a[k].Kind, b[k].Kind);
                    Assert.AreEqual(a[k].Window, b[k].Window);
                }
            }
        }
    }
}
