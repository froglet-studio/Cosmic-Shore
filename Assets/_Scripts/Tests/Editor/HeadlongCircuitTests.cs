using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Headlong circuit contract. The mode's whole proposition is that a corner is a
    /// DECISION - hold the line at 910 u/s and keep the ramp boost, or brake and pay 6.1 s to
    /// wind it back up - and that proposition is only true if the corners are actually cut to
    /// the Rhino's flat-out turn radius. That is asserted here across a 400-seed sweep of every
    /// shipped intensity rather than eyeballed in the editor, because a generated course is
    /// exactly the kind of thing that is fine on the seed you looked at.
    ///
    /// <para>The generator is deliberately unable to fail (it relaxes toward a regular octagon,
    /// which is always legal), so these tests are not asking "did it produce something" - they
    /// are asking whether what it produced still MEANS anything. A relaxation that silently
    /// collapsed every course to the base octagon would pass a liveness check and destroy the
    /// intensity ladder, so <see cref="Ladder_is_binding_and_ordered"/> asserts the opposite
    /// direction too.</para>
    /// </summary>
    public class HeadlongCircuitTests
    {
        // The mode's cell, matching HeadlongController's derived shell: Nucleus.prefab at
        // scale 400 is 391.9u (x1.22 -> 478) and CapsuleMembrane authors 1200 (x0.9 -> 1080).
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        static HeadlongCircuitSettings Settings(int intensity)
        {
            var s = HeadlongCircuitSettings.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return s;
        }

        static List<RaceGate> Circuit(int intensity, int seed) =>
            HeadlongCircuit.Generate(seed, Settings(intensity));

        // ── The vessel constants the whole ladder is derived from ────────────

        [Test]
        public void Flat_out_radius_matches_the_shipped_Rhino()
        {
            // 910 u/s over (910 x 0.4 + 90) = 454 deg/s is a 114.8u circle; a pilot holding the
            // ramp boost may spend only 0.28 of the stick, so the tightest circle they can fly
            // without dropping it is 114.8 / 0.28.
            Assert.AreEqual(114.8f, RaceCourseGeometry.MinTurnRadius(
                HeadlongCircuitSettings.RhinoTopSpeed,
                HeadlongCircuitSettings.RhinoRotationThrottleScaler,
                HeadlongCircuitSettings.RhinoTurnScaler), 0.5f,
                "Rhino min turn radius at top speed changed - re-derive the circuit ladder.");

            Assert.AreEqual(410.2f, HeadlongCircuitSettings.FlatOutRadius, 1f,
                "The flat-out radius IS the mode. If this moved, every corner moved with it.");
        }

        [Test]
        public void Turn_radius_converges_rather_than_diverging()
        {
            // The property that lets the Rhino be absurdly fast at all: with a positive
            // RotationThrottleScaler the minimum turn radius approaches 180/(pi*r) instead of
            // growing without bound. At r = 0 it diverges and no course can be sized.
            float r = HeadlongCircuitSettings.RhinoRotationThrottleScaler;
            float asymptote = 180f / (Mathf.PI * r);

            float atTop = RaceCourseGeometry.MinTurnRadius(910f, r, 90f);
            float atTenTimes = RaceCourseGeometry.MinTurnRadius(9100f, r, 90f);

            Assert.Less(atTop, asymptote, "radius must stay under its own asymptote");
            Assert.Less(atTenTimes, asymptote, "...at any speed");
            Assert.Less(atTenTimes - atTop, 30f,
                "a 10x speed increase should cost under 30u of radius - that is the convergence");
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
            // Fairness, not layout: pilots spawn on an equatorial ring, so only a point on that
            // ring's axis is equidistant from all of them. Anywhere else and whoever spawned
            // nearest gate 0 starts the lap ahead.
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
            // So one pass can never thread two gates, and the wrong gate can never be the nearer.
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

        // ── The corner budget, which IS the mode ─────────────────────────────

        [Test]
        public void Every_corner_fits_its_intensity_floor()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                float floor = s.CornerRadiusFactor * HeadlongCircuitSettings.FlatOutRadius;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    int n = c.Count;
                    for (int i = 0; i < n; i++)
                        Assert.GreaterOrEqual(
                            RaceCourseGeometry.CornerRadius(c[(i - 1 + n) % n].Position, c[i].Position,
                                                           c[(i + 1) % n].Position),
                            floor - 0.01f,
                            $"i{intensity} seed {seed}: corner {i} is sharper than the floor");
                }
            }
        }

        [Test]
        public void Ladder_is_binding_and_ordered()
        {
            // Two failures this catches that a floor-only check cannot:
            //   1. a relaxation that collapses every course to the base octagon (all four levels
            //      would then read the same and pass the floor trivially), and
            //   2. a ladder authored the wrong way round.
            var tightest = new float[5];
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                float worst = float.MaxValue;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    int n = c.Count;
                    for (int i = 0; i < n; i++)
                        worst = Mathf.Min(worst, RaceCourseGeometry.CornerRadius(
                            c[(i - 1 + n) % n].Position, c[i].Position, c[(i + 1) % n].Position));
                }
                tightest[intensity] = worst / HeadlongCircuitSettings.FlatOutRadius;
            }

            for (int intensity = 1; intensity <= 4; intensity++)
                Assert.Less(tightest[intensity],
                    HeadlongCircuitSettings.ForIntensity(intensity).CornerRadiusFactor * 1.05f,
                    $"i{intensity}: the floor is never reached, so the relaxation is not binding " +
                    "and this intensity is not the course it claims to be");

            for (int intensity = 2; intensity <= 4; intensity++)
                Assert.Less(tightest[intensity], tightest[intensity - 1],
                    $"i{intensity} must be tighter than i{intensity - 1}");

            // Intensity 1 and 2 are takeable flat out; 3 and 4 are not, which is the whole ladder.
            Assert.GreaterOrEqual(tightest[1], 1f, "i1 must be takeable without lifting");
            Assert.GreaterOrEqual(tightest[2], 1f, "i2 must be takeable without lifting");
            Assert.Less(tightest[4], 1f, "i4 must force a lift somewhere");
        }

        [Test]
        public void No_gate_stands_edge_on_to_the_line_you_arrive_on()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    int n = c.Count;
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 arrive = (c[i].Position - c[(i - 1 + n) % n].Position).normalized;
                        Vector3 leave = (c[(i + 1) % n].Position - c[i].Position).normalized;
                        Assert.LessOrEqual(RaceCourseGeometry.Angle(arrive, c[i].Axis),
                            s.MaxPresentDegrees + 0.01f, $"i{intensity} seed {seed}: gate {i} arrival");
                        Assert.LessOrEqual(RaceCourseGeometry.Angle(leave, c[i].Axis),
                            s.MaxPresentDegrees + 0.01f, $"i{intensity} seed {seed}: gate {i} departure");
                    }
                }
            }
        }

        [Test]
        public void Generation_is_deterministic()
        {
            // The server broadcasts the geometry rather than the seed (HeadlongController), so
            // this buys reproducibility and testability, not the network contract.
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var a = Circuit(intensity, 12345);
                var b = Circuit(intensity, 12345);
                for (int i = 0; i < a.Count; i++)
                    Assert.AreEqual(0f, (a[i].Position - b[i].Position).magnitude, 1e-4f,
                        $"i{intensity}: same seed produced a different circuit");
            }
        }
    }
}
