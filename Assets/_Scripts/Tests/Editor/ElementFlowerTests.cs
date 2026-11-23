#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The menu's element flower (Rev 24): the Hangar's UPGRADES and the arcade cards draw the player's bought level
    /// with <see cref="KitElementFlower"/>, which must colour its petals exactly as the match HUD does, and every
    /// element starts at level 1 (Shombith, 2026-10-26: "they always start with 1 value").
    /// </summary>
    public class ElementFlowerTests
    {
        [Test]
        public void Store_ShowsAnUnboughtElementAtLevelOne()
        {
            Assert.AreEqual(1, HangarUpgradeStore.Shown(0), "a vessel nobody upgraded reads level 1");
            Assert.AreEqual(1, HangarUpgradeStore.Shown(-3));
            Assert.AreEqual(4, HangarUpgradeStore.Shown(4));
            Assert.AreEqual(HangarUpgradeStore.MaxLevel, HangarUpgradeStore.Shown(99));
        }

        [Test]
        public void Store_TopsOutAtFive_AndReadsAnOldHigherLevelAsFive()
        {
            // Section 90 (Shombith 2026-10-27: "Max level 5"): five white petals is the top.
            Assert.AreEqual(5, HangarUpgradeStore.MaxLevel);
            Assert.AreEqual(5, HangarUpgradeStore.Shown(8), "a level bought before the cap reads 5");
        }

        [Test]
        public void Store_FirstUpgradeCostsTheSecondLevel()
        {
            // The owner's price: 100 x the level being bought. From the start level that is level 2.
            Assert.AreEqual(200, HangarUpgradeStore.CostOfNext(HangarUpgradeStore.StartLevel, 100));
        }

        [Test]
        public void Flower_LevelOneLightsOnePetalWhite_TheRestGrey()
        {
            var (flower, petals, bars) = Build();
            flower.Set(Element.Mass, 1);
            Assert.AreEqual(bars.WhiteColor, petals[0].color);
            for (int i = 1; i < petals.Length; i++) Assert.AreEqual(bars.GreyColor, petals[i].color, $"petal {i}");
            Object.DestroyImmediate(flower.gameObject);
        }

        [Test]
        public void Flower_MatchesTheHudFillAtEveryHangarLevel()
        {
            var (flower, petals, bars) = Build();
            var expected = new int[ElementalBarsConfigSO.PetalCount];
            for (int level = HangarUpgradeStore.StartLevel; level <= HangarUpgradeStore.MaxLevel; level++)
            {
                flower.Set(Element.Time, level);
                ElementalBarsConfigSO.DistributePetalValues(level, expected);
                for (int i = 0; i < petals.Length; i++)
                    Assert.AreEqual(bars.ColorForTick(expected[i]), petals[i].color, $"level {level}, petal {i}");
            }
            flower.SetLevel(10);
            foreach (var p in petals) Assert.AreEqual(bars.BlueColor, p.color, "level 10 is all blue, as in a match");
            Object.DestroyImmediate(flower.gameObject);
        }

        [Test]
        public void Flower_TakesTheElementsOwnPetalSprite()
        {
            var (flower, petals, bars) = Build();
            flower.Set(Element.Space, 3);
            foreach (var p in petals) Assert.AreSame(bars.GetPetalSprite(Element.Space), p.sprite);
            flower.Set(Element.Charge, 3);
            foreach (var p in petals) Assert.AreSame(bars.GetPetalSprite(Element.Charge), p.sprite);
            Object.DestroyImmediate(flower.gameObject);
        }

        static (KitElementFlower, Image[], ElementalBarsConfigSO) Build()
        {
            var bars = Resources.Load<ElementalBarsConfigSO>(ElementalBarsConfigSO.ResourcePath);
            Assert.IsNotNull(bars, "Resources/ElementalBarsConfig is missing");
            var go = new GameObject("Flower", typeof(RectTransform));
            var petals = new Image[ElementalBarsConfigSO.PetalCount];
            for (int i = 0; i < petals.Length; i++)
            {
                var p = new GameObject($"Petal{i}", typeof(RectTransform), typeof(Image));
                p.transform.SetParent(go.transform, false);
                petals[i] = p.GetComponent<Image>();
            }
            var flower = go.AddComponent<KitElementFlower>();
            var so = new UnityEditor.SerializedObject(flower);
            var list = so.FindProperty("petals");
            list.arraySize = petals.Length;
            for (int i = 0; i < petals.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = petals[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return (flower, petals, bars);
        }
    }
}
#endif
