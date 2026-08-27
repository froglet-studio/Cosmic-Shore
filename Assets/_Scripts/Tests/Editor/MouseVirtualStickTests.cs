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
    /// <para>The scheme has TWO regimes and most of these tests exist to hold the boundary
    /// between them: a spring-centred RATE stick near the middle (mouse speed is turn rate, let
    /// go and the vessel straightens) and a POSITION stick out in the hold annulus (a committed
    /// sweep parks it and the vessel keeps turning with the mouse still).
    /// <see cref="HoldingAHardTurnCostsABoundedSweep_NotAnEndlessOne"/> is the one that says why
    /// the second regime exists at all.</para>
    ///
    /// <para>Three of these exist because an implementation failed them, and none of the three is
    /// visible to the obvious "does it centre, does it clamp" checks:</para>
    /// <list type="bullet">
    /// <item><see cref="SustainedDrag_MapsMouseSpeedToDeflection"/> — the first cut sprang back
    /// only while the mouse was STILL, so the spring was off whenever you were steering and any
    /// drag at all wound up pinned at full deflection.</item>
    /// <item><see cref="SlowDrag_StillTurnsTheVessel"/> — the dead zone used to snap the
    /// accumulator itself, which is a ratchet: a 60 fps frame whose drag added less than the dead
    /// zone was zeroed every frame, so slow careful movement did nothing at all.</item>
    /// <item><see cref="HoldingAHardTurnCostsABoundedSweep_NotAnEndlessOne"/> — the pure spring
    /// shipped first, and holding a hard turn under it cost hundreds of pixels of desk per 180°.
    /// A control curve can be exactly right and still be unflyable because the DESK runs out.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class MouseVirtualStickTests
    {
        // The shipped Resources/MouseFlightConfig values.
        const float UnitsPerPixel = 0.011f;
        const float Spring = 3.5f;
        const float DeadZone = 0.02f;
        const float HoldInner = 0.88f;
        const float HoldOuter = 0.97f;
        const float Frame = 1f / 60f;

        /// <summary>The sustained drag that would hold the stick at the perimeter if the spring
        /// acted all the way out — i.e. under the pure-spring model.</summary>
        const float FullDeflectionSpeed = Spring / UnitsPerPixel;   // ~333 px/s

        // ------------------------------------------------------------------
        // Pure spring (no annulus) — the original model, still exactly expressible.

        static Vector2 Step(Vector2 stick, Vector2 delta, float dt = Frame, float spring = Spring)
            => MouseVirtualStick.Step(stick, delta, UnitsPerPixel, spring, dt);

        static Vector2 Published(Vector2 state) => MouseVirtualStick.Deflection(state, DeadZone);

        /// <summary>Hold a steady drag long enough to settle (6 s is ~21 time constants at the
        /// shipped spring, which lands far inside the closed form's tolerance).</summary>
        static Vector2 Drag(float pixelsPerSecond, float seconds = 6f, float dt = Frame,
                            float spring = Spring)
        {
            var stick = Vector2.zero;
            int frames = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < frames; i++)
                stick = Step(stick, new Vector2(pixelsPerSecond * dt, 0f), dt, spring);
            return stick;
        }

        // ------------------------------------------------------------------
        // The shipped model — spring near centre, dead in the annulus.

        static Vector2 StepHeld(Vector2 stick, Vector2 delta, float dt = Frame)
            => MouseVirtualStick.Step(stick, delta, UnitsPerPixel, Spring,
                                      HoldInner, HoldOuter, dt);

        static Vector2 DragHeld(float pixelsPerSecond, float seconds = 6f, float dt = Frame)
        {
            var stick = Vector2.zero;
            int frames = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < frames; i++)
                stick = StepHeld(stick, new Vector2(pixelsPerSecond * dt, 0f), dt);
            return stick;
        }

        /// <summary>A flick: <paramref name="pixels"/> of travel spread over
        /// <paramref name="seconds"/>, then stop. This is the gesture a player makes to ask
        /// "does this respond", and it is a claim about the TRANSIENT rather than the steady
        /// state - see <see cref="AFlickMustTurnTheVesselHard"/>.</summary>
        static Vector2 Flick(float pixels, float seconds)
        {
            var stick = Vector2.zero;
            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / Frame));
            float perFrame = pixels / frames;
            for (int i = 0; i < frames; i++)
                stick = StepHeld(stick, new Vector2(perFrame, 0f));
            return stick;
        }

        static Vector2 CoastHeld(Vector2 stick, float seconds, float dt = Frame)
        {
            int frames = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < frames; i++)
                stick = StepHeld(stick, Vector2.zero, dt);
            return stick;
        }

        // ==================================================================
        // Why the annulus exists

        /// <summary>
        /// Mouse travel spent keeping the stick at or beyond <see cref="HoldOuter"/> for
        /// <paramref name="seconds"/>, by a player who pushes exactly when it sags and not
        /// otherwise. Simulating BOTH models through the same controller is what makes the
        /// comparison honest — nothing here is an analytic shortcut that could stop matching the
        /// integrator after a retune.
        /// </summary>
        static float TravelToHoldHardOver(float seconds, float holdOuter)
        {
            var stick = Vector2.zero;
            float travel = 0f;
            int frames = Mathf.RoundToInt(seconds / Frame);
            for (int i = 0; i < frames; i++)
            {
                float push = stick.magnitude < HoldOuter ? FullDeflectionSpeed * Frame : 0f;
                travel += push;
                stick = MouseVirtualStick.Step(stick, new Vector2(push, 0f), UnitsPerPixel,
                                               Spring, HoldInner, holdOuter, Frame);
            }
            return travel;
        }

        [Test]
        public void HoldingAHardTurnCostsABoundedSweep_NotAnEndlessOne()
        {
            // THE reason this model changed. Under a pure spring, deflection is a function of
            // mouse SPEED, so holding the vessel hard over costs mouse travel for as long as the
            // turn lasts and the player runs out of desk long before the vessel runs out of turn.
            // Under the annulus it costs one sweep and then nothing at all - and the longer the
            // turn, the wider the gap, because only one of the two keeps spending.
            float withSpring = TravelToHoldHardOver(3f, holdOuter: 1f);
            float withAnnulus = TravelToHoldHardOver(3f, HoldOuter);

            Assert.Less(withAnnulus, withSpring * 0.5f,
                $"A three-second hard turn cost {withAnnulus:F0} px with the annulus against " +
                $"{withSpring:F0} px under the pure spring. If that gap closes, the annulus has " +
                "stopped doing the one job it was added for.");
            Assert.Less(withAnnulus, 400f,
                "One committed sweep, not a mousepad's worth.");

            // And it is bounded: a turn twice as long costs the same sweep, because the second
            // half is free.
            Assert.AreEqual(withAnnulus, TravelToHoldHardOver(6f, HoldOuter), 1f,
                "Nothing is spent holding the annulus, so the cost must not grow with the turn.");
        }

        [Test]
        public void AFlickMustTurnTheVesselHard()
        {
            // THE regression this suite did not catch the first time. The annulus originally
            // shipped with gain and spring lowered to 0.0045 / 1.5, chosen so the annulus sat a
            // "comfortable sweep" out - and the scheme read as completely dead. The SUSTAINED
            // curve was near enough unchanged (318 vs 333 px/s for full deflection), which is
            // what was measured; the IMPULSE response, which is what a player actually judges,
            // fell by four times: a 100 px flick went from 0.86 deflection to 0.40.
            //
            // A control curve is a claim about the steady state. A flick is a claim about the
            // transient. Tuning one while measuring only the other is how every number stays
            // defensible while the scheme stops working.
            Assert.Greater(Flick(100f, 0.15f).magnitude, 0.75f,
                "A 100 px flick must put the vessel most of the way over. Under the regressed " +
                "numbers this was 0.40, i.e. 17 deg/s on the Sparrow against 67.");
            Assert.Greater(Flick(60f, 0.15f).magnitude, 0.42f,
                "...and half that flick must be about half the turn, not a quarter of it.");
        }

        [Test]
        public void ASmallFlickStaysProportionalAndSelfCentres()
        {
            // The other half of the deal: the gain that makes a flick land hard must not make a
            // small aiming correction commit to anything.
            // Proportionality is the real claim, and it is what survives the spring acting
            // during the flick: pixels x gain OVERSTATES a flick (30 px x 0.011 = 0.33 against a
            // measured 0.257) because the spring is pulling back the whole time it lands.
            var small = Flick(30f, 0.15f);
            var twice = Flick(60f, 0.15f);
            Assert.AreEqual(2f, twice.magnitude / small.magnitude, 0.1f,
                "Twice the flick must be twice the turn - the near-centre regime is linear.");
            Assert.Less(small.magnitude, HoldInner,
                "...and a small correction is nowhere near the hold band.");

            Assert.AreEqual(Vector2.zero, Published(CoastHeld(Flick(100f, 0.15f), 4f)),
                "A flick that does not saturate the stick must settle back to centre. Only a " +
                "push that reaches the annulus holds.");
        }

        [Test]
        public void HoldAnnulus_KeepsTurningWithTheMouseStill()
        {
            var parked = DragHeld(FullDeflectionSpeed, 2f);
            Assert.GreaterOrEqual(parked.magnitude, HoldOuter,
                "A brisk sweep must reach the annulus.");

            var coasted = CoastHeld(parked, 10f);

            Assert.AreEqual(parked.magnitude, coasted.magnitude, 0.0005f,
                "Inside the annulus the spring is exactly zero, so ten seconds of stillness must " +
                "change nothing at all. This is the property the whole scheme is built on.");
        }

        [Test]
        public void EscapeSpeed_SeparatesAStablePartialTurnFromACommittedOne()
        {
            float escape = MouseVirtualStick.EscapeSpeed(
                UnitsPerPixel, Spring, HoldInner, HoldOuter);

            var below = DragHeld(escape * 0.9f, 6f);
            Assert.Less(below.magnitude, HoldOuter,
                "Under the escape speed the spring still balances the drag, so the player gets a " +
                "stable partial turn rather than a knife edge at the perimeter.");
            Assert.Greater(below.magnitude, 0.3f,
                "...and it is a REAL partial turn, not a dead zone with extra steps.");

            var above = DragHeld(escape * 1.1f, 6f);
            Assert.AreEqual(1f, above.magnitude, 0.001f,
                "Over the escape speed the stick runs away to the perimeter and stays there.");
        }

        [Test]
        public void BelowTheAnnulus_TheVesselStillStraightensWhenYouLetGo()
        {
            // The other half of the deal: everything inside the hold band is still a rate stick,
            // so an aiming correction undoes itself and the vessel does not accumulate heading
            // from every twitch.
            var stick = DragHeld(150f, 2f);
            Assert.Less(stick.magnitude, HoldInner, "A steady 150 px/s drag is a mid-range turn.");

            var settled = CoastHeld(stick, 2f);
            Assert.AreEqual(Vector2.zero, Published(settled),
                "Two seconds after the player stops, a mid-range turn must be fully out.");
        }

        [Test]
        public void SpringScale_IsFullInsideTheBand_ZeroOutside_AndMonotoneBetween()
        {
            Assert.AreEqual(1f, MouseVirtualStick.SpringScaleAtRadius(0f, HoldInner, HoldOuter), 1e-5f);
            Assert.AreEqual(1f, MouseVirtualStick.SpringScaleAtRadius(HoldInner, HoldInner, HoldOuter), 1e-5f);
            Assert.AreEqual(0f, MouseVirtualStick.SpringScaleAtRadius(HoldOuter, HoldInner, HoldOuter), 1e-5f);
            Assert.AreEqual(0f, MouseVirtualStick.SpringScaleAtRadius(1f, HoldInner, HoldOuter), 1e-5f);

            // Monotone is what makes the drift across the band self-consistent: the spring only
            // ever gets STRONGER as the stick falls inward, so there is nothing to oscillate
            // against and the only stable places are centred and the annulus.
            float previous = 1f;
            for (int i = 0; i <= 200; i++)
            {
                float scale = MouseVirtualStick.SpringScaleAtRadius(i / 200f, HoldInner, HoldOuter);
                Assert.LessOrEqual(scale, previous + 1e-6f);
                previous = scale;
            }
        }

        [Test]
        public void HoldOuterAtOne_ReproducesThePureSpringExactly()
        {
            // The annulus is opt-out, and opting out has to be bit-for-bit rather than nearly:
            // the pure-spring tests below are only evidence about the shipped code while this
            // holds.
            var withBand = Vector2.zero;
            var without = Vector2.zero;

            for (int i = 0; i < 120; i++)
            {
                var delta = new Vector2(6f, 2.5f);
                withBand = MouseVirtualStick.Step(withBand, delta, UnitsPerPixel, Spring, 1f, 1f, Frame);
                without = MouseVirtualStick.Step(without, delta, UnitsPerPixel, Spring, Frame);
                Assert.AreEqual(without.x, withBand.x, 0f);
                Assert.AreEqual(without.y, withBand.y, 0f);
            }
        }

        [Test]
        public void TheAnnulusNeverBanksDeflectionThePlayerMustUnwind()
        {
            // A stick parked at the rim has no spring pulling it back, so if a long sweep could
            // accumulate STATE past the perimeter the player would have to give every one of
            // those pixels back before the vessel responded. Clamping the state (not just the
            // report) is what stops that, and it matters far more here than under the spring.
            var stick = Vector2.zero;
            for (int i = 0; i < 120; i++)
                stick = StepHeld(stick, new Vector2(2000f * Frame, 0f));   // a hard flick right

            Assert.AreEqual(1f, stick.magnitude, 0.001f);

            stick = StepHeld(stick, new Vector2(-1f / UnitsPerPixel * 0.2f, 0f));
            Assert.Less(stick.x, 0.85f,
                "One fifth of the stick's travel back must move the stick one fifth, not undo a " +
                "banked sweep.");
        }

        // ==================================================================
        // The near-centre (rate) regime — unchanged claims, re-measured numbers

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
                    float simulated = Drag(speed, 6f, dt).magnitude;

                    Assert.AreEqual(predicted, simulated, 0.002f,
                        $"Closed form and integrator disagree at {speed:F0} px/s, {1f / dt:F0} fps.");
                }
        }

        [Test]
        public void SlowDrag_StillTurnsTheVessel()
        {
            // 40 px/s is a careful aiming drag - well under one dead zone of travel per frame.
            var published = Published(Drag(40f));

            Assert.Greater(published.magnitude, DeadZone,
                "A slow, careful drag must produce a slow turn. Snapping the ACCUMULATOR to the " +
                "dead zone (rather than only the published value) blocks it outright.");
            Assert.AreEqual(40f * UnitsPerPixel / Spring, published.magnitude, 0.005f,
                "...and it lands on the curve, rather than merely escaping the dead zone.");
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
            var stick = StepHeld(Vector2.zero, new Vector2(10000f, 0f));

            Assert.AreEqual(1f, stick.magnitude, 0.0001f,
                "BarrelRollController arms on |stick| >= perimeterThreshold (1 by default), so " +
                "the mouse must be able to reach the perimeter exactly.");
        }

        [Test]
        public void DeflectionNeverExceedsOne_OnAnyDiagonal()
        {
            var stick = StepHeld(Vector2.zero, new Vector2(10000f, 7000f));

            Assert.LessOrEqual(stick.magnitude, 1.0001f,
                "Clamping is to the unit CIRCLE, not per axis - a diagonal sweep must not " +
                "produce a radius of sqrt(2).");
        }

        [Test]
        public void ReleasingTheMousePublishesExactlyCentred()
        {
            var stick = new Vector2(1f, 0f);
            for (int i = 0; i < 240; i++)   // four seconds of stillness, pure spring
                stick = Step(stick, Vector2.zero);

            Assert.Greater(stick.magnitude, 0f,
                "The STATE decays exponentially and never reaches zero - that is expected.");
            Assert.AreEqual(Vector2.zero, Published(stick),
                "...and the dead zone applied at publish time is what actually lands on centre.");
        }

        [Test]
        public void ReturnIsPromptEnoughToAimWith()
        {
            // Measured from inside the hold band, which is where every aiming correction lives.
            var stick = new Vector2(HoldInner, 0f);
            for (int i = 0; i < Mathf.RoundToInt(1f / Frame); i++)
                stick = StepHeld(stick, Vector2.zero);

            Assert.Less(stick.magnitude, 0.2f,
                "A second after the player stops, an aiming turn must be mostly out - a slow " +
                "return reads as the vessel ignoring you.");
        }

        [Test]
        public void ReleaseIsFrameRateIndependent()
        {
            var slow = new Vector2(HoldInner, 0f);
            for (int i = 0; i < 30; i++) slow = StepHeld(slow, Vector2.zero, 1f / 30f);

            var fast = new Vector2(HoldInner, 0f);
            for (int i = 0; i < 240; i++) fast = StepHeld(fast, Vector2.zero, 1f / 240f);

            Assert.AreEqual(slow.magnitude, fast.magnitude, 0.002f);
        }

        [Test]
        public void NoSpring_IsAPureAccumulator()
        {
            var held = new Vector2(0.5f, 0f);
            for (int i = 0; i < 60; i++)
                held = Step(held, Vector2.zero, spring: 0f);

            Assert.AreEqual(0.5f, held.x, 0.0001f,
                "springPerSecond = 0 is the far end of the design space and must stay a real " +
                "option: no return anywhere, not just in the annulus.");

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

        // ==================================================================
        // The shipped asset

        [Test]
        public void ShippedConfigDefaultsAreFlyable()
        {
            var config = ScriptableObject.CreateInstance<MouseFlightConfigSO>();

            Assert.Greater(config.StickUnitsPerPixel, 0f);
            Assert.Greater(config.SpringPerSecond, 0f,
                "The shipped default carries a spring near centre; 0 is a supported choice but " +
                "not the one the fleet ships.");

            Assert.Less(config.PixelsToFullDeflection, 500f,
                "Hard over must be one comfortable sweep of the mouse, not a mousepad's worth.");
            Assert.Greater(config.PixelsToFullDeflection, 60f,
                "...and not a twitch, or fine aim has nowhere to live.");

            Assert.Less(config.HoldOuterRadius, 1f,
                "The shipped default HAS an annulus - without one, holding a turn costs mouse " +
                "travel forever, which is the defect this model exists to fix.");
            Assert.LessOrEqual(config.HoldInnerRadius, config.HoldOuterRadius);
            Assert.Less(config.PixelsToHoldAnnulus, config.PixelsToFullDeflection);

            // Committing has to be a deliberate act: comfortably above an aiming drag, comfortably
            // below a speed no hand produces.
            Assert.Greater(config.EscapeSpeed, 120f,
                "A careful aiming drag must not accidentally lock the vessel into a hard turn.");
            Assert.Less(config.EscapeSpeed, 600f,
                "...and committing must be reachable with one ordinary sweep.");

            float halfSpeed = config.SpringPerSecond * 0.5f / config.StickUnitsPerPixel;
            Assert.AreEqual(0.5f, config.SustainedDeflection(halfSpeed), 0.0001f,
                "Inside the band the curve is still exactly v x gain / spring.");
            Assert.AreEqual(1f, config.SustainedDeflection(config.EscapeSpeed * 2f), 0.0001f,
                "Past the escape speed the stick commits rather than overshooting the perimeter.");

            Assert.Less(config.DeadZone, 0.25f,
                "A dead zone approaching a quarter of the stick's travel would eat fine aim.");
            Assert.Less(config.DeadZone * config.PixelsToFullDeflection, 25f,
                "The dead zone is also a distance in pixels; more than a few dozen reads as the " +
                "mouse being ignored around centre.");

            Object.DestroyImmediate(config);
        }
    }
}
