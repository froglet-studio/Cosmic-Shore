#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The colour grade (Docs/VesselModels/COLOR_TRACE.md; Garrett, 2026-11-10, LOCKED: every colour comes from the
    /// colour set): every shipped set but Photic carries a UI and a world table, each a well-formed 33^3 grid written by
    /// Tools/Build/colour_grade.py; the default UI material the game draws canvases with is the graded one.
    /// </summary>
    public class ColorGradeTests
    {
        const string CatalogPath = "Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset";

        [Test]
        public void EveryWornSet_HasItsTables_AndPhoticNeedsNone()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ColorSetCatalogSO>(CatalogPath);
            Assert.IsNotNull(catalog);
            var photic = ColorTrace.FindAuthored(catalog);
            for (int i = 0; i < catalog.Count; i++)
            {
                var set = catalog.Get(i).colorSet;
                foreach (var role in new[] { "ui", "world" })
                {
                    var table = Resources.Load<TextAsset>("ColorGrades/" + set.name + "_" + role);
                    if (set == photic)
                    {
                        Assert.IsNull(table, "Photic's grade is the identity: it carries no table");
                        continue;
                    }
                    Assert.IsNotNull(table, $"{set.name} has no {role} grade: run python3 Tools/Build/colour_grade.py");
                    var b = table.bytes;
                    Assert.AreEqual("CSGR", System.Text.Encoding.ASCII.GetString(b, 0, 4), $"{set.name}_{role} is no grade table");
                    Assert.AreEqual(1, b[4], "table version");
                    int n = b[5];
                    Assert.AreEqual(33, n, "a 33^3 grid");
                    Assert.AreEqual(6 + n * n * n * 3, b.Length, $"{set.name}_{role} has the wrong size");
                    // the grid's corners: black and white keep (the grade leaves a grey alone)
                    Assert.AreEqual(0, b[6] + b[7] + b[8], "black stays black");
                    int last = 6 + (n * n * n - 1) * 3;
                    Assert.AreEqual(3 * 255, b[last] + b[last + 1] + b[last + 2], "white stays white");
                }
            }
        }

        [Test]
        public void TheGradedUIMaterial_DrawsWithTheGradedDefault()
        {
            var material = Resources.Load<Material>("ColorGrades/GradedUI");
            Assert.IsNotNull(material, "Resources/ColorGrades/GradedUI.mat");
            Assert.AreEqual("CosmicShore/UI/GradedDefault", material.shader.name);
        }
    }
}
#endif
