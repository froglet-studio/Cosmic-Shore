using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The safe-area contract: a full-screen safe area is a no-op (desktop), and any inset device
    /// safe area converts to normalized anchors exactly, clamped into 0..1. Both functions under
    /// test are pure, so this suite is the verification that does not need a device or the
    /// Device Simulator.
    /// </summary>
    public class SafeAreaFitterTests
    {
        const float Tolerance = 1e-6f;

        // Real reported safe areas, in the device's native pixel space.
        // iPhone X class, landscape: notch on one side, home indicator along the bottom.
        static readonly Rect NotchLandscape = new(132f, 63f, 2172f, 1062f);
        const int NotchLandscapeWidth = 2436;
        const int NotchLandscapeHeight = 1125;

        // An Android-style single-edge cutout, landscape left then landscape right. The project is
        // LANDSCAPE-ONLY (portrait auto-rotate is off in ProjectSettings), so this — not a portrait
        // flip — is the orientation change the fitter has to survive, and it happens at an
        // IDENTICAL resolution. Note the iPhone rects above are SYMMETRIC (the OS insets both ends
        // in landscape), so they cannot demonstrate a side swap; this device can.
        static readonly Rect CutoutLandscapeLeft = new(88f, 0f, 2252f, 1080f);
        static readonly Rect CutoutLandscapeRight = new(0f, 0f, 2252f, 1080f);
        const int CutoutWidth = 2340;
        const int CutoutHeight = 1080;

        // Same device held in portrait. Portrait is disabled project-wide; kept as math coverage on
        // the other axis, since the conversion is orientation-agnostic.
        static readonly Rect NotchPortrait = new(0f, 102f, 1125f, 2202f);
        const int NotchPortraitWidth = 1125;
        const int NotchPortraitHeight = 2436;

        // ── Full-screen detection: the desktop no-op ──

        [Test]
        public void FullScreenSafeArea_IsDetected()
        {
            Assert.IsTrue(SafeAreaFitter.IsFullScreenSafeArea(new Rect(0f, 0f, 1920f, 1080f), 1920, 1080),
                "A safe area equal to the screen must report as full screen so the fitter no-ops.");
        }

        [Test]
        public void FullScreenSafeArea_ToleratesSubPixelRounding()
        {
            Assert.IsTrue(SafeAreaFitter.IsFullScreenSafeArea(new Rect(0.25f, 0f, 1919.5f, 1080f), 1920, 1080),
                "Sub-pixel slack is rounding, not an inset.");
        }

        [Test]
        public void InsetSafeArea_IsNotFullScreen([Values(true, false)] bool landscape)
        {
            Rect safeArea = landscape ? NotchLandscape : NotchPortrait;
            int width = landscape ? NotchLandscapeWidth : NotchPortraitWidth;
            int height = landscape ? NotchLandscapeHeight : NotchPortraitHeight;

            Assert.IsFalse(SafeAreaFitter.IsFullScreenSafeArea(safeArea, width, height),
                "A device safe area with a cutout must not read as full screen.");
        }

        // ── Anchor math ──

        [Test]
        public void Anchors_MatchTheSafeAreaFraction_Landscape()
        {
            SafeAreaFitter.ComputeAnchors(NotchLandscape, NotchLandscapeWidth, NotchLandscapeHeight,
                out Vector2 anchorMin, out Vector2 anchorMax);

            Assert.AreEqual(132f / NotchLandscapeWidth, anchorMin.x, Tolerance, "left inset");
            Assert.AreEqual(63f / NotchLandscapeHeight, anchorMin.y, Tolerance, "bottom inset");
            Assert.AreEqual(2304f / NotchLandscapeWidth, anchorMax.x, Tolerance, "right edge");
            Assert.AreEqual(1f, anchorMax.y, Tolerance, "top edge is flush");
        }

        [Test]
        public void Anchors_MatchTheSafeAreaFraction_Portrait()
        {
            SafeAreaFitter.ComputeAnchors(NotchPortrait, NotchPortraitWidth, NotchPortraitHeight,
                out Vector2 anchorMin, out Vector2 anchorMax);

            Assert.AreEqual(0f, anchorMin.x, Tolerance, "left edge is flush");
            Assert.AreEqual(102f / NotchPortraitHeight, anchorMin.y, Tolerance, "home indicator");
            Assert.AreEqual(1f, anchorMax.x, Tolerance, "right edge is flush");
            Assert.AreEqual(2304f / NotchPortraitHeight, anchorMax.y, Tolerance, "notch");
        }

        [Test]
        public void Anchors_MirrorWhenTheCutoutSwapsSides()
        {
            // Landscape left vs landscape right: same resolution, same safe-area SIZE, opposite
            // side. A change check keyed on width/height alone would sleep through this, which is
            // why the fitter also compares the safe-area rect and Screen.orientation.
            SafeAreaFitter.ComputeAnchors(CutoutLandscapeLeft, CutoutWidth, CutoutHeight,
                out Vector2 leftMin, out Vector2 leftMax);
            SafeAreaFitter.ComputeAnchors(CutoutLandscapeRight, CutoutWidth, CutoutHeight,
                out Vector2 rightMin, out Vector2 rightMax);

            Assert.AreNotEqual(leftMin.x, rightMin.x, "the two landscape orientations must not agree");
            Assert.AreEqual(leftMin.x, 1f - rightMax.x, Tolerance, "the inset mirrors across the screen");
            Assert.AreEqual(leftMax.x, 1f - rightMin.x, Tolerance, "the flush edge mirrors too");
            Assert.AreEqual(leftMax.x - leftMin.x, rightMax.x - rightMin.x, Tolerance,
                "the safe span is the same width in both orientations");
        }

        [Test]
        public void Anchors_AreClampedIntoTheCanvas()
        {
            // A safe area reported larger than the screen must not push anchors outside 0..1.
            SafeAreaFitter.ComputeAnchors(new Rect(-10f, -10f, 2000f, 1200f), 1920, 1080,
                out Vector2 anchorMin, out Vector2 anchorMax);

            Assert.AreEqual(Vector2.zero, anchorMin, "anchorMin clamped");
            Assert.AreEqual(Vector2.one, anchorMax, "anchorMax clamped");
        }

        [Test]
        public void Anchors_FallBackToFullStretch_OnADegenerateScreen()
        {
            // Screen dimensions can read as zero for a frame during a device rotation.
            SafeAreaFitter.ComputeAnchors(new Rect(0f, 0f, 0f, 0f), 0, 0,
                out Vector2 anchorMin, out Vector2 anchorMax);

            Assert.AreEqual(Vector2.zero, anchorMin, "degenerate screen must not collapse the rect");
            Assert.AreEqual(Vector2.one, anchorMax, "degenerate screen must not collapse the rect");
        }
    }
}
