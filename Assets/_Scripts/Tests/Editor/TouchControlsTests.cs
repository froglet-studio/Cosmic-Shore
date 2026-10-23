using System.Collections.Generic;
using System.IO;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The phone controls' pure parts (Docs/Reports/MOBILE_VESSELS.md, Garrett 2026-10-21): the tilt
    /// signs a pilot steers by and the thumb layout the buttons sit in. Neither can be seen without a
    /// device, so the signs and the screen-size behaviour are pinned here.
    /// </summary>
    [TestFixture]
    public class TouchControlsTests
    {
        // ---- Tilt ---------------------------------------------------------------------------

        // The game locks the screen to LandscapeLeft: screen up = device +x, screen right = device -y,
        // z out of the glass. Upright in landscape, gravity points down the screen: device (-1, 0, 0).
        static Vector3 Gravity(float rollDegrees, float pitchDegrees)
        {
            // Start upright (gravity down the screen), tip the top away by pitch, then turn like a wheel.
            float p = pitchDegrees * Mathf.Deg2Rad, r = rollDegrees * Mathf.Deg2Rad;
            float inPlane = Mathf.Cos(p);
            float screenRight = Mathf.Sin(r) * inPlane;   // gravity swings right on a clockwise turn
            float screenUp = -Mathf.Cos(r) * inPlane;
            float outOfGlass = -Mathf.Sin(p);             // tipped away, the glass faces up
            return new Vector3(screenUp, -screenRight, outOfGlass);
        }

        [Test]
        public void UprightReadsLevel()
        {
            var a = TiltMath.Angles(Gravity(0f, 0f));
            Assert.That(a.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(a.y, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void TurningTheWheelClockwiseReadsAsARightTurn()
        {
            var reference = TiltMath.Angles(Gravity(0f, 30f));
            var stick = TiltMath.Stick(TiltMath.Angles(Gravity(15f, 30f)), reference, 25f, 2.5f);
            Assert.That(stick.x, Is.GreaterThan(0.5f));
            Assert.That(Mathf.Abs(stick.y), Is.LessThan(0.05f), "a pure wheel turn must not pitch");

            stick = TiltMath.Stick(TiltMath.Angles(Gravity(-15f, 30f)), reference, 25f, 2.5f);
            Assert.That(stick.x, Is.LessThan(-0.5f));
        }

        [Test]
        public void TippingTheTopAwayReadsForward_AndIsMeasuredFromHowThePhoneWasHeld()
        {
            // Held tipped back 35 degrees (a normal grip): that is neutral, not "pitched".
            var reference = TiltMath.Angles(Gravity(0f, 35f));
            Assert.That(TiltMath.Stick(reference, reference, 25f, 2.5f), Is.EqualTo(Vector2.zero));

            var stick = TiltMath.Stick(TiltMath.Angles(Gravity(0f, 50f)), reference, 25f, 2.5f);
            Assert.That(stick.y, Is.GreaterThan(0.5f));
            Assert.That(Mathf.Abs(stick.x), Is.LessThan(0.05f), "a pure tip must not turn");
        }

        [Test]
        public void TheDeadZoneHoldsAShakyHandStraight_AndTheStickRampsFromItsEdge()
        {
            var reference = Vector2.zero;
            Assert.That(TiltMath.Stick(new Vector2(2f, -2f), reference, 25f, 2.5f), Is.EqualTo(Vector2.zero));
            float justOut = TiltMath.Stick(new Vector2(3f, 0f), reference, 25f, 2.5f).x;
            Assert.That(justOut, Is.GreaterThan(0f).And.LessThan(0.05f), "no step leaving the dead zone");
            Assert.That(TiltMath.Stick(new Vector2(80f, 0f), reference, 25f, 2.5f).x, Is.EqualTo(1f));
        }

        [Test]
        public void LandscapeRightFlipsBothAxes()
        {
            var g = Gravity(15f, 30f);
            var left = TiltMath.Angles(g);
            var right = TiltMath.Angles(new Vector3(-g.x, -g.y, g.z), landscapeRight: true);
            Assert.That(right.x, Is.EqualTo(left.x).Within(0.01f));
            Assert.That(right.y, Is.EqualTo(left.y).Within(0.01f));
        }

        // ---- The Dolphin's aim: the phone as an augmented-reality window on roll ---------------

        // The attitude sensor's pose is device -> world, right-handed, device z out of the glass. A
        // turn clockwise as the player sees the glass is a NEGATIVE turn about the device's own z.
        static Quaternion Clockwise(float degrees) => Quaternion.AngleAxis(-degrees, Vector3.forward);

        static readonly Quaternion HeldPose =
            Quaternion.AngleAxis(70f, Vector3.forward) * Quaternion.AngleAxis(-55f, Vector3.right) *
            Quaternion.AngleAxis(12f, Vector3.up);

        [TestCase(0f)]
        [TestCase(30f)]
        [TestCase(-45f)]
        [TestCase(90f)]
        [TestCase(-90f)]
        public void TheRollIsOneToOneWithTheWheelTurn(float degrees)
        {
            Assert.That(TiltMath.RollBetween(HeldPose, HeldPose * Clockwise(degrees)),
                Is.EqualTo(degrees).Within(0.01f), "clockwise reads positive, degree for degree");
        }

        [Test]
        public void TippingTheTopOrTurningOnTheSpotIsNoRoll()
        {
            Assert.That(TiltMath.RollBetween(HeldPose, HeldPose * Quaternion.AngleAxis(25f, Vector3.right)),
                Is.EqualTo(0f).Within(0.01f), "tipping the top edge back");
            Assert.That(TiltMath.RollBetween(HeldPose, HeldPose * Quaternion.AngleAxis(40f, Vector3.up)),
                Is.EqualTo(0f).Within(0.01f), "turning about the screen's up");
        }

        [Test]
        public void AWheelTurnReadsTheSameWhileThePhoneIsTippedBack()
        {
            var tippedAndTurned = HeldPose * Quaternion.AngleAxis(30f, Vector3.right) * Clockwise(40f);
            Assert.That(TiltMath.RollBetween(HeldPose, tippedAndTurned), Is.EqualTo(40f).Within(0.01f));
        }

        [Test]
        public void TheRollReadsWithThePhoneLyingFlat()
        {
            // Gravity cannot tell a wheel turn from nothing here; the fused pose can.
            Assert.That(TiltMath.RollBetween(Quaternion.identity, Clockwise(60f)), Is.EqualTo(60f).Within(0.01f));
        }

        [Test]
        public void TheRollAgreesInSignWithTheGravityWheel()
        {
            // The fallback (no attitude sensor) reads the wheel from gravity; both call clockwise positive.
            var reference = TiltMath.Angles(Gravity(0f, 30f));
            float gravityWheel = Mathf.DeltaAngle(reference.x, TiltMath.Angles(Gravity(20f, 30f)).x);
            Assert.That(gravityWheel, Is.EqualTo(20f).Within(0.5f));
            Assert.That(TiltMath.RollBetween(HeldPose, HeldPose * Clockwise(20f)), Is.EqualTo(gravityWheel).Within(0.5f));
        }

        // ---- Layout -------------------------------------------------------------------------

        static readonly TouchButtonLayout.Settings Shipped = new()
        {
            ButtonMillimetres = 10f, GapMillimetres = 1.5f, InnerArcMillimetres = 22f,
            ArcSpacingMillimetres = 12f, MaxHeightFraction = 0.55f, MaxWidthFraction = 0.36f,
        };

        const float Totem = 1.75f;

        static IEnumerable<TestCaseData> Screens()
        {
            yield return new TestCaseData(2400, 1080, 400f, 8.4f).SetName("6.0in 1080p phone");
            yield return new TestCaseData(1280, 720, 294f, 7.0f).SetName("4.4in 720p phone");
            yield return new TestCaseData(2732, 2048, 264f, 9.9f).SetName("12.9in tablet");
        }

        [TestCaseSource(nameof(Screens))]
        public void SixButtonsSeatInTheCorner_ThumbSized_WithoutOverlap(int width, int height, float dpi, float minimumMm)
        {
            var aspects = new List<float> { 1f, 1f, Totem, Totem, Totem, Totem };   // the Brittle Star
            var safe = new Rect(0, 0, width, height);
            var result = TouchButtonLayout.Arrange(aspects, new Vector2(width, height), safe, dpi, Shipped);

            Assert.That(result.Fits, Is.True);
            Assert.That(result.Centres.Count, Is.EqualTo(aspects.Count));
            Assert.That(result.ButtonMillimetres, Is.GreaterThanOrEqualTo(minimumMm));
            Assert.That(result.ButtonMillimetres, Is.GreaterThanOrEqualTo(TouchButtonLayout.MinimumButtonMillimetres));

            float w = result.ButtonWidthPixels;
            for (int i = 0; i < aspects.Count; i++)
            {
                var c = result.Centres[i];
                float h = w * aspects[i];
                // Inside the safe area, and inside the corner box (the view stays clear).
                Assert.That(c.x + w / 2, Is.LessThanOrEqualTo(safe.xMax + 0.5f));
                Assert.That(c.y - h / 2, Is.GreaterThanOrEqualTo(safe.yMin - 0.5f));
                Assert.That(c.x - w / 2, Is.GreaterThanOrEqualTo(width * (1f - Shipped.MaxWidthFraction) - 0.5f));
                Assert.That(c.y + h / 2, Is.LessThanOrEqualTo(height * Shipped.MaxHeightFraction + 0.5f));
                for (int j = 0; j < i; j++)
                {
                    var d = result.Centres[j] - c;
                    float hj = w * aspects[j];
                    bool apart = Mathf.Abs(d.x) >= w - 0.01f || Mathf.Abs(d.y) >= (h + hj) / 2 - 0.01f;
                    Assert.That(apart, Is.True, $"buttons {j} and {i} overlap");
                }
            }
        }

        [Test]
        public void TheFirstButtonTakesTheEasiestReach()
        {
            var aspects = new List<float> { 1f, Totem, Totem, Totem, Totem };
            var result = TouchButtonLayout.Arrange(aspects, new Vector2(2400, 1080), new Rect(0, 0, 2400, 1080), 400f, Shipped);
            var corner = new Vector2(2400, 0);
            float first = (result.Centres[0] - corner).magnitude;
            for (int i = 1; i < result.Centres.Count; i++)
                Assert.That((result.Centres[i] - corner).magnitude, Is.GreaterThanOrEqualTo(first - 1f));
        }

        [Test]
        public void ANotchedSafeAreaMovesTheButtonsWithIt()
        {
            var aspects = new List<float> { Totem, Totem, Totem, Totem };
            var safe = new Rect(0, 60, 2250, 1020);   // a cutout on the right, a gesture bar at the bottom
            var result = TouchButtonLayout.Arrange(aspects, new Vector2(2400, 1080), safe, 400f, Shipped);
            Assert.That(result.Fits, Is.True);
            foreach (var c in result.Centres)
            {
                Assert.That(c.x + result.ButtonWidthPixels / 2, Is.LessThanOrEqualTo(safe.xMax + 0.5f));
                Assert.That(c.y - result.ButtonWidthPixels * Totem / 2, Is.GreaterThanOrEqualTo(safe.yMin - 0.5f));
            }
        }

        [Test]
        public void WithNoButtonsThereIsNothingToSeat()
        {
            var result = TouchButtonLayout.Arrange(new List<float>(), new Vector2(2400, 1080), new Rect(0, 0, 2400, 1080), 400f, Shipped);
            Assert.That(result.Fits, Is.True);
            Assert.That(result.Centres, Is.Empty);
        }

        // ---- The shipped library ----------------------------------------------------------

        [Test]
        public void TheShippedLibraryMatchesItsGenerator_AndNamesEveryHullOnce()
        {
            // Tools/Build/author_touch_profiles.py owns the asset; --check is the drift gate. Here: the
            // asset exists where the runtime loads it, and no hull is listed twice (Find takes the first).
            string path = "Assets/Resources/" + TouchControlProfileLibrarySO.ResourcePath + ".asset";
            Assert.That(File.Exists(path), Is.True, path);
            var seen = new HashSet<string>();
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.Trim();
                if (!t.StartsWith("- vessel:")) continue;
                Assert.That(seen.Add(t), Is.True, $"hull listed twice: {t}");
            }
            Assert.That(seen.Count, Is.GreaterThan(10));
        }

        [Test]
        public void OnlyTheMantaTurnsItsIdleTiltOver()
        {
            // Garrett, 2026-10-22: the Manta's soar steered the opposite way on both axes. The Thresher's
            // launch aim reads the same tilt and keeps it as is.
            var library = Resources.Load<TouchControlProfileLibrarySO>(TouchControlProfileLibrarySO.ResourcePath);
            Assert.That(library, Is.Not.Null);
            Assert.That(library.Find(VesselClassType.Manta).flipTiltSteer, Is.True);
            Assert.That(library.Find(VesselClassType.Thresher).flipTiltSteer, Is.False);
        }

        [Test]
        public void AnUnlistedHullFliesTheDefaultThumbLiftProfile()
        {
            var library = ScriptableObject.CreateInstance<TouchControlProfileLibrarySO>();
            var profile = library.Find(VesselClassType.Wrasse);
            Assert.That(profile.scheme, Is.EqualTo(TouchScheme.ThumbLifts));
            Assert.That(profile.extraButtons, Is.Empty);
            Assert.That(profile.GlyphFor(Element.Charge), Is.EqualTo(TouchGlyph.None));
            Object.DestroyImmediate(library);
        }
    }
}
