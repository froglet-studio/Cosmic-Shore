#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Data;
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
        public void EveryColourVisionMode_HasOneUnlockedSet()
        {
            // Settings > General > Colorblind mode offers only the visions a set is made for; every one should be.
            var catalog = Catalog;
            Assert.AreEqual(-1, catalog.IndexOfVision(ColorblindModeSetting.Off), "no set is made for Off");
            foreach (ColorblindModeSetting mode in System.Enum.GetValues(typeof(ColorblindModeSetting)))
            {
                if (mode == ColorblindModeSetting.Off) continue;
                var entry = catalog.Get(catalog.IndexOfVision(mode));
                Assert.IsNotNull(entry, $"no colour set is made for {mode}");
                Assert.IsTrue(entry.unlocked, $"the {mode} set is locked: an accessibility set is never a reward");
                Assert.IsNotNull(entry.colorSet, $"the {mode} entry names no palette");
                Assert.IsFalse(string.IsNullOrEmpty(entry.displayName), $"the {mode} set has no name for its option");
            }
        }

        [Test]
        public void AColourVisionMode_OutranksThePickedLook()
        {
            var catalog = ScriptableObject.CreateInstance<ColorSetCatalogSO>();
            var look = ScriptableObject.CreateInstance<SO_ColorSet>();
            var protan = ScriptableObject.CreateInstance<SO_ColorSet>();
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            entries.arraySize = 2;
            void Set(int i, string id, SO_ColorSet set, ColorblindModeSetting vision)
            {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = id;
                e.FindPropertyRelative("colorSet").objectReferenceValue = set;
                e.FindPropertyRelative("unlocked").boolValue = true;
                e.FindPropertyRelative("vision").enumValueIndex = (int)vision;
            }
            Set(0, "look", look, ColorblindModeSetting.Off);
            Set(1, "protan", protan, ColorblindModeSetting.Protanopia);
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(1, catalog.IndexOfVision(ColorblindModeSetting.Protanopia));
            Assert.AreSame(protan, ThemeManager.ResolveColorSet(catalog, "look", ColorblindModeSetting.Protanopia));
            Assert.AreSame(look, ThemeManager.ResolveColorSet(catalog, "look", ColorblindModeSetting.Off));
            Assert.AreSame(look, ThemeManager.ResolveColorSet(catalog, "look", ColorblindModeSetting.Tritanopia),
                "a mode no set is made for keeps the picked look");
            Assert.IsNull(ThemeManager.ResolveColorSet(null, "look", ColorblindModeSetting.Protanopia));

            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(look);
            Object.DestroyImmediate(protan);
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
