using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The AI's nose lead (<see cref="AIPilot.NoseLeadGain"/>): with it on, the pilot places its COMMAND past the aim so a
    /// nose that follows the command through a lag closes on the aim instead of overshooting it. Off (0) everywhere it
    /// ships; Regatta turns it on per hull from <see cref="RaceLineStyleSO.NoseLeadGain"/>. Offline model and numbers:
    /// <c>Tools/Build/scarab_nose_lead_sim.py</c> (its <c>slerp_beyond</c> is <see cref="AIPilot.NoseLeadDirection"/>).
    /// </summary>
    public class NoseLeadTests
    {
        static readonly Vector3 Nose = Vector3.forward;

        static Vector3 Off(float degrees) => Quaternion.AngleAxis(degrees, Vector3.up) * Vector3.forward;

        [Test]
        public void TheLead_TurnsGainTimesTheError_InTheSamePlane()
        {
            Vector3 lead = AIPilot.NoseLeadDirection(Nose, Off(10f), 2.5f, 40f);
            Assert.AreEqual(25f, Vector3.Angle(Nose, lead), 1e-3f);
            Assert.AreEqual(0f, lead.y, 1e-5f, "stays in the nose-aim plane");
            Assert.Greater(lead.x, 0f, "toward the aim, not away");
            Assert.AreEqual(1f, lead.magnitude, 1e-5f);
        }

        [Test]
        public void TheLead_StopsItsExtraDegreesPastTheAim_AndShortOfTheNosesTail()
        {
            Assert.AreEqual(100f, Vector3.Angle(Nose, AIPilot.NoseLeadDirection(Nose, Off(60f), 2.5f, 40f)), 1e-3f,
                "60 deg off: no more than 40 past the aim");
            Assert.AreEqual(AIPilot.NoseLeadMaxTurnDegrees,
                Vector3.Angle(Nose, AIPilot.NoseLeadDirection(Nose, Off(150f), 2.5f, 120f)), 1e-3f);
        }

        [Test]
        public void TheLead_IsTheAimItself_WhenOff_OrWithNoPlaneToTurnIn()
        {
            Vector3 aim = Off(30f);
            Assert.AreEqual(0f, Vector3.Angle(aim, AIPilot.NoseLeadDirection(Nose, aim, 0f, 40f)), 1e-3f, "gain 0");
            Assert.AreEqual(0f, Vector3.Angle(Nose, AIPilot.NoseLeadDirection(Nose, Nose, 2.5f, 40f)), 1e-3f, "on the nose");
            Assert.AreEqual(180f, Vector3.Angle(Nose, AIPilot.NoseLeadDirection(Nose, -Nose, 2.5f, 40f)), 1e-3f,
                "dead astern is TurnHardIfBehind's case, not the lead's");
        }

        [Test]
        public void TheGain_IsStatedAtTheFleetLag_AndScaledToTheHullsOwnRate()
        {
            float fleet = VesselTransformer.RotationFollowRate;
            Assert.AreEqual(2.5f, AIPilot.EffectiveNoseLeadGain(2.5f, fleet), 1e-5f);
            Assert.AreEqual(1.25f, AIPilot.EffectiveNoseLeadGain(2.5f, 2f * fleet), 1e-5f, "a nose twice as quick needs half");
            Assert.AreEqual(1f, AIPilot.EffectiveNoseLeadGain(2.5f, 9f), 1e-5f, "never below judging from the command");
            Assert.AreEqual(0f, AIPilot.EffectiveNoseLeadGain(2.5f, float.PositiveInfinity), "a nose with no lag: off");
            Assert.AreEqual(0f, AIPilot.EffectiveNoseLeadGain(0f, fleet), "off");
        }

        /// <summary>
        /// The claim the lead is for, on the steering law alone: AIPilot's stick (asin of the cross product, 100 / 50, so
        /// full from about 16 deg), the Scarab's Time-10 turn rate (0.1 x 324 + 100 deg/s) turning the COMMAND, and the
        /// nose following the command at the fleet's 1.5/s. Asked to swing 60 deg, judging from the nose overshoots by
        /// about 18 deg and settles inside 3 deg after about 4.5 s; the lead at 2.5 overshoots by about 1.5 and settles
        /// in about 1.1 s (the same loop in Python: scarab_nose_lead_sim.py's race steering).
        /// </summary>
        [Test]
        public void OnALaggingNose_TheLeadSettlesASixtyDegreeSwing_WhereTheNoseOvershoots()
        {
            Swing(0f, out float todayOvershoot, out float todaySettle);
            Swing(2.5f, out float leadOvershoot, out float leadSettle);
            Assert.Greater(todayOvershoot, 10f, "the negative control: judged from the nose, the lag overshoots");
            Assert.Less(leadOvershoot, 5f);
            Assert.Less(leadSettle, 0.5f * todaySettle);
        }

        static void Swing(float gain, out float overshoot, out float settleSeconds)
        {
            const float dt = 1f / 60f, omega = 0.1f * 324f + 100f, follow = 1.5f;
            Vector3 aim = Off(60f);
            Vector3 nose = Vector3.forward, command = Vector3.forward;
            overshoot = 0f;
            settleSeconds = 0f;
            for (int i = 0; i < 600; i++)
            {
                Vector3 want = gain > 0f ? AIPilot.NoseLeadDirection(nose, aim, gain, AIPilot.DefaultNoseLeadMaxExtraDegrees) : aim;
                Vector3 judged = gain > 0f ? command : nose;
                float err = Vector3.Angle(judged, want) * Mathf.Deg2Rad;
                float stick = Mathf.Min(1f, Mathf.Asin(Mathf.Min(1f, Mathf.Sin(err) * Mathf.Sin(err) * 100f / 50f))
                                            * Mathf.Rad2Deg * Mathf.Sin(err));
                if (Vector3.Dot(judged, want) < 0f) stick = 1f;                 // TurnHardIfBehind
                // The stick turns the command about the judged error's axis by its rate this frame, as Pitch / Yaw do,
                // whether or not the command is already past the aim.
                Vector3 axis = Vector3.Cross(judged, want);
                if (axis.sqrMagnitude > 1e-10f)
                    command = (Quaternion.AngleAxis(omega * stick * dt, axis.normalized) * command).normalized;
                nose = Vector3.Slerp(nose, command, follow * dt);
                float signed = Vector3.SignedAngle(Vector3.forward, nose, Vector3.up);
                overshoot = Mathf.Max(overshoot, signed - 60f);
                if (Mathf.Abs(signed - 60f) >= 3f) settleSeconds = (i + 1) * dt;   // the last moment outside 3 deg
            }
        }

        [Test]
        public void TheLead_IsOffUntilAModeSetsIt_AndNoShippedStyleSetsIt()
        {
            var style = ScriptableObject.CreateInstance<RaceLineStyleSO>();
            Assert.AreEqual(0f, style.NoseLeadGain, "a new style flies the steering as before");
            Assert.AreEqual(AIPilot.DefaultNoseLeadMaxExtraDegrees, style.NoseLeadMaxExtraDegrees);
            Object.DestroyImmediate(style);

            // Switched on by the AI's owner (Yash) after a real-game check: author_race_lines.py's HULLS, never by hand.
            var shipped = Resources.LoadAll<RaceLineStyleSO>(RaceLineStyleSO.ResourceFolder);
            Assert.IsNotEmpty(shipped, "Resources/RaceLines is missing");
            foreach (var s in shipped)
                Assert.AreEqual(0f, s.NoseLeadGain, $"{s.name} ships the nose lead on; update this test with the owner's yes");
        }
    }
}
