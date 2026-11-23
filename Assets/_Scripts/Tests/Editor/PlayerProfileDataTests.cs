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
    }
}
#endif
