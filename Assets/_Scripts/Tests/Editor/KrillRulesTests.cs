using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="KrillRules"/> and <see cref="KrillCardStat.ValueAt"/> against the Krill Lab (<c>Docs/Studios/KrillLab.html</c>:
    /// <c>sv</c>, <c>endPhase</c>, <c>dealHand</c>, <c>pick</c>, <c>phaseLen</c>, <c>capNow</c>, <c>minNow</c>) and the card list
    /// (<c>Docs/Studios/SwarmCardVessel/cards.json</c>). The golden table is GENERATED from cards.json: every number of the
    /// 24 wired cards at levels 1, 5, 10 and 15 as the deck page prints it (its <c>key</c>), so a formula drift on either
    /// side fails here. The phase and hand tests below use a small deck with one wired card per element, so every hand
    /// is known.
    /// </summary>
    [TestFixture]
    public class KrillRulesTests
    {
        readonly struct Golden
        {
            public readonly string Id;
            public readonly int Stat, Decimals, Level;
            public readonly KrillStatKind Kind;
            public readonly float A, B, Want;

            public Golden(string id, int stat, KrillStatKind kind, float a, float b, int decimals, int level, float want)
            {
                Id = id; Stat = stat; Kind = kind; A = a; B = b; Decimals = decimals; Level = level; Want = want;
            }
        }

        static readonly Golden[] Goldens =
        {
            new Golden("mass-mound-builders", 0, KrillStatKind.Linear, 0.5f, 0.25f, 2, 1, 0.5f),   // "0.50/s"
            new Golden("mass-mound-builders", 0, KrillStatKind.Linear, 0.5f, 0.25f, 2, 5, 1.5f),   // "1.50/s"
            new Golden("mass-mound-builders", 0, KrillStatKind.Linear, 0.5f, 0.25f, 2, 10, 2.75f),   // "2.75/s"
            new Golden("mass-mound-builders", 0, KrillStatKind.Linear, 0.5f, 0.25f, 2, 15, 4f),   // "4.00/s"
            new Golden("mass-danger-drip", 0, KrillStatKind.Linear, 0.3f, 0.15f, 2, 1, 0.3f),   // "0.30/s"
            new Golden("mass-danger-drip", 0, KrillStatKind.Linear, 0.3f, 0.15f, 2, 5, 0.9f),   // "0.90/s"
            new Golden("mass-danger-drip", 0, KrillStatKind.Linear, 0.3f, 0.15f, 2, 10, 1.65f),   // "1.65/s"
            new Golden("mass-danger-drip", 0, KrillStatKind.Linear, 0.3f, 0.15f, 2, 15, 2.4f),   // "2.40/s"
            new Golden("mass-carapace", 0, KrillStatKind.Geometric, 12f, 0.88f, 1, 1, 12f),   // "12.0 s"
            new Golden("mass-carapace", 0, KrillStatKind.Geometric, 12f, 0.88f, 1, 5, 7.2f),   // "7.2 s"
            new Golden("mass-carapace", 0, KrillStatKind.Geometric, 12f, 0.88f, 1, 10, 3.8f),   // "3.8 s"
            new Golden("mass-carapace", 0, KrillStatKind.Geometric, 12f, 0.88f, 1, 15, 2f),   // "2.0 s"
            new Golden("mass-brood", 0, KrillStatKind.Geometric, 6f, 0.88f, 1, 1, 6f),   // "6.0 s"
            new Golden("mass-brood", 0, KrillStatKind.Geometric, 6f, 0.88f, 1, 5, 3.6f),   // "3.6 s"
            new Golden("mass-brood", 0, KrillStatKind.Geometric, 6f, 0.88f, 1, 10, 1.9f),   // "1.9 s"
            new Golden("mass-brood", 0, KrillStatKind.Geometric, 6f, 0.88f, 1, 15, 1f),   // "1.0 s"
            new Golden("mass-big-family", 0, KrillStatKind.Linear, 46f, 6f, 0, 1, 46f),   // "46"
            new Golden("mass-big-family", 0, KrillStatKind.Linear, 46f, 6f, 0, 5, 70f),   // "70"
            new Golden("mass-big-family", 0, KrillStatKind.Linear, 46f, 6f, 0, 10, 100f),   // "100"
            new Golden("mass-big-family", 0, KrillStatKind.Linear, 46f, 6f, 0, 15, 130f),   // "130"
            new Golden("mass-big-family", 1, KrillStatKind.Linear, 6f, 0.5f, 0, 1, 6f),   // "6"
            new Golden("mass-big-family", 1, KrillStatKind.Linear, 6f, 0.5f, 0, 5, 8f),   // "8"
            new Golden("mass-big-family", 1, KrillStatKind.Linear, 6f, 0.5f, 0, 10, 10f),   // "10"
            new Golden("mass-big-family", 1, KrillStatKind.Linear, 6f, 0.5f, 0, 15, 13f),   // "13"
            new Golden("mass-snowball", 0, KrillStatKind.Linear, 3f, 0.5f, 1, 1, 3f),   // "3.0 s"
            new Golden("mass-snowball", 0, KrillStatKind.Linear, 3f, 0.5f, 1, 5, 5f),   // "5.0 s"
            new Golden("mass-snowball", 0, KrillStatKind.Linear, 3f, 0.5f, 1, 10, 7.5f),   // "7.5 s"
            new Golden("mass-snowball", 0, KrillStatKind.Linear, 3f, 0.5f, 1, 15, 10f),   // "10.0 s"
            new Golden("mass-snowball", 1, KrillStatKind.Linear, 2f, 1f, 0, 1, 2f),   // "2"
            new Golden("mass-snowball", 1, KrillStatKind.Linear, 2f, 1f, 0, 5, 6f),   // "6"
            new Golden("mass-snowball", 1, KrillStatKind.Linear, 2f, 1f, 0, 10, 11f),   // "11"
            new Golden("mass-snowball", 1, KrillStatKind.Linear, 2f, 1f, 0, 15, 16f),   // "16"
            new Golden("time-quickening", 0, KrillStatKind.Linear, 4f, 4f, 0, 1, 4f),   // "4%"
            new Golden("time-quickening", 0, KrillStatKind.Linear, 4f, 4f, 0, 5, 20f),   // "20%"
            new Golden("time-quickening", 0, KrillStatKind.Linear, 4f, 4f, 0, 10, 40f),   // "40%"
            new Golden("time-quickening", 0, KrillStatKind.Linear, 4f, 4f, 0, 15, 60f),   // "60%"
            new Golden("time-agility", 0, KrillStatKind.Linear, 90f, 15f, 0, 1, 90f),   // "90°/s"
            new Golden("time-agility", 0, KrillStatKind.Linear, 90f, 15f, 0, 5, 150f),   // "150°/s"
            new Golden("time-agility", 0, KrillStatKind.Linear, 90f, 15f, 0, 10, 225f),   // "225°/s"
            new Golden("time-agility", 0, KrillStatKind.Linear, 90f, 15f, 0, 15, 300f),   // "300°/s"
            new Golden("time-dragonfly", 0, KrillStatKind.Linear, 2f, 0.15f, 2, 1, 2f),   // "2.00×"
            new Golden("time-dragonfly", 0, KrillStatKind.Linear, 2f, 0.15f, 2, 5, 2.6f),   // "2.60×"
            new Golden("time-dragonfly", 0, KrillStatKind.Linear, 2f, 0.15f, 2, 10, 3.35f),   // "3.35×"
            new Golden("time-dragonfly", 0, KrillStatKind.Linear, 2f, 0.15f, 2, 15, 4.1f),   // "4.10×"
            new Golden("time-dragonfly", 1, KrillStatKind.Linear, 3f, 0.4f, 1, 1, 3f),   // "3.0 s"
            new Golden("time-dragonfly", 1, KrillStatKind.Linear, 3f, 0.4f, 1, 5, 4.6f),   // "4.6 s"
            new Golden("time-dragonfly", 1, KrillStatKind.Linear, 3f, 0.4f, 1, 10, 6.6f),   // "6.6 s"
            new Golden("time-dragonfly", 1, KrillStatKind.Linear, 3f, 0.4f, 1, 15, 8.6f),   // "8.6 s"
            new Golden("time-skip-ahead", 0, KrillStatKind.Geometric, 24f, 0.89f, 1, 1, 24f),   // "24.0 s"
            new Golden("time-skip-ahead", 0, KrillStatKind.Geometric, 24f, 0.89f, 1, 5, 15.1f),   // "15.1 s"
            new Golden("time-skip-ahead", 0, KrillStatKind.Geometric, 24f, 0.89f, 1, 10, 8.4f),   // "8.4 s"
            new Golden("time-skip-ahead", 0, KrillStatKind.Geometric, 24f, 0.89f, 1, 15, 4.7f),   // "4.7 s"
            new Golden("time-skip-ahead", 1, KrillStatKind.Linear, 300f, 50f, 0, 1, 300f),   // "300 u"
            new Golden("time-skip-ahead", 1, KrillStatKind.Linear, 300f, 50f, 0, 5, 500f),   // "500 u"
            new Golden("time-skip-ahead", 1, KrillStatKind.Linear, 300f, 50f, 0, 10, 750f),   // "750 u"
            new Golden("time-skip-ahead", 1, KrillStatKind.Linear, 300f, 50f, 0, 15, 1000f),   // "1000 u"
            new Golden("time-hurry-up", 0, KrillStatKind.Linear, 3f, 2f, 0, 1, 3f),   // "3%"
            new Golden("time-hurry-up", 0, KrillStatKind.Linear, 3f, 2f, 0, 5, 11f),   // "11%"
            new Golden("time-hurry-up", 0, KrillStatKind.Linear, 3f, 2f, 0, 10, 21f),   // "21%"
            new Golden("time-hurry-up", 0, KrillStatKind.Linear, 3f, 2f, 0, 15, 31f),   // "31%"
            new Golden("time-blink-step", 0, KrillStatKind.Linear, 10f, 4f, 0, 1, 10f),   // "10%"
            new Golden("time-blink-step", 0, KrillStatKind.Linear, 10f, 4f, 0, 5, 26f),   // "26%"
            new Golden("time-blink-step", 0, KrillStatKind.Linear, 10f, 4f, 0, 10, 46f),   // "46%"
            new Golden("time-blink-step", 0, KrillStatKind.Linear, 10f, 4f, 0, 15, 66f),   // "66%"
            new Golden("space-hole-punch", 0, KrillStatKind.Linear, 0.2f, 0.1f, 1, 1, 0.2f),   // "0.2/s"
            new Golden("space-hole-punch", 0, KrillStatKind.Linear, 0.2f, 0.1f, 1, 5, 0.6f),   // "0.6/s"
            new Golden("space-hole-punch", 0, KrillStatKind.Linear, 0.2f, 0.1f, 1, 10, 1.1f),   // "1.1/s"
            new Golden("space-hole-punch", 0, KrillStatKind.Linear, 0.2f, 0.1f, 1, 15, 1.6f),   // "1.6/s"
            new Golden("space-wide-bite", 0, KrillStatKind.Linear, 106f, 6f, 0, 1, 106f),   // "106%"
            new Golden("space-wide-bite", 0, KrillStatKind.Linear, 106f, 6f, 0, 5, 130f),   // "130%"
            new Golden("space-wide-bite", 0, KrillStatKind.Linear, 106f, 6f, 0, 10, 160f),   // "160%"
            new Golden("space-wide-bite", 0, KrillStatKind.Linear, 106f, 6f, 0, 15, 190f),   // "190%"
            new Golden("space-annex", 0, KrillStatKind.Linear, 0.1f, 0.05f, 2, 1, 0.1f),   // "0.10/s"
            new Golden("space-annex", 0, KrillStatKind.Linear, 0.1f, 0.05f, 2, 5, 0.3f),   // "0.30/s"
            new Golden("space-annex", 0, KrillStatKind.Linear, 0.1f, 0.05f, 2, 10, 0.55f),   // "0.55/s"
            new Golden("space-annex", 0, KrillStatKind.Linear, 0.1f, 0.05f, 2, 15, 0.8f),   // "0.80/s"
            new Golden("space-shield-eaters", 0, KrillStatKind.Geometric, 6f, 0.88f, 0, 1, 6f),   // "6"
            new Golden("space-shield-eaters", 0, KrillStatKind.Geometric, 6f, 0.88f, 0, 5, 3f),   // "3"
            new Golden("space-shield-eaters", 0, KrillStatKind.Geometric, 6f, 0.88f, 0, 10, 1f),   // "1"
            new Golden("space-shield-eaters", 0, KrillStatKind.Geometric, 6f, 0.88f, 0, 15, 1f),   // "1"
            new Golden("space-black-hole", 0, KrillStatKind.Linear, 2f, 0.4f, 1, 1, 2f),   // "2.0 s"
            new Golden("space-black-hole", 0, KrillStatKind.Linear, 2f, 0.4f, 1, 5, 3.6f),   // "3.6 s"
            new Golden("space-black-hole", 0, KrillStatKind.Linear, 2f, 0.4f, 1, 10, 5.6f),   // "5.6 s"
            new Golden("space-black-hole", 0, KrillStatKind.Linear, 2f, 0.4f, 1, 15, 7.6f),   // "7.6 s"
            new Golden("space-black-hole", 1, KrillStatKind.Linear, 150f, 25f, 0, 1, 150f),   // "150 u"
            new Golden("space-black-hole", 1, KrillStatKind.Linear, 150f, 25f, 0, 5, 250f),   // "250 u"
            new Golden("space-black-hole", 1, KrillStatKind.Linear, 150f, 25f, 0, 10, 375f),   // "375 u"
            new Golden("space-black-hole", 1, KrillStatKind.Linear, 150f, 25f, 0, 15, 500f),   // "500 u"
            new Golden("charge-lasers", 0, KrillStatKind.Linear, 9f, 6.5f, 0, 1, 9f),   // "9%"
            new Golden("charge-lasers", 0, KrillStatKind.Linear, 9f, 6.5f, 0, 5, 35f),   // "35%"
            new Golden("charge-lasers", 0, KrillStatKind.Linear, 9f, 6.5f, 0, 10, 67f),   // "67%"
            new Golden("charge-lasers", 0, KrillStatKind.Linear, 9f, 6.5f, 0, 15, 100f),   // "100%"
            new Golden("charge-lasers", 1, KrillStatKind.Linear, 120f, 15f, 0, 1, 120f),   // "120 u"
            new Golden("charge-lasers", 1, KrillStatKind.Linear, 120f, 15f, 0, 5, 180f),   // "180 u"
            new Golden("charge-lasers", 1, KrillStatKind.Linear, 120f, 15f, 0, 10, 255f),   // "255 u"
            new Golden("charge-lasers", 1, KrillStatKind.Linear, 120f, 15f, 0, 15, 330f),   // "330 u"
            new Golden("charge-emp", 0, KrillStatKind.Linear, 0.5f, 0.2f, 1, 1, 0.5f),   // "0.5 s"
            new Golden("charge-emp", 0, KrillStatKind.Linear, 0.5f, 0.2f, 1, 5, 1.3f),   // "1.3 s"
            new Golden("charge-emp", 0, KrillStatKind.Linear, 0.5f, 0.2f, 1, 10, 2.3f),   // "2.3 s"
            new Golden("charge-emp", 0, KrillStatKind.Linear, 0.5f, 0.2f, 1, 15, 3.3f),   // "3.3 s"
            new Golden("charge-emp", 1, KrillStatKind.Linear, 120f, 15f, 0, 1, 120f),   // "120 u"
            new Golden("charge-emp", 1, KrillStatKind.Linear, 120f, 15f, 0, 5, 180f),   // "180 u"
            new Golden("charge-emp", 1, KrillStatKind.Linear, 120f, 15f, 0, 10, 255f),   // "255 u"
            new Golden("charge-emp", 1, KrillStatKind.Linear, 120f, 15f, 0, 15, 330f),   // "330 u"
            new Golden("charge-tesla-coil", 0, KrillStatKind.Linear, 1f, 0.5f, 0, 1, 1f),   // "1"
            new Golden("charge-tesla-coil", 0, KrillStatKind.Linear, 1f, 0.5f, 0, 5, 3f),   // "3"
            new Golden("charge-tesla-coil", 0, KrillStatKind.Linear, 1f, 0.5f, 0, 10, 5f),   // "5"
            new Golden("charge-tesla-coil", 0, KrillStatKind.Linear, 1f, 0.5f, 0, 15, 8f),   // "8"
            new Golden("charge-tesla-coil", 1, KrillStatKind.Geometric, 3f, 0.93f, 1, 1, 3f),   // "3.0 s"
            new Golden("charge-tesla-coil", 1, KrillStatKind.Geometric, 3f, 0.93f, 1, 5, 2.2f),   // "2.2 s"
            new Golden("charge-tesla-coil", 1, KrillStatKind.Geometric, 3f, 0.93f, 1, 10, 1.6f),   // "1.6 s"
            new Golden("charge-tesla-coil", 1, KrillStatKind.Geometric, 3f, 0.93f, 1, 15, 1.1f),   // "1.1 s"
            new Golden("charge-short-circuit", 0, KrillStatKind.Geometric, 16f, 0.92f, 0, 1, 16f),   // "16"
            new Golden("charge-short-circuit", 0, KrillStatKind.Geometric, 16f, 0.92f, 0, 5, 11f),   // "11"
            new Golden("charge-short-circuit", 0, KrillStatKind.Geometric, 16f, 0.92f, 0, 10, 7f),   // "7"
            new Golden("charge-short-circuit", 0, KrillStatKind.Geometric, 16f, 0.92f, 0, 15, 4f),   // "4"
            new Golden("charge-short-circuit", 1, KrillStatKind.Linear, 1f, 0.25f, 0, 1, 1f),   // "1"
            new Golden("charge-short-circuit", 1, KrillStatKind.Linear, 1f, 0.25f, 0, 5, 2f),   // "2"
            new Golden("charge-short-circuit", 1, KrillStatKind.Linear, 1f, 0.25f, 0, 10, 3f),   // "3"
            new Golden("charge-short-circuit", 1, KrillStatKind.Linear, 1f, 0.25f, 0, 15, 4f),   // "4"
            new Golden("charge-sting", 0, KrillStatKind.Geometric, 12f, 0.86f, 0, 1, 12f),   // "12"
            new Golden("charge-sting", 0, KrillStatKind.Geometric, 12f, 0.86f, 0, 5, 6f),   // "6"
            new Golden("charge-sting", 0, KrillStatKind.Geometric, 12f, 0.86f, 0, 10, 3f),   // "3"
            new Golden("charge-sting", 0, KrillStatKind.Geometric, 12f, 0.86f, 0, 15, 1f),   // "1"
            new Golden("charge-sting", 1, KrillStatKind.Linear, 80f, 10f, 0, 1, 80f),   // "80 u"
            new Golden("charge-sting", 1, KrillStatKind.Linear, 80f, 10f, 0, 5, 120f),   // "120 u"
            new Golden("charge-sting", 1, KrillStatKind.Linear, 80f, 10f, 0, 10, 170f),   // "170 u"
            new Golden("charge-sting", 1, KrillStatKind.Linear, 80f, 10f, 0, 15, 220f),   // "220 u"
            new Golden("charge-chain-lightning", 0, KrillStatKind.Linear, 1f, 0.5f, 0, 1, 1f),   // "1"
            new Golden("charge-chain-lightning", 0, KrillStatKind.Linear, 1f, 0.5f, 0, 5, 3f),   // "3"
            new Golden("charge-chain-lightning", 0, KrillStatKind.Linear, 1f, 0.5f, 0, 10, 5f),   // "5"
            new Golden("charge-chain-lightning", 0, KrillStatKind.Linear, 1f, 0.5f, 0, 15, 8f),   // "8"
        };

        [Test]
        public void ValueAt_MatchesTheDeckPage_ForEveryWiredCardAtLevels1_5_10_15()
        {
            foreach (var g in Goldens)
            {
                var stat = new KrillCardStat("x", g.Kind, g.A, g.B, "", g.Decimals);
                float tol = 0.5f * Mathf.Pow(10f, -g.Decimals) + 1e-4f;
                Assert.AreEqual(g.Want, stat.ValueAt(g.Level), tol, $"{g.Id} stat {g.Stat} at level {g.Level}");
            }
        }

        [Test]
        public void ValueAt_ACountRoundsDown_AndAStepStatHasNoNumber()
        {
            Assert.AreEqual(6f, new KrillCardStat("Stings per petal", KrillStatKind.Geometric, 12f, 0.86f, "", 0).ValueAt(5), "12 x 0.86^4 = 6.56, a count: 6");
            Assert.IsTrue(float.IsNaN(new KrillCardStat("Swarms", KrillStatKind.Steps, 0f, 0f, "", 0, new[] { "2", "3" }).ValueAt(5)));
        }

        // ---- a known deck: one wired card per element (Big Family, Hurry Up, Lasers, Split) ----

        static KrillConfigSO Config()
        {
            var deck = KrillCardDeckSO.CreateForTests(new List<KrillCard>
            {
                new(KrillCardIds.Lasers, Element.Charge, "Lasers", true,
                    new KrillCardStat("Members armed", KrillStatKind.Linear, 9f, 6.5f, "%", 0),
                    new KrillCardStat("Range", KrillStatKind.Linear, 120f, 15f, "u", 0)),
                new(KrillCardIds.BigFamily, Element.Mass, "Big Family", true,
                    new KrillCardStat("Swarm cap", KrillStatKind.Linear, 46f, 6f, "", 0),
                    new KrillCardStat("Minimum size", KrillStatKind.Linear, 6f, 0.5f, "", 0)),
                new(KrillCardIds.Split, Element.Space, "Split", true,
                    new KrillCardStat("Swarms", KrillStatKind.Steps, 0f, 0f, "", 0, new[] { "2 for 8 s", "2", "3", "4" })),
                new(KrillCardIds.HurryUp, Element.Time, "Hurry Up", true,
                    new KrillCardStat("Phase time cut", KrillStatKind.Linear, 3f, 2f, "%", 0)),
                new(KrillCardIds.Agility, Element.Time, "Agility", false,
                    new KrillCardStat("Turn rate", KrillStatKind.Linear, 90f, 15f, "deg/s", 0)),
            });
            return KrillConfigSO.CreateForTests(deck);
        }

        [Test]
        public void TheCycle_RunsTheFourPhasesInOrder_OnTheirTimers_AndTheOmniPhaseDealsAHand()
        {
            var rules = new KrillRules(Config());
            Assert.AreEqual(KrillPhase.Omnicrystal, rules.Phase);
            Assert.IsFalse(rules.HasHand);
            rules.Tick(29.9f);
            Assert.AreEqual(KrillPhase.Omnicrystal, rules.Phase, "tOmni is 30 s");
            rules.Tick(0.2f);
            Assert.AreEqual(KrillPhase.ElementalCrystals, rules.Phase);
            Assert.IsTrue(rules.HasHand, "the omnicrystal phase deals a hand when it ends, crystal or not");
            Assert.AreEqual(1, rules.HandsDealt);
            rules.Tick(25.01f);
            Assert.AreEqual(KrillPhase.OpposingVessels, rules.Phase, "tElem is 25 s");
            rules.Tick(25.01f);
            Assert.AreEqual(KrillPhase.OpposingMass, rules.Phase);
            rules.Tick(25.01f);
            Assert.AreEqual(KrillPhase.Omnicrystal, rules.Phase, "the cycle goes round");
            Assert.AreEqual(1, rules.HandsDealt, "only the omnicrystal phase deals");
        }

        [Test]
        public void EachPhase_EndsEarlyOnItsGoal()
        {
            var rules = new KrillRules(Config());
            rules.OnOmniCollected();
            Assert.AreEqual(KrillPhase.ElementalCrystals, rules.Phase, "an omnicrystal ends its own phase");
            Assert.IsTrue(rules.HasHand);
            for (int i = 0; i < 4; i++) rules.OnElementalCollected(false);
            rules.Tick(0.01f);
            Assert.AreEqual(KrillPhase.ElementalCrystals, rules.Phase, "Y is 5");
            rules.OnElementalCollected(false);
            rules.Tick(0.01f);
            Assert.AreEqual(KrillPhase.OpposingVessels, rules.Phase);
            rules.OnDebuff();
            rules.Tick(0.01f);
            Assert.AreEqual(KrillPhase.OpposingMass, rules.Phase, "one debuff on a rival ends the vessel phase");
            for (int i = 0; i < 29; i++) rules.OnPrismRemoved();
            rules.Tick(0.01f);
            Assert.AreEqual(KrillPhase.OpposingMass, rules.Phase, "X is 30");
            rules.OnPrismRemoved();
            rules.Tick(0.01f);
            Assert.AreEqual(KrillPhase.Omnicrystal, rules.Phase);
        }

        [Test]
        public void AnOmnicrystalOutsideItsPhase_DealsAHand_AndAnElementalCrystalCountsOnlyInItsPhaseOrWhileSplit()
        {
            var rules = new KrillRules(Config());
            rules.EndPhase(false);
            Assert.AreEqual(KrillPhase.ElementalCrystals, rules.Phase);
            rules.Pick(Element.Mass);
            Assert.IsFalse(rules.HasHand);
            rules.EndPhase(false);
            rules.OnOmniCollected();
            Assert.AreEqual(KrillPhase.OpposingVessels, rules.Phase, "the phase does not move");
            Assert.IsTrue(rules.HasHand, "but a hand is dealt");
            rules.OnElementalCollected(false);
            Assert.AreEqual(0, rules.CollectedThisPhase);
            rules.OnElementalCollected(true);
            Assert.AreEqual(1, rules.CollectedThisPhase, "a split swarm is always on the next objective");
        }

        [Test]
        public void TheHand_IsOneWiredCardPerElement_InTheRowsOrder_AtTheElementsLevel()
        {
            var rules = new KrillRules(Config());
            rules.DealHand();
            Assert.AreEqual(KrillCardIds.Lasers, rules.HandCard(0).Card.Id, "slot 0 is Charge");
            Assert.AreEqual(KrillCardIds.BigFamily, rules.HandCard(1).Card.Id, "slot 1 is Mass");
            Assert.AreEqual(KrillCardIds.Split, rules.HandCard(2).Card.Id, "slot 2 is Space");
            Assert.AreEqual(KrillCardIds.HurryUp, rules.HandCard(3).Card.Id, "slot 3 is Time: Agility is not wired, so it is never dealt");
            Assert.AreEqual(1, rules.HandCard(0).Level, "lvCharge is 1");
        }

        [Test]
        public void APick_AddsTheCardsLevel_CappedAt15_AndSpendsTheHand()
        {
            var rules = new KrillRules(Config());
            Assert.IsFalse(rules.Pick(Element.Charge), "no hand, no pick");
            for (int i = 0; i < 20; i++)
            {
                rules.DealHand();
                Assert.IsTrue(rules.Pick(Element.Charge));
                Assert.IsFalse(rules.HasHand);
            }
            Assert.AreEqual(KrillRules.MaxLevel, rules.Level(KrillCardIds.Lasers));
            Assert.AreEqual(0, rules.Level(KrillCardIds.BigFamily), "the other cards of the hand are gone");
            Assert.AreEqual(15, rules.HeldInElement(Element.Charge));
        }

        [Test]
        public void BigFamily_RaisesTheCapAndTheProtectedMinimum_ByTheLabsBaselines()
        {
            var rules = new KrillRules(Config());
            Assert.AreEqual(40, rules.CapNow);
            Assert.AreEqual(6, rules.MinNow);
            rules.DealHand();
            rules.Pick(Element.Mass);
            Assert.AreEqual(46, rules.CapNow, "level 1: cap 46 - 40 = +6");
            Assert.AreEqual(6, rules.MinNow, "level 1: minimum 6 - 6 = +0");
            for (int i = 0; i < 4; i++) { rules.DealHand(); rules.Pick(Element.Mass); }
            Assert.AreEqual(5, rules.Level(KrillCardIds.BigFamily));
            Assert.AreEqual(70, rules.CapNow, "level 5: 46 + 24 = 70");
            Assert.AreEqual(8, rules.MinNow, "level 5: 6 + 2 = 8");
        }

        [Test]
        public void HurryUp_CutsEveryPhaseTimer_AndSplitMakesSwarmsByLevel()
        {
            var rules = new KrillRules(Config());
            Assert.AreEqual(30f, rules.PhaseLength(KrillPhase.Omnicrystal), 1e-4f);
            rules.DealHand();
            rules.Pick(Element.Time);
            Assert.AreEqual(30f * 0.97f, rules.PhaseLength(KrillPhase.Omnicrystal), 1e-4f, "level 1 cuts 3%");
            Assert.AreEqual(1, rules.SwarmCount(true), "no Split held");
            rules.DealHand();
            rules.Pick(Element.Space);
            Assert.AreEqual(1, rules.SwarmCount(false));
            Assert.AreEqual(2, rules.SwarmCount(true), "below level 5 the split is timed");
            for (int i = 0; i < 4; i++) { rules.DealHand(); rules.Pick(Element.Space); }
            Assert.AreEqual(2, rules.SwarmCount(false), "level 5: the split is permanent");
        }

        [Test]
        public void TheDraws_AreDeterministicFromTheSeed()
        {
            Assert.AreEqual(KrillRules.Hash01(7, 3), KrillRules.Hash01(7, 3));
            Assert.AreNotEqual(KrillRules.Hash01(7, 3), KrillRules.Hash01(7, 4));
            for (uint n = 0; n < 200; n++)
            {
                float v = KrillRules.Hash01(11, n);
                Assert.IsTrue(v >= 0f && v < 1f, $"draw {n} is {v}");
            }
        }
    }
}
