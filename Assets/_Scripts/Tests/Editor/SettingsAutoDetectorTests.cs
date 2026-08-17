#if UNITY_EDITOR
using CosmicShore.Core;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pure-math coverage for the pixel-budget half of <see cref="SettingsAutoDetector"/>.
    /// The capability score reads live <c>SystemInfo</c> and is therefore machine-dependent;
    /// the render-scale / AA helpers take pixel counts as arguments precisely so they can be
    /// pinned here without a display.
    /// </summary>
    public class SettingsAutoDetectorTests
    {
        // Reference displays, in pixels.
        const long Pixels1080p = 1920L * 1080L;   // 2.07M
        const long Pixels1440p = 2560L * 1440L;   // 3.69M
        const long Pixels4K = 3840L * 2160L;      // 8.29M
        const long PixelsMbp16 = 3456L * 2234L;   // 7.72M - 16" MacBook Pro native
        const long Pixels5K = 5120L * 2880L;      // 14.75M

        #region Render scale

        [Test]
        public void RenderScale_UnderBudget_RendersNative()
        {
            // A 1080p display is inside every tier's budget from High up.
            Assert.AreEqual(100, SettingsAutoDetector.RecommendRenderScalePercent(
                QualityPresetSetting.High, Pixels1080p));
            Assert.AreEqual(100, SettingsAutoDetector.RecommendRenderScalePercent(
                QualityPresetSetting.Ultra, Pixels1080p));
        }

        [Test]
        public void RenderScale_UnknownDisplay_RendersNative()
        {
            // Batch mode / CI: no display information is not a licence to guess.
            Assert.AreEqual(100, SettingsAutoDetector.RecommendRenderScalePercent(
                QualityPresetSetting.Ultra, 0L));
            Assert.AreEqual(100, SettingsAutoDetector.RecommendRenderScalePercent(
                QualityPresetSetting.Ultra, -1L));
        }

        [Test]
        public void RenderScale_RetinaMacBook_ScalesDown()
        {
            // The case this exists for: an 8-core / 16 GB MacBook scores VeryHigh and would
            // otherwise render 7.7M pixels a frame on an overdraw-bound renderer.
            int percent = SettingsAutoDetector.RecommendRenderScalePercent(
                QualityPresetSetting.VeryHigh, PixelsMbp16);

            Assert.Less(percent, 100, "A 7.7M-pixel panel must not render native at VeryHigh.");
            Assert.GreaterOrEqual(percent, 50);
        }

        [Test]
        public void RenderScale_BringsPixelsToBudget()
        {
            // Render scale is linear per axis, so the resulting AREA should land at (or just
            // under) the tier budget - that is the whole contract of the square root.
            const QualityPresetSetting tier = QualityPresetSetting.VeryHigh;
            int percent = SettingsAutoDetector.RecommendRenderScalePercent(tier, Pixels5K);

            long effective = Pixels5K * percent * percent / 10_000L;
            long budget = SettingsAutoDetector.PixelBudgetFor(tier);

            // Allow 2% slack for the integer rounding of the percentage.
            Assert.LessOrEqual(effective, (long)(budget * 1.02f));
            Assert.Greater(effective, (long)(budget * 0.90f));
        }

        [Test]
        public void RenderScale_NeverBelowFloor()
        {
            // An extreme panel on the weakest tier still must not smear the prism edges away.
            int percent = SettingsAutoDetector.RecommendRenderScalePercent(
                QualityPresetSetting.VeryLow, Pixels5K * 4);

            Assert.GreaterOrEqual(percent, 50);
        }

        [Test]
        public void RenderScale_HigherTier_NeverScalesDownMore()
        {
            // Monotonic in tier: a stronger machine may render more pixels, never fewer.
            foreach (long pixels in new[] { Pixels1440p, Pixels4K, PixelsMbp16, Pixels5K })
            {
                int low = SettingsAutoDetector.RecommendRenderScalePercent(QualityPresetSetting.Low, pixels);
                int high = SettingsAutoDetector.RecommendRenderScalePercent(QualityPresetSetting.High, pixels);
                int ultra = SettingsAutoDetector.RecommendRenderScalePercent(QualityPresetSetting.Ultra, pixels);

                Assert.LessOrEqual(low, high, $"Low > High at {pixels} px");
                Assert.LessOrEqual(high, ultra, $"High > Ultra at {pixels} px");
            }
        }

        [Test]
        public void RenderScale_MorePixels_NeverScalesUp()
        {
            // Monotonic in pixels: a denser panel can only ask for the same or a lower scale.
            int at1080 = SettingsAutoDetector.RecommendRenderScalePercent(QualityPresetSetting.High, Pixels1080p);
            int at1440 = SettingsAutoDetector.RecommendRenderScalePercent(QualityPresetSetting.High, Pixels1440p);
            int at4K = SettingsAutoDetector.RecommendRenderScalePercent(QualityPresetSetting.High, Pixels4K);
            int at5K = SettingsAutoDetector.RecommendRenderScalePercent(QualityPresetSetting.High, Pixels5K);

            Assert.GreaterOrEqual(at1080, at1440);
            Assert.GreaterOrEqual(at1440, at4K);
            Assert.GreaterOrEqual(at4K, at5K);
        }

        #endregion

        #region Anti-aliasing

        [Test]
        public void AntiAliasing_LowTiers_UsePostProcessAA()
        {
            Assert.AreEqual(AntiAliasingSetting.FXAA,
                SettingsAutoDetector.RecommendAntiAliasing(QualityPresetSetting.VeryLow, Pixels1080p));
            Assert.AreEqual(AntiAliasingSetting.FXAA,
                SettingsAutoDetector.RecommendAntiAliasing(QualityPresetSetting.Low, Pixels1080p));
            Assert.AreEqual(AntiAliasingSetting.SMAA,
                SettingsAutoDetector.RecommendAntiAliasing(QualityPresetSetting.Medium, Pixels1080p));
        }

        [Test]
        public void AntiAliasing_StrongTierModestPanel_GetsMsaa4x()
        {
            Assert.AreEqual(AntiAliasingSetting.MSAA4x,
                SettingsAutoDetector.RecommendAntiAliasing(QualityPresetSetting.High, Pixels1080p));
            Assert.AreEqual(AntiAliasingSetting.MSAA4x,
                SettingsAutoDetector.RecommendAntiAliasing(QualityPresetSetting.Ultra, Pixels1080p));
        }

        [Test]
        public void AntiAliasing_StepsDownAsPixelsRise()
        {
            // The regression this guards: MSAA4x handed to a high-DPI panel because the tier,
            // not the pixel count, chose the sample count.
            Assert.AreEqual(AntiAliasingSetting.MSAA2x,
                SettingsAutoDetector.RecommendAntiAliasing(QualityPresetSetting.Ultra, 4_000_000L));
            Assert.AreEqual(AntiAliasingSetting.SMAA,
                SettingsAutoDetector.RecommendAntiAliasing(QualityPresetSetting.Ultra, Pixels4K));
        }

        [Test]
        public void AntiAliasing_UnknownPixelCount_StaysGenerous()
        {
            // 0 pixels means "no display information", not "a huge display".
            Assert.AreEqual(AntiAliasingSetting.MSAA4x,
                SettingsAutoDetector.RecommendAntiAliasing(QualityPresetSetting.High, 0L));
        }

        #endregion

        #region Budgets

        [Test]
        public void PixelBudget_IsMonotonicInTier()
        {
            Assert.LessOrEqual(
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.VeryLow),
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.Low));
            Assert.LessOrEqual(
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.Low),
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.Medium));
            Assert.LessOrEqual(
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.Medium),
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.High));
            Assert.LessOrEqual(
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.High),
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.VeryHigh));
            Assert.LessOrEqual(
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.VeryHigh),
                SettingsAutoDetector.PixelBudgetFor(QualityPresetSetting.Ultra));
        }

        #endregion
    }
}
#endif
