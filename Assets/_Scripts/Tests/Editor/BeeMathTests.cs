using System.Collections.Generic;
using NUnit.Framework;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>The bees' pure rules (Docs/ECOSYSTEM.md §66): the comb, the honey store's conservation, the colony's size,
    /// a bee's decision and its flower pick.</summary>
    [TestFixture]
    public class BeeMathTests
    {
        [Test]
        public void TheComb_IsADiscOfDistinctCells_WholeAfterEveryRing()
        {
            Assert.AreEqual(1, BeeMath.CellsInRings(0));
            Assert.AreEqual(7, BeeMath.CellsInRings(1));
            Assert.AreEqual(19, BeeMath.CellsInRings(2));
            Assert.AreEqual(37, BeeMath.CellsInRings(3));
            var seen = new HashSet<(int, int)>();
            for (int k = 0; k < BeeMath.CellsInRings(3); k++)
            {
                BeeMath.SpiralCell(k, out int q, out int r);
                int ring = System.Math.Max(System.Math.Abs(q), System.Math.Max(System.Math.Abs(r), System.Math.Abs(q + r)));
                Assert.IsTrue(seen.Add((q, r)), $"cell {k} repeats ({q}, {r})");
                Assert.AreEqual(k == 0 ? 0 : k < 7 ? 1 : k < 19 ? 2 : 3, ring, $"cell {k} is on the wrong ring");
            }
        }

        [Test]
        public void TheComb_GrowsOutward_NeighbourByNeighbour()
        {
            // every cell after the first touches a cell already laid, so the comb never grows in disconnected lumps
            var laid = new HashSet<(int, int)>();
            int[][] dirs = { new[] { 1, 0 }, new[] { 1, -1 }, new[] { 0, -1 }, new[] { -1, 0 }, new[] { -1, 1 }, new[] { 0, 1 } };
            for (int k = 0; k < BeeMath.CellsInRings(3); k++)
            {
                BeeMath.SpiralCell(k, out int q, out int r);
                if (k > 0)
                {
                    bool touches = false;
                    foreach (var d in dirs) touches |= laid.Contains((q + d[0], r + d[1]));
                    Assert.IsTrue(touches, $"cell {k} ({q}, {r}) touches nothing laid before it");
                }
                laid.Add((q, r));
            }
        }

        [Test]
        public void TheCombsStack_AlternatelyFrontAndBack()
        {
            BeeMath.CellPosition(0, 19, 4.2f, 7f, out _, out _, out float z0);
            BeeMath.CellPosition(19, 19, 4.2f, 7f, out _, out _, out float z1);
            BeeMath.CellPosition(38, 19, 4.2f, 7f, out _, out _, out float z2);
            BeeMath.CellPosition(57, 19, 4.2f, 7f, out _, out _, out float z3);
            Assert.AreEqual(0f, z0);
            Assert.AreEqual(7f, z1);
            Assert.AreEqual(-7f, z2);
            Assert.AreEqual(14f, z3);
        }

        [Test]
        public void TheHoneyStore_NeverMintsMass()
        {
            var store = new BeeMath.HoneyStore();
            store.Deposit(20f);
            store.Deposit(-5f);   // nothing negative is ever deposited
            Assert.IsTrue(store.TrySpend(16f));
            Assert.IsFalse(store.TrySpend(16f), "a cell was built from mass the store does not hold");
            store.Deposit(30f);
            Assert.IsTrue(store.TrySpend(16f));
            store.Refund(16f);    // the cell could not be laid: the spend is given back
            Assert.AreEqual(store.Deposited, store.Spent + store.Stored, 1e-4f, "Deposited == Spent + Stored");
            Assert.AreEqual(50f, store.Deposited, 1e-4f);
            Assert.AreEqual(16f, store.Spent, 1e-4f);
        }

        [Test]
        public void TheLedger_BuildsEachDomainsComb_FromThatDomainsMass()
        {
            // Garrett 2026-11-01: the hive is "a structure made from all three". A cell is paid by ONE domain's mass and laid
            // in that domain, so mass is conserved per domain as well as in total.
            const int ruby = 2, gold = 4;
            var ledger = new BeeMath.HoneyLedger();
            ledger.Deposit(ruby, 20f);
            ledger.Deposit(gold, 10f);
            ledger.Deposit(gold, 9f);
            Assert.IsTrue(ledger.TrySpend(16f, out int first));
            Assert.AreEqual(ruby, first, "the domain holding the most pays first");
            Assert.IsTrue(ledger.TrySpend(16f, out int second));
            Assert.AreEqual(gold, second, "then the next one that can pay alone");
            Assert.IsFalse(ledger.TrySpend(16f, out int none), "no domain holds a whole cell: nothing is mixed into one");
            Assert.AreEqual(-1, none);
            ledger.Refund(gold, 16f);
            Assert.AreEqual(39f, ledger.Deposited, 1e-4f);
            Assert.AreEqual(ledger.Deposited, ledger.Spent + ledger.Stored, 1e-4f, "Deposited == Spent + Stored, in total");
            for (int d = 0; d < BeeMath.HoneyLedger.Slots; d++)
            {
                var s = ledger.Of(d);
                Assert.AreEqual(s.Deposited, s.Spent + s.Stored, 1e-4f, $"domain {d}: Deposited == Spent + Stored");
            }
            ledger.Deposit(9, 50f);   // not a domain: refused
            Assert.AreEqual(39f, ledger.Deposited, 1e-4f);
        }

        [Test]
        public void TheColony_GrowsWithTheComb_WithinItsBounds()
        {
            Assert.AreEqual(3, BeeMath.BeesFor(12, 12, 9, 3, 8), "the seed comb keeps the minimum");
            Assert.AreEqual(3, BeeMath.BeesFor(20, 12, 9, 3, 8));
            Assert.AreEqual(4, BeeMath.BeesFor(21, 12, 9, 3, 8), "one more bee per 9 cells past the seed");
            Assert.AreEqual(8, BeeMath.BeesFor(57, 12, 9, 3, 8), "never above the maximum");
            Assert.AreEqual(0, BeeMath.BeesFor(57, 12, 9, 3, 0));
        }

        [Test]
        public void ABee_GoesHomeFull_RaidsIntruders_ElsePollinates()
        {
            Assert.AreEqual(BeeIntent.Return, BeeMath.Decide(32f, 32f, true, 0, 3), "a full load always goes home");
            Assert.AreEqual(BeeIntent.Raid, BeeMath.Decide(0f, 32f, true, 0, 3));
            Assert.AreEqual(BeeIntent.Raid, BeeMath.Decide(10f, 32f, true, 5, 3), "an intruder beats a finished trip");
            Assert.AreEqual(BeeIntent.Return, BeeMath.Decide(10f, 32f, false, 3, 3), "a trip done with something carried");
            Assert.AreEqual(BeeIntent.Pollinate, BeeMath.Decide(0f, 32f, false, 3, 3), "an empty bee pollinates");
            Assert.AreEqual(BeeIntent.Pollinate, BeeMath.Decide(10f, 32f, false, 1, 3));
            Assert.AreEqual(BeeIntent.Raid, BeeMath.Decide(50f, 0f, true, 0, 3), "no capacity (no hive): never full");
        }

        [Test]
        public void Bees_FanOutOverFlowers()
        {
            Assert.AreEqual(-1, BeeMath.PickIndex(0, 1, 0));
            var picks = new HashSet<int>();
            for (int bee = 0; bee < 32; bee++)
            {
                int i = BeeMath.PickIndex(10, bee * 7919, 0);
                Assert.That(i, Is.InRange(0, 9));
                picks.Add(i);
            }
            Assert.GreaterOrEqual(picks.Count, 6, "32 bees landed on fewer than 6 of 10 flowers");
            Assert.AreEqual(BeeMath.PickIndex(10, 5, 2), BeeMath.PickIndex(10, 5, 2), "deterministic");
        }
    }
}
