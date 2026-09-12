using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Edit-mode coverage for the SHAPE of a round's in-flight growth — how far along its swell
    /// a projectile is at a given point in its flight. The two shipped shapes are "swell across
    /// the whole flight" (the Sparrow's full-auto tracer, window 1) and "swell early, then
    /// hold" (the skyburst missile, window 0.2).
    ///
    /// Lives under an Editor/ folder per CLAUDE.md — a test anywhere else compiles into the
    /// player and breaks the Windows build at the IL2CPP linker.
    /// </summary>
    public class RoundGrowthRampTests
    {
        // ------------------------------------------------- swell across the whole flight

        [Test]
        public void AFullWindowIsTheOldLinearSwell()
        {
            // Window 1 is the default and must be byte-for-byte the behaviour every existing
            // round already had: a straight lerp from 1 at the muzzle to the factor at the end.
            Assert.AreEqual(1f, RoundGrowthRamp.At(0f, 6f), 1e-5f);
            Assert.AreEqual(2.25f, RoundGrowthRamp.At(0.25f, 6f), 1e-5f);
            Assert.AreEqual(3.5f, RoundGrowthRamp.At(0.5f, 6f), 1e-5f);
            Assert.AreEqual(6f, RoundGrowthRamp.At(1f, 6f), 1e-5f);
        }

        // ------------------------------------------------------ swell early, then hold

        [Test]
        public void AnEarlyWindowReachesFullSizeAtTheWindow()
        {
            // The skyburst's shape: 20× reached at 20% of the flight, on a straight ramp.
            Assert.AreEqual(1f, RoundGrowthRamp.At(0f, 20f, 0.2f), 1e-4f);
            Assert.AreEqual(10.5f, RoundGrowthRamp.At(0.1f, 20f, 0.2f), 1e-4f);   // halfway up
            Assert.AreEqual(20f, RoundGrowthRamp.At(0.2f, 20f, 0.2f), 1e-4f);     // arrived
        }

        [Test]
        public void ItHoldsForEveryFrameAfterTheWindow()
        {
            // "Then hold" is the whole point: nothing past the window may move the size, or the
            // round would keep swelling toward the player and stop reading as a fixed object.
            for (float p = 0.2f; p <= 1.5f; p += 0.05f)
                Assert.AreEqual(20f, RoundGrowthRamp.At(p, 20f, 0.2f), 1e-4f, $"progress {p}");
        }

        [Test]
        public void ItIsMonotonicAndNeverOvershoots()
        {
            // A round can only ever grow INTO its size. An overshoot-and-settle would read as a
            // pop, and a fall-back would read as the missile shrinking as it closes.
            float previous = 0f;
            for (float p = 0f; p <= 1f; p += 0.01f)
            {
                float g = RoundGrowthRamp.At(p, 20f, 0.2f);
                Assert.That(g, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"progress {p}");
                Assert.That(g, Is.InRange(1f, 20f), $"progress {p}");
                previous = g;
            }
        }

        [Test]
        public void ProgressBeforeTheMuzzleIsClamped()
        {
            // Defensive: a caller handing back a negative progress must not shrink the round
            // below its launch size (which would put the model inside the ship).
            Assert.AreEqual(1f, RoundGrowthRamp.At(-0.5f, 20f, 0.2f), 1e-4f);
            Assert.AreEqual(1f, RoundGrowthRamp.At(-5f, 6f), 1e-4f);
        }

        [Test]
        public void ANonPositiveWindowIsFullSizeImmediately()
        {
            // Not a divide by zero — "already there". The Range attribute keeps the inspector
            // off it, but the function is the contract, not the attribute.
            Assert.AreEqual(20f, RoundGrowthRamp.At(0f, 20f, 0f), 1e-4f);
            Assert.AreEqual(20f, RoundGrowthRamp.At(0.5f, 20f, -1f), 1e-4f);
            Assert.IsTrue(RoundGrowthRamp.IsComplete(0f, 0f));
        }

        [Test]
        public void AFactorOfOneIsNoGrowthAtAnyPointOfAnyWindow()
        {
            // The sanctioned opt-out has to hold for the shape as well as the curve.
            for (float p = 0f; p <= 1f; p += 0.1f)
            {
                Assert.AreEqual(1f, RoundGrowthRamp.At(p, 1f), 1e-4f);
                Assert.AreEqual(1f, RoundGrowthRamp.At(p, 1f, 0.2f), 1e-4f);
            }
        }

        // ------------------------------------------------------------------ the latch

        [Test]
        public void IsCompleteFlipsExactlyAtTheWindowAndStays()
        {
            // Projectile stops re-writing the transform once this is true, so a false positive
            // would freeze a round mid-swell — worse than the per-frame write it saves.
            Assert.IsFalse(RoundGrowthRamp.IsComplete(0.19f, 0.2f));
            Assert.IsTrue(RoundGrowthRamp.IsComplete(0.2f, 0.2f));
            Assert.IsTrue(RoundGrowthRamp.IsComplete(0.9f, 0.2f));

            // A full-flight round only settles on its last frame, which is why the latch costs
            // the tracer nothing and changes nothing about it.
            Assert.IsFalse(RoundGrowthRamp.IsComplete(0.99f));
            Assert.IsTrue(RoundGrowthRamp.IsComplete(1f));
        }

        [Test]
        public void TheLatchOnlyEverFiresWhereTheSizeHasStoppedMoving()
        {
            // The two must agree: it is only safe to stop writing the transform at a progress
            // where every later progress yields the same size.
            foreach (float window in new[] { 0.05f, 0.2f, 0.5f, 1f })
                for (float p = 0f; p <= 1f; p += 0.01f)
                    if (RoundGrowthRamp.IsComplete(p, window))
                        Assert.AreEqual(RoundGrowthRamp.At(1f, 20f, window),
                                        RoundGrowthRamp.At(p, 20f, window), 1e-4f,
                                        $"window {window}, progress {p}");
        }
    }
}
