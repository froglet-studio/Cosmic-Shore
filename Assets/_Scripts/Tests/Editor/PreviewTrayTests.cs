#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The configure window's FULL SCREEN as a tray (Docs/MENU_REDESIGN_B.md section 108): the preview renders a texture
    /// as wide as the tray and shows the share of it its frame has room for, so the framed view is unchanged and the open
    /// tray shows more of the arena, never a stretched one.
    /// </summary>
    [TestFixture]
    public class PreviewTrayTests
    {
        // The arcade configure window at 1080: a 1100 x 620 frame, a tray 700 wider, so a texture 1800 / 620 wide.
        const float FrameW = 1100f, FrameH = 620f, TrayW = 1800f;
        static readonly float TextureAspect = TrayW / FrameH;

        [Test]
        public void TheFramedPreview_ShowsTheCentreOfTheWideTexture()
        {
            Assert.AreEqual(FrameW / TrayW, ModePreviewWindow.VisibleWidth(FrameW, FrameH, TextureAspect), 1e-4,
                "the framed view is the same picture as before: the centre 61 % of a texture 1.64x wider");
        }

        [Test]
        public void TheOpenTray_ShowsAllOfIt_AndNeverStretches()
        {
            Assert.AreEqual(1f, ModePreviewWindow.VisibleWidth(TrayW, FrameH, TextureAspect), 1e-4);
            Assert.AreEqual(1f, ModePreviewWindow.VisibleWidth(TrayW + 300, FrameH, TextureAspect), 1e-4,
                "past the texture's width it shows all of it (clamped)");
            float half = ModePreviewWindow.VisibleWidth((FrameW + TrayW) / 2, FrameH, TextureAspect);
            Assert.That(half, Is.GreaterThan(FrameW / TrayW).And.LessThan(1f), "the tray reveals more as it opens");
        }

        [Test]
        public void AFrameTheTextureMatches_ShowsAllOfIt()
        {
            Assert.AreEqual(1f, ModePreviewWindow.VisibleWidth(FrameW, FrameH, FrameW / FrameH), 1e-4,
                "textureWidthScale 1 (every other preview) draws the whole texture, as before");
            Assert.AreEqual(1f, ModePreviewWindow.VisibleWidth(FrameW, 0f, 2f), "an unmeasured frame shows it all");
        }
    }
}
#endif
