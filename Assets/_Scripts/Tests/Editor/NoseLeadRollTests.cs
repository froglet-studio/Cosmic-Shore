using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The nose lead's ROLL (<see cref="NoseLeadSteering.RollStick"/>): a dual-stick AI hull's roll stick is its yaw stick,
    /// and none while the lead steers. Uses only the pure steering math, so it also runs headless against the Unity shim
    /// (<c>bash Tools/Build/nose_lead_harness/run.sh</c>). The same law offline: <c>Tools/Build/ai_nose_lead.py</c>
    /// (<c>roll_stick</c>, law "zero") in manta_redline_sim, skein_urchin_sim and thresher_time_sim.
    /// </summary>
    public class NoseLeadRollTests
    {
        [Test]
        public void WithTheLeadOff_TheRollStickIsTheYawStick_AsAlways()
        {
            foreach (float yaw in new[] { -1f, -0.37f, 0f, 0.5f, 1f })
                Assert.AreEqual(yaw, NoseLeadSteering.RollStick(false, yaw), 0f, $"yaw {yaw}");
        }

        [Test]
        public void WithTheLeadOn_NoRollStick_WhateverTheYawStick()
        {
            foreach (float yaw in new[] { -1f, -0.37f, 0.5f, 1f })
                Assert.AreEqual(0f, NoseLeadSteering.RollStick(true, yaw), 0f, $"yaw {yaw}");
        }

        [Test]
        public void TheStick_IsTheSameMathAsBefore_ForwardAndBehind()
        {
            // 30 deg off on the yaw axis, far away: the asin saturates, full stick on x, none on y.
            Vector3 cross = Vector3.Cross(Vector3.forward, Quaternion.AngleAxis(30f, Vector3.up) * Vector3.forward);
            Vector2 s = NoseLeadSteering.ErrorStick(cross, 0.8f, 1e6f, 50f, 100f);
            Assert.AreEqual(1f, s.x, 1e-6f);
            Assert.AreEqual(0f, s.y, 1e-6f);
            // Behind: TurnHardIfBehind turns full on the cross product's own axis.
            Vector2 behind = NoseLeadSteering.ErrorStick(new Vector3(0.001f, 0f, 0f), -0.9f, 1e6f, 50f, 100f);
            Assert.AreEqual(1f, behind.y, 1e-6f);
            Assert.AreEqual(0f, behind.x, 1e-6f);
        }

        /// <summary>
        /// The claim the law is for, closed loop: a dual-stick hull (VesselTransformer's Roll, Yaw, Pitch onto the
        /// command about the NOSE's axes, a Manta at 200 u/s: 140 / 70 / 90 deg/s) whose nose follows the command at the
        /// fleet's 1.5/s, the lead at 2.5 / 40 deg, asked for a run of aims off both axes. With the roll stick off while
        /// leading the nose settles on every aim; flying the LED yaw stick as its roll (the lead as first built, PR #1786:
        /// the negative control) it corkscrews and misses some. Offline, the same loop is the race models' (2026-11-07).
        /// </summary>
        [Test]
        public void OnALaggingNose_TheLeadWithNoRoll_SettlesOnEveryAim_WhereTheLedRollCorkscrews()
        {
            int settled = Fly(rollLaw: true, out float worstFinal);
            int settledLedRoll = Fly(rollLaw: false, out float worstLedRoll);
            Assert.AreEqual(Aims.Length, settled, $"the law settles every aim (worst final error {worstFinal:F1} deg)");
            Assert.Less(settledLedRoll, Aims.Length,
                $"the negative control: the led roll settles {settledLedRoll} of {Aims.Length} (worst {worstLedRoll:F1} deg)");
        }

        static readonly Vector3[] Aims =
        {
            Quaternion.Euler(-40f, 70f, 0f) * Vector3.forward,
            Quaternion.Euler(35f, -60f, 0f) * Vector3.forward,
            Quaternion.Euler(-60f, -20f, 0f) * Vector3.forward,
            Quaternion.Euler(20f, 110f, 0f) * Vector3.forward,
            Quaternion.Euler(50f, 40f, 0f) * Vector3.forward,
            Quaternion.Euler(-25f, -95f, 0f) * Vector3.forward,
        };

        /// <summary>Flies each aim in turn for 4 s (from wherever the last one left the hull) and counts those the nose
        /// ends within 5 deg of, holding there for the last half second.</summary>
        static int Fly(bool rollLaw, out float worstFinal)
        {
            const float dt = 1f / 60f, follow = 1.5f, gain = 2.5f, extra = 40f;
            const float roll = 140f, yawRate = 70f, pitch = 90f;
            Quaternion nose = Quaternion.identity, command = Quaternion.identity;
            int settled = 0;
            worstFinal = 0f;
            foreach (Vector3 aimDirection in Aims)
            {
                Vector3 aim = aimDirection.normalized;
                bool held = true;
                const int frames = 240, holdFrom = 210;
                for (int i = 0; i < frames; i++)
                {
                    Vector3 fwd = nose * Vector3.forward;
                    Vector3 judged = command * Vector3.forward;
                    Vector3 want = NoseLeadSteering.Direction(fwd, aim, gain, extra);
                    Vector3 localCross = Quaternion.Inverse(nose) * Vector3.Cross(judged, want);
                    Vector2 stick = NoseLeadSteering.ErrorStick(localCross, Vector3.Dot(judged, want), 1e6f, 50f, 100f);
                    float rollStick = rollLaw ? NoseLeadSteering.RollStick(true, stick.x) : stick.x;
                    command = Quaternion.AngleAxis(rollStick * roll * dt, nose * Vector3.forward) * command;
                    command = Quaternion.AngleAxis(stick.x * yawRate * dt, nose * Vector3.up) * command;
                    command = Quaternion.AngleAxis(stick.y * pitch * dt, nose * Vector3.right) * command;
                    nose = Quaternion.Slerp(nose, command, follow * dt);
                    if (i >= holdFrom && Vector3.Angle(nose * Vector3.forward, aim) > 5f) held = false;
                }
                float final = Vector3.Angle(nose * Vector3.forward, aim);
                worstFinal = Mathf.Max(worstFinal, final);
                if (held) settled++;
            }
            return settled;
        }
    }
}
