#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Rhino sword combos (RHINO_SWORD_COMBOS.md). The detector is the rule that keeps combos
    /// OUT of the way of the analog swordsmanship — a held trigger is positioning, never a tap,
    /// and the both-triggers stance is a chord, never "RL" — so it is driven here with the same
    /// 60 Hz trigger samples a pilot produces. The path sampler and the end-pose wrap are the
    /// two pieces of math a flourish's look rests on.
    /// </summary>
    [TestFixture]
    public class RhinoSwordComboTests
    {
        const float Dt = 1f / 60f;

        static RhinoSwordComboDetector NewDetector() => new(new RhinoSwordComboDetector.Settings
        {
            PressThreshold = 0.5f,
            ReleaseThreshold = 0.2f,
            ComboWindowSeconds = 0.35f,
            TapMaxHoldSeconds = 0.3f,
            ChordWindowSeconds = 0.08f,
        });

        /// <summary>One scripted input segment: hold (left, right) for <c>seconds</c>.</summary>
        struct Seg
        {
            public float L, R, Seconds;
            public Seg(float l, float r, float s) { L = l; R = r; Seconds = s; }
        }

        static Seg Tap(char side, float held = 0.1f) => side == 'R' ? new Seg(0, 1, held) : new Seg(1, 0, held);
        static Seg Gap(float s = 0.1f) => new(0, 0, s);

        static List<string> Run(RhinoSwordComboDetector d, params Seg[] segs)
        {
            var found = new List<string>();
            float t = 0f;
            foreach (var s in segs)
            {
                for (float e = 0f; e < s.Seconds - 1e-4f; e += Dt)
                {
                    if (d.Step(t, s.L, s.R, out var combo)) found.Add(combo.Sequence);
                    t += Dt;
                }
            }
            return found;
        }

        [TestCase("RR")]
        [TestCase("LL")]
        [TestCase("RL")]
        [TestCase("LR")]
        public void TwoTaps_EmitTheTwoLetterCombo(string seq)
        {
            var found = Run(NewDetector(), Tap(seq[0]), Gap(), Tap(seq[1]), Gap(0.5f));
            CollectionAssert.AreEqual(new[] { seq }, found);
        }

        [TestCase("RRR")]
        [TestCase("LLL")]
        [TestCase("RLR")]
        [TestCase("LRL")]
        [TestCase("RRL")]
        [TestCase("LLR")]
        [TestCase("RLL")]
        [TestCase("LRR")]
        public void ThreeTaps_EmitTheTwoLetterComboThenUpgradeToTheFinisher(string seq)
        {
            var found = Run(NewDetector(), Tap(seq[0]), Gap(), Tap(seq[1]), Gap(), Tap(seq[2]), Gap(0.5f));
            CollectionAssert.AreEqual(new[] { seq.Substring(0, 2), seq }, found);
        }

        [Test]
        public void AFinisherClosesTheChain_TheNextTapStartsFresh()
        {
            var found = Run(NewDetector(), Tap('R'), Gap(), Tap('R'), Gap(), Tap('R'), Gap(), Tap('L'), Gap(), Tap('L'), Gap(0.5f));
            CollectionAssert.AreEqual(new[] { "RR", "RRR", "LL" }, found);
        }

        [Test]
        public void AHeldTrigger_IsPositioning_NotATap()
        {
            // Hold right for half a second (placing the sword), then tap left. The window is
            // widened so ONLY the hold rule can be what refuses the link.
            var d = new RhinoSwordComboDetector(new RhinoSwordComboDetector.Settings
            {
                PressThreshold = 0.5f, ReleaseThreshold = 0.2f, ComboWindowSeconds = 5f,
                TapMaxHoldSeconds = 0.3f, ChordWindowSeconds = 0.08f,
            });
            var found = Run(d, Tap('R', 0.5f), Gap(0.05f), Tap('L'), Gap(0.5f));
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void FeatheringTheStance_NeverLeaksAComboAfterward()
        {
            // Hold both (energizing), flick right off and back on, release both, then tap right
            // once. Without the chord latch the flick would seed a chain and the tap would read RR.
            var found = Run(NewDetector(), new Seg(1, 1, 0.5f), new Seg(1, 0, 0.05f), new Seg(1, 1, 0.1f),
                            Gap(0.1f), Tap('R'), Gap(0.5f));
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void SlowTaps_NeverLink()
        {
            var found = Run(NewDetector(), Tap('R'), Gap(0.4f), Tap('R'), Gap(0.5f));
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void BothTriggersTogether_IsTheEnergizeStance_NeverACombo()
        {
            var found = Run(NewDetector(), new Seg(1, 1, 0.8f), Gap(), Tap('R'), Gap(0.5f));
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void ARolledTap_StillLinks()
        {
            // Right tapped, left pressed 0.12 s later while right is still coming off.
            var found = Run(NewDetector(), new Seg(0, 1, 0.12f), new Seg(1, 1, 0.05f), new Seg(1, 0, 0.1f), Gap(0.5f));
            CollectionAssert.AreEqual(new[] { "RL" }, found);
        }

        [Test]
        public void PartialPulls_BelowThePressThreshold_AreNotTaps()
        {
            var found = Run(NewDetector(), new Seg(0, 0.4f, 0.1f), Gap(), new Seg(0, 0.4f, 0.1f), Gap(0.5f));
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void SuppressedPresses_DoNotChain()
        {
            var d = NewDetector();
            d.SuppressPressesUntil(10f);
            var found = Run(d, Tap('R'), Gap(), Tap('R'), Gap(0.5f));
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void SamplePath_HitsEveryKeyExactly_AndClampsOutside()
        {
            var keys = new List<RhinoSwordComboKeyframe>
            {
                new() { time = 0f, yaw = 0f },
                new() { time = 0.3f, yaw = 90f, roll = 80f, pitch = 10f },
                new() { time = 0.7f, yaw = -40f, pitch = 60f, thrust = 0.5f },
                new() { time = 1f, yaw = 380f },
            };
            foreach (var k in keys)
            {
                var s = RhinoSwordComboPath.SamplePath(keys, k.time);
                Assert.AreEqual(k.yaw, s.yaw, 1e-3f);
                Assert.AreEqual(k.roll, s.roll, 1e-3f);
                Assert.AreEqual(k.pitch, s.pitch, 1e-3f);
                Assert.AreEqual(k.thrust, s.thrust, 1e-3f);
            }
            Assert.AreEqual(0f, RhinoSwordComboPath.SamplePath(keys, -1f).yaw, 1e-4f);
            Assert.AreEqual(380f, RhinoSwordComboPath.SamplePath(keys, 2f).yaw, 1e-4f);
        }

        [Test]
        public void SamplePath_IsContinuous()
        {
            var keys = new List<RhinoSwordComboKeyframe>
            {
                new() { time = 0f }, new() { time = 0.2f, yaw = 360f }, new() { time = 0.5f, yaw = -90f, pitch = -200f }, new() { time = 1f, yaw = 20f },
            };
            var prev = RhinoSwordComboPath.SamplePath(keys, 0f);
            for (int i = 1; i <= 1000; i++)
            {
                var s = RhinoSwordComboPath.SamplePath(keys, i / 1000f);
                Assert.Less(Mathf.Abs(s.yaw - prev.yaw), 15f, $"jump at {i}");
                Assert.Less(Mathf.Abs(s.pitch - prev.pitch), 15f, $"jump at {i}");
                prev = s;
            }
        }

        [TestCase(380f, 20f)]
        [TestCase(-345f, 15f)]
        [TestCase(720f, 0f)]
        [TestCase(-180f, 180f)]
        [TestCase(180f, 180f)]
        [TestCase(-90f, -90f)]
        public void WrapDegrees_LandsInHalfOpenRange(float input, float expected)
        {
            Assert.AreEqual(expected, SwordPoseChannels.WrapDegrees(input), 1e-3f);
        }

        [Test]
        public void Wrapping_NeverChangesTheDrawnPose()
        {
            var c = new SwordPoseChannels(740f, -520f, -345f, 0.3f);
            Quaternion Pose(SwordPoseChannels p) =>
                Quaternion.AngleAxis(p.yaw, Vector3.up) * Quaternion.AngleAxis(p.roll, Vector3.forward) * Quaternion.AngleAxis(p.pitch, Vector3.right);
            float angle = Quaternion.Angle(Pose(c), Pose(c.Wrapped()));
            Assert.Less(angle, 0.05f);
        }

        [Test]
        public void Library_EnergizedFallsBackToBase_ButPrefersItsOwnVariant()
        {
            var lib = ScriptableObject.CreateInstance<RhinoSwordComboLibrarySO>();
            const string k = "\"keys\":[{\"time\":0},{\"time\":1,\"yaw\":90}]";
            JsonUtility.FromJsonOverwrite(
                "{\"combos\":[" +
                "{\"sequence\":\"RR\",\"energized\":false,\"displayName\":\"base\"," + k + "}," +
                "{\"sequence\":\"RR\",\"energized\":true,\"displayName\":\"up\"," + k + "}," +
                "{\"sequence\":\"LL\",\"energized\":false,\"displayName\":\"onlyBase\"," + k + "}]}", lib);

            Assert.IsTrue(lib.TryGet("RR", true, out var p)); Assert.AreEqual("up", p.DisplayName);
            Assert.IsTrue(lib.TryGet("RR", false, out p)); Assert.AreEqual("base", p.DisplayName);
            Assert.IsTrue(lib.TryGet("LL", true, out p)); Assert.AreEqual("onlyBase", p.DisplayName);
            Assert.IsFalse(lib.TryGet("RLR", false, out _));
            Object.DestroyImmediate(lib);
        }
    }
}
#endif
