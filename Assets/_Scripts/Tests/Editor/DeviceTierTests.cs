#if UNITY_EDITOR
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
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
    }
}
#endif
