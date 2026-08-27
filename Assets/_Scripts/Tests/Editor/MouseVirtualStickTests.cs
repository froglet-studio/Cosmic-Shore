using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The one-thumb mouse scheme's load-bearing math: turning a mouse DELTA into the stick
    /// POSITION a single-stick vessel steers on (<see cref="SingleStickMouseInputStrategy"/>).
    ///
    /// <para>Two of these tests exist because the first implementation failed them, and both
    /// failures were invisible to the obvious "does it centre, does it clamp" checks:</para>
    /// <list type="bullet">
    /// <item><see cref="SustainedDrag_MapsMouseSpeedToDeflection"/> — the first cut sprang back
    /// only while the mouse was STILL, so the spring was off whenever you were steering and any
    /// drag at all wound up pinned at full deflection.</item>
    /// <item><see cref="SlowDrag_StillTurnsTheVessel"/> — the dead zone used to snap the
    /// accumulator itself, which is a ratchet: a 60 fps frame under ~110 px/s added less than the
    /// dead zone and was zeroed every frame, so slow careful movement did nothing at all.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class MouseVirtualStickTests
    {
        // The shipped Resources/MouseFlightConfig values.
        const float UnitsPerPixel = 0.011f;
        const float Spring = 3.5f;
        const float DeadZone = 0.02f;
        const float Frame = 1f / 60f;

        /// <summary>The sustained drag speed that holds the stick at the perimeter.</summary>
        const float FullDeflectionSpeed = Spring / UnitsPerPixel;   // ~318 px/s

        static Vector2 Step(Vector2 stick, Vector2 delta, float dt = Frame, float spring = Spring)
            => MouseVirtualStick.Step(stick, delta, UnitsPerPixel, spring, dt);

        static Vector2 Published(Vector2 state) => MouseVirtualStick.Deflection(state, DeadZone);

        /// <summary>Hold a steady drag long enough to settle (3 s is ~10 time constants).</summary>
        static Vector2 Drag(float pixelsPerSecond, float seconds = 3f, float dt = Frame,
                            float spring = Spring)
        {
            var stick = Vector2.zero;
            int frames = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < frames; i++)
                stick = Step(stick, new Vector2(pixelsPerSecond * dt, 0f), dt, spring);
            return stick;
        }

        [Test]
        public void SustainedDrag_MapsMouseSpeedToDeflection()
        {
            // THE property: how fast you move the mouse is how hard the vessel turns, with a
            // stable partial deflection at every speed rather than a knife edge at the perimeter.
            Assert.AreEqual(0.25f, Drag(FullDeflectionSpeed * 0.25f).magnitude, 0.005f);
            Assert.AreEqual(0.50f, Drag(FullDeflectionSpeed * 0.50f).magnitude, 0.005f);
            Assert.AreEqual(0.75f, Drag(FullDeflectionSpeed * 0.75f).magnitude, 0.005f);
            Assert.AreEqual(1.00f, Drag(FullDeflectionSpeed).magnitude, 0.005f);
        }

        [Test]
        public void SustainedDeflection_PredictsTheIntegratorAtEveryFrameRate()
        {
            // The closed form is what a tuner reasons about, so it has to be what the integrator
            // does - and it has to stay true off 60 fps, or the scheme's gain changes with the
            // player's hardware.
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f })
                foreach (float speed in new[] { 60f, 120f, 240f, FullDeflectionSpeed })
                {
                    float predicted = MouseVirtualStick.SustainedDeflection(
                        speed, UnitsPerPixel, Spring);
                    float simulated = Drag(speed, 3f, dt).magnitude;

                    Assert.AreEqual(predicted, simulated, 0.002f,
                        $"Closed form and integrator disagree at {speed:F0} px/s, {1f / dt:F0} fps.");
                }
        }

        [Test]
        public void SlowDrag_StillTurnsTheVessel()
        {
            // 40 px/s is a careful aiming drag - well under one dead zone of travel per frame.
            var published = Published(Drag(40f));

            Assert.Greater(published.magnitude, 0.1f,
                "A slow, careful drag must produce a slow turn. Snapping the ACCUMULATOR to the " +
                "dead zone (rather than only the published value) blocks it outright.");
        }

        [Test]
        public void Flick_DeflectsByRoughlyPixelsTimesGain()
        {
            var stick = Step(Vector2.zero, new Vector2(50f, 0f));

            // Exactly px * gain * (1 - e) / (spring * dt): ~3% short at 60 fps, because the
            // spring correctly acts on the deflection during the frame it landed in.
            Assert.AreEqual(50f * UnitsPerPixel, stick.x, 0.03f);
            Assert.AreEqual(0f, stick.y, 0.0001f);
        }

        [Test]
        public void DeflectionReachesExactlyOne_SoTheStrafingRollCanArm()
        {
            var stick = Step(Vector2.zero, new Vector2(10000f, 0f));

            Assert.AreEqual(1f, stick.magnitude, 0.0001f,
                "BarrelRollController arms on |stick| >= perimeterThreshold (1 by default), so " +
                "the mouse must be able to reach the perimeter exactly.");
        }

        [Test]
        public void DeflectionNeverExceedsOne_OnAnyDiagonal()
        {
            var stick = Step(Vector2.zero, new Vector2(10000f, 7000f));

            Assert.LessOrEqual(stick.magnitude, 1.0001f,
                "Clamping is to the unit CIRCLE, not per axis - a diagonal sweep must not " +
                "produce a radius of sqrt(2).");
        }

        [Test]
        public void ReleasingTheMousePublishesExactlyCentred()
        {
            var stick = new Vector2(1f, 0f);
            for (int i = 0; i < 120; i++)   // two seconds of stillness
                stick = Step(stick, Vector2.zero);

            Assert.Greater(stick.magnitude, 0f,
                "The STATE decays exponentially and never reaches zero - that is expected.");
            Assert.AreEqual(Vector2.zero, Published(stick),
                "...and the dead zone applied at publish time is what actually lands on centre.");
        }

        [Test]
        public void ReturnIsPromptEnoughToAimWith()
        {
            var stick = new Vector2(1f, 0f);
            for (int i = 0; i < Mathf.RoundToInt(0.5f / Frame); i++)
                stick = Step(stick, Vector2.zero);

            Assert.Less(stick.magnitude, 0.25f,
                "Half a second after the player stops, the turn must be mostly out - a slow " +
                "return reads as the vessel ignoring you.");
        }

        [Test]
        public void ReleaseIsFrameRateIndependent()
        {
            var slow = new Vector2(1f, 0f);
            for (int i = 0; i < 30; i++) slow = Step(slow, Vector2.zero, 1f / 30f);

            var fast = new Vector2(1f, 0f);
            for (int i = 0; i < 240; i++) fast = Step(fast, Vector2.zero, 1f / 240f);

            Assert.AreEqual(slow.magnitude, fast.magnitude, 0.002f);
        }

        [Test]
        public void NoSpring_IsAPureAccumulator()
        {
            var held = new Vector2(0.5f, 0f);
            for (int i = 0; i < 60; i++)
                held = Step(held, Vector2.zero, spring: 0f);

            Assert.AreEqual(0.5f, held.x, 0.0001f,
                "springPerSecond = 0 is the other school of mouse flight and must be a real " +
                "option: push once and the vessel keeps turning until you push back.");

            var flick = Step(Vector2.zero, new Vector2(50f, 0f), spring: 0f);
            Assert.AreEqual(50f * UnitsPerPixel, flick.x, 0.0001f,
                "With no spring the gain is exactly pixels x unitsPerPixel.");
        }

        [Test]
        public void DeadZoneHidesAResidualButPassesARealPush()
        {
            Assert.AreEqual(Vector2.zero, Published(new Vector2(DeadZone * 0.5f, 0f)));
            Assert.AreEqual(0.2f, Published(new Vector2(0.2f, 0f)).x, 0.0001f);
        }

        [Test]
        public void ShippedConfigDefaultsAreFlyable()
        {
            var config = ScriptableObject.CreateInstance<MouseFlightConfigSO>();

            Assert.Greater(config.StickUnitsPerPixel, 0f);
            Assert.Greater(config.SpringPerSecond, 0f,
                "The shipped default carries a spring; 0 is a supported choice but not the one " +
                "the fleet ships.");

            float fullDeflectionSpeed = config.SpringPerSecond / config.StickUnitsPerPixel;
            Assert.Greater(fullDeflectionSpeed, 100f, "A twitch must not pin the stick.");
            Assert.Less(fullDeflectionSpeed, 1200f, "Full deflection must be within one sweep.");

            Assert.AreEqual(0.5f, config.SustainedDeflection(fullDeflectionSpeed * 0.5f), 0.0001f);
            Assert.AreEqual(1f, config.SustainedDeflection(fullDeflectionSpeed * 4f), 0.0001f,
                "The curve saturates rather than overshooting the perimeter.");

            Assert.Less(config.DeadZone, 0.25f,
                "A dead zone approaching a quarter of the stick's travel would eat fine aim.");

            Object.DestroyImmediate(config);
        }
    }
}
