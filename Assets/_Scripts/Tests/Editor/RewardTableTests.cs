using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The payout policy, and the shipped numbers.
    ///
    /// These exist because the economy used to live as a serialized list on a UI component,
    /// duplicated across nine scenes with five more surfaces on a retired field - a shape in
    /// which "what does the game actually pay?" had no single answer and no way to assert one.
    /// The values are Docs/ECONOMY_TABLES.md Table 2.
    /// </summary>
    public class RewardTableTests
    {
        RewardTableSO _table;

        [SetUp]
        public void SetUp() => _table = ScriptableObject.CreateInstance<RewardTableSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_table);

        [Test]
        public void FirstAndSecondPay_LastPlaceNever()
        {
            // Three domains: 1st and 2nd pay, last earns nothing.
            Assert.AreEqual(200, _table.CrystalsForPlace(0, 3), "1st of 3");
            Assert.AreEqual(50, _table.CrystalsForPlace(1, 3), "2nd of 3");
            Assert.AreEqual(0, _table.CrystalsForPlace(2, 3), "last of 3");
        }

        [Test]
        public void WithTwoDomains_TheRunnerUpIsALoser()
        {
            // The intended read: with two domains there is no silver medal. The table WOULD pay
            // index 1 fifty crystals; the last-place rule overrides it.
            Assert.AreEqual(200, _table.CrystalsForPlace(0, 2));
            Assert.AreEqual(0, _table.CrystalsForPlace(1, 2),
                "with two domains, second place IS last place and must earn nothing");
        }

        [Test]
        public void SoloFieldStillPays()
        {
            // A one-domain field has no last place to demote - otherwise a solo run against AI
            // teammates on the same domain would silently pay zero.
            Assert.AreEqual(200, _table.CrystalsForPlace(0, 1));
        }

        [Test]
        public void OffTheTable_EarnsNothingRatherThanThrowing()
        {
            Assert.AreEqual(0, _table.CrystalsForPlace(-1, 3), "a domain that never placed");
            Assert.AreEqual(0, _table.CrystalsForPlace(7, 9), "a place past the end of the table");
        }

        [Test]
        public void PlacementGrant_IsRepeatableAndCarriesItsSource()
        {
            var grant = _table.PlacementGrant(0, 3, "game_placement");
            Assert.AreEqual(RewardKind.Crystals, grant.Kind);
            Assert.AreEqual(200, grant.Amount);
            Assert.AreEqual("game_placement", grant.Source);
            Assert.AreEqual(RewardDedupe.None, grant.Dedupe,
                "a placement is earned every game - deduping it would pay only the first win");
            Assert.IsTrue(grant.IsPayable);
        }

        [Test]
        public void AZeroPayout_IsNotPayableAndSoNeverReachesTheWallet()
        {
            var grant = _table.PlacementGrant(2, 3, "game_placement");
            Assert.AreEqual(0, grant.Amount);
            Assert.IsFalse(grant.IsPayable,
                "last place earning nothing is an outcome, not an error - it must simply not " +
                "reach the wallet or the reward UI");
        }

        [Test]
        public void ShippedAsset_MatchesTheEconomyTable()
        {
            var shipped = Resources.Load<RewardTableSO>(RewardTableSO.ResourcePath);
            Assert.IsNotNull(shipped,
                $"Resources/{RewardTableSO.ResourcePath} is missing - every crystal payout in " +
                "the game reads its numbers from that one asset.");

            // Docs/ECONOMY_TABLES.md Table 2, and Table 3's "20 wins per vessel" depends on the
            // first-place number specifically.
            Assert.AreEqual(200, shipped.CrystalsForPlace(0, 3), "1st place");
            Assert.AreEqual(50, shipped.CrystalsForPlace(1, 3), "2nd place");
            Assert.AreEqual(0, shipped.CrystalsForPlace(2, 3), "last place");
        }

        [Test]
        public void EntitlementGrant_CannotBeAuthoredToPayTwice()
        {
            var grant = RewardGrant.Entitlement("skin.squirrel.aurora", "test");
            Assert.AreEqual(RewardDedupe.Account, grant.Dedupe);
            Assert.AreEqual(grant.EntitlementId, grant.DedupeKey,
                "an entitlement is its own dedupe key, so there is no way to author one that " +
                "grants twice");
        }
    }
}
