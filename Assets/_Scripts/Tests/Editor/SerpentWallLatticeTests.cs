using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Serpent seed wall's geometry (R_VesselActions/SERPENT_SEED_WALL.md): the spacing that
    /// makes shielded bricks touch long vertex to short vertex, the symmetric holes, and the
    /// Mass-5 twist that opens one parity of hole and closes the other.
    /// </summary>
    public class SerpentWallLatticeTests
    {
        const float Short = 3f;   // the Serpent trail's BaseScale short side

        [Test]
        public void PitchIsHalfLongPlusHalfShort()
        {
            Assert.AreEqual(4.5f, SerpentWallLattice.Pitch(Short), 1e-5f);
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
        public void ShieldedBricksTouchLongVertexToShortVertex()
        {
            float pitch = SerpentWallLattice.Pitch(Short);
            var seed = new Vector2[4];
            var right = new Vector2[4];
            var above = new Vector2[4];
            SerpentWallLattice.Rhombus(0, 0, Short, pitch, 0f, seed);    // long axis up
            SerpentWallLattice.Rhombus(1, 0, Short, pitch, 0f, right);   // long axis right
            SerpentWallLattice.Rhombus(0, 1, Short, pitch, 0f, above);   // long axis right

            // Seed's SHORT vertex (+x) meets the right neighbour's LONG vertex (-x).
            Assert.That(Vector2.Distance(seed[1], right[2]), Is.LessThan(1e-4f));
            // Seed's LONG vertex (+y) meets the upper neighbour's SHORT vertex (-y).
            Assert.That(Vector2.Distance(seed[0], above[3]), Is.LessThan(1e-4f));
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
            // The holes are what make the pattern symmetric rather than a strict tiling. Their
            // corners are the four contact points, so the hole is a square of side
            // sqrt(0.5^2 + 1^2) x short = 1.118 x short.
            var even = SerpentWallLattice.LargestClearSquare(0, 0, Short, 0f);
            var odd = SerpentWallLattice.LargestClearSquare(1, 0, Short, 0f);
            float expected = Mathf.Sqrt(1.25f) * Short;
            Assert.AreEqual(expected, even.side, 0.05f);
            Assert.AreEqual(expected, odd.side, 0.05f);
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
