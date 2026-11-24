#if UNITY_EDITOR
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Ark's helm (<see cref="ArkHelm"/>): it gathers way instead of snapping to speed, never overshoots the speed it
    /// is asked for, swings its bow at a bounded rate and still always arrives, weaves on distance (so every machine in a
    /// party draws the same course), lines up on the core, and rocks back to rest after a flinch.
    /// </summary>
    public class ArkHelmTests
    {
        const string DefinitionPath = "Assets/_SO_Assets/Toys/ArkReverseModel.asset";
        static readonly ArkHelmSettings S = ArkHelmSettings.Default;

        [Test]
        public void Speed_GathersWay_WithoutOvershoot()
        {
            var helm = new ArkHelm(S);
            float speed = 0f, last = 0f, dt = 1f / 30f;
            for (int i = 0; i < 30 * 20; i++)
            {
                speed = helm.StepSpeed(speed, 45f, dt);
                Assert.LessOrEqual(speed, 45f + 1e-4f, "never past the profile's speed");
                Assert.LessOrEqual((speed - last) / dt, S.Acceleration + 1e-3f, "never faster than the acceleration limit");
                last = speed;
            }
            Assert.AreEqual(45f, speed, 0.5f, "reaches cruise within 20 s");
            Assert.Greater(new ArkHelm(S).StepSpeed(0f, 45f, dt), 0f);
            Assert.Less(new ArkHelm(S).StepSpeed(0f, 45f, 1f), 45f, "one long frame does not snap to cruise");
        }

        [Test]
        public void Speed_Brakes_ToRest()
        {
            var helm = new ArkHelm(S);
            float speed = 14f;
            for (int i = 0; i < 30 * 10; i++) speed = helm.StepSpeed(speed, 0f, 1f / 30f);
            Assert.AreEqual(0f, speed, 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(170f)]
        public void Steering_AlwaysArrives_AtABoundedTurnRate(float startOffDegrees)
        {
            var helm = new ArkHelm(S);
            var position = Vector3.zero;
            var destination = new Vector3(0f, 200f, 3000f);
            var heading = Quaternion.AngleAxis(startOffDegrees, Vector3.up) * Vector3.forward;
            float speed = 0f, dt = 1f / 30f;
            bool arrived = false;
            for (int i = 0; i < 30 * 240 && !arrived; i++)
            {
                var to = destination - position;
                float dist = to.magnitude;
                if (dist <= 0.5f) { arrived = true; break; }
                speed = helm.StepSpeed(speed, dist < 400f ? 14f : 45f, dt);
                var dir = to / dist;
                float step = Mathf.Min(speed * dt, dist);
                var next = helm.Steer(heading, dir, dist, step, dt);
                Assert.LessOrEqual(Vector3.Angle(heading, next), S.MaxTurnRate * dt + 1e-3f, "the bow swings at a bounded rate");
                heading = next;
                position += (dist <= S.CaptureRange ? dir : heading) * step;
            }
            Assert.IsTrue(arrived, $"the Ark reached the core from {startOffDegrees} degrees off (left {Vector3.Distance(position, destination):F1} u)");
        }

        [Test]
        public void Weave_FadesOut_NearTheCore()
        {
            var helm = new ArkHelm(S);
            helm.Steer(Vector3.forward, Vector3.forward, 5000f, S.WeaveWavelength * 0.25f, 0.1f);   // a quarter-wave in
            Assert.Greater(Vector3.Angle(Vector3.forward, helm.WeavedBearing(Vector3.forward, 5000f)), 1f, "it weaves in open water");
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, helm.WeavedBearing(Vector3.forward, S.CaptureRange)), 1e-3f,
                "it lines up on the core");
        }

        [Test]
        public void Weave_FollowsDistance_NotFrameRate()
        {
            var a = new ArkHelm(S);
            var b = new ArkHelm(S);
            for (int i = 0; i < 300; i++) a.Steer(Vector3.forward, Vector3.forward, 5000f, 2f, 1f / 60f);
            for (int i = 0; i < 60; i++) b.Steer(Vector3.forward, Vector3.forward, 5000f, 10f, 1f / 12f);
            Assert.AreEqual(a.Travelled, b.Travelled, 1e-2f);
            Assert.AreEqual(0f, Vector3.Angle(a.WeavedBearing(Vector3.forward, 5000f), b.WeavedBearing(Vector3.forward, 5000f)), 1e-2f,
                "two machines at different frame rates draw the same course");
        }

        [Test]
        public void Flinch_RocksAwayFromTheHit_AndSettles()
        {
            var helm = new ArkHelm(S);
            helm.Attitude(0f, true, 0.01f, out _, out _);
            Assert.IsTrue(helm.Flinch(new Vector3(30f, 0f, 0f)), "a hit on the right flinches");
            Assert.IsFalse(helm.Flinch(new Vector3(30f, 0f, 0f)), "a second hit inside the cooldown does not");
            Assert.Less(helm.Jink, 0f, "the bow jinks left, away from the right-side hit");
            float peak = 0f;
            for (int i = 0; i < 30; i++)
            {
                helm.Attitude(0f, true, 1f / 30f, out _, out _);
                peak = Mathf.Max(peak, helm.FlinchRollAngle);
            }
            Assert.Greater(peak, S.FlinchRoll * 0.3f, "the right side rocks up");
            Assert.LessOrEqual(peak, S.FlinchRoll * 1.05f, "by about FlinchRoll, not more");
            for (int i = 0; i < 30 * 8; i++) helm.Attitude(0f, true, 1f / 30f, out _, out _);
            Assert.AreEqual(0f, helm.FlinchRollAngle, 0.05f, "and settles");
            Assert.AreEqual(0f, helm.Jink, 1e-3f);
        }

        [Test]
        public void Attitude_BanksIntoTheTurn()
        {
            var helm = new ArkHelm(S);
            Quaternion rotation = Quaternion.identity;
            for (int i = 0; i < 90; i++) helm.Attitude(10f, true, 1f / 30f, out rotation, out _);   // turning right
            Assert.Less(Vector3.Dot(rotation * Vector3.right, Vector3.up), -0.1f, "the right side dips in a right turn");
        }

        [Test]
        public void Sanitized_ZeroedSettings_SailTheDefault()
        {
            Assert.AreEqual(ArkHelmSettings.Default.MaxTurnRate, default(ArkHelmSettings).Sanitized().MaxTurnRate);
        }

        [Test]
        public void ArkReverse_IsGivenAHelm()
        {
            var def = AssetDatabase.LoadAssetAtPath<ArkModelDefinitionSO>(DefinitionPath);
            Assert.IsNotNull(def, DefinitionPath);
            Assert.Greater(def.Helm.MaxTurnRate, 0f, "the voyage Ark's model authors its helm");
            Assert.Greater(def.Helm.WeaveYaw, 0f, "and it weaves");
        }
    }
}
#endif
