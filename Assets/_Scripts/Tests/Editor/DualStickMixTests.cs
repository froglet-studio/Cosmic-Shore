using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    [TestFixture]
    public class DualStickMixTests
    {
        static readonly Vector2 W = new(0f, 1f);
        static readonly Vector2 S = new(0f, -1f);
        static readonly Vector2 A = new(-1f, 0f);
        static readonly Vector2 D = new(1f, 0f);
        static readonly Vector2 P = new(0f, 1f);
        static readonly Vector2 Semicolon = new(0f, -1f);
        static readonly Vector2 L = new(-1f, 0f);
        static readonly Vector2 Quote = new(1f, 0f);
        static readonly Vector2 Neutral = Vector2.zero;

        [Test]
        public void NeutralHorizontals_XDiffIsCruise()
        {
            var mix = DualStickMix.Mix(Neutral, Neutral);
            Assert.AreEqual(0.5f, mix.XDiff, 0.0001f);
        }

        [Test]
        public void WAndP_PitchUpStacks_XDiffStaysCruise()
        {
            var wOnly = DualStickMix.Mix(W, Neutral);
            var wp = DualStickMix.Mix(W, P);

            Assert.Greater(Mathf.Abs(wp.YSum), Mathf.Abs(wOnly.YSum),
                "W+P must stack YSum (pitch) beyond W alone.");
            Assert.AreEqual(wOnly.YSum < 0f, wp.YSum < 0f, "Pitch sign must match W-only.");
            Assert.AreEqual(0.5f, wp.XDiff, 0.0001f);
        }

        [Test]
        public void AAndQuote_HighXDiff_Fast()
        {
            var mix = DualStickMix.Mix(A, Quote);
            Assert.Greater(mix.XDiff, 0.5f);
            Assert.AreEqual(1f, mix.XDiff, 0.0001f);
        }

        [Test]
        public void LAndD_LowXDiff_Slow()
        {
            var mix = DualStickMix.Mix(D, L);
            Assert.Less(mix.XDiff, 0.5f);
            Assert.AreEqual(0f, mix.XDiff, 0.0001f);
        }

        [Test]
        public void PAndS_YDiffIsRollLeft()
        {
            var mix = DualStickMix.Mix(S, P);
            Assert.Greater(mix.YDiff, 0f, "P+S → right.y - left.y > 0 (roll left).");
        }

        [Test]
        public void WAndSemicolon_YDiffIsRollRight()
        {
            var mix = DualStickMix.Mix(W, Semicolon);
            Assert.Less(mix.YDiff, 0f, "W+; → right.y - left.y < 0 (roll right).");
        }

        [Test]
        public void AAndL_XSumIsYawLeft()
        {
            var mix = DualStickMix.Mix(A, L);
            Assert.Less(mix.XSum, 0f, "A+L → XSum yaw left.");
        }
    }
}
