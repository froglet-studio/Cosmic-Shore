#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The host's AI-like steering measure (fair play, Threat Board T6). The thresholds are set later from logged
    /// play; these tests hold the two numbers to their meaning: a nose that never stops correcting reads as never
    /// still, and stop-and-flick steering reads as mostly still with a large change in turn rate.
    /// </summary>
    public class SteeringProfileTests
    {
        const float Dt = 0.1f;

        static Vector3 Yaw(float degrees) => Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;

        [Test]
        public void ContinuousCorrection_IsNeverStill_WithLowJerk()
        {
            var p = new SteeringProfile();
            float heading = 0f;
            p.Restart(Yaw(heading));
            for (int i = 1; i <= 600; i++)
            {
                heading += (20f + 5f * Mathf.Sin(i * 0.05f)) * Dt; // 15-25 deg/s, always turning
                p.Add(Yaw(heading), Dt, 2f);
            }
            Assert.AreEqual(0f, p.IdlePercent, 1e-3f);
            Assert.Less(p.MeanTurnJerk, 5f);
            Assert.AreEqual(60f, p.Seconds, 1e-3f);
        }

        [Test]
        public void StopAndFlick_IsMostlyStill_WithHighJerk()
        {
            var p = new SteeringProfile();
            float heading = 0f;
            p.Restart(Yaw(heading));
            for (int i = 1; i <= 600; i++)
            {
                bool flick = i % 13 < 3; // 0.3 s at 90 deg/s, then 1 s still
                if (flick) heading += 90f * Dt;
                p.Add(Yaw(heading), Dt, 2f);
            }
            Assert.Greater(p.IdlePercent, 70f);
            Assert.Greater(p.MeanTurnJerk, 100f);
        }

        [Test]
        public void Restart_DoesNotCountTheJumpAsATurn()
        {
            var p = new SteeringProfile();
            p.Restart(Yaw(0f));
            p.Add(Yaw(0f), Dt, 2f);
            p.Restart(Yaw(180f)); // respawn facing the other way
            p.Add(Yaw(180f), Dt, 2f);
            Assert.AreEqual(2, p.Samples);
            Assert.AreEqual(100f, p.IdlePercent, 1e-3f);
            Assert.AreEqual(0f, p.MeanTurnJerk, 1e-3f);
        }
    }
}
#endif
