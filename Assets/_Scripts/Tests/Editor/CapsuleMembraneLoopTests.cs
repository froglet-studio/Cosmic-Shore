#if UNITY_EDITOR
using CosmicShore.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="CapsuleMembraneLoop"/> - the membrane wobble generated from its ten bake inputs instead of
    /// read from the 3.4 MB baked asset. The generated loop is only a replacement if it IS the bake, so the
    /// first test compares it with the shipped asset capsule by capsule; the rest pin the on-demand and
    /// sharing behaviour the no-load-spike claim depends on.
    /// </summary>
    public class CapsuleMembraneLoopTests
    {
        const string ShippedPresetPath = "Assets/_SO_Assets/Cell Data/CapsuleMembraneAnimation.asset";

        [SetUp]
        public void SetUp() => CapsuleMembraneLoop.ClearCache();

        [TearDown]
        public void TearDown() => CapsuleMembraneLoop.ClearCache();

        static CapsuleMembraneAnimationSO.BakeSignature Small(int seed = 42) => new()
        {
            subdivisions = 2,
            radius = 1200f,
            seed = seed,
            placementJitter = 0.15f,
            radialJitter = 0.05f,
            noiseFrequency = 0.6f,
            noiseAmplitude = 10f,
            pulseSpeed = 0.4f,
            loopDuration = 12f,
            bakeFps = 5,
        };

        [Test]
        public void Generated_MatchesTheShippedBake()
        {
            var preset = AssetDatabase.LoadAssetAtPath<CapsuleMembraneAnimationSO>(ShippedPresetPath);
            if (preset == null || !preset.IsValid)
                Assert.Ignore($"No valid baked preset at {ShippedPresetPath} to compare against.");

            var loop = CapsuleMembraneLoop.Create(preset.Signature);
            Assert.AreEqual(preset.CapsuleCount, loop.CapsuleCount, "capsule count");
            Assert.AreEqual(preset.FrameCount, loop.FrameCount, "frame count");
            Assert.AreEqual(preset.LoopDuration, loop.LoopDuration, "loop duration");

            for (int i = 0; i < loop.CapsuleCount; i++)
                Assert.Less((preset.CapsuleOffsets[i] - loop.Offsets[i]).magnitude, 1e-3f, $"offset {i}");

            var baked = preset.Rotations;
            foreach (int f in new[] { 0, 1, loop.FrameCount / 2, loop.FrameCount - 1 })
            {
                var frame = loop.Frame(f);
                for (int i = 0; i < loop.CapsuleCount; i++)
                {
                    float dot = Mathf.Abs(Quaternion.Dot(baked[f * loop.CapsuleCount + i], frame[i]));
                    Assert.Greater(dot, 1f - 1e-5f, $"frame {f} capsule {i}");
                }
            }
        }

        [Test]
        public void Keyframes_AreGeneratedOnlyWhenAskedFor()
        {
            var loop = CapsuleMembraneLoop.Get(Small());
            Assert.AreEqual(0, loop.GeneratedFrameCount, "nothing generated up front");

            var first = loop.Frame(3);
            Assert.AreEqual(1, loop.GeneratedFrameCount);
            Assert.AreSame(first, loop.Frame(3), "a keyframe is generated once and kept");
            Assert.AreEqual(1, loop.GeneratedFrameCount);
        }

        [Test]
        public void Warm_SpreadsAKeyframe_AndMatchesAOneShotFill()
        {
            var warmed = CapsuleMembraneLoop.Create(Small());
            int slice = (warmed.CapsuleCount + 2) / 3;
            warmed.Warm(5, slice);
            Assert.IsFalse(warmed.IsComplete(5), "a third of a keyframe is not the keyframe");
            Assert.AreEqual(0, warmed.GeneratedFrameCount);
            warmed.Warm(5, slice);
            warmed.Warm(5, slice);
            Assert.IsTrue(warmed.IsComplete(5));
            Assert.AreEqual(1, warmed.GeneratedFrameCount);

            var oneShot = CapsuleMembraneLoop.Create(Small()).Frame(5);
            var pieces = warmed.Frame(5);
            for (int i = 0; i < oneShot.Length; i++)
                Assert.AreEqual(oneShot[i], pieces[i], $"capsule {i}");
        }

        [Test]
        public void EqualSettings_ShareOneLoop_DifferentSettingsDoNot()
        {
            var a = CapsuleMembraneLoop.Get(Small());
            var b = CapsuleMembraneLoop.Get(Small());
            var c = CapsuleMembraneLoop.Get(Small(seed: 7));

            Assert.AreSame(a, b);
            Assert.AreNotSame(a, c);
            Assert.AreEqual(2, CapsuleMembraneLoop.CachedLoopCount);
            Assert.AreNotEqual(a.Offsets[5], c.Offsets[5], "a different seed is a different layout");
        }

        [Test]
        public void Loop_IsSeamless()
        {
            // Keyframe N would be keyframe 0: the sample point goes once around a closed circle. Check that the
            // step from the last keyframe back to the first is no bigger than an ordinary step.
            var loop = CapsuleMembraneLoop.Get(Small());
            int last = loop.FrameCount - 1;
            float wrap = 0f, step = 0f;
            for (int i = 0; i < loop.CapsuleCount; i++)
            {
                wrap = Mathf.Max(wrap, Quaternion.Angle(loop.Frame(last)[i], loop.Frame(0)[i]));
                step = Mathf.Max(step, Quaternion.Angle(loop.Frame(0)[i], loop.Frame(1)[i]));
            }
            Assert.LessOrEqual(wrap, step * 1.5f + 1e-3f, $"wrap {wrap:0.###} deg vs step {step:0.###} deg");
        }

        [Test]
        public void FrameCount_FollowsTheBakeRule()
        {
            var sig = Small();
            Assert.AreEqual(60, CapsuleMembraneLoop.FrameCountFor(sig));
            sig.loopDuration = 0.1f;
            Assert.AreEqual(2, CapsuleMembraneLoop.FrameCountFor(sig), "never fewer than two keyframes");
        }
    }
}
#endif
