using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Serpent seed wall's geometry (R_VesselActions/SERPENT_SEED_WALL.md): the spacing that
    /// leaves a gap between shielded bricks long vertex to short vertex, the symmetric holes, the
    /// Mass-5 danger panel that fills every hole with an even crack, and the rotations the runtime
    /// lays with agreeing with those maths.
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
            SerpentWallLattice.Rhombus(0, 0, Short, pitch, seed);    // long axis up
            SerpentWallLattice.Rhombus(1, 0, Short, pitch, right);   // long axis right
            SerpentWallLattice.Rhombus(0, 1, Short, pitch, above);   // long axis right

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
            // at the touching pitch they read as touching. There must be clear air between
            // neighbouring diamonds, and between the seed's whole 3x footprint and them.
            float pitch = SerpentWallLattice.Pitch(Short);
            float air = 0.05f * Short;
            var a = new Vector2[4];
            var seed = new Vector2[4];
            var b = new Vector2[4];
            SerpentWallLattice.Rhombus(0, 0, Short, pitch, a);
            SerpentWallLattice.StellatedFootprint(0, 0, Short, pitch, seed);
            foreach (var n in Neighbours)
            {
                SerpentWallLattice.Rhombus(n.x, n.y, Short, pitch, b);
                Assert.IsFalse(Overlap(a, b, air), $"shielded seed and {n} come within {air}");
                Assert.IsFalse(Overlap(seed, b, air), $"super-shielded seed and {n} come within {air}");
            }
        }

        static readonly Vector2Int[] Neighbours =
        {
            new(1, 0), new(0, 1), new(1, 1), new(-1, 1), new(-1, 0), new(0, -1), new(-1, -1), new(1, -1),
        };

        /// <summary>True when no edge normal of either quad separates them by at least
        /// <paramref name="gap"/>. Separation along any unit axis is a lower bound on the true
        /// distance, so "no overlap at gap g" proves the quads are at least g apart.</summary>
        static bool Overlap(Vector2[] a, Vector2[] b, float gap) => !Separated(a, a, b, gap) && !Separated(b, a, b, gap);

        static bool Separated(Vector2[] edges, Vector2[] a, Vector2[] b, float gap)
        {
            for (int e = 0; e < edges.Length; e++)
            {
                Vector2 d = edges[(e + 1) % edges.Length] - edges[e];
                var axis = new Vector2(-d.y, d.x).normalized;
                float aMin = float.MaxValue, aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
                foreach (var p in a) { float v = Vector2.Dot(p, axis); aMin = Mathf.Min(aMin, v); aMax = Mathf.Max(aMax, v); }
                foreach (var p in b) { float v = Vector2.Dot(p, axis); bMin = Mathf.Min(bMin, v); bMax = Mathf.Max(bMax, v); }
                if (aMax + gap <= bMin + 1e-5f || bMax + gap <= aMin + 1e-5f) return true;
            }
            return false;
        }

        static Vector2[][] CellBricks(int i, int j)
        {
            float pitch = SerpentWallLattice.Pitch(Short);
            var bricks = new Vector2[4][];
            int k = 0;
            foreach (var s in new[] { (i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1) })
            {
                bricks[k] = new Vector2[4];
                SerpentWallLattice.Rhombus(s.Item1, s.Item2, Short, pitch, bricks[k]);
                k++;
            }
            return bricks;
        }

        static Vector2[] Square(Vector2 center, float side, float degrees)
        {
            float h = side * 0.5f, rad = degrees * Mathf.Deg2Rad, c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            Vector2 R(float x, float y) => center + new Vector2(x * c - y * s, x * s + y * c);
            return new[] { R(h, h), R(-h, h), R(-h, -h), R(h, -h) };
        }

        /// <summary>Brute-force oracle: the largest centred square that clears the cell's four
        /// shields, over every angle at 0.25 degree steps.</summary>
        static float LargestSquareBySearch(int i, int j)
        {
            var bricks = CellBricks(i, j);
            Vector2 center = SerpentWallLattice.CellCenter(i, j, SerpentWallLattice.Pitch(Short));
            float best = 0f;
            for (float deg = 0f; deg < 90f; deg += 0.25f)
            {
                float lo = 0f, hi = 10f * Short;
                for (int iter = 0; iter < 30; iter++)
                {
                    float mid = 0.5f * (lo + hi);
                    var sq = Square(center, mid, deg);
                    bool hit = false;
                    foreach (var b in bricks) hit |= Overlap(sq, b, 0f);
                    if (hit) hi = mid; else lo = mid;
                }
                best = Mathf.Max(best, lo);
            }
            return best;
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
        public void EveryHoleTakesTheSamePanelAndItIsTheLargestThatFits()
        {
            // Garrett, 2026-10-13: one panel size in every hole, not half of them. The closed form
            // is the hole itself (4.025 x short), and a brute-force search finds nothing bigger.
            var expected = SerpentWallLattice.CellPanel(0, 0, Short, 0f).side;
            Assert.AreEqual(9f / Mathf.Sqrt(5f) * Short, expected, 1e-3f);
            foreach (var cell in new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0),
                                         new Vector2Int(0, -1), new Vector2Int(2, 3) })
            {
                var panel = SerpentWallLattice.CellPanel(cell.x, cell.y, Short, 0f);
                Assert.AreEqual(expected, panel.side, 1e-4f, $"cell {cell}");
                Assert.LessOrEqual(LargestSquareBySearch(cell.x, cell.y), panel.side + 0.01f, $"cell {cell}");
            }
        }

        [Test]
        public void PanelLeavesAnEvenCrackToEveryShield()
        {
            // The crack is the clearance on all four sides, never more on one side and touching on
            // another: the panel's sides run parallel to the shield edges facing the hole.
            float clearance = 0.25f * Short;
            float pitch = SerpentWallLattice.Pitch(Short);
            foreach (var cell in new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, -1) })
            {
                var (side, angle) = SerpentWallLattice.CellPanel(cell.x, cell.y, Short, clearance);
                Assert.Greater(side, 0f);
                var sq = Square(SerpentWallLattice.CellCenter(cell.x, cell.y, pitch), side, angle);
                foreach (var b in CellBricks(cell.x, cell.y))
                {
                    Assert.IsFalse(Overlap(sq, b, clearance - 1e-3f), $"cell {cell}: panel within the crack");
                    Assert.IsTrue(Overlap(sq, b, clearance + 1e-2f), $"cell {cell}: crack wider than asked");
                }
            }
        }

        [Test]
        public void EvenAndOddHolesTiltTheMirrorWays()
        {
            float tilt = Mathf.Atan2(1f, SerpentWallLattice.Aspect) * Mathf.Rad2Deg;
            Assert.AreEqual(tilt, SerpentWallLattice.CellPanel(0, 0, Short, 0f).angleDegrees, 1e-4f);
            Assert.AreEqual(90f - tilt, SerpentWallLattice.CellPanel(1, 0, Short, 0f).angleDegrees, 1e-4f);
        }

        [Test]
        public void BrickRotationTurnsTheBrickWhereTheRhombusSaysItIs()
        {
            // Checked in an arbitrary frame so the frame's own rotation cannot hide a quarter turn.
            Quaternion frame = Quaternion.Euler(20f, -35f, 50f);
            float pitch = SerpentWallLattice.Pitch(Short);
            var r = new Vector2[4];
            foreach (var site in new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) })
            {
                SerpentWallLattice.Rhombus(site.x, site.y, Short, pitch, r);
                Vector2 c = SerpentWallLattice.SiteCenter(site.x, site.y, pitch);
                Vector3 longAxis = SerpentWallLattice.BrickRotation(frame, site.x, site.y) * Vector3.up;
                Vector3 local = Quaternion.Inverse(frame) * longAxis;
                Vector2 expected = (r[0] - c).normalized;
                Assert.AreEqual(0f, local.z, 1e-4f, "the brick stays in the wall plane");
                // An axis, not a direction: a long-axis-right brick's +y may face either way.
                float alignment = Mathf.Abs(Vector2.Dot(expected, new Vector2(local.x, local.y)));
                Assert.AreEqual(1f, alignment, 1e-4f, $"site {site}");
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
    }
}
