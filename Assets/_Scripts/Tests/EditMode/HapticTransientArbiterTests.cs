#if UNITY_EDITOR
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// HapticTransientArbiter Tests - Validates admission control on the single
    /// haptic channel.
    ///
    /// WHY THIS MATTERS:
    /// The Lofelt runtime holds exactly one clip; every Load() evicts the
    /// playing one. Without correct arbitration, chatty categories (prism
    /// breaks, skims, UI ticks) would chop big moments (mine explosions,
    /// vessel impacts) mid-swell — the difference between haptics that feel
    /// composed and haptics that feel like noise.
    /// </summary>
    [TestFixture]
    public class HapticTransientArbiterTests
    {
        static HapticTransientArbiter.Settings Settings() => HapticTransientArbiter.Settings.Default;

        static HapticTransientArbiter.Request Request(
            int key = 1, int priority = 50, float strength = 0.8f, float duration = 0.3f, float cooldown = 0.1f)
            => new()
            {
                CategoryKey = key,
                Priority = priority,
                Strength = strength,
                Duration = duration,
                CooldownSeconds = cooldown,
            };

        [Test]
        public void TryAdmit_FirstRequest_IsAdmitted()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(), now: 0f, Settings()));
            Assert.IsTrue(arbiter.IsPlaying(0.1f));
        }

        [Test]
        public void TryAdmit_BelowMinStrength_IsRejected()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsFalse(arbiter.TryAdmit(Request(strength: 0.01f), now: 0f, Settings()));
        }

        [Test]
        public void TryAdmit_SameCategoryInsideCooldown_IsRejected()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 7, cooldown: 0.5f, duration: 0.05f), now: 0f, Settings()));

            // Channel is free again (duration elapsed) but the category is still cooling down.
            Assert.IsFalse(arbiter.TryAdmit(Request(key: 7, cooldown: 0.5f), now: 0.3f, Settings()));
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 7, cooldown: 0.5f), now: 0.6f, Settings()));
        }

        [Test]
        public void TryAdmit_LowerPriority_CannotStealTheChannel()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 1, priority: 90, duration: 1f), now: 0f, Settings()));

            Assert.IsFalse(arbiter.TryAdmit(Request(key: 2, priority: 40), now: 0.2f, Settings()));
        }

        [Test]
        public void TryAdmit_HigherPriority_PreemptsImmediately()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 1, priority: 40, duration: 1f), now: 0f, Settings()));

            Assert.IsTrue(arbiter.TryAdmit(Request(key: 2, priority: 90), now: 0.01f, Settings()),
                "a mine explosion must punch through a playing UI tick, even inside the global interval");
        }

        [Test]
        public void TryAdmit_EqualPriority_RequiresComparableStrength()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 1, priority: 50, strength: 1f, duration: 1f), now: 0f, Settings()));

            Assert.IsFalse(arbiter.TryAdmit(Request(key: 2, priority: 50, strength: 0.3f), now: 0.2f, Settings()),
                "a weak equal-priority event must not chop a strong one mid-play");
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 3, priority: 50, strength: 0.9f), now: 0.2f, Settings()));
        }

        [Test]
        public void TryAdmit_DuringPlayingTail_AnyAdmissibleRequestWins()
        {
            var settings = Settings();
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 1, priority: 90, duration: 0.5f), now: 0f, Settings()));

            // 0.02s of tail left (< TailSeconds) — even a low-priority request may take over.
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 2, priority: 10), now: 0.48f, settings));
        }

        [Test]
        public void TryAdmit_GlobalMinInterval_BlocksSameFrameBursts()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 1, priority: 50, duration: 0.01f), now: 0f, Settings()));

            // Different category, 5ms later. The 10ms clip is in its tail (which
            // would otherwise allow a takeover), so the rejection exercises the
            // global-interval floor specifically.
            Assert.IsFalse(arbiter.TryAdmit(Request(key: 2, priority: 50), now: 0.005f, Settings()));

            // Same request outside the tail AND outside the global interval,
            // channel free (clip ended at 10ms) — admitted.
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 2, priority: 50), now: 0.05f, Settings()));
        }

        [Test]
        public void NotifyStopped_FreesTheChannel()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 1, priority: 90, duration: 5f), now: 0f, Settings()));

            arbiter.NotifyStopped();

            Assert.IsFalse(arbiter.IsPlaying(0.1f));
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 2, priority: 10), now: 0.1f, Settings()));
        }

        [Test]
        public void Reset_ClearsCooldowns()
        {
            var arbiter = new HapticTransientArbiter();
            Assert.IsTrue(arbiter.TryAdmit(Request(key: 5, cooldown: 10f, duration: 0.01f), now: 0f, Settings()));

            arbiter.Reset();

            Assert.IsTrue(arbiter.TryAdmit(Request(key: 5, cooldown: 10f), now: 0.1f, Settings()));
        }

        [Test]
        public void RemainingSeconds_TracksPlayback()
        {
            var arbiter = new HapticTransientArbiter();
            arbiter.TryAdmit(Request(duration: 1f), now: 0f, Settings());

            Assert.AreEqual(0.6f, arbiter.RemainingSeconds(0.4f), 1e-4f);
            Assert.AreEqual(0f, arbiter.RemainingSeconds(2f));
        }
    }
}
#endif
