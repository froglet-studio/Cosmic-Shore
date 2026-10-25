using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// ONE Y-inversion rule across devices and hulls (Garrett, 2026-10-21: "get rid of ... inconsistent
    /// y-inversions that are not left to the settings").
    ///
    /// <para>A two-stick hull pitches by <c>YSum</c> (<c>VesselTransformer.Pitch</c>: a positive angle about
    /// the hull's right dives) and a one-thumb hull by <c>-EasedLeftJoystickPosition.y</c>
    /// (<c>SingleStickVesselTransformer.Pitch</c>, <c>ScarabVesselTransformer.Pitch</c>). So the same push must
    /// give <c>YSum</c> and <c>-EasedLeft.y</c> the SAME sign, with Invert Y off and with it on. Before this,
    /// the setting flipped <c>YSum</c> only: on a pad or keyboard it did nothing on the nine one-thumb hulls,
    /// touch never read it, and dual mouse pitched the other way by default.</para>
    /// </summary>
    [TestFixture]
    public class InputInversionTests
    {
        const string IoDir = "Assets/_Scripts/Controller/IO";

        static float DualStickPitch(DualStickMixResult m) => m.YSum;
        static float OneStickPitch(DualStickMixResult m) => -m.EasedLeft.y;

        [TestCase(false)]
        [TestCase(true)]
        public void APushUpPitchesBothKindsOfHullTheSameWay(bool invertY)
        {
            var up = new Vector2(0f, 0.8f);
            var mix = DualStickMix.Mix(up, up, invertY);
            Assert.That(Mathf.Sign(DualStickPitch(mix)), Is.EqualTo(Mathf.Sign(OneStickPitch(mix))),
                "a two-stick and a one-thumb hull must pitch the same way for the same push");
        }

        [Test]
        public void InvertYTurnsOverEveryVerticalReading_AndNothingElse()
        {
            var left = new Vector2(0.3f, 0.6f);
            var right = new Vector2(-0.2f, 0.4f);
            var plain = DualStickMix.Mix(left, right);
            var inverted = DualStickMix.Mix(left, right, invertY: true);

            Assert.That(inverted.YSum, Is.EqualTo(-plain.YSum).Within(1e-6f));
            Assert.That(inverted.YDiff, Is.EqualTo(-plain.YDiff).Within(1e-6f));
            Assert.That(inverted.EasedLeft.y, Is.EqualTo(-plain.EasedLeft.y).Within(1e-6f));
            Assert.That(inverted.EasedRight.y, Is.EqualTo(-plain.EasedRight.y).Within(1e-6f));
            Assert.That(inverted.XSum, Is.EqualTo(plain.XSum));
            Assert.That(inverted.XDiff, Is.EqualTo(plain.XDiff));
            Assert.That(inverted.EasedLeft.x, Is.EqualTo(plain.EasedLeft.x));
        }

        /// <summary>
        /// Every input strategy that writes pitch either goes through the shared rule
        /// (<c>ApplyInvertSettings</c>, or <c>DualStickMix</c> with the flags) or inverts at the source
        /// (the one-thumb mouse, which turns the stick over before it is read). A strategy that writes
        /// <c>YSum</c> with neither, or writes it without the shared minus, is the defect this guards.
        /// </summary>
        [Test]
        public void EveryStrategyThatWritesPitchHonoursTheSetting_WithTheSameSign()
        {
            string[] strategies =
            {
                "GamepadInputStrategy.cs", "KeyboardInputStrategy.cs", "TouchInputStrategy.cs",
                "DualMouseInputStrategy.cs", "SingleStickMouseInputStrategy.cs",
            };
            foreach (var name in strategies)
            {
                string text = File.ReadAllText(Path.Combine(IoDir, name));
                bool shared = text.Contains("ApplyInvertSettings()")
                              || (text.Contains("DualStickMix.Mix(") && text.Contains("InvertYEnabled"))
                              || Regex.IsMatch(text, @"InvertYEnabled\s*\?\s*new Vector2\(\s*\w+\.x\s*,\s*-");
                Assert.That(shared, Is.True, $"{name} writes pitch without the Invert Y setting");

                foreach (Match m in Regex.Matches(text, @"inputStatus\.YSum\s*=\s*([^;]+);"))
                {
                    string rhs = m.Groups[1].Value.Trim();
                    if (rhs == "0" || rhs == "0f" || rhs.StartsWith("mix.")) continue;
                    Assert.That(rhs.StartsWith("-Ease("), Is.True,
                        $"{name}: `YSum = {rhs}` - pitch must carry the fleet's sign (-Ease, as DualStickMix)");
                }
            }
        }
    }
}
