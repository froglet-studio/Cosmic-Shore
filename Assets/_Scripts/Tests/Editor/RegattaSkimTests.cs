#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Squirrel skimming Regatta's braided rails without striking them: the skim line keeps its
    /// tuned gap above whatever shell its course has (<see cref="SkimRaceCourse.SkimLift"/>), the
    /// ring is aimed where the pilot's OWN rail crosses it (<see cref="RegattaRingObjective.TryLaneCrossing"/>),
    /// and Regatta tells the pilot that rival rails are mass and direct flight is off. Skim Race's
    /// answers are held unchanged beside each one.
    /// </summary>
    public class RegattaSkimTests
    {
        static SkimRaceCourse Straight(Vector3 prismScale, int count = 20)
        {
            var pts = new List<Vector3>();
            var nrm = new List<Vector3>();
            var rot = new List<Quaternion>();
            var half = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                pts.Add(new Vector3(0f, 0f, i * 8f));
                nrm.Add(Vector3.up);
                rot.Add(Quaternion.identity);
                half.Add(prismScale * SkimRaceShell.SuperShieldHalfScale);
            }
            return new SkimRaceCourse(pts, nrm, rot, half);
        }

        [Test]
        public void SkimLift_IsZeroOnSkimRacePlates()
        {
            Assert.AreEqual(0f, Straight(new Vector3(10f, 1f, 3f)).SkimLift,
                "Skim Race's own plates are the reference: its tuned skim height must not move");
        }

        [Test]
        public void SkimLift_LiftsTheLineAboveRegattaRailShells()
        {
            var rail = Straight(new Vector3(6f, 6f, 8f));
            Assert.AreEqual(7.5f, rail.SkimLift, 1e-4f, "a 6 u rail's shell reaches 9 u along up; the plate's 1.5 u");
            // The tuned skim line sits SkimHeight above the prism centre plus the lift: outside the shell.
            float line = 4.346f + rail.SkimLift;
            Assert.Greater(rail.ShellClearance(new Vector3(0f, line, 40f), 5, 6, out _), 0f,
                "the skim line clears the rail's contact shell");
            Assert.AreEqual(0f, rail.ShellClearance(new Vector3(0f, 4.346f, 40f), 5, 6, out _),
                "without the lift the line ran INSIDE the shell - every frame a strike");
        }

        [Test]
        public void SkimLift_IsZeroWithoutShells()
        {
            var pts = new List<Vector3> { Vector3.zero, Vector3.forward * 10f, Vector3.forward * 20f };
            Assert.AreEqual(0f, new SkimRaceCourse(pts, null).SkimLift);
        }

        // A closed lane: a circle of radius 300 in the XZ plane, centred on the origin.
        static List<Vector3> CircleLane(int count = 240)
        {
            var lane = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                lane.Add(new Vector3(Mathf.Cos(a) * 300f, 0f, Mathf.Sin(a) * 300f));
            }
            return lane;
        }

        [Test]
        public void LaneCrossing_FindsWhereTheRailCrossesTheRing()
        {
            // A ring at (300, 0, 0) facing along the lane's travel (+z there), mouth 54 (Regatta I4's).
            // Its centre is offset 22 u inward from the rail, as the braid's spine sits from a lane.
            var centre = new Vector3(278f, 0f, 0f);
            Assert.IsTrue(RegattaRingObjective.TryLaneCrossing(CircleLane(), centre, Vector3.forward, 54f,
                out int seg, out Vector3 p));
            Assert.GreaterOrEqual(seg, 0);
            Assert.AreEqual(300f, p.x, 0.5f, "on the rail");
            Assert.AreEqual(0f, p.z, 0.5f, "in the ring's plane");
            Assert.Less(Vector3.Distance(p, centre), 54f, "inside the mouth, so the ring is threaded");
        }

        [Test]
        public void LaneCrossing_IgnoresTheFarSideOfTheLoop()
        {
            // The plane z = 0 also cuts the circle at x = -300, 578 u from the centre: never that one.
            Assert.IsTrue(RegattaRingObjective.TryLaneCrossing(CircleLane(), new Vector3(278f, 0f, 0f), Vector3.forward, 54f,
                out _, out Vector3 p));
            Assert.Greater(p.x, 0f);
        }

        [Test]
        public void LaneCrossing_FalseWhenTheRailMissesTheMouth()
        {
            Assert.IsFalse(RegattaRingObjective.TryLaneCrossing(CircleLane(), new Vector3(150f, 0f, 0f), Vector3.forward, 54f,
                out int seg, out Vector3 p));
            Assert.AreEqual(-1, seg);
            Assert.AreEqual(new Vector3(150f, 0f, 0f), p, "falls back to the centre");
        }

        [Test]
        public void Regatta_RivalRailsAreMass_AndDirectFlightIsOff()
        {
            var gd = ScriptableObject.CreateInstance<GameDataSO>();
            try
            {
                gd.GameMode = GameModes.Regatta;
                var regatta = SkimRaceObjective.For(gd);
                Assert.IsFalse(regatta.AllowsDirectFlight, "a straight line from rail to ring crosses the braid");
                gd.GameMode = GameModes.SkimRace;
                var skim = SkimRaceObjective.For(gd);
                Assert.IsTrue(skim.AllowsDirectFlight, "Skim Race keeps its calibrated direct flight");
                Assert.IsTrue(skim.IsCourseMass(null, null), "Skim Race: every super-shielded prism is the track");
            }
            finally { Object.DestroyImmediate(gd); }
        }

        [Test]
        public void Driver_DirectFlightSwitch_DefaultsOn()
        {
            var cfg = ScriptableObject.CreateInstance<SkimRaceAIConfigSO>();
            try { Assert.IsTrue(new SkimRaceDriver(cfg).AllowDirectFlight); }
            finally { Object.DestroyImmediate(cfg); }
        }
    }
}
#endif
