#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Profile's colour sets (Docs/MENU_REDESIGN_B.md section 123) against the SHIPPED catalog and container: every
    /// entry names a real palette, the container points at the catalog, a saved pick resolves only to an unlocked
    /// entry, and a swatch shows an HDR colour at its hue instead of clipping it.
    /// </summary>
    public class ColorSetCatalogTests
    {
        const string CatalogPath = "Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset";
        const string ContainerPath = "Assets/_SO_Assets/ThemeManagerDataContainer.asset";

        static ColorSetCatalogSO Catalog => AssetDatabase.LoadAssetAtPath<ColorSetCatalogSO>(CatalogPath);

        [Test]
        public void EveryEntry_NamesAPalette_AndAnId()
        {
            var catalog = Catalog;
            Assert.IsNotNull(catalog, CatalogPath);
            Assert.GreaterOrEqual(catalog.Count, 3, "Nocturne, Photic and Dart Frog");
            for (int i = 0; i < catalog.Count; i++)
            {
                var entry = catalog.Get(i);
                Assert.IsNotNull(entry.colorSet, $"entry {i} names no palette");
                Assert.IsFalse(string.IsNullOrEmpty(entry.id), $"entry {i} has no id to save the pick under");
                Assert.AreEqual(i, catalog.IndexOfId(entry.id), $"id '{entry.id}' is not unique");
            }
        }

        [Test]
        public void TheContainer_PointsAtTheCatalog_AndItsPaletteIsInIt()
        {
            var container = AssetDatabase.LoadAssetAtPath<ThemeManagerDataContainerSO>(ContainerPath);
            Assert.AreSame(Catalog, container.Catalog);
            Assert.GreaterOrEqual(Catalog.IndexOf(container.ColorSet), 0, "the default palette is not a pickable set");
        }

        [Test]
        public void ASavedPick_ResolvesOnlyToAnUnlockedEntry()
        {
            var catalog = ScriptableObject.CreateInstance<ColorSetCatalogSO>();
            var open = ScriptableObject.CreateInstance<SO_ColorSet>();
            var closed = ScriptableObject.CreateInstance<SO_ColorSet>();
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            entries.arraySize = 2;
            void Set(int i, string id, SO_ColorSet set, bool unlocked)
            {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = id;
                e.FindPropertyRelative("colorSet").objectReferenceValue = set;
                e.FindPropertyRelative("unlocked").boolValue = unlocked;
            }
            Set(0, "open", open, true);
            Set(1, "closed", closed, false);
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreSame(open, ThemeManager.PickedColorSet(catalog, "open"));
            Assert.IsNull(ThemeManager.PickedColorSet(catalog, "closed"), "a locked set must not be applied from a saved pick");
            Assert.IsNull(ThemeManager.PickedColorSet(catalog, "gone"), "an id no entry carries falls back to the authored palette");
            Assert.IsNull(ThemeManager.PickedColorSet(null, "open"));

            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(open);
            Object.DestroyImmediate(closed);
        }

        [Test]
        public void EveryColourVision_HasOneUnlockedSetInTheCatalog()
        {
            // The sets made for colour blindness, low vision and contrast are picked in the Profile's rail like any
            // other set; Settings > General > Colour sets only opens the rail on them.
            var catalog = Catalog;
            Assert.AreEqual(-1, catalog.IndexOfVision(ColourVision.None), "no set is made for None");
            foreach (ColourVision vision in System.Enum.GetValues(typeof(ColourVision)))
            {
                if (vision == ColourVision.None) continue;
                var entry = catalog.Get(catalog.IndexOfVision(vision));
                Assert.IsNotNull(entry, $"no colour set is made for {vision}");
                Assert.IsTrue(entry.unlocked, $"the {vision} set is locked: an accessibility set is never a reward");
                Assert.IsNotNull(entry.colorSet, $"the {vision} entry names no palette");
                Assert.IsFalse(string.IsNullOrEmpty(entry.displayName), $"the {vision} set has no name");
            }
            var first = catalog.Get(catalog.IndexOfFirstVisionSet());
            Assert.IsNotNull(first, "the Settings breadcrumb has no vision set to open on");
            Assert.AreNotEqual(ColourVision.None, first.vision);
        }

        [Test]
        public void AVisionSet_IsFree_EvenWhenMarkedLocked()
        {
            // Accessibility is never a reward (Shombith, 2026-11-09): a set made for a vision is worn whatever its
            // unlocked flag says; a look set marked locked is not.
            var catalog = ScriptableObject.CreateInstance<ColorSetCatalogSO>();
            var look = ScriptableObject.CreateInstance<SO_ColorSet>();
            var vision = ScriptableObject.CreateInstance<SO_ColorSet>();
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            entries.arraySize = 2;
            void Set(int i, string id, SO_ColorSet set, ColourVision v)
            {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = id;
                e.FindPropertyRelative("colorSet").objectReferenceValue = set;
                e.FindPropertyRelative("unlocked").boolValue = false;
                e.FindPropertyRelative("vision").intValue = (int)v;
            }
            Set(0, "look", look, ColourVision.None);
            Set(1, "vision", vision, ColourVision.Deuteranopia);
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(ColorSetCatalogSO.IsPickable(catalog.Get(0)));
            Assert.IsTrue(ColorSetCatalogSO.IsPickable(catalog.Get(1)));
            Assert.IsNull(ThemeManager.PickedColorSet(catalog, "look"));
            Assert.AreSame(vision, ThemeManager.PickedColorSet(catalog, "vision"));
            Assert.AreEqual(1, catalog.IndexOfFirstVisionSet());

            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(look);
            Object.DestroyImmediate(vision);
        }

        [Test]
        public void TheList_GroupsStartersThenColorBlindThenPremium()
        {
            // Rev 112 D (section 187): one list, Yours from the start first, then Color Blind supported (Free), then
            // Premium, each in the catalog's card order; every set appears once.
            var catalog = Catalog;
            var order = new System.Collections.Generic.List<int>();
            ProfileColorSetsPanel.ListOrder(catalog, order);
            Assert.AreEqual(catalog.Count, order.Count);
            Assert.AreEqual(catalog.Count, new System.Collections.Generic.HashSet<int>(order).Count, "a set appears twice");
            int last = -1;
            foreach (var i in order)
            {
                int kind = (int)ColorSetCatalogSO.KindOf(catalog.Get(i));
                Assert.GreaterOrEqual(kind, last, $"'{catalog.Get(i).id}' is out of its group");
                last = kind;
            }
            Assert.AreEqual(catalog.IndexOfFirstVisionSet(),
                order.Find(i => ColorSetCatalogSO.KindOf(catalog.Get(i)) == ColorSetKind.ColorBlind),
                "the Settings breadcrumb opens the first Color Blind supported set");
        }

        [Test]
        public void AVisionSet_IsColorBlindSupported_AndFree_WhateverItsPrice()
        {
            var entry = new ColorSetCatalogSO.Entry { vision = ColourVision.Deuteranopia, price = 900, unlocked = false };
            Assert.AreEqual(ColorSetKind.ColorBlind, ColorSetCatalogSO.KindOf(entry));
            Assert.AreEqual(ColorSetKind.Premium, ColorSetCatalogSO.KindOf(new ColorSetCatalogSO.Entry { price = 900 }));
            Assert.AreEqual(ColorSetKind.Starter, ColorSetCatalogSO.KindOf(new ColorSetCatalogSO.Entry()));
        }

        [Test]
        public void TheFilters_ShowTheirOwnKind_AndAllShowsEvery()
        {
            foreach (ColorSetKind kind in System.Enum.GetValues(typeof(ColorSetKind)))
                Assert.IsTrue(ProfileColorSetsPanel.Shows(ProfileColorSetsPanel.Filter.All, kind), kind.ToString());
            Assert.IsTrue(ProfileColorSetsPanel.Shows(ProfileColorSetsPanel.Filter.ColorBlind, ColorSetKind.ColorBlind));
            Assert.IsFalse(ProfileColorSetsPanel.Shows(ProfileColorSetsPanel.Filter.ColorBlind, ColorSetKind.Starter),
                "Nocturne and Photic are not colour-blind sets");
            Assert.IsTrue(ProfileColorSetsPanel.Shows(ProfileColorSetsPanel.Filter.Premium, ColorSetKind.Premium));
            Assert.IsFalse(ProfileColorSetsPanel.Shows(ProfileColorSetsPanel.Filter.Premium, ColorSetKind.ColorBlind));
        }

        [Test]
        public void ABoughtSet_IsPickable_ThroughThePlayersRewards()
        {
            var look = ScriptableObject.CreateInstance<SO_ColorSet>();
            var entry = new ColorSetCatalogSO.Entry { id = "boughtset", colorSet = look, unlocked = false, price = 800 };
            var before = ColorSetCatalogSO.Purchased;
            try
            {
                ColorSetCatalogSO.Purchased = null;
                Assert.IsFalse(ColorSetCatalogSO.IsPickable(entry), "not bought, not unlocked");
                ColorSetCatalogSO.Purchased = id => id == "boughtset";
                Assert.IsTrue(ColorSetCatalogSO.IsPickable(entry), "bought");
                Assert.AreEqual("colorset:boughtset", ColorSetCatalogSO.RewardId("boughtset"));
            }
            finally
            {
                ColorSetCatalogSO.Purchased = before;
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void ASwatch_KeepsAnHdrColoursHue()
        {
            // Nocturne's Jade rim is authored HDR (0.12, 1.14, 1.12): clipped per channel it would turn grey-white.
            var shown = ProfileColorSetsPanel.Display(new Color(0.12f, 1.135f, 1.117f));
            Assert.AreEqual(1f, shown.a);
            Assert.Greater(shown.g, shown.r + 0.3f, "the swatch lost the rim's hue");
            Assert.LessOrEqual(Mathf.Max(shown.r, Mathf.Max(shown.g, shown.b)), 1f);
        }
    }
}
#endif
