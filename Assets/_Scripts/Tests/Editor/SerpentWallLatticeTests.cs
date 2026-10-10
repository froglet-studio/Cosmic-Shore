using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Serpent seed wall's geometry (R_VesselActions/SERPENT_SEED_WALL.md): the spacing that
    /// leaves a gap between shielded bricks long vertex to short vertex, the symmetric holes, the
    /// Mass-5 twist that opens one parity of hole and closes the other, and the rotations the
    /// runtime lays with agreeing with those maths.
    /// </summary>
    public class SerpentWallLatticeTests
    {
        const float Short = 3f;   // the Serpent trail's BaseScale short side

        [Test]
        public void PitchIsTheShieldReachOfHalfLongPlusHalfShort()
        {
            // The shield octahedron reaches 3x the box's half-extents, so shielded vertices would
            // meet at 3 x (6/2 + 3/2) = 13.5 (not the box's own 4.5, which packed them into a
            // clump), and the pitch adds half a short side of air: 15.
            Assert.AreEqual(3f, SerpentWallLattice.ShieldReach, 1e-6f);
            Assert.AreEqual(15f, SerpentWallLattice.Pitch(Short), 1e-5f);
            Assert.AreEqual(new Vector3(3f, 6f, 0.5f), SerpentWallLattice.BrickScale(Short, 0.5f));
        }

        [Test]
        public void MassScalesSizeAndSpacingTogether()
        {
            // One multiplier, two dimensions: the pitch is derived from the brick, so a Mass-10
            // wall is the resting wall at twice the scale, not bigger bricks crammed together.
            float rest = SerpentWallLattice.Pitch(Short) / Short;
            float full = SerpentWallLattice.Pitch(Short * 2f) / (Short * 2f);
            Assert.AreEqual(rest, full, 1e-5f);
        }

        [Test]
        public void ShieldedBricksPointLongVertexAtShortVertexWithAGap()
        {
            float pitch = SerpentWallLattice.Pitch(Short);
            var seed = new Vector2[4];
            var right = new Vector2[4];
            var above = new Vector2[4];
            SerpentWallLattice.Rhombus(0, 0, Short, pitch, 0f, seed);    // long axis up
            SerpentWallLattice.Rhombus(1, 0, Short, pitch, 0f, right);   // long axis right
            SerpentWallLattice.Rhombus(0, 1, Short, pitch, 0f, above);   // long axis right

            // Seed's SHORT vertex (+x) faces the right neighbour's LONG vertex (-x) across the gap;
            // touching shields read as one clump in play.
            float gap = SerpentWallLattice.ShieldGap * Short;
            Assert.AreEqual(gap, Vector2.Distance(seed[1], right[2]), 1e-4f);
            // Seed's LONG vertex (+y) faces the upper neighbour's SHORT vertex (-y).
            Assert.AreEqual(gap, Vector2.Distance(seed[0], above[3]), 1e-4f);
        }

        [Test]
        public void ShieldedNeighboursNeverInterpenetrate()
        {
            // The regressions: at the box pitch every shielded brick overlapped its neighbours, and
            // at the touching pitch they read as touching and the super-shielded seed's stellation
            // cut into them once twisted. Every twist the config allows must leave clear air
            // between neighbouring diamonds, and between the seed's whole 3x footprint and them.
            float pitch = SerpentWallLattice.Pitch(Short);
            float air = 0.05f * Short;
            var a = new Vector2[4];
            var seed = new Vector2[4];
            var b = new Vector2[4];
            for (float twist = 0f; twist <= SerpentWallLattice.MaxTwistDegrees; twist += 2.5f)
            foreach (var n in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(-1, 1),
                                      new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(1, -1) })
            {
                SerpentWallLattice.Rhombus(0, 0, Short, pitch, twist, a);
                SerpentWallLattice.StellatedFootprint(0, 0, Short, pitch, twist, seed);
                SerpentWallLattice.Rhombus(n.x, n.y, Short, pitch, twist, b);
                Assert.IsFalse(Overlap(a, b, air), $"shielded seed and {n} come within {air} at twist {twist}");
                Assert.IsFalse(Overlap(seed, b, air), $"super-shielded seed and {n} come within {air} at twist {twist}");
            }
        }

        static bool Overlap(Vector2[] a, Vector2[] b, float eps) => !Separated(a, a, b, eps) && !Separated(b, a, b, eps);

        static bool Separated(Vector2[] edges, Vector2[] a, Vector2[] b, float eps)
        {
            for (int e = 0; e < edges.Length; e++)
            {
                Vector2 d = edges[(e + 1) % edges.Length] - edges[e];
                var axis = new Vector2(-d.y, d.x).normalized;
                float aMin = float.MaxValue, aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
                foreach (var p in a) { float v = Vector2.Dot(p, axis); aMin = Mathf.Min(aMin, v); aMax = Mathf.Max(aMax, v); }
                foreach (var p in b) { float v = Vector2.Dot(p, axis); bMin = Mathf.Min(bMin, v); bMax = Mathf.Max(bMax, v); }
                if (aMax <= bMin + eps || bMax <= aMin + eps) return true;
            }
            return false;
        }

        [Test]
        public void SitesAlternateOrientationLikeACheckerboard()
        {
            Assert.IsTrue(SerpentWallLattice.IsLongAxisUp(0, 0));
            Assert.IsFalse(SerpentWallLattice.IsLongAxisUp(1, 0));
            Assert.IsFalse(SerpentWallLattice.IsLongAxisUp(0, -1));
            Assert.IsTrue(SerpentWallLattice.IsLongAxisUp(1, 1));
        }

        [Test]
        public void AtRestEveryHoleIsTheSameSquare()
        {
            // The holes are what make the pattern symmetric rather than a strict tiling. With the
            // gap the hole is 4 x short (the 3.354 x short it was with touching vertices, widened).
            var even = SerpentWallLattice.LargestClearSquare(0, 0, Short, 0f);
            var odd = SerpentWallLattice.LargestClearSquare(1, 0, Short, 0f);
            Assert.AreEqual(4f * Short, even.side, 0.05f);
            Assert.AreEqual(even.side, odd.side, 0.05f);
        }

        [Test]
        public void TwistOpensOneParityAndClosesTheOther()
        {
            float rest = SerpentWallLattice.LargestClearSquare(0, 0, Short, 0f).side;
            var even = SerpentWallLattice.LargestClearSquare(0, 0, Short, 15f).side;
            var odd = SerpentWallLattice.LargestClearSquare(1, 0, Short, 15f).side;

            Assert.That(Mathf.Max(even, odd), Is.GreaterThan(rest + 0.1f), "one parity opens");
            Assert.That(Mathf.Min(even, odd), Is.LessThan(rest - 0.1f), "the other closes");

            int open = SerpentWallLattice.OpeningParity(Short, 15f);
            Assert.AreEqual(odd > even ? 1 : 0, open);
        }

        [Test]
        public void ClockwiseTwistOpensTheOddCells()
        {
            // Pinned so the panels' parity is a known fact, not just self-consistent maths.
            Assert.AreEqual(1, SerpentWallLattice.OpeningParity(Short, 15f));
        }

        [Test]
        public void BrickRotationTurnsTheBrickWhereTheRhombusSaysItIs()
        {
            // The regression: the assembler signed its twist independently of these maths, turned
            // the bricks the other way, and laid the danger panels in the cells that had CLOSED.
            // Checked in an arbitrary frame so the frame's own rotation cannot hide a sign.
            Quaternion frame = Quaternion.Euler(20f, -35f, 50f);
            float pitch = SerpentWallLattice.Pitch(Short);
            var r = new Vector2[4];
            foreach (var site in new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) })
            foreach (float twist in new[] { 0f, 15f, SerpentWallLattice.MaxTwistDegrees })
            {
                SerpentWallLattice.Rhombus(site.x, site.y, Short, pitch, twist, r);
                Vector2 c = SerpentWallLattice.SiteCenter(site.x, site.y, pitch);
                Vector3 longAxis = SerpentWallLattice.BrickRotation(frame, site.x, site.y, twist) * Vector3.up;
                Vector3 local = Quaternion.Inverse(frame) * longAxis;
                Vector2 expected = (r[0] - c).normalized;
                Assert.AreEqual(0f, local.z, 1e-4f, "the twist stays in the wall plane");
                // An axis, not a direction: a long-axis-right brick's +y may face either way. A
                // twist signed the wrong way is off by twice the twist and fails this.
                float alignment = Mathf.Abs(Vector2.Dot(expected, new Vector2(local.x, local.y)));
                Assert.AreEqual(1f, alignment, 1e-4f, $"site {site} twist {twist}");
            }
        }

        [Test]
        public void PanelRotationTurnsThePanelToTheClearSquaresAngle()
        {
            Quaternion frame = Quaternion.Euler(-10f, 70f, 25f);
            foreach (float angle in new[] { 0f, 12f, 49f })
            {
                Vector3 local = Quaternion.Inverse(frame) * (SerpentWallLattice.PanelRotation(frame, angle) * Vector3.right);
                float rad = angle * Mathf.Deg2Rad;
                Assert.AreEqual(Mathf.Cos(rad), local.x, 1e-4f, $"angle {angle}");
                Assert.AreEqual(Mathf.Sin(rad), local.y, 1e-4f, $"angle {angle}");
            }
        }

        [Test]
        public void GrowthOrderIsADiscAroundTheSeed()
        {
            List<Vector2Int> order = SerpentWallLattice.GrowthOrder(8);
            Assert.AreEqual(8, order.Count);
            CollectionAssert.DoesNotContain(order, Vector2Int.zero);
            // The first four are the seed's edge neighbours, before any diagonal.
            for (int k = 0; k < 4; k++)
                Assert.AreEqual(1, Mathf.Abs(order[k].x) + Mathf.Abs(order[k].y));
            CollectionAssert.AllItemsAreUnique(order);
        }
    }
}
