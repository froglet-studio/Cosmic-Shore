#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Regatta's shuffled course pool (Garrett, 2026-11-06: one Regatta level, several course variants shuffled, no
    /// intensities). The contract: the host deals every enabled circuit once before any repeats and never the same one
    /// twice running; a pinned variant (a replay, a benchmark) wins; the draw reaches clients in the game-config sync;
    /// the scene's Cell lays CellConfigs[variant]; and the shipped pool is four DIFFERENT circuits, each a Regatta arena.
    /// </summary>
    public class RegattaCoursePoolTests
    {
        const string PoolPath = "Assets/_SO_Assets/Cell Configs/Regatta Cell/Regatta Course Pool.asset";

        [TearDown]
        public void ClearPin() => CourseVariantOverride.Forced = -1;

        static List<bool> All(int n) => Enumerable.Repeat(true, n).ToList();

        // ── The shuffle bag ────────────────────────────────────────────────────────────────

        [Test]
        public void Bag_DealsEveryEnabledEntryOncePerDeck()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var bag = new CourseShuffleBag();
                var rng = new System.Random(seed);
                for (int deck = 0; deck < 5; deck++)
                {
                    var dealt = new List<int>();
                    for (int k = 0; k < 4; k++) dealt.Add(bag.Next(All(4), rng));
                    CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, dealt, $"seed {seed} deck {deck}: {string.Join(",", dealt)}");
                }
            }
        }

        [Test]
        public void Bag_NeverDealsTheSameEntryTwiceRunning()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var bag = new CourseShuffleBag();
                var rng = new System.Random(seed);
                int last = -1;
                for (int k = 0; k < 200; k++)
                {
                    int next = bag.Next(All(3), rng);
                    Assert.AreNotEqual(last, next, $"seed {seed} deal {k}: the same circuit twice running");
                    last = next;
                }
            }
        }

        [Test]
        public void Bag_SkipsDisabledEntries_AndAnswersMinusOneWhenNoneIsEnabled()
        {
            var bag = new CourseShuffleBag();
            var rng = new System.Random(7);
            var enabled = new List<bool> { true, false, true, false };
            for (int k = 0; k < 40; k++)
                Assert.That(bag.Next(enabled, rng), Is.EqualTo(0).Or.EqualTo(2));

            Assert.AreEqual(-1, new CourseShuffleBag().Next(new List<bool> { false, false }, rng));
            Assert.AreEqual(-1, new CourseShuffleBag().Next(new List<bool>(), rng));
        }

        [Test]
        public void Bag_OneEntryPool_DealsItEveryTime()
        {
            var bag = new CourseShuffleBag();
            var rng = new System.Random(3);
            for (int k = 0; k < 10; k++) Assert.AreEqual(0, bag.Next(All(1), rng));
        }

        [Test]
        public void Bag_StartsAFreshDeckWhenTheEnabledSetChanges()
        {
            var bag = new CourseShuffleBag();
            var rng = new System.Random(11);
            bag.Next(All(4), rng);
            Assert.AreEqual(3, bag.Remaining);
            var onlyThree = new List<bool> { false, false, false, true };
            Assert.AreEqual(3, bag.Next(onlyThree, rng), "a deck dealt under the old pool must not survive a change");
        }

        // ── The pool asset's draw ──────────────────────────────────────────────────────────

        static RegattaCoursePoolSO Pool(params bool[] enabled)
        {
            var pool = ScriptableObject.CreateInstance<RegattaCoursePoolSO>();
            var entries = enabled.Select((on, i) => new RegattaCoursePoolSO.Entry
            {
                Label = $"c{i}",
                CellConfig = ScriptableObject.CreateInstance<CellConfigDataSO>(),
                Enabled = on,
            }).ToList();
            typeof(RegattaCoursePoolSO).GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pool, entries);
            return pool;
        }

        [Test]
        public void Pool_PinnedVariantWins_AndAnOutOfRangePinIsIgnored()
        {
            var pool = Pool(true, true, true);
            CourseVariantOverride.Forced = 2;
            for (int k = 0; k < 5; k++) Assert.AreEqual(2, pool.Draw(new System.Random(k)));

            CourseVariantOverride.Forced = 9;   // a replay of a bigger pool: deal as usual
            Assert.That(pool.Draw(new System.Random(1)), Is.InRange(0, 2));
        }

        [Test]
        public void Pool_NeverDealsADisabledEntry_AndFallsBackToZeroWhenNoneIsEnabled()
        {
            var pool = Pool(false, true, false, true);
            var rng = new System.Random(5);
            for (int k = 0; k < 40; k++) Assert.That(pool.Draw(rng), Is.EqualTo(1).Or.EqualTo(3));

            Assert.AreEqual(0, Pool(false, false).Draw(new System.Random(2)));
            Assert.AreEqual(0, ScriptableObject.CreateInstance<RegattaCoursePoolSO>().Draw(new System.Random(2)));
        }

        [Test]
        public void Pool_MismatchWith_NamesAnOrderOrCountDifference()
        {
            var pool = Pool(true, true);
            var a = pool.Entries[0].CellConfig;
            var b = pool.Entries[1].CellConfig;
            Assert.IsNotNull(pool.MismatchWith(new[] { b, a }), "a swapped order must be reported");
            Assert.IsNotNull(pool.MismatchWith(new[] { a }), "a short Cell list must be reported");
            // The configs lay no rails here, so even the right order is refused: an entry must be a Regatta arena.
            StringAssert.Contains("SpawnableRegattaRails", pool.MismatchWith(new[] { a, b }));
        }

        // ── The Cell's choice ──────────────────────────────────────────────────────────────

        [Test]
        public void Cell_CourseVariantIndex_IsTheVariant_ZeroWhenUndrawn_ClampedWhenOver()
        {
            Assert.AreEqual(2, Cell.CourseVariantIndexFor(2, 4));
            Assert.AreEqual(0, Cell.CourseVariantIndexFor(-1, 4));
            Assert.AreEqual(3, Cell.CourseVariantIndexFor(7, 4));
            Assert.AreEqual(0, Cell.CourseVariantIndexFor(1, 0));
        }

        // ── The wire: the draw rides the game-config sync ──────────────────────────────────

        [Test]
        public void ConfigSync_CarriesTheCourseVariant_AsAnIntBeforeTheRpcParams()
        {
            var rpc = typeof(MultiplayerMiniGameControllerBase).GetMethod("SyncGameConfigToClients_ClientRpc",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(rpc, "the game-config sync RPC was renamed - the course variant no longer reaches clients");
            var ps = rpc.GetParameters();
            Assert.GreaterOrEqual(ps.Length, 2);
            Assert.AreEqual("courseVariant", ps[ps.Length - 2].Name);
            Assert.AreEqual(typeof(int), ps[ps.Length - 2].ParameterType);
        }

        [Test]
        public void GameData_CourseVariant_StartsUndrawn()
        {
            var data = ScriptableObject.CreateInstance<GameDataSO>();
            Assert.AreEqual(-1, data.CourseVariant);
            Object.DestroyImmediate(data);
        }

        // ── The shipped pool ───────────────────────────────────────────────────────────────

        [Test]
        public void ShippedPool_IsFourDifferentRegattaCircuits()
        {
            var pool = AssetDatabase.LoadAssetAtPath<RegattaCoursePoolSO>(PoolPath);
            Assert.IsNotNull(pool, $"{PoolPath} is missing - run Tools/Build/author_regatta_assets.py");
            Assert.AreEqual(4, pool.Count);

            var firstGates = new List<Vector3>();
            for (int i = 0; i < pool.Count; i++)
            {
                var rails = pool.Entries[i].Rails;
                Assert.IsNotNull(rails, $"entry {i} lays no SpawnableRegattaRails");
                Assert.IsTrue(pool.Entries[i].Enabled, $"entry {i} is disabled");
                var gates = rails.BuildGatesNow();
                Assert.AreEqual(RegattaCourse.RingsPerLap, gates.Count, $"entry {i}: ring count");
                foreach (var seen in firstGates)
                    Assert.Greater((gates[1].Position - seen).magnitude, 1f, $"entry {i} lays a circuit already in the pool");
                firstGates.Add(gates[1].Position);
            }
        }
    }
}
#endif
