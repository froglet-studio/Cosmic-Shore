#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pure-static tests for the AI domain placement primitives.
    /// Covers the contiguous active-domain slice, the 3-tier tie-break in
    /// <see cref="ServerPlayerVesselInitializerWithAI.GetBalancedDomain"/>, and
    /// <see cref="GameDataSO.IsActiveDomain"/>.
    /// </summary>
    [TestFixture]
    public class ServerPlayerVesselInitializerWithAITests
    {
        // ── BuildActiveDomains ──────────────────────────────────────────────

        [Test]
        public void BuildActiveDomains_DC1_ReturnsJadeOnly()
        {
            var active = ServerPlayerVesselInitializerWithAI.BuildActiveDomains(1);
            CollectionAssert.AreEqual(new[] { Domains.Jade }, active);
        }

        [Test]
        public void BuildActiveDomains_DC2_ReturnsJadeRuby()
        {
            var active = ServerPlayerVesselInitializerWithAI.BuildActiveDomains(2);
            CollectionAssert.AreEqual(new[] { Domains.Jade, Domains.Ruby }, active);
        }

        [Test]
        public void BuildActiveDomains_DC3_ReturnsJadeRubyGold()
        {
            var active = ServerPlayerVesselInitializerWithAI.BuildActiveDomains(3);
            CollectionAssert.AreEqual(new[] { Domains.Jade, Domains.Ruby, Domains.Gold }, active);
        }

        [Test]
        public void BuildActiveDomains_OverMax_ClampsToActiveDomainsLength()
        {
            var active = ServerPlayerVesselInitializerWithAI.BuildActiveDomains(99);
            Assert.AreEqual(GameDataSO.ActiveDomains.Length, active.Count);
        }

        [Test]
        public void BuildActiveDomains_Zero_ClampsToOne()
        {
            var active = ServerPlayerVesselInitializerWithAI.BuildActiveDomains(0);
            CollectionAssert.AreEqual(new[] { Domains.Jade }, active);
        }

        // ── IsActiveDomain ──────────────────────────────────────────────────

        [Test]
        public void IsActiveDomain_DC1_OnlyJadeActive()
        {
            Assert.IsTrue(GameDataSO.IsActiveDomain(Domains.Jade, 1));
            Assert.IsFalse(GameDataSO.IsActiveDomain(Domains.Ruby, 1));
            Assert.IsFalse(GameDataSO.IsActiveDomain(Domains.Gold, 1));
            Assert.IsFalse(GameDataSO.IsActiveDomain(Domains.Blue, 1));
        }

        [Test]
        public void IsActiveDomain_DC2_JadeRubyActive()
        {
            Assert.IsTrue(GameDataSO.IsActiveDomain(Domains.Jade, 2));
            Assert.IsTrue(GameDataSO.IsActiveDomain(Domains.Ruby, 2));
            Assert.IsFalse(GameDataSO.IsActiveDomain(Domains.Gold, 2));
            Assert.IsFalse(GameDataSO.IsActiveDomain(Domains.Blue, 2));
        }

        [Test]
        public void IsActiveDomain_DC3_JadeRubyGoldActive_NotBlue()
        {
            Assert.IsTrue(GameDataSO.IsActiveDomain(Domains.Jade, 3));
            Assert.IsTrue(GameDataSO.IsActiveDomain(Domains.Ruby, 3));
            Assert.IsTrue(GameDataSO.IsActiveDomain(Domains.Gold, 3));
            Assert.IsFalse(GameDataSO.IsActiveDomain(Domains.Blue, 3));
        }

        // ── GetBalancedDomain: tier 1 (lowest total) ────────────────────────

        [Test]
        public void GetBalancedDomain_Tier1_LowestTotal_WinsAlone()
        {
            var totals  = new Dictionary<Domains, int> { { Domains.Jade, 0 }, { Domains.Ruby, 1 }, { Domains.Gold, 1 } };
            var humans  = new Dictionary<Domains, int> { { Domains.Jade, 0 }, { Domains.Ruby, 1 }, { Domains.Gold, 1 } };
            Assert.AreEqual(Domains.Jade, ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totals, humans));
        }

        [Test]
        public void GetBalancedDomain_Tier1_LowestTotalIsRuby_ReturnsRuby()
        {
            var totals  = new Dictionary<Domains, int> { { Domains.Jade, 2 }, { Domains.Ruby, 0 }, { Domains.Gold, 1 } };
            var humans  = new Dictionary<Domains, int> { { Domains.Jade, 2 }, { Domains.Ruby, 0 }, { Domains.Gold, 1 } };
            Assert.AreEqual(Domains.Ruby, ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totals, humans));
        }

        // ── GetBalancedDomain: tier 2 (fewest humans among tied totals) ─────

        [Test]
        public void GetBalancedDomain_Tier2_FewestHumansWins()
        {
            // All three tied on total=1; Ruby has zero humans → Ruby wins.
            var totals  = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 1 }, { Domains.Gold, 1 } };
            var humans  = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 0 }, { Domains.Gold, 1 } };
            Assert.AreEqual(Domains.Ruby, ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totals, humans));
        }

        [Test]
        public void GetBalancedDomain_Tier2_OnlyGoldIsAISolo_ReturnsGold()
        {
            // Jade + Ruby each have 1 human; Gold has 1 AI-only. Tied totals (1,1,1).
            // Gold has zero humans → wins tier 2.
            var totals  = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 1 }, { Domains.Gold, 1 } };
            var humans  = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 1 }, { Domains.Gold, 0 } };
            Assert.AreEqual(Domains.Gold, ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totals, humans));
        }

        // ── GetBalancedDomain: tier 3 (enum order, Jade > Ruby > Gold) ──────

        [Test]
        public void GetBalancedDomain_Tier3_AllEqual_ReturnsJade()
        {
            var totals  = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 1 }, { Domains.Gold, 1 } };
            var humans  = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 1 }, { Domains.Gold, 1 } };
            Assert.AreEqual(Domains.Jade, ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totals, humans));
        }

        [Test]
        public void GetBalancedDomain_Tier3_JadeAndRubyTie_RubyHasFewerHumans_TieBreakStill()
        {
            // totals tie at 1 across all three but Ruby has fewer humans than Jade or Gold.
            var totals  = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 1 }, { Domains.Gold, 1 } };
            var humans  = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 0 }, { Domains.Gold, 1 } };
            Assert.AreEqual(Domains.Ruby, ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totals, humans));
        }

        // ── Worked example: solo Jade host, DC=3, 3 AIs ─────────────────────

        [Test]
        public void GetBalancedDomain_SoloJadeHost_3AI_ProducesRubyGoldJade()
        {
            // Initial state: host on Jade, DC=3, no AI yet.
            var totals = new Dictionary<Domains, int>
                { { Domains.Jade, 1 }, { Domains.Ruby, 0 }, { Domains.Gold, 0 } };
            var humans = new Dictionary<Domains, int>
                { { Domains.Jade, 1 }, { Domains.Ruby, 0 }, { Domains.Gold, 0 } };

            var sequence = new List<Domains>();
            for (int i = 0; i < 3; i++)
            {
                var d = ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totals, humans);
                sequence.Add(d);
                totals[d]++;
            }

            // AI #1: totals {1,0,0} → Ruby (lowest total, tied with Gold; Ruby wins tier 3 by enum order).
            // AI #2: totals {1,1,0} → Gold (lowest total).
            // AI #3: totals {1,1,1} → fewest humans → Ruby or Gold (both have 0 humans).
            //         Tier 3 picks Ruby by enum order.
            CollectionAssert.AreEqual(
                new[] { Domains.Ruby, Domains.Gold, Domains.Ruby },
                sequence);

            // Final totals: Jade=1, Ruby=2, Gold=1 (= 4 total, the requested PC=4).
            Assert.AreEqual(1, totals[Domains.Jade]);
            Assert.AreEqual(2, totals[Domains.Ruby]);
            Assert.AreEqual(1, totals[Domains.Gold]);
        }
    }
}
#endif
