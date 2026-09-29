using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Redline circuit contract. The mode's proposition is that a corner is a question -
    /// how much Soar is it worth? - and that is only true if the corners are actually cut
    /// around the Manta's full-boost turn radius, with the intensity ladder DEMANDING the
    /// corners it claims rather than merely permitting them (HEADLONG.md §1: a floor is a
    /// permission, not a demand). Asserted across a 400-seed sweep of every intensity, because
    /// a generated course is exactly the kind of thing that is fine on the seed you looked at.
    ///
    /// <para>The solver is Headlong's and is deliberately unable to fail, so the liveness half
    /// (closed ring, shell, mouth separation, pole placement) is re-asserted here against the
    /// Manta's settings - a change to the shared solver has to keep BOTH modes' contracts - and
    /// the ladder half is the Manta's own.</para>
    /// </summary>
    public class RedlineCourseTests
    {
        // The mode's cell, matching RedlineController's derived shell: Nucleus.prefab at scale
        // 400 is 391.9u (x1.22 -> 478) and CapsuleMembrane authors 1200 (x0.9 -> 1080).
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        static HeadlongCircuitSettings Settings(int intensity)
        {
            var s = RedlineCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return s;
        }

        static List<RaceGate> Circuit(int intensity, int seed) =>
            HeadlongCircuit.Generate(seed, Settings(intensity));

        static float CornerRadius(List<RaceGate> c, int i)
        {
            int n = c.Count;
            return RaceCourseGeometry.CornerRadius(c[(i - 1 + n) % n].Position, c[i].Position,
                                                   c[(i + 1) % n].Position);
        }

        /// <summary>Corner radii of one lap, tightest first.</summary>
        static float[] SortedCorners(List<RaceGate> c)
        {
            var radii = new float[c.Count];
            for (int i = 0; i < c.Count; i++) radii[i] = CornerRadius(c, i);
            System.Array.Sort(radii);
            return radii;
        }

        static float Median(List<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[sorted.Count / 2];
        }

        // ── The vessel constants the whole ladder is derived from ────────────

        [Test]
        public void Full_boost_radius_matches_the_shipped_Manta()
        {
            Assert.AreEqual(720f, RedlineCourse.MantaTopSpeed, 0.1f,
                "180 x 4 + 0. If DefaultThrottleScaler or boostMultiplier moved on Manta.prefab, so did every corner.");

            // 720 u/s over (720 x 0.2 + 30) = 174 deg/s: a 237u circle. The Manta's absolute
            // minimum at full boost, with no trigger yaw left over (both triggers are flat).
            Assert.AreEqual(237.1f, RedlineCourse.FullBoostRadius, 0.5f,
                "Manta full-boost radius changed - re-derive the circuit ladder.");

            // Releasing one trigger entirely buys 60 deg/s of yaw AND drops to cruise: 82u.
            Assert.AreEqual(81.9f, RedlineCourse.CornerRadiusAtBoost(0f), 0.5f,
                "Manta released-trigger radius changed - re-derive the safety floors.");
        }

        [Test]
        public void Corner_radius_is_monotone_in_boost()
        {
            // The curve is inverted by bisection (FastestBoostForCorner), which is only valid
            // if more boost always means a wider circle. Both terms push the same way - speed
            // rises and trigger yaw falls - but assert it rather than trust it.
            float previous = 0f;
            for (int step = 0; step <= 20; step++)
            {
                float radius = RedlineCourse.CornerRadiusAtBoost(step / 20f);
                Assert.Greater(radius, previous, $"radius must grow with boost (step {step})");
                previous = radius;
            }

            Assert.AreEqual(1f, RedlineCourse.FastestBoostForCorner(RedlineCourse.FullBoostRadius + 1f), 1e-4f);
            Assert.AreEqual(0f, RedlineCourse.FastestBoostForCorner(RedlineCourse.CornerRadiusAtBoost(0f) - 1f), 1e-4f);
            Assert.AreEqual(0.5f, RedlineCourse.FastestBoostForCorner(RedlineCourse.CornerRadiusAtBoost(0.5f)), 1e-3f,
                "the inverse must recover the boost the forward curve was evaluated at");
        }

        [Test]
        public void Turn_radius_converges_rather_than_diverging()
        {
            // With a positive RotationThrottleScaler the minimum turn radius approaches
            // 180/(pi*r) = 286u instead of growing without bound - which is what lets a course be
            // cut against a hull whose top speed an element level can still raise.
            float r = RedlineCourse.MantaRotationThrottleScaler;
            float asymptote = 180f / (Mathf.PI * r);
            float atTop = RaceCourseGeometry.MinTurnRadius(RedlineCourse.MantaTopSpeed, r, RedlineCourse.MantaTurnScaler);
            float atTimeTen = RaceCourseGeometry.MinTurnRadius(RedlineCourse.MantaTopSpeed * 1.3f, r, RedlineCourse.MantaTurnScaler);

            Assert.Less(atTop, asymptote);
            Assert.Less(atTimeTen, asymptote);
            Assert.Less(atTimeTen - atTop, 15f,
                "a Time-10 Manta should need under 15u more radius than a resting one - that is the convergence");
        }

        // ── The circuit is a circuit ─────────────────────────────────────────

        [Test]
        public void Every_intensity_yields_a_closed_ring_of_gates()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    Assert.IsNotNull(c, $"i{intensity} seed {seed}: generator returned null");
                    Assert.AreEqual(s.GateCount, c.Count, $"i{intensity} seed {seed}: gate count");
                    foreach (var g in c)
                    {
                        Assert.AreEqual(1f, g.Axis.magnitude, 1e-3f, "gate axis must be unit");
                        Assert.AreEqual(s.RingRadius, g.Radius, 1e-3f, "mouth radius");
                    }
                }
            }
        }

        [Test]
        public void Gate_zero_sits_on_the_spawn_pole()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                    Assert.Less(RaceCourseGeometry.Angle(Circuit(intensity, seed)[0].Position, Vector3.up),
                        0.1f, $"i{intensity} seed {seed}: gate 0 is off the spawn pole");
        }

        [Test]
        public void Every_gate_stays_inside_the_cell_shell()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                    foreach (var g in Circuit(intensity, seed))
                    {
                        float r = g.Position.magnitude;
                        Assert.GreaterOrEqual(r, Inner - 0.01f, $"i{intensity} seed {seed}: inside the nucleus");
                        Assert.LessOrEqual(r, Outer + 0.01f, $"i{intensity} seed {seed}: outside the membrane");
                    }
        }

        [Test]
        public void No_two_mouths_are_within_a_ring_diameter()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                float min = s.RingRadius * 2f;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    for (int i = 0; i < c.Count; i++)
                        for (int j = i + 1; j < c.Count; j++)
                            Assert.GreaterOrEqual((c[i].Position - c[j].Position).magnitude, min - 0.01f,
                                $"i{intensity} seed {seed}: gates {i} and {j} overlap");
                }
            }
        }

        [Test]
        public void Every_corner_fits_its_absolute_safety_floor()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                Assert.Greater(s.CornerFloorRadius, 0f, "Redline states its floor in absolute units");
                Assert.GreaterOrEqual(s.CornerFloorRadius, RedlineCourse.CornerRadiusAtBoost(0f),
                    $"i{intensity}: the floor must never ask for a corner tighter than a Manta with one trigger released can fly");
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    for (int i = 0; i < c.Count; i++)
                        Assert.GreaterOrEqual(CornerRadius(c, i), s.CornerFloorRadius - 0.01f,
                            $"i{intensity} seed {seed}: corner {i} is sharper than the floor");
                }
            }
        }

        [Test]
        public void Presentation_cap_covers_half_the_hardest_turn()
        {
            // A gate faces its corner's bisector, so a corner's half-turn is spent from the cap
            // before any jitter is; a cap under the half-turn zeroes the jitter budget exactly at
            // the corners that most need to face the pilot. Measured, not authored.
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                float worstHalfTurn = 0f;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    int n = c.Count;
                    for (int i = 0; i < n; i++)
                        worstHalfTurn = Mathf.Max(worstHalfTurn, 0.5f * RaceCourseGeometry.Angle(
                            c[i].Position - c[(i - 1 + n) % n].Position, c[(i + 1) % n].Position - c[i].Position));
                }
                Assert.Greater(s.MaxPresentDegrees, worstHalfTurn,
                    $"i{intensity}: MaxPresentDegrees {s.MaxPresentDegrees} does not cover the measured worst half-turn {worstHalfTurn:F1}");
            }
        }

        [Test]
        public void Course_is_deterministic_per_seed()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var a = Circuit(intensity, 12345);
                var b = Circuit(intensity, 12345);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(0f, (a[i].Position - b[i].Position).magnitude, 1e-5f, "positions");
                    Assert.AreEqual(0f, (a[i].Axis - b[i].Axis).magnitude, 1e-5f, "axes");
                }
            }
        }

        // ── The ladder, which IS the mode ────────────────────────────────────

        /// <summary>
        /// How many corners of a median lap cost Soar - that is, sit inside the full-boost
        /// radius. 0 / 1 / 2 at levels 1-3, and at least 2 at level 4 (its third corner is a
        /// knife-edge, asserted separately).
        /// </summary>
        [Test]
        public void Every_intensity_demands_the_corners_it_claims()
        {
            float full = RedlineCourse.FullBoostRadius;
            int[] expected = { 0, 0, 1, 2, 2 };   // index = intensity; level 4 is ">= 2"
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var counts = new List<int>();
                for (int seed = 1; seed <= Seeds; seed++)
                    counts.Add(SortedCorners(Circuit(intensity, seed)).Count(r => r < full));

                var sorted = counts.OrderBy(c => c).ToList();
                int median = sorted[sorted.Count / 2];
                if (intensity < 4)
                    Assert.AreEqual(expected[intensity], median,
                        $"i{intensity}: a median lap must have exactly {expected[intensity]} corner(s) that cost Soar");
                else
                    Assert.GreaterOrEqual(median, 2, "i4: a median lap must have at least two corners that cost Soar");
            }
        }

        [Test]
        public void Level_four_has_a_knife_edge_third_corner()
        {
            // The geometry cannot deliver three corners clearly inside 237u with eight gates -
            // measured over base radius, profile and spread, every variant left the third corner
            // between 230 and 250u - so the design states what it can deliver: a third corner AT
            // the full-boost radius, holdable flat out only by a pilot who is exact.
            var thirds = new List<float>();
            for (int seed = 1; seed <= Seeds; seed++)
                thirds.Add(SortedCorners(Circuit(4, seed))[2]);
            Assert.LessOrEqual(Median(thirds), RedlineCourse.FullBoostRadius * 1.03f,
                "i4's third-tightest corner must sit on the full-boost radius (within 3%)");
        }

        [Test]
        public void The_hardest_corner_of_each_level_costs_more_speed_than_the_last()
        {
            // Speed, not radius: the ladder is stated in what a corner COSTS the pilot, via the
            // Manta's own curve. Each level's hardest corner must give up at least 5 more points
            // of top speed than the level below.
            var costs = new float[5];
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var tightest = new List<float>();
                for (int seed = 1; seed <= Seeds; seed++)
                    tightest.Add(SortedCorners(Circuit(intensity, seed))[0]);
                costs[intensity] = RedlineCourse.FastestSpeedForCorner(Median(tightest)) / RedlineCourse.MantaTopSpeed;
            }

            Assert.AreEqual(1f, costs[1], 1e-3f, "i1's hardest corner must hold full boost");
            for (int intensity = 2; intensity <= 4; intensity++)
                Assert.LessOrEqual(costs[intensity], costs[intensity - 1] - 0.05f,
                    $"i{intensity}'s hardest corner must cost at least 5 more points of top speed than i{intensity - 1}'s");
            Assert.Less(costs[4], 0.4f, "i4 must produce a real hairpin - under 40% of top speed");
        }

        [Test]
        public void Legs_leave_the_Manta_runway()
        {
            // The hull the mode is named for wants straights. At level 1 no corner costs Soar, so
            // the whole lap IS the runway and leg length is beside the point (a near-regular
            // octagon at 820 has 628u legs); at every level where a corner takes boost away, a
            // median lap's LONGEST leg must be at least a second flat out to win it back on, and
            // the shortest may never fall under half a second anywhere.
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var longest = new List<float>();
                var shortest = new List<float>();
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    float lo = float.MaxValue, hi = 0f;
                    for (int i = 0; i < c.Count; i++)
                    {
                        float leg = (c[(i + 1) % c.Count].Position - c[i].Position).magnitude;
                        lo = Mathf.Min(lo, leg);
                        hi = Mathf.Max(hi, leg);
                    }
                    longest.Add(hi);
                    shortest.Add(lo);
                }
                if (intensity >= 2)
                    Assert.GreaterOrEqual(Median(longest), RedlineCourse.MantaTopSpeed,
                        $"i{intensity}: the longest leg of a median lap must be a second at top speed");
                Assert.GreaterOrEqual(Median(shortest), RedlineCourse.MantaTopSpeed * 0.5f,
                    $"i{intensity}: the shortest leg of a median lap must be half a second at top speed");
            }
        }
    }
}
