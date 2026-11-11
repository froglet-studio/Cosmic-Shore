using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Belfry, Thresher Time's clockwork course (ThresherBelfry, 2026-11-07): a ring hangs at its gate and swings
    /// across the line, maces cross the straights and clear them, shutters only at the top, the ladder only climbs, every
    /// peer computes the same pose, and the arena can carry it.
    /// </summary>
    public class ThresherBelfryTests
    {
        static readonly float[] Mouths = { 96f, 72f, 58f, 46f };

        /// <summary>An eight-gate loop like the race's: a tilted ring round the cell, the axis along the loop.</summary>
        static List<RaceGate> Course(int intensity)
        {
            var gates = new List<RaceGate>();
            const int n = 8;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var p = new Vector3(Mathf.Cos(a) * 800f, Mathf.Sin(a * 2f) * 120f, Mathf.Sin(a) * 800f);
                float b = (i + 1) * Mathf.PI * 2f / n;
                var q = new Vector3(Mathf.Cos(b) * 800f, Mathf.Sin(b * 2f) * 120f, Mathf.Sin(b) * 800f);
                gates.Add(new RaceGate(p, (q - p).normalized, Mouths[intensity - 1]));
            }
            return gates;
        }

        static ThresherBelfry.Layout Build(int intensity)
        {
            var layout = new ThresherBelfry.Layout();
            ThresherBelfry.Build(Course(intensity), intensity, layout);
            return layout;
        }

        [Test]
        public void EveryRingHangsAtItsGate_AndSwingsAcrossTheLine([Values(1, 2, 3, 4)] int intensity)
        {
            var layout = Build(intensity);
            Assert.AreEqual(8, layout.Rings.Count);
            for (int i = 0; i < layout.Rings.Count; i++)
            {
                var ring = layout.Rings[i];
                var gate = layout.Gates[i];
                // At the swing's middle (angle 0) the ring is exactly on its gate.
                float zero = (Mathf.PI - ring.Phase) / ring.Omega;
                Assert.AreEqual(0f, ring.AngleAt(zero), 1e-3f);
                Assert.AreEqual(0f, (ring.BobAt(zero) - gate.Position).magnitude, 0.05f, "the rest pose is the gate");
                // It always hangs at the chain's length, and it moves ACROSS the flight line, not along it.
                for (float t = 0f; t < 10f; t += 0.37f)
                {
                    Vector3 bob = ring.BobAt(t);
                    Assert.AreEqual(ring.Length, (bob - ring.Pivot).magnitude, 0.01f);
                    Assert.AreEqual(0f, Vector3.Dot(bob - gate.Position, gate.Axis.normalized), 0.05f,
                        "the ring stays in its own mouth plane");
                }
                Assert.AreEqual(0f, Vector3.Dot(ring.Up, gate.Axis.normalized), 1e-3f, "the chain hangs square to the line");
            }
        }

        [Test]
        public void TheLadderOnlyClimbs_AndARingCanAlwaysBeChased()
        {
            const float cruise = 125f;   // the Thresher's cruise (THRESHER.md)
            for (int i = 1; i <= 4; i++)
            {
                var s = ThresherBelfry.SpecFor(i);
                float topSpeed = s.RingLength * s.RingSwingDeg * Mathf.Deg2Rad * 2f * Mathf.PI / s.RingPeriod;
                Assert.Less(topSpeed, cruise, $"I{i}: a ring at {topSpeed:0} u/s outruns a cruising Thresher");
                if (i == 4) break;
                var next = ThresherBelfry.SpecFor(i + 1);
                float sweep = s.RingLength * Mathf.Sin(s.RingSwingDeg * Mathf.Deg2Rad);
                float nextSweep = next.RingLength * Mathf.Sin(next.RingSwingDeg * Mathf.Deg2Rad);
                Assert.Greater(nextSweep, sweep, $"I{i + 1} swings no wider than I{i}");
                Assert.Less(next.RingPeriod, s.RingPeriod, $"I{i + 1} swings no faster than I{i}");
                Assert.GreaterOrEqual(Build(i + 1).Maces.Count, Build(i).Maces.Count, $"I{i + 1} has fewer maces");
            }
        }

        [Test]
        public void MacesFromI2_ShuttersOnlyAtI4()
        {
            Assert.AreEqual(0, Build(1).Maces.Count, "I1 is an open course");
            Assert.AreEqual(4, Build(2).Maces.Count, "I2: a mace on every other straight");
            Assert.AreEqual(8, Build(3).Maces.Count, "I3: a mace on every straight");
            Assert.AreEqual(16, Build(4).Maces.Count, "I4: two a straight");
            for (int i = 1; i <= 3; i++)
                Assert.IsFalse(Build(i).Parts.Exists(p => p.Role == ThresherBelfry.Role.Shutter), $"I{i} has no shutters");
            Assert.IsTrue(Build(4).Parts.Exists(p => p.Role == ThresherBelfry.Role.Shutter));
            Assert.IsFalse(Build(2).Parts.Exists(p => p.Kind == PrismKind.Danger), "I2 cannot hurt you");
            Assert.IsTrue(Build(3).Parts.Exists(p => p.Role == ThresherBelfry.Role.MaceHead && p.Kind == PrismKind.Danger));
        }

        [Test]
        public void AMaceCrossesTheLine_ThenClearsIt([Values(2, 3, 4)] int intensity)
        {
            var layout = Build(intensity);
            const float hull = 10f;
            foreach (var mace in layout.Maces)
            {
                // Through the middle of its swing the head sits on the straight...
                float zero = (Mathf.PI - mace.Phase) / mace.Omega;
                Vector3 onLine = mace.Pivot - mace.Up * mace.Length;
                Assert.AreEqual(0f, (mace.BobAt(zero) - onLine).magnitude, 0.05f, "the head crosses the line");
                // ...and at either end it is clear of a hull flying the straight: there is a window.
                float clear = mace.Length * Mathf.Sin(mace.Swing);
                Assert.Greater(clear, ThresherBelfry.HeadReach + hull, "the swing never clears the line");
            }
        }

        [Test]
        public void ShuttersStayInsideTheMouth_AndNeverShutCompletely()
        {
            var layout = Build(4);
            for (float t = 0f; t < 8f; t += 0.29f)
            {
                foreach (var part in layout.Parts)
                {
                    if (part.Role != ThresherBelfry.Role.Shutter) continue;
                    ThresherBelfry.PoseAt(layout, part, t, out var pos, out _);
                    var gate = ThresherBelfry.GateAt(layout, part.Group, t);
                    float off = (pos - gate.Position).magnitude;
                    Assert.LessOrEqual(off, gate.Radius * 1.42f, "a bar left its mouth");
                    float x = Mathf.Abs(Vector3.Dot(pos - gate.Position, layout.Rings[part.Group].Side));
                    Assert.GreaterOrEqual(x, gate.Radius * ThresherBelfry.ShutterMinOpen - 1e-3f,
                        "the bars met in the middle: the mouth shut");
                }
            }
        }

        [Test]
        public void ClosedForm_EveryPeerPosesTheSame_NeighboursDiffer()
        {
            var a = Build(4);
            var b = Build(4);
            Assert.AreEqual(a.Parts.Count, b.Parts.Count);
            for (int k = 0; k < a.Parts.Count; k += 7)
            {
                ThresherBelfry.PoseAt(a, a.Parts[k], 12.3f, out var pa, out var ra);
                ThresherBelfry.PoseAt(b, b.Parts[k], 12.3f, out var pb, out var rb);
                Assert.AreEqual(pa, pb);
                Assert.AreEqual(ra, rb);
            }
            Assert.AreNotEqual(a.Rings[0].Phase, a.Rings[1].Phase, "neighbouring rings swing out of step");
        }

        [Test]
        public void TheArenaCarriesIt([Values(1, 2, 3, 4)] int intensity)
        {
            Assert.LessOrEqual(Build(intensity).Parts.Count, 600, "moving prisms posed every frame on every peer");
        }

        [Test]
        public void NegativeControl_AMaceThatDoesNotSwingNeverClears()
        {
            // The window test above has teeth: a mace hung with no swing sits on the line forever.
            var still = new ThresherBelfry.Pendulum(0, Vector3.up * 150f, Vector3.up, Vector3.right, Vector3.forward, 150f,
                                                    0f, 1f, 0f);
            Assert.LessOrEqual(still.Length * Mathf.Sin(still.Swing), ThresherBelfry.HeadReach + 10f);
        }
    }
}
