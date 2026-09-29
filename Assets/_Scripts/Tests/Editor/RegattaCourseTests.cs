#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Regatta arena contract: the rails are the racing line, so every lane must cross
    /// every ring INSIDE its mouth, every lane must be a closed loop of evenly spaced prisms,
    /// and the three lanes must be the same length to within a few percent - a domain whose
    /// rail is shorter has a shorter race. Asserted across a seed sweep of every intensity
    /// because a generated course is exactly the kind of thing that is fine on the seed you
    /// looked at, AND on the four shipped seeds exactly, because those are what a match lays.
    /// </summary>
    public class RegattaCourseTests
    {
        const int Seeds = 60;
        const float PrismHalfWidth = 3f;   // (6,6,8) prism, laid with +z down the rail

        static IEnumerable<int> Intensities => Enumerable.Range(1, 4);

        static List<RaceGate> Gates(int intensity, int seed) =>
            HeadlongCircuit.Generate(seed, RegattaCourse.ForIntensity(intensity));

        static RegattaRailLayout Rails(List<RaceGate> gates) =>
            RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails);

        /// <summary>The seeds a MATCH actually lays: the default seed per intensity.</summary>
        static IEnumerable<(int intensity, int seed)> Shipped()
        {
            foreach (int i in Intensities)
                yield return (i, RegattaCourse.SeedForIntensity(RegattaCourse.DefaultSeed, i));
        }

        static IEnumerable<(int intensity, int seed)> Sweep()
        {
            foreach (var s in Shipped()) yield return s;
            foreach (int i in Intensities)
                for (int k = 1; k <= Seeds; k++)
                    yield return (i, RegattaCourse.SeedForIntensity(RegattaCourse.DefaultSeed + k * 104729, i));
        }

        [Test]
        public void EveryCircuit_HasRingsPerLapGates_InsideTheShell()
        {
            foreach (var (intensity, seed) in Sweep())
            {
                var gates = Gates(intensity, seed);
                Assert.AreEqual(RegattaCourse.RingsPerLap, gates.Count, $"i{intensity} seed {seed}: ring count");
                foreach (var g in gates)
                {
                    float r = g.Position.magnitude;
                    Assert.GreaterOrEqual(r, RegattaCourse.InnerRadius - 1f, $"i{intensity} seed {seed}: gate inside the nucleus shell");
                    Assert.LessOrEqual(r, RegattaCourse.OuterRadius + 1f, $"i{intensity} seed {seed}: gate outside the membrane shell");
                }
            }
        }

        [Test]
        public void GateZero_SitsOnTheSpawnPole()
        {
            foreach (var (intensity, seed) in Sweep())
            {
                var gates = Gates(intensity, seed);
                float angle = Vector3.Angle(gates[0].Position, Vector3.up);
                Assert.Less(angle, 0.5f, $"i{intensity} seed {seed}: gate 0 is {angle:F2} deg off the pole");
            }
        }

        [Test]
        public void EveryLane_CrossesEveryRing_InsideTheMouth()
        {
            var rs = RegattaCourse.DefaultRails;
            foreach (var (intensity, seed) in Sweep())
            {
                var gates = Gates(intensity, seed);
                var rails = Rails(gates);
                Assert.AreEqual(RegattaCourse.LaneCount, rails.LanePositions.Count);

                for (int gi = 0; gi < gates.Count; gi++)
                {
                    var g = gates[gi];
                    for (int lane = 0; lane < rails.LanePositions.Count; lane++)
                    {
                        // The prism nearest the ring CENTRE on this lane. (Nearest to the ring
                        // PLANE is wrong: a plane cuts a closed loop at least twice, and the far
                        // crossing is on the other side of the arena.)
                        float bestDist = float.MaxValue;
                        float planeAtBest = 0f, lateralAtBest = 0f;
                        foreach (var p in rails.LanePositions[lane])
                        {
                            Vector3 d = p - g.Position;
                            float dist = d.magnitude;
                            if (dist >= bestDist) continue;
                            bestDist = dist;
                            float along = Vector3.Dot(d, g.Axis);
                            planeAtBest = Mathf.Abs(along);
                            lateralAtBest = (d - g.Axis * along).magnitude;
                        }

                        Assert.LessOrEqual(planeAtBest, rs.PrismSpacing,
                            $"i{intensity} seed {seed} gate {gi} lane {lane}: nearest prism is {planeAtBest:F1} u off the ring plane");
                        Assert.That(lateralAtBest, Is.EqualTo(rs.LaneOffset).Within(rs.PrismSpacing),
                            $"i{intensity} seed {seed} gate {gi} lane {lane}: lane crosses {lateralAtBest:F1} u out, not at the lane offset");
                        Assert.LessOrEqual(lateralAtBest + PrismHalfWidth * 2f, g.Radius,
                            $"i{intensity} seed {seed} gate {gi} lane {lane}: lane {lateralAtBest:F1} u out does not clear the {g.Radius} u mouth by a prism");
                    }
                }
            }
        }

        [Test]
        public void Lanes_AreEvenlySpaced_ClosedLoops()
        {
            var rs = RegattaCourse.DefaultRails;
            foreach (var (intensity, seed) in Sweep())
            {
                var rails = Rails(Gates(intensity, seed));
                for (int lane = 0; lane < rails.LanePositions.Count; lane++)
                {
                    var p = rails.LanePositions[lane];
                    Assert.Greater(p.Length, 100, $"i{intensity} seed {seed} lane {lane}: too few prisms");
                    for (int k = 0; k < p.Length; k++)
                    {
                        float step = (p[(k + 1) % p.Length] - p[k]).magnitude;   // includes the closing step
                        Assert.That(step, Is.EqualTo(rs.PrismSpacing).Within(rs.PrismSpacing * 0.25f),
                            $"i{intensity} seed {seed} lane {lane} prism {k}: spacing {step:F2}");
                    }
                }
            }
        }

        [Test]
        public void Lanes_AreTheSameLength_SoNoDomainRacesShorter()
        {
            foreach (var (intensity, seed) in Sweep())
            {
                var rails = Rails(Gates(intensity, seed));
                float min = rails.LaneLengths.Min();
                float max = rails.LaneLengths.Max();
                Assert.LessOrEqual((max - min) / min, 0.03f,
                    $"i{intensity} seed {seed}: lane lengths spread {(max - min) / min:P1}");
            }
        }

        [Test]
        public void Lanes_NeverTouch()
        {
            // Two 6-wide prisms need 6 u between centres to not overlap; the super-shield reaches
            // 1.5x the leaf, so the fused cables need 18. The braid is built at 38 (22 x sqrt 3).
            const float minSeparation = 18f;
            foreach (var (intensity, seed) in Shipped())
            {
                var rails = Rails(Gates(intensity, seed));
                for (int a = 0; a < rails.LanePositions.Count; a++)
                for (int b = a + 1; b < rails.LanePositions.Count; b++)
                {
                    var pa = rails.LanePositions[a];
                    var pb = rails.LanePositions[b];
                    float best = float.MaxValue;
                    for (int i = 0; i < pa.Length; i += 3)
                    for (int j = 0; j < pb.Length; j += 3)
                        best = Mathf.Min(best, (pa[i] - pb[j]).sqrMagnitude);
                    Assert.GreaterOrEqual(Mathf.Sqrt(best), minSeparation,
                        $"i{intensity} seed {seed}: lanes {a} and {b} come within {Mathf.Sqrt(best):F1} u");
                }
            }
        }

        [Test]
        public void Ladder_MouthsShrink_FloorsTighten_CapsCoverHalfTheHardestTurn()
        {
            float prevMouth = float.MaxValue, prevFloor = float.MaxValue;
            foreach (int i in Intensities)
            {
                var s = RegattaCourse.ForIntensity(i);
                Assert.Less(s.RingRadius, prevMouth, $"i{i}: mouth does not shrink");
                Assert.LessOrEqual(s.CornerFloorRadius, prevFloor, $"i{i}: floor does not tighten");
                Assert.GreaterOrEqual(s.MaxPresentDegrees, s.CornerProfile.Max() * 0.5f,
                    $"i{i}: presentation cap does not cover half the hardest authored turn");
                Assert.That(s.CornerProfile.Sum(), Is.EqualTo(360f).Within(0.5f), $"i{i}: a lap turns through 360");
                // Three lanes at the offset, plus a prism, inside the tightest mouth.
                Assert.Less(RegattaCourse.DefaultRails.LaneOffset + PrismHalfWidth * 2f, s.RingRadius, $"i{i}: lanes do not fit the mouth");
                prevMouth = s.RingRadius;
                prevFloor = s.CornerFloorRadius;
            }
        }

        [Test]
        public void ShippedCircuits_HonourTheirCornerFloor()
        {
            foreach (var (intensity, seed) in Shipped())
            {
                var gates = Gates(intensity, seed);
                float floor = RegattaCourse.ForIntensity(intensity).CornerFloorRadius;
                int n = gates.Count;
                for (int i = 0; i < n; i++)
                {
                    float r = RaceCourseGeometry.CornerRadius(gates[(i - 1 + n) % n].Position,
                                                              gates[i].Position, gates[(i + 1) % n].Position);
                    Assert.GreaterOrEqual(r, floor * 0.98f, $"i{intensity}: corner {i} is {r:F0} u under the {floor} u floor");
                }
            }
        }
    }
}
#endif
