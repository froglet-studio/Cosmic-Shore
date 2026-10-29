#if UNITY_EDITOR
using CosmicShore.Editor.AI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="ThirdEyeStill"/> - the Third Eye's overlay drawn into a saved still. These pin what a still would get
    /// wrong: a line that misses its end pixels, a ring that fills its middle, a stroke past the image's edge that
    /// throws instead of being cut, and a half-transparent colour that replaces the picture instead of tinting it.
    /// </summary>
    public class ThirdEyeStillTests
    {
        static Color32[] Black(int w, int h)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 255);
            return px;
        }

        static bool Lit(Color32[] px, int w, int x, int y) => px[y * w + x].r > 0 || px[y * w + x].g > 0 || px[y * w + x].b > 0;

        [Test]
        public void Line_LightsBothEndsAndThePathBetween_AndNothingFarFromIt()
        {
            var px = Black(10, 10);
            ThirdEyeStill.Line(px, 10, 10, new Vector2(1, 1), new Vector2(8, 8), Color.white, 1f);
            Assert.IsTrue(Lit(px, 10, 1, 1));
            Assert.IsTrue(Lit(px, 10, 8, 8));
            Assert.IsTrue(Lit(px, 10, 5, 5));
            Assert.IsFalse(Lit(px, 10, 8, 1), "a pixel far from the line stays the picture");
        }

        [Test]
        public void Ring_DrawsTheCircleNotItsMiddle()
        {
            var px = Black(40, 40);
            ThirdEyeStill.Ring(px, 40, 40, new Vector2(20, 20), 10f, Color.white, 1f);
            Assert.IsTrue(Lit(px, 40, 30, 20));
            Assert.IsTrue(Lit(px, 40, 20, 10));
            Assert.IsFalse(Lit(px, 40, 20, 20), "the ring's centre stays the picture");
        }

        [Test]
        public void StrokesPastTheEdge_AreCut_NotThrown()
        {
            var px = Black(8, 8);
            Assert.DoesNotThrow(() => ThirdEyeStill.Line(px, 8, 8, new Vector2(-50, 4), new Vector2(50, 4), Color.white, 3f));
            Assert.DoesNotThrow(() => ThirdEyeStill.Ring(px, 8, 8, new Vector2(0, 0), 30f, Color.white, 2f));
            Assert.DoesNotThrow(() => ThirdEyeStill.Disc(px, 8, 8, new Vector2(100, 100), 4f, Color.white));
            Assert.IsTrue(Lit(px, 8, 0, 4) && Lit(px, 8, 7, 4), "the part inside the image is drawn");
        }

        [Test]
        public void Blend_TintsByAlpha_AndKeepsTheStillOpaque()
        {
            var c = ThirdEyeStill.Blend(new Color32(100, 100, 100, 255), new Color(1f, 0f, 0f, 0.5f));
            Assert.AreEqual(178, c.r, 1);
            Assert.AreEqual(50, c.g, 1);
            Assert.AreEqual(255, c.a);
            var none = ThirdEyeStill.Blend(new Color32(100, 100, 100, 255), new Color(1f, 1f, 1f, 0f));
            Assert.AreEqual(100, none.r, "a clear colour leaves the picture as it was");
        }

        [Test]
        public void ToPixel_MapsTheViewportOntoTheImage_OriginBottomLeft()
        {
            Assert.AreEqual(new Vector2(0, 0), ThirdEyeStill.ToPixel(new Vector3(0, 0, 5), 200, 100));
            Assert.AreEqual(new Vector2(100, 75), ThirdEyeStill.ToPixel(new Vector3(0.5f, 0.75f, 5), 200, 100));
        }
    }
}
#endif
