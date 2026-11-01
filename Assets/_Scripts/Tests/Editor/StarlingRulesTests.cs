using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="StarlingRules"/> and <see cref="StarlingCardStat.ValueAt"/> against the Starlings Lab (<c>Docs/Studios/StarlingsLab.html</c>:
    /// <c>sv</c>, <c>endPhase</c>, <c>dealHand</c>, <c>pick</c>, <c>phaseLen</c>, <c>capNow</c>, <c>minNow</c>) and the card list
    /// (<c>Docs/Studios/SwarmCardVessel/cards.json</c>). The golden table is GENERATED from cards.json: every number of the
    /// 24 wired cards at levels 1, 5, 10 and 15 as the deck page prints it (its <c>key</c>), so a formula drift on either
    /// side fails here. The phase and hand tests below use a small deck with one wired card per element, so every hand
    /// is known.
    /// </summary>
    [TestFixture]
    public class StarlingRulesTests
    {
        readonly struct Golden
        {
            public readonly string Id;
            public readonly int Stat, Decimals, Level;
            public readonly StarlingStatKind Kind;
            public readonly float A, B, Want;

            public Golden(string id, int stat, StarlingStatKind kind, float a, float b, int decimals, int level, float want)
            {
                Id = id; Stat = stat; Kind = kind; A = a; B = b; Decimals = decimals; Level = level; Want = want;
            }
        }

        static readonly Golden[] Goldens =
        {
            new Golden("mass-mound-builders", 0, StarlingStatKind.Linear, 0.5f, 0.25f, 2, 1, 0.5f),   // "0.50/s"
            new Golden("mass-mound-builders", 0, StarlingStatKind.Linear, 0.5f, 0.25f, 2, 5, 1.5f),   // "1.50/s"
            new Golden("mass-mound-builders", 0, StarlingStatKind.Linear, 0.5f, 0.25f, 2, 10, 2.75f),   // "2.75/s"
            new Golden("mass-mound-builders", 0, StarlingStatKind.Linear, 0.5f, 0.25f, 2, 15, 4f),   // "4.00/s"
            new Golden("mass-danger-drip", 0, StarlingStatKind.Linear, 0.3f, 0.15f, 2, 1, 0.3f),   // "0.30/s"
            new Golden("mass-danger-drip", 0, StarlingStatKind.Linear, 0.3f, 0.15f, 2, 5, 0.9f),   // "0.90/s"
            new Golden("mass-danger-drip", 0, StarlingStatKind.Linear, 0.3f, 0.15f, 2, 10, 1.65f),   // "1.65/s"
            new Golden("mass-danger-drip", 0, StarlingStatKind.Linear, 0.3f, 0.15f, 2, 15, 2.4f),   // "2.40/s"
            new Golden("mass-carapace", 0, StarlingStatKind.Geometric, 12f, 0.88f, 1, 1, 12f),   // "12.0 s"
            new Golden("mass-carapace", 0, StarlingStatKind.Geometric, 12f, 0.88f, 1, 5, 7.2f),   // "7.2 s"
            new Golden("mass-carapace", 0, StarlingStatKind.Geometric, 12f, 0.88f, 1, 10, 3.8f),   // "3.8 s"
            new Golden("mass-carapace", 0, StarlingStatKind.Geometric, 12f, 0.88f, 1, 15, 2f),   // "2.0 s"
            new Golden("mass-brood", 0, StarlingStatKind.Geometric, 6f, 0.88f, 1, 1, 6f),   // "6.0 s"
            new Golden("mass-brood", 0, StarlingStatKind.Geometric, 6f, 0.88f, 1, 5, 3.6f),   // "3.6 s"
            new Golden("mass-brood", 0, StarlingStatKind.Geometric, 6f, 0.88f, 1, 10, 1.9f),   // "1.9 s"
            new Golden("mass-brood", 0, StarlingStatKind.Geometric, 6f, 0.88f, 1, 15, 1f),   // "1.0 s"
            new Golden("mass-big-family", 0, StarlingStatKind.Linear, 46f, 6f, 0, 1, 46f),   // "46"
            new Golden("mass-big-family", 0, StarlingStatKind.Linear, 46f, 6f, 0, 5, 70f),   // "70"
            new Golden("mass-big-family", 0, StarlingStatKind.Linear, 46f, 6f, 0, 10, 100f),   // "100"
            new Golden("mass-big-family", 0, StarlingStatKind.Linear, 46f, 6f, 0, 15, 130f),   // "130"
            new Golden("mass-big-family", 1, StarlingStatKind.Linear, 6f, 0.5f, 0, 1, 6f),   // "6"
            new Golden("mass-big-family", 1, StarlingStatKind.Linear, 6f, 0.5f, 0, 5, 8f),   // "8"
            new Golden("mass-big-family", 1, StarlingStatKind.Linear, 6f, 0.5f, 0, 10, 10f),   // "10"
            new Golden("mass-big-family", 1, StarlingStatKind.Linear, 6f, 0.5f, 0, 15, 13f),   // "13"
            new Golden("mass-snowball", 0, StarlingStatKind.Linear, 3f, 0.5f, 1, 1, 3f),   // "3.0 s"
            new Golden("mass-snowball", 0, StarlingStatKind.Linear, 3f, 0.5f, 1, 5, 5f),   // "5.0 s"
            new Golden("mass-snowball", 0, StarlingStatKind.Linear, 3f, 0.5f, 1, 10, 7.5f),   // "7.5 s"
            new Golden("mass-snowball", 0, StarlingStatKind.Linear, 3f, 0.5f, 1, 15, 10f),   // "10.0 s"
            new Golden("mass-snowball", 1, StarlingStatKind.Linear, 2f, 1f, 0, 1, 2f),   // "2"
            new Golden("mass-snowball", 1, StarlingStatKind.Linear, 2f, 1f, 0, 5, 6f),   // "6"
            new Golden("mass-snowball", 1, StarlingStatKind.Linear, 2f, 1f, 0, 10, 11f),   // "11"
            new Golden("mass-snowball", 1, StarlingStatKind.Linear, 2f, 1f, 0, 15, 16f),   // "16"
            new Golden("time-quickening", 0, StarlingStatKind.Linear, 4f, 4f, 0, 1, 4f),   // "4%"
            new Golden("time-quickening", 0, StarlingStatKind.Linear, 4f, 4f, 0, 5, 20f),   // "20%"
            new Golden("time-quickening", 0, StarlingStatKind.Linear, 4f, 4f, 0, 10, 40f),   // "40%"
            new Golden("time-quickening", 0, StarlingStatKind.Linear, 4f, 4f, 0, 15, 60f),   // "60%"
            new Golden("time-agility", 0, StarlingStatKind.Linear, 90f, 15f, 0, 1, 90f),   // "90°/s"
            new Golden("time-agility", 0, StarlingStatKind.Linear, 90f, 15f, 0, 5, 150f),   // "150°/s"
            new Golden("time-agility", 0, StarlingStatKind.Linear, 90f, 15f, 0, 10, 225f),   // "225°/s"
            new Golden("time-agility", 0, StarlingStatKind.Linear, 90f, 15f, 0, 15, 300f),   // "300°/s"
            new Golden("time-dragonfly", 0, StarlingStatKind.Linear, 2f, 0.15f, 2, 1, 2f),   // "2.00×"
            new Golden("time-dragonfly", 0, StarlingStatKind.Linear, 2f, 0.15f, 2, 5, 2.6f),   // "2.60×"
            new Golden("time-dragonfly", 0, StarlingStatKind.Linear, 2f, 0.15f, 2, 10, 3.35f),   // "3.35×"
            new Golden("time-dragonfly", 0, StarlingStatKind.Linear, 2f, 0.15f, 2, 15, 4.1f),   // "4.10×"
            new Golden("time-dragonfly", 1, StarlingStatKind.Linear, 3f, 0.4f, 1, 1, 3f),   // "3.0 s"
            new Golden("time-dragonfly", 1, StarlingStatKind.Linear, 3f, 0.4f, 1, 5, 4.6f),   // "4.6 s"
            new Golden("time-dragonfly", 1, StarlingStatKind.Linear, 3f, 0.4f, 1, 10, 6.6f),   // "6.6 s"
            new Golden("time-dragonfly", 1, StarlingStatKind.Linear, 3f, 0.4f, 1, 15, 8.6f),   // "8.6 s"
            new Golden("time-skip-ahead", 0, StarlingStatKind.Geometric, 24f, 0.89f, 1, 1, 24f),   // "24.0 s"
            new Golden("time-skip-ahead", 0, StarlingStatKind.Geometric, 24f, 0.89f, 1, 5, 15.1f),   // "15.1 s"
            new Golden("time-skip-ahead", 0, StarlingStatKind.Geometric, 24f, 0.89f, 1, 10, 8.4f),   // "8.4 s"
            new Golden("time-skip-ahead", 0, StarlingStatKind.Geometric, 24f, 0.89f, 1, 15, 4.7f),   // "4.7 s"
            new Golden("time-skip-ahead", 1, StarlingStatKind.Linear, 300f, 50f, 0, 1, 300f),   // "300 u"
            new Golden("time-skip-ahead", 1, StarlingStatKind.Linear, 300f, 50f, 0, 5, 500f),   // "500 u"
            new Golden("time-skip-ahead", 1, StarlingStatKind.Linear, 300f, 50f, 0, 10, 750f),   // "750 u"
            new Golden("time-skip-ahead", 1, StarlingStatKind.Linear, 300f, 50f, 0, 15, 1000f),   // "1000 u"
            new Golden("time-hurry-up", 0, StarlingStatKind.Linear, 3f, 2f, 0, 1, 3f),   // "3%"
            new Golden("time-hurry-up", 0, StarlingStatKind.Linear, 3f, 2f, 0, 5, 11f),   // "11%"
            new Golden("time-hurry-up", 0, StarlingStatKind.Linear, 3f, 2f, 0, 10, 21f),   // "21%"
            new Golden("time-hurry-up", 0, StarlingStatKind.Linear, 3f, 2f, 0, 15, 31f),   // "31%"
            new Golden("time-blink-step", 0, StarlingStatKind.Linear, 10f, 4f, 0, 1, 10f),   // "10%"
            new Golden("time-blink-step", 0, StarlingStatKind.Linear, 10f, 4f, 0, 5, 26f),   // "26%"
            new Golden("time-blink-step", 0, StarlingStatKind.Linear, 10f, 4f, 0, 10, 46f),   // "46%"
            new Golden("time-blink-step", 0, StarlingStatKind.Linear, 10f, 4f, 0, 15, 66f),   // "66%"
            new Golden("space-hole-punch", 0, StarlingStatKind.Linear, 0.2f, 0.1f, 1, 1, 0.2f),   // "0.2/s"
            new Golden("space-hole-punch", 0, StarlingStatKind.Linear, 0.2f, 0.1f, 1, 5, 0.6f),   // "0.6/s"
            new Golden("space-hole-punch", 0, StarlingStatKind.Linear, 0.2f, 0.1f, 1, 10, 1.1f),   // "1.1/s"
            new Golden("space-hole-punch", 0, StarlingStatKind.Linear, 0.2f, 0.1f, 1, 15, 1.6f),   // "1.6/s"
            new Golden("space-wide-bite", 0, StarlingStatKind.Linear, 106f, 6f, 0, 1, 106f),   // "106%"
            new Golden("space-wide-bite", 0, StarlingStatKind.Linear, 106f, 6f, 0, 5, 130f),   // "130%"
            new Golden("space-wide-bite", 0, StarlingStatKind.Linear, 106f, 6f, 0, 10, 160f),   // "160%"
            new Golden("space-wide-bite", 0, StarlingStatKind.Linear, 106f, 6f, 0, 15, 190f),   // "190%"
            new Golden("space-annex", 0, StarlingStatKind.Linear, 0.1f, 0.05f, 2, 1, 0.1f),   // "0.10/s"
            new Golden("space-annex", 0, StarlingStatKind.Linear, 0.1f, 0.05f, 2, 5, 0.3f),   // "0.30/s"
            new Golden("space-annex", 0, StarlingStatKind.Linear, 0.1f, 0.05f, 2, 10, 0.55f),   // "0.55/s"
            new Golden("space-annex", 0, StarlingStatKind.Linear, 0.1f, 0.05f, 2, 15, 0.8f),   // "0.80/s"
            new Golden("space-shield-eaters", 0, StarlingStatKind.Geometric, 6f, 0.88f, 0, 1, 6f),   // "6"
            new Golden("space-shield-eaters", 0, StarlingStatKind.Geometric, 6f, 0.88f, 0, 5, 3f),   // "3"
            new Golden("space-shield-eaters", 0, StarlingStatKind.Geometric, 6f, 0.88f, 0, 10, 1f),   // "1"
            new Golden("space-shield-eaters", 0, StarlingStatKind.Geometric, 6f, 0.88f, 0, 15, 1f),   // "1"
            new Golden("space-black-hole", 0, StarlingStatKind.Linear, 2f, 0.4f, 1, 1, 2f),   // "2.0 s"
            new Golden("space-black-hole", 0, StarlingStatKind.Linear, 2f, 0.4f, 1, 5, 3.6f),   // "3.6 s"
            new Golden("space-black-hole", 0, StarlingStatKind.Linear, 2f, 0.4f, 1, 10, 5.6f),   // "5.6 s"
            new Golden("space-black-hole", 0, StarlingStatKind.Linear, 2f, 0.4f, 1, 15, 7.6f),   // "7.6 s"
            new Golden("space-black-hole", 1, StarlingStatKind.Linear, 150f, 25f, 0, 1, 150f),   // "150 u"
            new Golden("space-black-hole", 1, StarlingStatKind.Linear, 150f, 25f, 0, 5, 250f),   // "250 u"
            new Golden("space-black-hole", 1, StarlingStatKind.Linear, 150f, 25f, 0, 10, 375f),   // "375 u"
            new Golden("space-black-hole", 1, StarlingStatKind.Linear, 150f, 25f, 0, 15, 500f),   // "500 u"
            new Golden("charge-lasers", 0, StarlingStatKind.Linear, 9f, 6.5f, 0, 1, 9f),   // "9%"
            new Golden("charge-lasers", 0, StarlingStatKind.Linear, 9f, 6.5f, 0, 5, 35f),   // "35%"
            new Golden("charge-lasers", 0, StarlingStatKind.Linear, 9f, 6.5f, 0, 10, 67f),   // "67%"
            new Golden("charge-lasers", 0, StarlingStatKind.Linear, 9f, 6.5f, 0, 15, 100f),   // "100%"
            new Golden("charge-lasers", 1, StarlingStatKind.Linear, 120f, 15f, 0, 1, 120f),   // "120 u"
            new Golden("charge-lasers", 1, StarlingStatKind.Linear, 120f, 15f, 0, 5, 180f),   // "180 u"
            new Golden("charge-lasers", 1, StarlingStatKind.Linear, 120f, 15f, 0, 10, 255f),   // "255 u"
            new Golden("charge-lasers", 1, StarlingStatKind.Linear, 120f, 15f, 0, 15, 330f),   // "330 u"
            new Golden("charge-emp", 0, StarlingStatKind.Linear, 0.5f, 0.2f, 1, 1, 0.5f),   // "0.5 s"
            new Golden("charge-emp", 0, StarlingStatKind.Linear, 0.5f, 0.2f, 1, 5, 1.3f),   // "1.3 s"
            new Golden("charge-emp", 0, StarlingStatKind.Linear, 0.5f, 0.2f, 1, 10, 2.3f),   // "2.3 s"
            new Golden("charge-emp", 0, StarlingStatKind.Linear, 0.5f, 0.2f, 1, 15, 3.3f),   // "3.3 s"
            new Golden("charge-emp", 1, StarlingStatKind.Linear, 120f, 15f, 0, 1, 120f),   // "120 u"
            new Golden("charge-emp", 1, StarlingStatKind.Linear, 120f, 15f, 0, 5, 180f),   // "180 u"
            new Golden("charge-emp", 1, StarlingStatKind.Linear, 120f, 15f, 0, 10, 255f),   // "255 u"
            new Golden("charge-emp", 1, StarlingStatKind.Linear, 120f, 15f, 0, 15, 330f),   // "330 u"
            new Golden("charge-tesla-coil", 0, StarlingStatKind.Linear, 1f, 0.5f, 0, 1, 1f),   // "1"
            new Golden("charge-tesla-coil", 0, StarlingStatKind.Linear, 1f, 0.5f, 0, 5, 3f),   // "3"
            new Golden("charge-tesla-coil", 0, StarlingStatKind.Linear, 1f, 0.5f, 0, 10, 5f),   // "5"
            new Golden("charge-tesla-coil", 0, StarlingStatKind.Linear, 1f, 0.5f, 0, 15, 8f),   // "8"
            new Golden("charge-tesla-coil", 1, StarlingStatKind.Geometric, 3f, 0.93f, 1, 1, 3f),   // "3.0 s"
            new Golden("charge-tesla-coil", 1, StarlingStatKind.Geometric, 3f, 0.93f, 1, 5, 2.2f),   // "2.2 s"
            new Golden("charge-tesla-coil", 1, StarlingStatKind.Geometric, 3f, 0.93f, 1, 10, 1.6f),   // "1.6 s"
            new Golden("charge-tesla-coil", 1, StarlingStatKind.Geometric, 3f, 0.93f, 1, 15, 1.1f),   // "1.1 s"
            new Golden("charge-short-circuit", 0, StarlingStatKind.Geometric, 16f, 0.92f, 0, 1, 16f),   // "16"
            new Golden("charge-short-circuit", 0, StarlingStatKind.Geometric, 16f, 0.92f, 0, 5, 11f),   // "11"
            new Golden("charge-short-circuit", 0, StarlingStatKind.Geometric, 16f, 0.92f, 0, 10, 7f),   // "7"
            new Golden("charge-short-circuit", 0, StarlingStatKind.Geometric, 16f, 0.92f, 0, 15, 4f),   // "4"
            new Golden("charge-short-circuit", 1, StarlingStatKind.Linear, 1f, 0.25f, 0, 1, 1f),   // "1"
            new Golden("charge-short-circuit", 1, StarlingStatKind.Linear, 1f, 0.25f, 0, 5, 2f),   // "2"
            new Golden("charge-short-circuit", 1, StarlingStatKind.Linear, 1f, 0.25f, 0, 10, 3f),   // "3"
            new Golden("charge-short-circuit", 1, StarlingStatKind.Linear, 1f, 0.25f, 0, 15, 4f),   // "4"
            new Golden("charge-sting", 0, StarlingStatKind.Geometric, 12f, 0.86f, 0, 1, 12f),   // "12"
            new Golden("charge-sting", 0, StarlingStatKind.Geometric, 12f, 0.86f, 0, 5, 6f),   // "6"
            new Golden("charge-sting", 0, StarlingStatKind.Geometric, 12f, 0.86f, 0, 10, 3f),   // "3"
            new Golden("charge-sting", 0, StarlingStatKind.Geometric, 12f, 0.86f, 0, 15, 1f),   // "1"
            new Golden("charge-sting", 1, StarlingStatKind.Linear, 80f, 10f, 0, 1, 80f),   // "80 u"
            new Golden("charge-sting", 1, StarlingStatKind.Linear, 80f, 10f, 0, 5, 120f),   // "120 u"
            new Golden("charge-sting", 1, StarlingStatKind.Linear, 80f, 10f, 0, 10, 170f),   // "170 u"
            new Golden("charge-sting", 1, StarlingStatKind.Linear, 80f, 10f, 0, 15, 220f),   // "220 u"
            new Golden("charge-chain-lightning", 0, StarlingStatKind.Linear, 1f, 0.5f, 0, 1, 1f),   // "1"
            new Golden("charge-chain-lightning", 0, StarlingStatKind.Linear, 1f, 0.5f, 0, 5, 3f),   // "3"
            new Golden("charge-chain-lightning", 0, StarlingStatKind.Linear, 1f, 0.5f, 0, 10, 5f),   // "5"
            new Golden("charge-chain-lightning", 0, StarlingStatKind.Linear, 1f, 0.5f, 0, 15, 8f),   // "8"
        };

        [Test]
        public void ValueAt_MatchesTheDeckPage_ForEveryWiredCardAtLevels1_5_10_15()
        {
            foreach (var g in Goldens)
            {
                var stat = new StarlingCardStat("x", g.Kind, g.A, g.B, "", g.Decimals);
                float tol = 0.5f * Mathf.Pow(10f, -g.Decimals) + 1e-4f;
                Assert.AreEqual(g.Want, stat.ValueAt(g.Level), tol, $"{g.Id} stat {g.Stat} at level {g.Level}");
            }
        }

        [Test]
        public void ValueAt_ACountRoundsDown_AndAStepStatHasNoNumber()
        {
            Assert.AreEqual(6f, new StarlingCardStat("Stings per petal", StarlingStatKind.Geometric, 12f, 0.86f, "", 0).ValueAt(5), "12 x 0.86^4 = 6.56, a count: 6");
            Assert.IsTrue(float.IsNaN(new StarlingCardStat("Swarms", StarlingStatKind.Steps, 0f, 0f, "", 0, new[] { "2", "3" }).ValueAt(5)));
        }

        // ---- a known deck: one wired card per element (Big Family, Hurry Up, Lasers, Split) ----

        static StarlingConfigSO Config()
        {
            var deck = StarlingCardDeckSO.CreateForTests(new List<StarlingCard>
            {
                new(StarlingCardIds.Lasers, Element.Charge, "Lasers", true,
                    new StarlingCardStat("Members armed", StarlingStatKind.Linear, 9f, 6.5f, "%", 0),
                    new StarlingCardStat("Range", StarlingStatKind.Linear, 120f, 15f, "u", 0)),
                new(StarlingCardIds.BigFamily, Element.Mass, "Big Family", true,
                    new StarlingCardStat("Swarm cap", StarlingStatKind.Linear, 46f, 6f, "", 0),
                    new StarlingCardStat("Minimum size", StarlingStatKind.Linear, 6f, 0.5f, "", 0)),
                new(StarlingCardIds.Split, Element.Space, "Split", true,
                    new StarlingCardStat("Swarms", StarlingStatKind.Steps, 0f, 0f, "", 0, new[] { "2 for 8 s", "2", "3", "4" })),
                new(StarlingCardIds.HurryUp, Element.Time, "Hurry Up", true,
                    new StarlingCardStat("Phase time cut", StarlingStatKind.Linear, 3f, 2f, "%", 0)),
                new(StarlingCardIds.Agility, Element.Time, "Agility", false,
                    new StarlingCardStat("Turn rate", StarlingStatKind.Linear, 90f, 15f, "deg/s", 0)),
            });
            return StarlingConfigSO.CreateForTests(deck);
        }

        [Test]
        public void TheCycle_RunsTheFourPhasesInOrder_OnTheirTimers_AndTheOmniPhaseDealsAHand()
        {
            var rules = new StarlingRules(Config());
            Assert.AreEqual(StarlingPhase.Omnicrystal, rules.Phase);
            Assert.IsFalse(rules.HasHand);
            rules.Tick(29.9f);
            Assert.AreEqual(StarlingPhase.Omnicrystal, rules.Phase, "tOmni is 30 s");
            rules.Tick(0.2f);
            Assert.AreEqual(StarlingPhase.ElementalCrystals, rules.Phase);
            Assert.IsTrue(rules.HasHand, "the omnicrystal phase deals a hand when it ends, crystal or not");
            Assert.AreEqual(1, rules.HandsDealt);
            rules.Tick(25.01f);
            Assert.AreEqual(StarlingPhase.OpposingVessels, rules.Phase, "tElem is 25 s");
            rules.Tick(25.01f);
            Assert.AreEqual(StarlingPhase.OpposingMass, rules.Phase);
            rules.Tick(25.01f);
            Assert.AreEqual(StarlingPhase.Omnicrystal, rules.Phase, "the cycle goes round");
            Assert.AreEqual(1, rules.HandsDealt, "only the omnicrystal phase deals");
        }

        [Test]
        public void EachPhase_EndsEarlyOnItsGoal()
        {
            var rules = new StarlingRules(Config());
            rules.OnOmniCollected();
            Assert.AreEqual(StarlingPhase.ElementalCrystals, rules.Phase, "an omnicrystal ends its own phase");
            Assert.IsTrue(rules.HasHand);
            for (int i = 0; i < 4; i++) rules.OnElementalCollected(false);
            rules.Tick(0.01f);
            Assert.AreEqual(StarlingPhase.ElementalCrystals, rules.Phase, "Y is 5");
            rules.OnElementalCollected(false);
            rules.Tick(0.01f);
            Assert.AreEqual(StarlingPhase.OpposingVessels, rules.Phase);
            rules.OnDebuff();
            rules.Tick(0.01f);
            Assert.AreEqual(StarlingPhase.OpposingMass, rules.Phase, "one debuff on a rival ends the vessel phase");
            for (int i = 0; i < 29; i++) rules.OnPrismRemoved();
            rules.Tick(0.01f);
            Assert.AreEqual(StarlingPhase.OpposingMass, rules.Phase, "X is 30");
            rules.OnPrismRemoved();
            rules.Tick(0.01f);
            Assert.AreEqual(StarlingPhase.Omnicrystal, rules.Phase);
        }

        [Test]
        public void AnOmnicrystalOutsideItsPhase_DealsAHand_AndAnElementalCrystalCountsOnlyInItsPhaseOrWhileSplit()
        {
            var rules = new StarlingRules(Config());
            rules.EndPhase(false);
            Assert.AreEqual(StarlingPhase.ElementalCrystals, rules.Phase);
            rules.Pick(Element.Mass);
            Assert.IsFalse(rules.HasHand);
            rules.EndPhase(false);
            rules.OnOmniCollected();
            Assert.AreEqual(StarlingPhase.OpposingVessels, rules.Phase, "the phase does not move");
            Assert.IsTrue(rules.HasHand, "but a hand is dealt");
            rules.OnElementalCollected(false);
            Assert.AreEqual(0, rules.CollectedThisPhase);
            rules.OnElementalCollected(true);
            Assert.AreEqual(1, rules.CollectedThisPhase, "a split swarm is always on the next objective");
        }

        [Test]
        public void TheHand_IsOneWiredCardPerElement_InTheRowsOrder_AtTheElementsLevel()
        {
            var rules = new StarlingRules(Config());
            rules.DealHand();
            Assert.AreEqual(StarlingCardIds.Lasers, rules.HandCard(0).Card.Id, "slot 0 is Charge");
            Assert.AreEqual(StarlingCardIds.BigFamily, rules.HandCard(1).Card.Id, "slot 1 is Mass");
            Assert.AreEqual(StarlingCardIds.Split, rules.HandCard(2).Card.Id, "slot 2 is Space");
            Assert.AreEqual(StarlingCardIds.HurryUp, rules.HandCard(3).Card.Id, "slot 3 is Time: Agility is not wired, so it is never dealt");
            Assert.AreEqual(1, rules.HandCard(0).Level, "lvCharge is 1");
        }

        [Test]
        public void APick_AddsTheCardsLevel_CappedAt15_AndSpendsTheHand()
        {
            var rules = new StarlingRules(Config());
            Assert.IsFalse(rules.Pick(Element.Charge), "no hand, no pick");
            rules.DealHand();
            Assert.IsTrue(rules.Pick(Element.Charge), "the first hand is the spread hand");
            Assert.IsFalse(rules.HasHand);
            for (int i = 1; i < 20; i++)
            {
                rules.DealHand();
                Assert.IsTrue(rules.PickFocus(), "every later hand is a Charge hand");
                Assert.IsFalse(rules.HasHand);
            }
            Assert.AreEqual(StarlingRules.MaxLevel, rules.Level(StarlingCardIds.Lasers));
            Assert.AreEqual(0, rules.Level(StarlingCardIds.BigFamily), "the other cards of the hand are gone");
            Assert.AreEqual(15, rules.HeldInElement(Element.Charge));
        }

        [Test]
        public void BigFamily_RaisesTheCapAndTheProtectedMinimum_ByTheLabsBaselines()
        {
            var rules = new StarlingRules(Config());
            Assert.AreEqual(200, rules.CapNow);
            Assert.AreEqual(30, rules.MinNow);
            Take(rules, Element.Mass);
            Assert.AreEqual(206, rules.CapNow, "level 1: cap 200 + (46 - 40) = +6");
            Assert.AreEqual(30, rules.MinNow, "level 1: minimum 30 + (6 - 6) = +0");
            for (int i = 0; i < 4; i++) Take(rules, Element.Mass);
            Assert.AreEqual(5, rules.Level(StarlingCardIds.BigFamily));
            Assert.AreEqual(230, rules.CapNow, "level 5: 200 + (70 - 40) = 230");
            Assert.AreEqual(32, rules.MinNow, "level 5: 30 + (8 - 6) = 32");
        }

        [Test]
        public void HurryUp_CutsEveryPhaseTimer_AndSplitMakesSwarmsByLevel()
        {
            var rules = new StarlingRules(Config());
            Assert.AreEqual(30f, rules.PhaseLength(StarlingPhase.Omnicrystal), 1e-4f);
            Take(rules, Element.Time);
            Assert.AreEqual(30f * 0.97f, rules.PhaseLength(StarlingPhase.Omnicrystal), 1e-4f, "level 1 cuts 3%");
            Assert.AreEqual(1, rules.SwarmCount(true), "no Split held");
            Take(rules, Element.Space);
            Assert.AreEqual(1, rules.SwarmCount(false));
            Assert.AreEqual(2, rules.SwarmCount(true), "below level 5 the split is timed");
            for (int i = 0; i < 4; i++) Take(rules, Element.Space);
            Assert.AreEqual(2, rules.SwarmCount(false), "level 5: the split is permanent");
        }

        /// <summary>Draw a hand of <paramref name="element"/> and take its first card (the known deck wires one card per element).</summary>
        static void Take(StarlingRules rules, Element element)
        {
            Assert.IsTrue(rules.ChooseDrawElement(element));
            rules.DealHand();
            Assert.IsTrue(rules.PickAt(0));
        }

        // ---- the draw element (Garrett, 2026-11-01) ----

        /// <summary>A deck with six wired Time cards and one of each other element (the shipped deck wires six per element).</summary>
        static StarlingConfigSO TimeHeavyConfig()
        {
            var cards = new List<StarlingCard>
            {
                new(StarlingCardIds.Lasers, Element.Charge, "Lasers", true),
                new(StarlingCardIds.BigFamily, Element.Mass, "Big Family", true),
                new(StarlingCardIds.Split, Element.Space, "Split", true),
            };
            foreach (var id in new[] { StarlingCardIds.Quickening, StarlingCardIds.Agility, StarlingCardIds.Dragonfly,
                                       StarlingCardIds.SkipAhead, StarlingCardIds.HurryUp, StarlingCardIds.BlinkStep })
                cards.Add(new StarlingCard(id, Element.Time, id, true));
            return StarlingConfigSO.CreateForTests(StarlingCardDeckSO.CreateForTests(cards));
        }

        [Test]
        public void TheFirstPick_SetsTheDrawElement_AndEveryLaterHandIsFiveDifferentCardsOfIt()
        {
            var rules = new StarlingRules(TimeHeavyConfig());
            Assert.AreEqual(Element.None, rules.DrawElement);
            rules.DealHand();
            Assert.IsFalse(rules.IsElementHand, "no draw element yet: the spread hand");
            Assert.AreEqual(4, rules.HandSize);
            Assert.IsTrue(rules.Pick(Element.Time));
            Assert.AreEqual(Element.Time, rules.DrawElement, "nothing chosen: it keeps the picked card's element");

            for (int hand = 0; hand < 20; hand++)
            {
                rules.DealHand();
                Assert.IsTrue(rules.IsElementHand);
                Assert.AreEqual(Element.Time, rules.HandElement);
                Assert.AreEqual(StarlingRules.ElementHandSize, rules.HandSize, "five of the six wired Time cards");
                var seen = new HashSet<string>();
                for (int i = 0; i < rules.HandSize; i++)
                {
                    var card = rules.HandCard(i);
                    Assert.IsFalse(card.IsEmpty);
                    Assert.AreEqual(Element.Time, card.Card.Element);
                    Assert.AreEqual(1, card.Level, "at the element's level");
                    Assert.IsTrue(seen.Add(card.Card.Id), $"{card.Card.Id} offered twice");
                }
                Assert.IsTrue(rules.HandCard(StarlingRules.ElementHandSize).IsEmpty);
                Assert.IsFalse(rules.Pick(Element.Time), "an element hand is not picked by element");
                Assert.IsTrue(rules.PickFocus());
                Assert.AreEqual(Element.Time, rules.DrawElement, "nothing chosen: it keeps the old one");
            }
        }

        [Test]
        public void TheDrawElement_IsChosenOnlyWithNoHandUp_AndAnElementWithFewerWiredCardsOffersWhatItHas()
        {
            var rules = new StarlingRules(TimeHeavyConfig());
            Assert.IsTrue(rules.ChooseDrawElement(Element.Mass), "it can be chosen before the first hand");
            Assert.IsFalse(rules.ChooseDrawElement(Element.None));
            rules.EndPhase(false);   // the omnicrystal phase's timeout draw
            Assert.IsTrue(rules.IsElementHand);
            Assert.AreEqual(1, rules.HandSize, "Mass wires one card here");
            Assert.AreEqual(StarlingCardIds.BigFamily, rules.HandCard(0).Card.Id);
            Assert.IsFalse(rules.ChooseDrawElement(Element.Space), "pick from the hand up first");
            Assert.IsTrue(rules.PickAt(0));
            Assert.IsTrue(rules.ChooseDrawElement(Element.Space));
            rules.OnOmniCollected();   // an omnicrystal draw outside its phase
            Assert.AreEqual(Element.Space, rules.HandElement);
            Assert.AreEqual(StarlingCardIds.Split, rules.HandCard(0).Card.Id);
        }

        [Test]
        public void TheFocus_WrapsAlongTheHand_AndPickingTakesTheHighlightedCard()
        {
            var rules = new StarlingRules(TimeHeavyConfig());
            rules.ChooseDrawElement(Element.Time);
            rules.DealHand();
            Assert.AreEqual(0, rules.Focus);
            rules.MoveFocus(-1);
            Assert.AreEqual(4, rules.Focus, "left from the first card wraps to the last");
            rules.MoveFocus(1);
            rules.MoveFocus(1);
            rules.MoveFocus(1);
            Assert.AreEqual(2, rules.Focus);
            string id = rules.HandCard(2).Card.Id;
            Assert.IsTrue(rules.PickFocus());
            Assert.AreEqual(1, rules.Level(id));
            Assert.AreEqual(1, rules.HeldInElement(Element.Time), "only the highlighted card was taken");
            rules.DealHand();
            Assert.AreEqual(0, rules.Focus, "a new hand starts on its first card");
        }

        [Test]
        public void ANewRun_ForgetsTheDrawElement()
        {
            var rules = new StarlingRules(TimeHeavyConfig());
            rules.ChooseDrawElement(Element.Charge);
            rules.Reset(3);
            Assert.AreEqual(Element.None, rules.DrawElement);
            rules.DealHand();
            Assert.IsFalse(rules.IsElementHand);
        }

        [Test]
        public void TheDraws_AreDeterministicFromTheSeed()
        {
            Assert.AreEqual(StarlingRules.Hash01(7, 3), StarlingRules.Hash01(7, 3));
            Assert.AreNotEqual(StarlingRules.Hash01(7, 3), StarlingRules.Hash01(7, 4));
            for (uint n = 0; n < 200; n++)
            {
                float v = StarlingRules.Hash01(11, n);
                Assert.IsTrue(v >= 0f && v < 1f, $"draw {n} is {v}");
            }
        }
            // ---- the pilot's controls (Garrett, 2026-10-21) ----

        [Test]
        public void Diffs_RestInsideTheDeadBand_AndRampToFull()
        {
            Assert.AreEqual(0f, StarlingPilotControls.SpreadAxis(0.5f, 0.12f), 1e-6f, "a resting pad (x-diff 0.5) zooms");
            Assert.AreEqual(0f, StarlingPilotControls.SpreadAxis(0.52f, 0.12f), 1e-6f, "a hair of spread zooms");
            Assert.AreEqual(1f, StarlingPilotControls.SpreadAxis(1f, 0.12f), 1e-6f);
            Assert.AreEqual(-1f, StarlingPilotControls.SpreadAxis(0f, 0.12f), 1e-6f);
            Assert.AreEqual(0.5f, StarlingPilotControls.Axis(0.56f, 0.12f), 1e-4f);
        }

        [Test]
        public void Spread_ZoomsIn_Squeeze_ZoomsOut_WithinTheBounds()
        {
            const float neutral = -1200f;
            float inward = StarlingPilotControls.Zoomed(neutral, neutral, 1f, 1.2f, 0.1f, 0.35f, 2.5f);
            float outward = StarlingPilotControls.Zoomed(neutral, neutral, -1f, 1.2f, 0.1f, 0.35f, 2.5f);
            Assert.Greater(inward, neutral, "spread apart should bring the camera closer (less negative)");
            Assert.Less(outward, neutral, "squeezed should push the camera away");
            Assert.AreEqual(neutral * 0.35f, StarlingPilotControls.Zoomed(neutral, neutral, 1f, 1.2f, 100f, 0.35f, 2.5f), 1e-3f);
            Assert.AreEqual(neutral * 2.5f, StarlingPilotControls.Zoomed(neutral, neutral, -1f, 1.2f, 100f, 0.35f, 2.5f), 1e-3f);
        }

        [Test]
        public void Pinch_IsOneToOne_AndSpin_IsTheFingersAngle()
        {
            const float neutral = -1200f;
            Assert.AreEqual(-600f, StarlingPilotControls.Pinched(neutral, neutral, 100f, 200f, 0.35f, 2.5f), 1e-3f,
                "fingers twice as far apart halve the distance");
            Assert.AreEqual(-2400f, StarlingPilotControls.Pinched(neutral, neutral, 200f, 100f, 0.35f, 2.5f), 1e-3f);
            Assert.AreEqual(90f, StarlingPilotControls.Spun(1f, 0f, 0f, 1f), 1e-3f, "a quarter turn counter-clockwise");
            Assert.AreEqual(-90f, StarlingPilotControls.Spun(1f, 0f, 0f, -1f), 1e-3f);
        }

        [Test]
        public void AQuickPress_Picks_OnRelease()
        {
            var press = new StarlingPickPress();
            press.Begin(Element.Mass, 0f, triggerHeld: false);
            press.Tick(0.2f, false, 0.45f);
            Assert.AreEqual(Element.None, press.Details);
            Assert.IsTrue(press.End(Element.Mass), "a quick press picks");
            Assert.AreEqual(Element.None, press.Pressed);
        }

        [Test]
        public void ALongPress_ShowsDetails_AndPicksNothing()
        {
            var press = new StarlingPickPress();
            press.Begin(Element.Time, 0f, triggerHeld: false);
            press.Tick(0.5f, false, 0.45f);
            Assert.AreEqual(Element.Time, press.Details, "a long press opens the card's details");
            Assert.IsFalse(press.End(Element.Time), "letting go of a long press picks nothing");
            Assert.AreEqual(Element.None, press.Details);
        }

        [Test]
        public void ATriggerHeld_ShowsDetails_AtOnce()
        {
            var press = new StarlingPickPress();
            press.Begin(Element.Charge, 0f, triggerHeld: true);
            Assert.AreEqual(Element.Charge, press.Details);
            Assert.IsFalse(press.End(Element.Charge));

            press.Begin(Element.Space, 1f, triggerHeld: false);
            press.Tick(1.05f, true, 0.45f);
            Assert.AreEqual(Element.Space, press.Details, "pulling a trigger mid-press opens the details too");
            Assert.IsFalse(press.End(Element.Space));
        }

        [Test]
        public void AReleaseOfAnotherButton_DoesNotPick()
        {
            var press = new StarlingPickPress();
            press.Begin(Element.Mass, 0f, false);
            Assert.IsFalse(press.End(Element.Time));
            Assert.IsTrue(press.End(Element.Mass));
        }

        [Test]
        public void OpenDetails_TurnToAnotherElement_AndPickNothing()
        {
            var press = new StarlingPickPress();
            press.Begin(Element.Mass, 0f, false);
            press.Tick(0.5f, false, 0.45f);
            Assert.AreEqual(Element.Mass, press.Details);

            press.Begin(Element.Charge, 0.6f, false);
            Assert.AreEqual(Element.Charge, press.Details, "another button pressed while the details are open turns them");
            Assert.IsFalse(press.End(Element.Charge), "browsing picks nothing");
            Assert.AreEqual(Element.Mass, press.Details, "the details return to the button still held");

            press.Begin(Element.Time, 0.7f, false);
            Assert.IsFalse(press.End(Element.Mass));
            Assert.AreEqual(Element.Time, press.Details, "the details stay open while any pick button is down");
            Assert.IsFalse(press.End(Element.Time));
            Assert.AreEqual(Element.None, press.Details, "they close with the last button");

            press.Begin(Element.Space, 1f, false);
            Assert.IsTrue(press.End(Element.Space), "after browsing, a quick press picks again");
        }

        [Test]
        public void CameraRoll_TurnsTheWorld_TheWayFlightRolls()
        {
            Assert.AreEqual(-1f, StarlingPilotControls.StickRoll(1f, 0.12f), 1e-6f, "the camera rolls against y-diff");
            Assert.AreEqual(0f, StarlingPilotControls.StickRoll(0.05f, 0.12f), 1e-6f);
            Assert.AreEqual(-90f, StarlingPilotControls.SpinRoll(1f, 0f, 0f, 1f), 1e-3f,
                "a counter-clockwise spin rolls the camera clockwise, so the world turns with the fingers");
        }
    }
}
