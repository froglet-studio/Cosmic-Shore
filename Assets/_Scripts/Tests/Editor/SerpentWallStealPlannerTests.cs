using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Serpent seed wall's edge-steal (SerpentWallStealPlanner, SERPENT_SEED_WALL.md "Growth"):
    /// the wall grows toward the mass beside its edge rather than as a disc, only within the steal
    /// radius of an edge site, one prism per site, and one sweep can creep several rings.
    /// </summary>
    public class SerpentWallStealPlannerTests
    {
        const float Pitch = 15f;

        static HashSet<Vector2Int> SeedOnly() => new() { Vector2Int.zero };

        static Dictionary<Vector2Int, int> Plan(HashSet<Vector2Int> filled, List<Vector3> candidates,
            float radius, int maxClaims = 100, int maxWaves = 10)
        {
            var claims = new List<SerpentWallStealPlanner.Claim>();
            new SerpentWallStealPlanner().Plan(filled, candidates, Pitch, radius, maxClaims, maxWaves, claims);
            var bySite = new Dictionary<Vector2Int, int>();
            var used = new HashSet<int>();
            foreach (var c in claims)
            {
                Assert.IsTrue(bySite.TryAdd(c.Site, c.Candidate), $"site {c.Site} claimed twice");
                Assert.IsTrue(used.Add(c.Candidate), $"prism {c.Candidate} claimed twice");
                Assert.IsFalse(filled.Contains(c.Site), $"filled site {c.Site} claimed");
            }
            return bySite;
        }

        [Test]
        public void GrowsAlongATrailInsteadOfAsADisc()
        {
            // A trail running out to the right of the seed, in the wall's plane, one prism per
            // pitch: the wall eats along it and nowhere else.
            var trail = new List<Vector3>();
            for (int k = 1; k <= 10; k++) trail.Add(new Vector3(k * Pitch, 0f, 0f));

            var claims = Plan(SeedOnly(), trail, radius: 20f);

            Assert.AreEqual(10, claims.Count);
            for (int k = 1; k <= 10; k++)
                Assert.IsTrue(claims.ContainsKey(new Vector2Int(k, 0)), $"site ({k}, 0)");
        }

        [Test]
        public void NothingOutsideTheStealRadiusOfTheEdgeIsTaken()
        {
            // 30 units from the seed's nearest edge site, with a 20-unit radius.
            var far = new List<Vector3> { new(Pitch + 30f, 0f, 0f), new(0f, 0f, 40f) };
            Assert.AreEqual(0, Plan(SeedOnly(), far, radius: 20f).Count);
            // The same prisms come into reach when the radius grows (Space).
            Assert.AreEqual(2, Plan(SeedOnly(), far, radius: 45f).Count);
        }

        [Test]
        public void TakesTheTrailBehindTheSeedOnlyAsFarAsTheEdgeReaches()
        {
            // The Serpent's own trail runs back from the seed along the wall's normal. The edge
            // sites sit one pitch out in the plane, so only the nearest few prisms are in reach.
            var trail = new List<Vector3>();
            for (int k = 1; k <= 10; k++) trail.Add(new Vector3(0f, 0f, -k * 6f));

            var claims = Plan(SeedOnly(), trail, radius: 40f);

            Assert.Greater(claims.Count, 0);
            Assert.Less(claims.Count, 10);
        }

        [Test]
        public void OneSweepCreepsAsManyRingsAsItHasWaves()
        {
            var trail = new List<Vector3>();
            for (int k = 1; k <= 10; k++) trail.Add(new Vector3(k * Pitch, 0f, 0f));

            Assert.AreEqual(1, Plan(SeedOnly(), trail, radius: 20f, maxWaves: 1).Count);
            Assert.AreEqual(3, Plan(SeedOnly(), trail, radius: 20f, maxWaves: 3).Count);
        }

        [Test]
        public void ASweepClaimsNoMoreThanItsCap()
        {
            var cloud = new List<Vector3>();
            for (int x = -4; x <= 4; x++)
            for (int y = -4; y <= 4; y++)
                cloud.Add(new Vector3(x * Pitch, y * Pitch, 2f));

            Assert.AreEqual(7, Plan(SeedOnly(), cloud, radius: 20f, maxClaims: 7).Count);
        }

        [Test]
        public void TheNearerPrismWinsTheSite()
        {
            var two = new List<Vector3> { new(Pitch + 8f, 0f, 0f), new(Pitch + 1f, 0f, 0f) };
            var claims = Plan(SeedOnly(), two, radius: 20f, maxWaves: 1);
            Assert.AreEqual(1, claims[new Vector2Int(1, 0)]);
        }

        [Test]
        public void GrowsFromEveryBrickNotOnlyTheSeed()
        {
            // A wall already reaching out to (3, 0): a prism beside its far end is taken there,
            // although it is out of the seed's own reach.
            var filled = new HashSet<Vector2Int> { Vector2Int.zero, new(1, 0), new(2, 0), new(3, 0) };
            var beside = new List<Vector3> { new(3 * Pitch, Pitch, 0f) };
            var claims = Plan(filled, beside, radius: 5f);
            Assert.AreEqual(0, claims[new Vector2Int(3, 1)]);
        }
    }
}
