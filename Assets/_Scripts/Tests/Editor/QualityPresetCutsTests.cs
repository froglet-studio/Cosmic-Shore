using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// What each quality preset takes off the GPU (<c>Docs/PLATFORM_UNIFICATION.md</c> §3.10): the
    /// shipped table, the "on pick" writes, the Custom fallback and the tier-merge rule.
    /// </summary>
    public class QualityPresetCutsTests
    {
        const string AssetPath = "Assets/Resources/" + QualityPresetCutsSO.ResourcePath + ".asset";
        const string ProceduralSkyPath = "Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat";
        const string BakedSkyPath = "Assets/Resources/PlatformSkyboxes/StaticHyperSeaSkybox.mat";
        const string BakedSkyTexturePath = "Assets/_Graphics/Skyboxes/StaticHyperSeaSky.png";

        static QualityPresetCutsSO Shipped()
        {
            var asset = AssetDatabase.LoadAssetAtPath<QualityPresetCutsSO>(AssetPath);
            Assert.IsNotNull(asset, $"{AssetPath} is missing");
            return asset;
        }

        static PresetCuts Row(QualityPresetSetting preset)
        {
            var row = Shipped().For(preset);
            Assert.IsNotNull(row, $"{preset} has no row");
            return row;
        }

        [Test]
        public void ShippedAsset_LoadsFromResources() =>
            Assert.IsNotNull(Resources.Load<QualityPresetCutsSO>(QualityPresetCutsSO.ResourcePath));

        [Test]
        public void Medium_BakesTheSky_AndKeepsTheVisibleSettings()
        {
            var medium = Row(QualityPresetSetting.Medium);
            Assert.IsTrue(medium.BakedSky, "Medium stops the animated procedural sky");
            Assert.IsFalse(medium.SetsAntiAliasing, "AA is Low's cut, not Medium's");
            Assert.AreEqual(100, medium.MaxRenderScalePercent);
            Assert.IsFalse(medium.DisableHdr);
        }

        [TestCase(QualityPresetSetting.Low)]
        [TestCase(QualityPresetSetting.VeryLow)]
        public void LowPresets_CutTheHeavyHitters(QualityPresetSetting preset)
        {
            var row = Row(preset);
            Assert.IsTrue(row.BakedSky);
            Assert.IsTrue(row.SetsAntiAliasing);
            Assert.AreEqual(AntiAliasingSetting.Off, row.AntiAliasing);
            Assert.Less(row.MaxRenderScalePercent, 100);
            Assert.IsTrue(row.DisableHdr);
            Assert.IsTrue(row.CutsBloom);
            Assert.Less(row.CytoplasmDensity, 1f, "cytoplasm thinned");
            Assert.GreaterOrEqual(row.CytoplasmDensity, GraphicsCuts.MinCytoplasmDensity, "cytoplasm kept");
            Assert.GreaterOrEqual(row.MembraneMaxSubdivisions, 0);
            Assert.Less(row.OffscreenViewMaxRenderScale, 1f);
        }

        [Test]
        public void OnlyVeryLow_DrawsOneViewAtATime()
        {
            // Garrett, 2026-10-22: "if the lowest graphics setting are selected don't display pips" (CrewSingleView)
            for (var p = QualityPresetSetting.VeryLow; p <= QualityPresetSetting.Ultra; p++)
                Assert.AreEqual(p == QualityPresetSetting.VeryLow, Row(p).SingleView, $"{p} single view");
        }

        [Test]
        public void EachLowerPreset_CutsAtLeastAsMuchAsTheOneAbove()
        {
            for (var p = QualityPresetSetting.VeryLow; p < QualityPresetSetting.Ultra; p++)
            {
                var lower = Row(p);
                var upper = Row(p + 1);
                Assert.LessOrEqual(lower.MaxRenderScalePercent, upper.MaxRenderScalePercent, $"{p} render scale");
                Assert.LessOrEqual(lower.OffscreenViewMaxRenderScale, upper.OffscreenViewMaxRenderScale, $"{p} wormhole view");
                Assert.IsTrue(lower.BakedSky || !upper.BakedSky, $"{p} sky");
                Assert.IsTrue(lower.DisableHdr || !upper.DisableHdr, $"{p} HDR");
                Assert.LessOrEqual(lower.CytoplasmDensity, upper.CytoplasmDensity, $"{p} cytoplasm");
            }
        }

        [TestCase(QualityPresetSetting.High)]
        [TestCase(QualityPresetSetting.VeryHigh)]
        [TestCase(QualityPresetSetting.Ultra)]
        public void HighPresets_CutNothing(QualityPresetSetting preset)
        {
            var row = Row(preset);
            Assert.IsFalse(row.SetsAntiAliasing);
            Assert.AreEqual(100, row.MaxRenderScalePercent);
            Assert.IsFalse(row.BakedSky);
            Assert.IsFalse(row.DisableHdr);
            Assert.IsFalse(row.CutsBloom);
            Assert.AreEqual(-1, row.MembraneMaxSubdivisions);
            Assert.AreEqual(1f, row.OffscreenViewMaxRenderScale);
            Assert.AreEqual(1f, row.CytoplasmDensity);

            var data = new GraphicsSettingsData { AntiAliasing = AntiAliasingSetting.MSAA4x, RenderScalePercent = 100 };
            row.ApplyPickTo(data);
            Assert.AreEqual(AntiAliasingSetting.MSAA4x, data.AntiAliasing);
            Assert.AreEqual(100, data.RenderScalePercent);
        }

        [Test]
        public void BakedSky_ReplacesTheProceduralSky()
        {
            var procedural = AssetDatabase.LoadAssetAtPath<Material>(ProceduralSkyPath);
            var baked = AssetDatabase.LoadAssetAtPath<Material>(BakedSkyPath);
            Assert.IsNotNull(procedural);
            Assert.AreEqual(baked, Shipped().BakedSkyFor(procedural));
        }

        [Test]
        public void Asset_DoesNotHoldTheBakedSkyResident()
        {
            // Loaded on every device: the 4096x2048 bake must be reachable only by its Resources path.
            var dependencies = AssetDatabase.GetDependencies(AssetPath, true);
            CollectionAssert.DoesNotContain(dependencies, BakedSkyTexturePath);
            CollectionAssert.DoesNotContain(dependencies, BakedSkyPath);
        }

        [Test]
        public void LowPick_TurnsAaOff_AndLowersRenderScaleWithItsUpscaler()
        {
            var row = Row(QualityPresetSetting.Low);
            var data = new GraphicsSettingsData
            {
                AntiAliasing = AntiAliasingSetting.SMAA,
                RenderScalePercent = 100,
                Upscaling = UpscalingSetting.Auto,
            };
            row.ApplyPickTo(data);
            Assert.AreEqual(AntiAliasingSetting.Off, data.AntiAliasing);
            Assert.AreEqual(row.MaxRenderScalePercent, data.RenderScalePercent);
            Assert.AreEqual(row.UpscalingWhenScaled, data.Upscaling);
        }

        [Test]
        public void Pick_NeverRaisesRenderScale()
        {
            var row = Row(QualityPresetSetting.Low);
            var data = new GraphicsSettingsData { RenderScalePercent = 60, Upscaling = UpscalingSetting.FSR };
            row.ApplyPickTo(data);
            Assert.AreEqual(60, data.RenderScalePercent);
            Assert.AreEqual(UpscalingSetting.FSR, data.Upscaling, "an untouched scale keeps its upscaler");
        }

        [Test]
        public void Custom_KeepsTheCutsOfThePresetItStartedFrom()
        {
            var data = new GraphicsSettingsData
            {
                QualityPreset = QualityPresetSetting.Custom,
                BasePreset = QualityPresetSetting.Low,
            };
            Assert.AreEqual(QualityPresetSetting.Low, data.EffectivePreset);

            data.QualityPreset = QualityPresetSetting.Medium;
            Assert.AreEqual(QualityPresetSetting.Medium, data.EffectivePreset);
        }

        [Test]
        public void CytoplasmShards_AreThinned_NeverRemoved()
        {
            // The shards point at the crystal: no tier or preset combination may leave a cell without them.
            Assert.AreEqual(1f, GraphicsCuts.LowerDensity(1f, 1f));
            Assert.AreEqual(0.25f, GraphicsCuts.LowerDensity(1f, 0.25f));
            Assert.AreEqual(0.25f, GraphicsCuts.LowerDensity(0.25f, 0.5f));
            Assert.AreEqual(GraphicsCuts.MinCytoplasmDensity, GraphicsCuts.LowerDensity(0f, 1f));
            Assert.Greater(GraphicsCuts.MinCytoplasmDensity, 0f);
        }

        [Test]
        public void LevelCaps_TakeTheTighter_AndMinusOneIsNone()
        {
            Assert.AreEqual(-1, GraphicsCuts.TighterLevelCap(-1, -1));
            Assert.AreEqual(3, GraphicsCuts.TighterLevelCap(-1, 3));
            Assert.AreEqual(3, GraphicsCuts.TighterLevelCap(3, -1));
            Assert.AreEqual(2, GraphicsCuts.TighterLevelCap(3, 2));
            Assert.AreEqual(2, GraphicsCuts.TighterLevelCap(2, 3));
        }
    }
}
