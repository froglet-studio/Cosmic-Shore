#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Game;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Device tiers (<c>Docs/PLATFORM_UNIFICATION.md</c> §3): the pure classifier, the shipped
    /// profile set, and the tier-authored first-run graphics recommendation. The facts are passed in,
    /// so every phone below is pinned without the phone.
    /// </summary>
    public class DeviceTierTests
    {
        static DeviceFacts Android(int ramMB, string gpu) => new(true, DeviceOs.Android, ramMB, 8, gpu);
        static DeviceFacts Ios(int ramMB) => new(true, DeviceOs.IOS, ramMB, 6, "Apple A15 GPU");

        static DeviceTier Classify(DeviceFacts facts) =>
            DeviceTierClassifier.Classify(facts, DeviceTierRules.Default, out _);

        #region Classifier

        [Test]
        public void NotHandheld_IsDesktop()
        {
            var desktop = new DeviceFacts(false, DeviceOs.Other, 16384, 12, "NVIDIA GeForce RTX 3070");
            Assert.AreEqual(DeviceTier.Desktop, Classify(desktop));
        }

        [Test]
        public void Iphone_ByRam()
        {
            Assert.AreEqual(DeviceTier.MobileLow, Classify(Ios(1985)), "2 GB iPhone (6s-8, SE 1)");
            Assert.AreEqual(DeviceTier.MobileHigh, Classify(Ios(2816)), "3 GB iPhone");
            Assert.AreEqual(DeviceTier.MobileHigh, Classify(Ios(5650)), "6 GB iPhone");
        }

        [Test]
        public void FourGigAndroid_IsMobileLow_WhateverItsGpu()
        {
            // The phone the plan started from: a 4 GB budget Samsung. RAM decides before the GPU.
            Assert.AreEqual(DeviceTier.MobileLow, Classify(Android(3712, "Mali-G52 MC2")));
            Assert.AreEqual(DeviceTier.MobileLow, Classify(Android(3712, "Adreno (TM) 740")));
        }

        [TestCase("Adreno (TM) 640")]
        [TestCase("Adreno (TM) 642L")]
        [TestCase("Adreno (TM) 650")]
        [TestCase("Adreno (TM) 740")]
        [TestCase("Adreno (TM) 830")]
        [TestCase("Mali-G76")]
        [TestCase("Mali-G78")]
        [TestCase("Mali-G710")]
        [TestCase("Immortalis-G720")]
        [TestCase("Samsung Xclipse 920")]
        public void FlagshipAndroid_IsMobileHigh(string gpu)
        {
            Assert.AreEqual(DeviceTier.MobileHigh, Classify(Android(7600, gpu)), gpu);
        }

        [TestCase("Adreno (TM) 506")]
        [TestCase("Adreno (TM) 610")]
        [TestCase("Adreno (TM) 619")]
        [TestCase("Mali-G52 MC2")]
        [TestCase("Mali-G57 MC2")]
        [TestCase("Mali-G68")]
        [TestCase("Mali-G72")]
        [TestCase("Mali-G610")]
        [TestCase("PowerVR Rogue GE8320")]
        [TestCase("")]
        public void BudgetOrUnknownGpu_IsMobileLow_EvenWithRam(string gpu)
        {
            Assert.AreEqual(DeviceTier.MobileLow, Classify(Android(7600, gpu)), gpu);
        }

        [Test]
        public void MalformedPattern_IsSkipped_NotThrown()
        {
            var rules = new DeviceTierRules { AndroidHighEndGpuPatterns = new[] { "(", "Adreno.*7[0-9][0-9]" } };
            Assert.AreEqual(DeviceTier.MobileHigh,
                DeviceTierClassifier.Classify(Android(7600, "Adreno (TM) 740"), rules, out _));
        }

        [TestCase("iOS 17.5", DeviceOs.IOS)]
        [TestCase("iPadOS 17.5", DeviceOs.IOS)]
        [TestCase("Android OS 14 / API-34 (UP1A.231005.007)", DeviceOs.Android)]
        [TestCase("Windows 11  (10.0.22631) 64bit", DeviceOs.Other)]
        [TestCase("", DeviceOs.Other)]
        public void OsFromName(string operatingSystem, DeviceOs expected)
        {
            Assert.AreEqual(expected, DeviceTierClassifier.OsFromName(operatingSystem));
        }

        #endregion

        #region Shipped profile set

        [Test]
        public void ProfileSet_Ships_WithEveryTierAssigned()
        {
            var set = Resources.Load<PlatformProfileSetSO>(PlatformProfileSetSO.ResourcePath);
            Assert.IsNotNull(set, "Assets/Resources/PlatformProfiles.asset");
            foreach (DeviceTier tier in System.Enum.GetValues(typeof(DeviceTier)))
                Assert.IsNotNull(set.For(tier), $"no profile assigned for {tier}");
        }

        [Test]
        public void DesktopAndMobileHigh_KeepTheCapabilityHeuristic()
        {
            // The promise of step 3: Windows and iOS get exactly the recommendation they got before
            // tiers existed. Turning the heuristic off on either is a deliberate behaviour change.
            var set = Resources.Load<PlatformProfileSetSO>(PlatformProfileSetSO.ResourcePath);
            Assert.IsTrue(set.For(DeviceTier.Desktop).AutoDetect.UseCapabilityHeuristic, "Desktop");
            Assert.IsTrue(set.For(DeviceTier.MobileHigh).AutoDetect.UseCapabilityHeuristic, "MobileHigh");
            Assert.IsFalse(set.For(DeviceTier.MobileLow).AutoDetect.UseCapabilityHeuristic, "MobileLow");
        }

        [Test]
        public void ProfileSet_Rules_MatchTheCodeFallback()
        {
            // DeviceTierRules.Default is what runs if the asset goes missing; keep the two in step.
            var set = Resources.Load<PlatformProfileSetSO>(PlatformProfileSetSO.ResourcePath);
            var asset = set.Rules;
            var code = DeviceTierRules.Default;
            Assert.AreEqual(code.IosLowMemoryMB, asset.IosLowMemoryMB);
            Assert.AreEqual(code.AndroidHighMinMemoryMB, asset.AndroidHighMinMemoryMB);
            CollectionAssert.AreEqual(code.AndroidHighEndGpuPatterns, asset.AndroidHighEndGpuPatterns);
        }

        #endregion

        #region Tier-authored recommendation

        static PlatformAutoDetect LowProfile(int maxFps = 60) => new(false, QualityPresetSetting.VeryLow,
            1_300_000, 50, UpscalingSetting.Linear, AntiAliasingSetting.FXAA, maxFps);

        [Test]
        public void Recommendation_FitsA1080x2400PhoneIntoTheBudget()
        {
            var d = SettingsAutoDetector.RecommendFromProfile(LowProfile(), 1080L * 2400L, 120, 8);
            Assert.AreEqual(71, d.RenderScalePercent);   // sqrt(1.3M / 2.59M)
            Assert.AreEqual(UpscalingSetting.Linear, d.Upscaling);
            Assert.AreEqual(AntiAliasingSetting.FXAA, d.AntiAliasing);
            Assert.AreEqual(QualityPresetSetting.VeryLow, d.QualityPreset);
            Assert.AreEqual(60, d.TargetFrameRate, "capped by the profile, not the 120 Hz panel");
        }

        [Test]
        public void Recommendation_UnderBudget_RendersNative_WithNoUpscaler()
        {
            var d = SettingsAutoDetector.RecommendFromProfile(LowProfile(), 720L * 1600L, 60, 8);
            Assert.AreEqual(100, d.RenderScalePercent);
            Assert.AreEqual(UpscalingSetting.Auto, d.Upscaling);
        }

        [Test]
        public void Recommendation_FrameCap()
        {
            Assert.AreEqual(30, SettingsAutoDetector.RecommendFromProfile(LowProfile(30), 0, 120, 8).TargetFrameRate);
            Assert.AreEqual(60, SettingsAutoDetector.RecommendFromProfile(LowProfile(), 0, 0, 8).TargetFrameRate,
                "unknown refresh rate");
            Assert.AreEqual(50, SettingsAutoDetector.RecommendFromProfile(LowProfile(), 0, 50, 8).TargetFrameRate,
                "a slower panel caps lower");
        }

        [Test]
        public void PresetRenderScale_MatchesThePreTierFormula()
        {
            // The preset overload now delegates to the budget one. Pin it against the formula it
            // replaced (transcribed from the pre-tier SettingsAutoDetector), so the Desktop and
            // MobileHigh heuristic provably did not move.
            long[] displays = { 1920L * 1080L, 2560L * 1440L, 3840L * 2160L, 5120L * 2880L, 1080L * 2400L, 0L };
            foreach (QualityPresetSetting preset in System.Enum.GetValues(typeof(QualityPresetSetting)))
            {
                if (preset == QualityPresetSetting.Custom) continue;
                foreach (long pixels in displays)
                    Assert.AreEqual(PreTierRenderScale(SettingsAutoDetector.PixelBudgetFor(preset), pixels),
                        SettingsAutoDetector.RecommendRenderScalePercent(preset, pixels), $"{preset} at {pixels}");
            }
        }

        static int PreTierRenderScale(long budget, long nativePixels)
        {
            if (nativePixels <= 0) return 100;
            if (nativePixels <= budget) return 100;
            int percent = Mathf.RoundToInt(Mathf.Sqrt((float)budget / nativePixels) * 100f);
            return Mathf.Clamp(percent, 50, 100);
        }

        #endregion

        #region Render tier (Step 4)

        const string ProceduralSkyPath = "Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat";
        const string BakedSkyPath = "Assets/Resources/PlatformSkyboxes/StaticHyperSeaSkybox.mat";
        const string BakedSkyTexturePath = "Assets/_Graphics/Skyboxes/StaticHyperSeaSky.png";

        static PlatformProfileSO ShippedProfile(DeviceTier tier) =>
            Resources.Load<PlatformProfileSetSO>(PlatformProfileSetSO.ResourcePath).For(tier);

        [TestCase(DeviceTier.Desktop)]
        [TestCase(DeviceTier.MobileHigh)]
        public void DesktopAndMobileHigh_ChangeNothingAboutRendering(DeviceTier tier)
        {
            // The promise of step 4 for Windows and iOS: every render field at its no-change value.
            var profile = ShippedProfile(tier);
            var proceduralSky = AssetDatabase.LoadAssetAtPath<Material>(ProceduralSkyPath);
            Assert.IsFalse(profile.DisableHdr, $"{tier} HDR");
            Assert.AreEqual(-1, profile.MembraneMaxSubdivisions, $"{tier} membrane cap");
            Assert.AreEqual(1f, profile.FoldGateWindowMaxRenderScale, $"{tier} fold-gate cap");
            Assert.IsNull(profile.SkyboxReplacementFor(proceduralSky), $"{tier} skybox swap");
        }

        [Test]
        public void MobileLow_SwapsTheProceduralSkyForItsBake()
        {
            var proceduralSky = AssetDatabase.LoadAssetAtPath<Material>(ProceduralSkyPath);
            var bakedSky = AssetDatabase.LoadAssetAtPath<Material>(BakedSkyPath);
            Assert.IsNotNull(proceduralSky, ProceduralSkyPath);
            Assert.IsNotNull(bakedSky, BakedSkyPath);
            var profile = ShippedProfile(DeviceTier.MobileLow);
            Assert.AreEqual(bakedSky, profile.SkyboxReplacementFor(proceduralSky));
            Assert.IsNull(profile.SkyboxReplacementFor(bakedSky), "a replacement is not itself replaced");
            Assert.IsTrue(profile.DisableHdr, "MobileLow HDR");
            Assert.AreEqual(3, profile.MembraneMaxSubdivisions, "MobileLow membrane cap");
        }

        [Test]
        public void ProfileSet_DoesNotHoldTheBakedSkyResident()
        {
            // Every tier loads the whole profile set, so anything it references - directly or
            // through a profile - is in memory on Windows and iOS too. The 4096x2048 bake must be
            // reachable only by its Resources path, loaded on the tier that swaps.
            var dependencies = AssetDatabase.GetDependencies(
                "Assets/Resources/" + PlatformProfileSetSO.ResourcePath + ".asset", true);
            CollectionAssert.DoesNotContain(dependencies, BakedSkyTexturePath);
            CollectionAssert.DoesNotContain(dependencies, BakedSkyPath);
        }

        [Test]
        public void FoldGateTarget_AFreshTargetAlwaysFitsItsOwnFootprint()
        {
            // The reuse rule and the allocation rule must agree, or a target is released the
            // frame after it was made. They disagreed at the 32-texel floor (a 64 target for a
            // 32 footprint failed a 51.2 shrink bound), so every distant gate reallocated every
            // frame on every platform.
            foreach (int cap in new[] { 540, 960, 1440 })
                for (int needW = 32; needW <= cap; needW++)
                    for (int needH = 32; needH <= cap; needH += 37)
                    {
                        int w = CosmicShore.Gameplay.FoldGatePortalView.TargetSize(needW, cap);
                        int h = CosmicShore.Gameplay.FoldGatePortalView.TargetSize(needH, cap);
                        Assert.IsTrue(CosmicShore.Gameplay.FoldGatePortalView.TargetFits(w, h, needW, needH),
                            $"cap {cap}: a {w}x{h} target made for {needW}x{needH} is rejected");
                    }
        }

        [Test]
        public void FoldGateTarget_ShrinksWhenGrosslyLarger_AndRegrowsWhenTooSmall()
        {
            Assert.IsFalse(CosmicShore.Gameplay.FoldGatePortalView.TargetFits(1024, 1024, 64, 64), "a receding gate frees its big target");
            Assert.IsFalse(CosmicShore.Gameplay.FoldGatePortalView.TargetFits(64, 64, 65, 40), "a target narrower than the footprint");
            Assert.IsTrue(CosmicShore.Gameplay.FoldGatePortalView.TargetFits(96, 96, 70, 70), "a little headroom is kept");
        }

        [Test]
        public void IcosphereVertexCount_PerLevel()
        {
            int[] expected = { 12, 42, 162, 642, 2562 };
            for (int level = 0; level < expected.Length; level++)
                Assert.AreEqual(expected[level], CapsuleMembrane.IcosphereVertexCount(level), $"level {level}");
        }

        [Test]
        public void Icosphere_LowerLevelIsAPrefixOfHigherLevel()
        {
            // The membrane cap draws the first N baked capsules and calls that a lower-level
            // membrane. That is only true while the generator APPENDS midpoints and never reorders:
            // pin it, so a generator rewrite cannot silently turn the cap into a scattered subset.
            var generate = typeof(CapsuleMembrane).GetMethod("GenerateIcosphereVertices",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(generate, "CapsuleMembrane.GenerateIcosphereVertices");
            var full = (List<Vector3>)generate.Invoke(null, new object[] { 4 });
            for (int level = 0; level < 4; level++)
            {
                var lower = (List<Vector3>)generate.Invoke(null, new object[] { level });
                Assert.AreEqual(CapsuleMembrane.IcosphereVertexCount(level), lower.Count, $"level {level} count");
                for (int i = 0; i < lower.Count; i++)
                    Assert.IsTrue((full[i] - lower[i]).sqrMagnitude < 1e-12f, $"level {level} vertex {i}");
            }
        }

        #endregion

        #region Content tier (Step 5)

        [TestCase(DeviceTier.Desktop)]
        [TestCase(DeviceTier.MobileHigh)]
        public void DesktopAndMobileHigh_ChangeNothingAboutContent(DeviceTier tier)
        {
            // The promise of step 5 for Windows and iOS: every content field at its no-change value.
            var profile = ShippedProfile(tier);
            Assert.IsFalse(profile.MenuAutopilotLaysNoTrail, $"{tier} menu trail");
            Assert.AreEqual(0, profile.FreestyleCellPrismBudget, $"{tier} freestyle budget");
            Assert.IsFalse(profile.SkimRaceTrail.Active, $"{tier} Skim Race trail cap");
            Assert.IsFalse(profile.JoustTrail.Active, $"{tier} Joust trail cap");
            Assert.IsFalse(profile.DeactivateMenuWhileFlying, $"{tier} menu teardown");
            Assert.IsFalse(profile.QuietScoreGlow, $"{tier} glow");
            Assert.IsFalse(profile.DisableCytoplasm, $"{tier} cytoplasm");

            var authored = new CosmicShore.Gameplay.ConveyorConfig();
            var applied = new CosmicShore.Gameplay.ConveyorConfig();
            profile.Wanderway.ApplyTo(applied);
            AssertSameBelt(authored, applied, tier.ToString());
        }

        [Test]
        public void MobileLow_RunsTheStripsContentNumbers()
        {
            // Garrett's strip, as a profile: the race caps (PerfStrip.SkimRaceTrail* / JoustTrail*),
            // the freestyle wait (10,000 / 9,700) and Wander_WithoutArk.asset's belt.
            var profile = ShippedProfile(DeviceTier.MobileLow);
            Assert.IsTrue(profile.MenuAutopilotLaysNoTrail, "menu trail");
            Assert.AreEqual(10000, profile.FreestyleCellPrismBudget, "freestyle budget");
            Assert.AreEqual(9700, profile.FreestyleCellPrismResume, "freestyle resume");
            Assert.Less(profile.FreestyleCellPrismResume, profile.FreestyleCellPrismBudget, "hysteresis");
            Assert.AreEqual(2000, profile.SkimRaceTrail.PerVessel(1), "Skim Race solo");
            Assert.AreEqual(800, profile.SkimRaceTrail.PerVessel(12), "Skim Race 12 seats");
            Assert.AreEqual(1200, profile.JoustTrail.PerVessel(2), "Joust 2 seats");
            Assert.AreEqual(400, profile.JoustTrail.PerVessel(12), "Joust 12 seats");
            Assert.IsTrue(profile.DeactivateMenuWhileFlying && profile.QuietScoreGlow && profile.DisableCytoplasm);

            var cfg = new CosmicShore.Gameplay.ConveyorConfig();
            profile.Wanderway.ApplyTo(cfg);
            Assert.AreEqual(8, cfg.PoolSize, "pool");
            Assert.AreEqual(150, cfg.PrismBudget, "prisms per scene");
            Assert.AreEqual(4, cfg.AheadTargetScenes, "ahead");
            Assert.AreEqual(2, cfg.MaxCrystalsPerScene, "crystals");
            Assert.IsFalse(cfg.LifeformScenes, "lifeforms");
            Assert.AreEqual(2, cfg.MaxConcurrentArrivals, "arrivals");
        }

        // The strip's CappedTrailPrismsPerVessel: a share of the race budget, clamped to
        // [floor, ceiling], so the cap does not multiply with the seat count.
        [TestCase(0, 2000)]
        [TestCase(1, 2000)]
        [TestCase(3, 2000)]
        [TestCase(4, 1500)]
        [TestCase(6, 1000)]
        [TestCase(8, 800)]
        [TestCase(12, 800)]
        public void RaceTrailBudget_SharesTheRaceBudget(int vessels, int expected)
        {
            var budget = new PlatformProfileSO.RaceTrailBudget(6000, 800, 2000);
            Assert.AreEqual(expected, budget.PerVessel(vessels));
        }

        [Test]
        public void RaceTrailBudget_ZeroIsOff_AndAFloorAboveTheCeilingWins()
        {
            Assert.IsFalse(default(PlatformProfileSO.RaceTrailBudget).Active, "default");
            Assert.IsFalse(new PlatformProfileSO.RaceTrailBudget(0, 800, 2000).Active, "zero budget");
            Assert.AreEqual(500, new PlatformProfileSO.RaceTrailBudget(100, 500, 200).PerVessel(1),
                "a misauthored ceiling under the floor must not drop below the floor");
            Assert.AreEqual(1, new PlatformProfileSO.RaceTrailBudget(1, 0, 0).PerVessel(5),
                "never a zero-prism cap");
        }

        [Test]
        public void WanderwayBudget_KeepAll_LeavesTheAuthoredBeltAlone()
        {
            var authored = new CosmicShore.Gameplay.ConveyorConfig
            {
                PoolSize = 13, PrismBudget = 777, AheadTargetScenes = 3, MaxCrystalsPerScene = 0,
                LifeformScenes = false, MaxConcurrentArrivals = 5,
            };
            var applied = new CosmicShore.Gameplay.ConveyorConfig
            {
                PoolSize = 13, PrismBudget = 777, AheadTargetScenes = 3, MaxCrystalsPerScene = 0,
                LifeformScenes = false, MaxConcurrentArrivals = 5,
            };
            PlatformProfileSO.WanderwayBudget.KeepAll.ApplyTo(applied);
            AssertSameBelt(authored, applied, "KeepAll");
            Assert.DoesNotThrow(() => PlatformProfileSO.WanderwayBudget.KeepAll.ApplyTo(null));
        }

        static void AssertSameBelt(CosmicShore.Gameplay.ConveyorConfig expected,
                                   CosmicShore.Gameplay.ConveyorConfig actual, string label)
        {
            Assert.AreEqual(expected.PoolSize, actual.PoolSize, $"{label} pool");
            Assert.AreEqual(expected.PrismBudget, actual.PrismBudget, $"{label} prisms per scene");
            Assert.AreEqual(expected.AheadTargetScenes, actual.AheadTargetScenes, $"{label} ahead");
            Assert.AreEqual(expected.MaxCrystalsPerScene, actual.MaxCrystalsPerScene, $"{label} crystals");
            Assert.AreEqual(expected.LifeformScenes, actual.LifeformScenes, $"{label} lifeforms");
            Assert.AreEqual(expected.MaxConcurrentArrivals, actual.MaxConcurrentArrivals, $"{label} arrivals");
        }

        #endregion
    }
}
#endif
