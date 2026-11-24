#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="RetentionPay"/>: party pay, voyage dock pay and painting pay, the economy fixes of the retention build
    /// (Docs/LoopAtlas/sim/REPORT.md section 2).
    /// </summary>
    public class RetentionPayTests
    {
        static readonly List<Domains> Order = new() { Domains.Jade, Domains.Ruby, Domains.Gold };

        [Test]
        public void PartyMember_IsPaidByTheBestPlacedPartyDomain()
        {
            // The night crew: three on Ruby (second), one on Jade (first). Every one of them is paid as first.
            Assert.AreEqual(Domains.Jade, RetentionPay.PayDomain(Domains.Ruby, Order, new[] { Domains.Ruby, Domains.Jade }));
        }

        [Test]
        public void NoParty_IsPaidByOwnDomain()
        {
            Assert.AreEqual(Domains.Gold, RetentionPay.PayDomain(Domains.Gold, Order, new Domains[0]));
            Assert.AreEqual(Domains.Gold, RetentionPay.PayDomain(Domains.Gold, Order, null));
        }

        [Test]
        public void PartyPlacedWorse_NeverLowersOwnPay()
        {
            Assert.AreEqual(Domains.Jade, RetentionPay.PayDomain(Domains.Jade, Order, new[] { Domains.Gold }));
        }

        [Test]
        public void PartyDomainNotInMatch_IsIgnored()
        {
            Assert.AreEqual(Domains.Gold, RetentionPay.PayDomain(Domains.Gold, Order, new[] { Domains.Blue }));
        }

        [Test]
        public void VoyageDock_PaysPerJob_UpToTheCap()
        {
            Assert.AreEqual(0, RetentionPay.VoyageWalletCrystals(0, 600, 1800));
            Assert.AreEqual(600, RetentionPay.VoyageWalletCrystals(1, 600, 1800));
            Assert.AreEqual(1800, RetentionPay.VoyageWalletCrystals(5, 600, 1800));
            Assert.AreEqual(3000, RetentionPay.VoyageWalletCrystals(5, 600, 0));
        }

        [Test]
        public void Painting_PaysUntilTheDaysLimit()
        {
            Assert.AreEqual(15, RetentionPay.PaintingCrystals(0, 15, 6));
            Assert.AreEqual(15, RetentionPay.PaintingCrystals(5, 15, 6));
            Assert.AreEqual(0, RetentionPay.PaintingCrystals(6, 15, 6));
            Assert.AreEqual(0, RetentionPay.PaintingCrystals(0, 0, 6));
        }
    }
}
#endif
