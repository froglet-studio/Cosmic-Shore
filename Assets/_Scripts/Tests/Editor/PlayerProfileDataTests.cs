#if UNITY_EDITOR
using CosmicShore.UI;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Hangar's bought levels live on the cloud profile beside the balance that paid for them
    /// (<see cref="ProfileEconomy.HangarUpgrades"/>, REFACTOR_BACKLOG.md S67). Serialized the way
    /// UGSCloudSaveProvider and LocalCloudDataCache do it, with Newtonsoft.
    /// </summary>
    public class PlayerProfileDataTests
    {
        [Test]
        public void ProfileSavedBeforeHangarLevels_LoadsWithNone()
        {
            const string old = "{\"SchemaVersion\":2,\"Economy\":{\"CrystalBalance\":37,\"UnlockedRewardIds\":[]}}";
            var data = JsonConvert.DeserializeObject<PlayerProfileData>(old);
            Assert.AreEqual(37, data.Economy.CrystalBalance);
            Assert.IsNotNull(data.Economy.HangarUpgrades, "a profile from before the field still has a list");
            Assert.IsEmpty(data.Economy.HangarUpgrades);
        }

        [Test]
        public void HangarLevels_SurviveTheCloudRoundTrip()
        {
            var data = new PlayerProfileData();
            data.Economy.CrystalBalance = 100;
            data.Economy.HangarUpgrades.Add(new HangarUpgradeLevel { Vessel = 16, Element = 2, Level = 3 });

            var back = JsonConvert.DeserializeObject<PlayerProfileData>(JsonConvert.SerializeObject(data));
            Assert.AreEqual(100, back.Economy.CrystalBalance);
            Assert.AreEqual(1, back.Economy.HangarUpgrades.Count);
            var level = back.Economy.HangarUpgrades[0];
            Assert.AreEqual((16, 2, 3), (level.Vessel, level.Element, level.Level));
        }

        [Test]
        public void ProfileSavedBeforeElementalCrystals_LoadsWithNone()
        {
            const string old = "{\"SchemaVersion\":2,\"Economy\":{\"CrystalBalance\":5}}";
            var data = JsonConvert.DeserializeObject<PlayerProfileData>(old);
            Assert.IsNotNull(data.Economy.ElementalCrystals, "a profile from before the field still has a list");
            Assert.IsEmpty(data.Economy.ElementalCrystals);
        }

        [Test]
        public void ElementalCrystals_HeldAndSpent_SurviveTheCloudRoundTrip()
        {
            var data = new PlayerProfileData();
            data.Economy.ElementalCrystals.Add(new ElementalCrystalBalance { Element = 2, Held = 7, Earned = 12, Spent = 5 });

            var back = JsonConvert.DeserializeObject<PlayerProfileData>(JsonConvert.SerializeObject(data));
            var mass = back.Economy.ElementalCrystals[0];
            Assert.AreEqual((2, 7, 12L, 5L), (mass.Element, mass.Held, mass.Earned, mass.Spent));
        }

        [Test]
        public void StartingGrant_GivesTenOfEachElementToANewProfile()
        {
            var economy = new PlayerProfileData().Economy;
            Assert.IsTrue(economy.GrantStartingElementalCrystals(10));
            Assert.AreEqual(4, economy.ElementalCrystals.Count);
            for (int element = 1; element <= 4; element++)
            {
                var b = economy.ElementalCrystals.Find(x => x.Element == element);
                Assert.IsNotNull(b, $"element {element} has no entry");
                Assert.AreEqual((10, 10L, 0L), (b.Held, b.Earned, b.Spent));
            }
        }

        [Test]
        public void StartingGrant_NeverRefillsAnElementThePlayerSpent()
        {
            var economy = new PlayerProfileData().Economy;
            economy.ElementalCrystals.Add(new ElementalCrystalBalance { Element = 4, Held = 0, Earned = 10, Spent = 10 });
            Assert.IsTrue(economy.GrantStartingElementalCrystals(10), "the three missing elements are granted");
            Assert.AreEqual(0, economy.ElementalCrystals.Find(x => x.Element == 4).Held, "Time stays spent");
            Assert.IsFalse(economy.GrantStartingElementalCrystals(10), "a second grant adds nothing");
            Assert.AreEqual(4, economy.ElementalCrystals.Count);
        }

        [Test]
        public void StartingGrant_OfZeroGivesNothing()
        {
            var economy = new PlayerProfileData().Economy;
            Assert.IsFalse(economy.GrantStartingElementalCrystals(0));
            Assert.IsEmpty(economy.ElementalCrystals);
        }
    }
}
#endif
