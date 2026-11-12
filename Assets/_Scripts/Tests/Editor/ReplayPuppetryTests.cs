#if UNITY_EDITOR
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The stick a replayed hull's puppetry is driven by is the inverse of VesselTransformer's
    /// Pitch / Yaw / Roll: a hull turned at a known stick for one step must read back as that stick.
    /// </summary>
    public class ReplayPuppetryTests
    {
        static readonly Vector3 Full = ReplayPuppetry.FullStickRates(60f, 0f, 130f, 130f, 130f, 1f);

        // One flight-model step: the same AngleAxis about the hull's own axes that VesselTransformer applies.
        static Quaternion Step(Quaternion rot, Vector3 stick, float dt)
        {
            var right = rot * Vector3.right; var up = rot * Vector3.up; var fwd = rot * Vector3.forward;
            var turn = Quaternion.AngleAxis(stick.x * Full.x * dt, right)
                     * Quaternion.AngleAxis(stick.y * Full.y * dt, up)
                     * Quaternion.AngleAxis(stick.z * Full.z * dt, fwd);
            return turn * rot;
        }

        [TestCase(0.5f, 0f, 0f)]
        [TestCase(0f, -0.7f, 0f)]
        [TestCase(0f, 0f, 1f)]
        public void A_single_axis_turn_reads_back_as_its_stick(float pitch, float yaw, float roll)
        {
            var from = Quaternion.Euler(10f, 40f, -5f);
            var stick = new Vector3(pitch, yaw, roll);
            var to = Step(from, stick, 0.1f);
            var read = ReplayPuppetry.StickFor(ReplayPuppetry.LocalTurnRate(from, to, 0.1f), Full);
            Assert.That(Vector3.Distance(read, stick), Is.LessThan(0.02f), $"read {read} for {stick}");
        }

        [Test]
        public void A_turn_faster_than_full_stick_clamps()
        {
            var from = Quaternion.identity;
            var to = Quaternion.AngleAxis(90f, Vector3.up);
            var read = ReplayPuppetry.StickFor(ReplayPuppetry.LocalTurnRate(from, to, 0.1f), Full);
            Assert.That(read.y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void A_seek_is_not_a_turn()
        {
            var rate = ReplayPuppetry.LocalTurnRate(Quaternion.identity, Quaternion.Euler(0f, 120f, 0f), 3f);
            Assert.That(rate, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Throttle_reads_speed_between_minimum_and_full()
        {
            Assert.That(ReplayPuppetry.ThrottleFor(35f, 10f, 50f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ReplayPuppetry.ThrottleFor(5f, 10f, 50f), Is.EqualTo(0f));
            Assert.That(ReplayPuppetry.ThrottleFor(90f, 10f, 50f), Is.EqualTo(1f));
        }
    }
}
#endif
