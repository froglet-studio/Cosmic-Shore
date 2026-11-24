#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-attacker petal ledger (<see cref="PetalLedger"/>, <c>Docs/ElementalAbilitySystem/SILENCE.md</c>
    /// §6): score(i) is the petals opposing pilots lose because of pilot i. These pin what counts, that
    /// totals accumulate per attacker and reset per match, and that the score is readable where a match
    /// reads it: <see cref="ScoringMetric.PetalsTaken"/> off <c>IRoundStats.PetalsTaken</c>, and its Charge
    /// genre. The transfer's own credits (strip, steal, danger
    /// burn, the owner-only rule, the client's forward to the server) run in
    /// <c>Tools/Build/elemental_transfer_harness</c> against the shipped <c>ElementalTransfer</c>.
    /// </summary>
    public class PetalLedgerTests
    {
        readonly List<(string name, int petals)> _raised = new();

        void OnCredited(string name, int petals) => _raised.Add((name, petals));

        [SetUp]
        public void SetUp()
        {
            PetalLedger.Reset();
            _raised.Clear();
            PetalLedger.Credited += OnCredited;
        }

        [TearDown]
        public void TearDown()
        {
            PetalLedger.Credited -= OnCredited;
            PetalLedger.Reset();
        }

        [Test]
        public void ATakeOnSomebodyElse_IsCreditedToTheAttacker()
        {
            Assert.IsTrue(PetalLedger.Record("Ava", "Bo", 3));
            Assert.AreEqual(3, PetalLedger.TotalFor("Ava"));
            Assert.AreEqual(0, PetalLedger.TotalFor("Bo"), "the victim earns nothing for losing");
            CollectionAssert.AreEqual(new[] { ("Ava", 3) }, _raised);
        }

        [Test]
        public void TakesAccumulatePerAttacker()
        {
            PetalLedger.Record("Ava", "Bo", 2);
            PetalLedger.Record("Cy", "Bo", 1);
            PetalLedger.Record("Ava", "Cy", 4);

            Assert.AreEqual(6, PetalLedger.TotalFor("Ava"));
            Assert.AreEqual(1, PetalLedger.TotalFor("Cy"));
            Assert.AreEqual(2, PetalLedger.Totals.Count);
        }

        [Test]
        [TestCase("Ava", "Ava", 3, TestName = "a pilot's loss to themselves")]
        [TestCase("Ava", "Bo", 0, TestName = "a take of nothing (warded, empty)")]
        [TestCase("Ava", "Bo", -2, TestName = "a negative count")]
        [TestCase(null, "Bo", 3, TestName = "no attacker")]
        [TestCase("", "Bo", 3, TestName = "an empty attacker")]
        public void WhatDoesNotCount_CreditsNobody(string attacker, string victim, int petals)
        {
            Assert.IsFalse(PetalLedger.Counts(attacker, victim, petals));
            Assert.IsFalse(PetalLedger.Record(attacker, victim, petals));
            Assert.AreEqual(0, PetalLedger.Totals.Count);
            Assert.IsEmpty(_raised, "an uncounted take must not reach StatsManager");
        }

        [Test]
        public void AVictimWithNoName_StillCreditsTheAttacker()
        {
            // A burn off a victim whose pilot record is gone still came off an opponent.
            Assert.IsTrue(PetalLedger.Record("Ava", null, 1));
            Assert.AreEqual(1, PetalLedger.TotalFor("Ava"));
        }

        [Test]
        public void Reset_StartsTheMatchAtZero()
        {
            PetalLedger.Record("Ava", "Bo", 5);
            PetalLedger.Reset();
            Assert.AreEqual(0, PetalLedger.TotalFor("Ava"));
            Assert.AreEqual(0, PetalLedger.Totals.Count);
        }

        [Test]
        public void TotalFor_AnUnknownOrEmptyName_IsZero()
        {
            Assert.AreEqual(0, PetalLedger.TotalFor("nobody"));
            Assert.AreEqual(0, PetalLedger.TotalFor(null));
            Assert.AreEqual(0, PetalLedger.TotalFor(""));
        }

        [Test]
        public void TheScoringMetric_ReadsPetalsTaken()
        {
            var stats = new ScoringRuleFieldedDomainTests.FakeStats { PetalsTaken = 7, CombatPoints = 90 };
            Assert.AreEqual(7, ScoringMetrics.Read(stats, ScoringMetric.PetalsTaken),
                "PetalsTaken must read the ledger's stat, not the combat points it replaces");
        }

        [Test]
        public void TheScoringMetric_IsACombatGenre()
        {
            Assert.IsTrue(ModeGenre.TryElementForMetric(ScoringMetric.PetalsTaken, out var element));
            Assert.AreEqual(Element.Charge, element);
        }

        [Test]
        public void TheNumbersAreClaimed()
        {
            // Serialized in scoring rules: a renumber would re-point every asset.
            Assert.AreEqual(13, (int)ScoringMetric.PetalsTaken);
        }
    }
}
#endif
