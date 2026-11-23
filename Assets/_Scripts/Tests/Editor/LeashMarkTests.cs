using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The leash's mark (Garrett, 2026-10-21: "distinguish them by giving their spindles a slight hue shift in your domain.
    /// do the same thing for other tamer classes like the angler"): a tamed creature's spindles lean toward its holder's
    /// domain hue (<see cref="Spindle.LeanHue"/>), and the mark travels to puppets packed in one uint
    /// (<see cref="FaunaNetworkSync.PackLeashMark"/>), with the herd's glow.
    /// </summary>
    [TestFixture]
    public class LeashMarkTests
    {
        const float Eps = 1e-3f;

        static float Hue(Color c)
        {
            Color.RGBToHSV(c, out float h, out _, out _);
            return h;
        }

        [Test]
        public void LeanHue_TurnsTheHuePartWayTowardTheTarget_TheShortWay()
        {
            var blue = Color.HSVToRGB(0.65f, 0.8f, 0.9f);
            var green = Color.HSVToRGB(0.40f, 0.9f, 0.9f);
            Assert.AreEqual(0.65f - 0.25f * 0.35f, Hue(Spindle.LeanHue(blue, green, 0.35f)), Eps);
            // across 0: red-orange (0.05) toward magenta (0.90) turns down through 0, not up through green
            var orange = Color.HSVToRGB(0.05f, 0.8f, 0.9f);
            var magenta = Color.HSVToRGB(0.90f, 0.9f, 0.9f);
            Assert.AreEqual(Mathf.Repeat(0.05f - 0.15f * 0.5f, 1f), Hue(Spindle.LeanHue(orange, magenta, 0.5f)), Eps);
        }

        [Test]
        public void LeanHue_KeepsSaturationValueAndAlpha_HdrIncluded()
        {
            var hdr = Color.HSVToRGB(0.65f, 0.6f, 1.5f, true);
            hdr.a = 0.4f;
            var leaned = Spindle.LeanHue(hdr, Color.HSVToRGB(0.1f, 1f, 1f), 0.35f);
            Color.RGBToHSV(leaned, out _, out float s, out float v);
            Assert.AreEqual(0.6f, s, Eps);
            Assert.AreEqual(1.5f, v, Eps);
            Assert.AreEqual(0.4f, leaned.a, Eps);
        }

        [Test]
        public void LeanHue_NoLeanOrAGreyTarget_ChangesNothing()
        {
            var blue = Color.HSVToRGB(0.65f, 0.8f, 0.9f);
            Assert.AreEqual(blue, Spindle.LeanHue(blue, Color.green, 0f));
            Assert.AreEqual(blue, Spindle.LeanHue(blue, Color.gray, 0.5f));
        }

        [Test]
        public void Mark_RoundTripsOnTheWire_AndNoLeanIsWild()
        {
            uint packed = FaunaNetworkSync.PackLeashMark(Domains.Gold, 0.35f, 0f);
            FaunaNetworkSync.UnpackLeashMark(packed, out var domain, out float lean, out float glow);
            Assert.AreEqual(Domains.Gold, domain);
            Assert.AreEqual(0.35f, lean, 1f / 255f);
            Assert.AreEqual(0f, glow);
            Assert.AreEqual(0u, FaunaNetworkSync.PackLeashMark(Domains.Ruby, 0f, 0f));
            FaunaNetworkSync.UnpackLeashMark(0, out _, out float none, out float unlit);
            Assert.AreEqual(0f, none);
            Assert.AreEqual(0f, unlit);
        }

        [Test]
        public void Mark_CarriesTheGlow_WithTheLeanAndTheDomainIntact()
        {
            // Garrett, 2026-10-23: "fauna that get herded should light up when in herding mode"
            uint packed = FaunaNetworkSync.PackLeashMark(Domains.Blue, 0.35f, 1.5f);
            FaunaNetworkSync.UnpackLeashMark(packed, out var domain, out float lean, out float glow);
            Assert.AreEqual(Domains.Blue, domain);
            Assert.AreEqual(0.35f, lean, 1f / 255f);
            Assert.AreEqual(1.5f, glow, Spindle.GlowQuantum);
            // a glow alone is still a mark, and the byte saturates instead of wrapping
            Assert.AreNotEqual(0u, FaunaNetworkSync.PackLeashMark(Domains.Jade, 0f, 0.5f));
            Assert.AreEqual(255, Spindle.QuantizeGlow(100f));
        }

        [Test]
        public void Glow_MultipliesRgb_KeepsHueAndAlpha()
        {
            var c = new Color(0.2f, 0.4f, 0.8f, 0.5f);
            var lit = Spindle.Glow(c, 2.5f);
            Assert.AreEqual(0.5f, lit.r, Eps);
            Assert.AreEqual(1.0f, lit.g, Eps);
            Assert.AreEqual(2.0f, lit.b, Eps);
            Assert.AreEqual(0.5f, lit.a, Eps);
            Assert.AreEqual(Hue(c), Hue(lit), Eps);
        }
    }
}
