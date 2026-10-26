using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Holds <see cref="OmegaMath"/> to the Vessel Studio's Omega Crews lab it was ported from. Every golden number below
    /// is the page's OWN code (<c>Docs/Studios/VesselStudio/omega-studio.js</c>, part B, on Ys-bleeding-edge) run in
    /// headless Chromium on the same inputs (<c>omega_goldens.cjs</c>, 2026-10-26: omegaFlight per crew, slotOf, the
    /// meter over a scripted minute, mults at every level, the omegaCore cone test, the dock ease).
    /// </summary>
    [TestFixture]
    public class OmegaMathTests
    {
        static readonly OmegaMath.Params P = OmegaMath.Params.Studio;

        static void Near(Vector3 expected, Vector3 actual, float tol, string what)
        {
            Assert.AreEqual(expected.x, actual.x, tol, what + ".x");
            Assert.AreEqual(expected.y, actual.y, tol, what + ".y");
            Assert.AreEqual(expected.z, actual.z, tol, what + ".z");
        }

        [Test]
        public void Tables_AreThePages_NineMembersAndThreeCrewsThatUseEachOnce()
        {
            Assert.AreEqual(9, OmegaMath.Members.Length);
            Assert.AreEqual(3, OmegaMath.Crews.Length);
            var used = new bool[9];
            foreach (var c in OmegaMath.Crews)
            {
                Assert.AreEqual(3, c.Members.Length, c.Name);
                foreach (int m in c.Members) { Assert.IsFalse(used[m], OmegaMath.Members[m].Name + " is in two crews"); used[m] = true; }
            }
            Assert.AreEqual("Stoat", OmegaMath.Members[OmegaMath.CrewOf(OmegaMath.Crew.Warden).Members[0]].Name, "the Stoat leads the Warden (Yash 2026-10-26)");
            // every crew is a mobility vessel, an attacker and a defender (Yash 2026-10-26: defender, midfielder, attacker)
            Assert.AreEqual("Sparrow", OmegaMath.Members[OmegaMath.CrewOf(OmegaMath.Crew.Warden).Members[(int)OmegaMath.Role.Attack]].Name);
            Assert.AreEqual("Rhino", OmegaMath.Members[OmegaMath.CrewOf(OmegaMath.Crew.Warden).Members[(int)OmegaMath.Role.Defence]].Name);
            Assert.AreEqual("Scarab", OmegaMath.Members[OmegaMath.CrewOf(OmegaMath.Crew.Bulwark).Members[(int)OmegaMath.Role.Mobility]].Name);
            Assert.AreEqual("Thresher", OmegaMath.Members[OmegaMath.CrewOf(OmegaMath.Crew.Tempest).Members[(int)OmegaMath.Role.Attack]].Name);
            Assert.AreEqual(60f, OmegaMath.Members[OmegaMath.Stoat].Cruise); Assert.AreEqual(110f, OmegaMath.Members[OmegaMath.Stoat].Turn);
            Assert.AreEqual(OmegaMath.MoveKind.Cannon, OmegaMath.Members[OmegaMath.Grizzly].Kind);
            Assert.AreEqual(OmegaMath.WeaponKind.Guns, OmegaMath.CrewOf(OmegaMath.Crew.Tempest).Weapon);
        }

        [TestCase(OmegaMath.Crew.Warden, 72.833333f, 62f, 78f)]
        [TestCase(OmegaMath.Crew.Bulwark, 66.733333f, 47.5f, 55f)]
        [TestCase(OmegaMath.Crew.Tempest, 88.4f, 87.5f, 102.5f)]
        public void OmegaFlight_IsTheMembersAverageTimesTheCrewsMultipliers(OmegaMath.Crew crew, float cruise, float turn, float roll)
        {
            var f = OmegaMath.OmegaFlight(OmegaMath.CrewOf(crew));
            Assert.AreEqual(cruise, f.Cruise, 1e-3f, "cruise");
            Assert.AreEqual(turn, f.Turn, 1e-3f, "turn");
            Assert.AreEqual(roll, f.Roll, 1e-3f, "roll");
        }

        [Test]
        public void Slots_AreThePagesSlotOf_InTheGatherPointsFrame()
        {
            Near(Vector3.zero, OmegaMath.SlotOffset(0), 0f, "slot 0");
            Near(new Vector3(0f, 9f, -4f), OmegaMath.SlotOffset(1), 0f, "slot 1 (9 up, 4 back)");
            Near(new Vector3(0f, -8f, -6f), OmegaMath.SlotOffset(2), 0f, "slot 2 (8 down, 6 back)");
            // a leader at (10, 0, 0) facing +x: slot 1 is 9 up and 4 back along x
            var rot = Quaternion.LookRotation(Vector3.right, Vector3.up);
            Near(new Vector3(6f, 9f, 0f), OmegaMath.SlotWorld(new Vector3(10f, 0f, 0f), rot, 1), 1e-4f, "slot 1 in the world");
        }

        [Test]
        public void Meter_OverAScriptedMinute_MatchesThePage()
        {
            float meter = 0.5f;
            meter = OmegaMath.Regen(meter, P.Regen, 10f);
            Assert.AreEqual(0.7f, meter, 1e-5f, "10 s apart");
            meter = OmegaMath.OnCrystal(meter, P.MeterPerCrystal, false, P.CombinedCrystal);
            meter = OmegaMath.OnCrystal(meter, P.MeterPerCrystal, false, P.CombinedCrystal);
            Assert.AreEqual(1f, meter, 1e-5f, "two crystals apart cap the meter");
            meter = OmegaMath.Drain(meter, P.Drain, false, P.ShieldDrain, 15f);
            meter = OmegaMath.Drain(meter, P.Drain, true, P.ShieldDrain, 5f);
            Assert.AreEqual(0f, meter, 1e-5f, "20 s combined, 5 of them shielded, empties it");
            meter = OmegaMath.OnCrystal(meter, P.MeterPerCrystal, true, P.CombinedCrystal);
            Assert.AreEqual(0.11f, meter, 1e-5f, "a combined crystal pays half");
            meter = OmegaMath.Finisher(meter, P.FinisherCost);
            Assert.AreEqual(0f, meter, 1e-5f, "the finisher spends what there is");
            Assert.IsTrue(OmegaMath.CanCombine(0.3f, P.CombineAt)); Assert.IsFalse(OmegaMath.CanCombine(0.29f, P.CombineAt));
            Assert.IsTrue(OmegaMath.CanFinish(0.9f, P.FinisherAt)); Assert.IsFalse(OmegaMath.CanFinish(0.89f, P.FinisherAt));
        }

        [Test]
        public void ElementMults_AtZeroAreTheLabNumbers_AtTenAreThePagesRanges()
        {
            var zero = OmegaMath.ElementMults(P, OmegaMath.Levels.Zero);
            Assert.AreEqual(1f, zero.WeaponPower, 1e-6f); Assert.AreEqual(0.06f, zero.ShieldDrain, 1e-6f);
            Assert.AreEqual(180f, zero.FinisherCone, 1e-6f); Assert.AreEqual(1.6f, zero.CombineSeconds, 1e-6f);
            Assert.IsFalse(zero.L5Charge || zero.L5Mass || zero.L5Space || zero.L5Time);
            var full = OmegaMath.ElementMults(P, OmegaMath.Levels.Full);
            Assert.AreEqual(1.6f, full.WeaponPower, 1e-6f, "Charge: weapon power 1 to 1.6");
            Assert.AreEqual(0.024f, full.ShieldDrain, 1e-6f, "Mass: shield drain 1 to 0.4");
            Assert.AreEqual(270f, full.FinisherCone, 1e-4f, "Space: finisher cone 1 to 1.5");
            Assert.AreEqual(0.8f, full.CombineSeconds, 1e-6f, "Time: combine 1 to 0.5");
            Assert.IsTrue(full.L5Charge && full.L5Mass && full.L5Space && full.L5Time);
        }

        [TestCase(0f, 0f, 50f, true)]
        [TestCase(20f, 0f, 50f, true)]
        [TestCase(33f, 0f, 50f, false)]
        [TestCase(0f, 60f, 100f, false)]
        [TestCase(0f, 0f, 181f, false)]
        [TestCase(0f, 0f, -5f, false)]
        public void FinisherCone_IsThePagesTest_PerpWithinAlongTimes045Plus10(float x, float y, float z, bool inside)
        {
            Assert.AreEqual(inside, OmegaMath.InFinisherCone(new Vector3(x, y, z), 180f));
        }

        [Test]
        public void FinisherQueryHalfAngle_BracketsTheExactCone()
        {
            float reach = 180f;
            float half = OmegaMath.FinisherQueryHalfAngle(reach);
            // the widest point the exact test accepts, at the reach, sits inside the query cone
            float perpAtReach = reach * 0.45f + 10f;
            Assert.Greater(Mathf.Tan(half * Mathf.Deg2Rad) * reach, perpAtReach - 1e-3f);
        }

        [TestCase(0.25f, 0.15625f, 3.5625f)]
        [TestCase(0.5f, 0.5f, 7f)]
        [TestCase(0.75f, 0.84375f, 10.4375f)]
        public void DockEase_AndRate_AreThePages(float k, float ease, float rate)
        {
            Assert.AreEqual(ease, OmegaMath.DockEase(k), 1e-6f, "e = k*k*(3-2k)");
            Assert.AreEqual(rate, OmegaMath.DockRate(OmegaMath.DockEase(k)), 1e-6f, "2 + 10e");
            Assert.AreEqual(Mathf.Min(1f, rate / 60f), OmegaMath.DockStep(OmegaMath.DockEase(k), 1f / 60f), 1e-6f);
        }

        [Test]
        public void Split_PlacesTheMembersRoundTheOmega_AtNineTenthsOfItsSpeed()
        {
            Near(new Vector3(0f, 0f, 8f), OmegaMath.SplitOffset(0), 0f, "member 0 ahead");
            Near(new Vector3(16f, 0f, -6f), OmegaMath.SplitOffset(1), 0f, "member 1 right and back");
            Near(new Vector3(-16f, 0f, -6f), OmegaMath.SplitOffset(2), 0f, "member 2 left and back");
            Assert.AreEqual(0.9f, OmegaMath.SplitSpeedShare);
        }

        [Test]
        public void Aim_TurnsAtTheTurretRate_WithinTheCone_AndRightIsRight_UpIsUp()
        {
            float yaw = 0f, pitch = 0f;
            OmegaMath.AimStep(ref yaw, ref pitch, new Vector2(1f, 1f), 0.1f);
            Assert.AreEqual(0.22f, yaw, 1e-6f); Assert.AreEqual(0.22f, pitch, 1e-6f);
            OmegaMath.AimStep(ref yaw, ref pitch, new Vector2(1f, 1f), 10f);
            Assert.AreEqual(0.9f, yaw, 1e-6f, "yaw bounded"); Assert.AreEqual(0.7f, pitch, 1e-6f, "pitch bounded");
            var right = OmegaMath.AimDirection(Quaternion.identity, 0.5f, 0f);
            Assert.Greater(right.x, 0.4f, "yaw turns the aim right"); Assert.AreEqual(0f, right.y, 1e-6f);
            var up = OmegaMath.AimDirection(Quaternion.identity, 0f, 0.5f);
            Assert.Greater(up.y, 0.4f, "pitch lifts the aim"); Assert.AreEqual(0f, up.x, 1e-6f);
            Assert.AreEqual(1f, right.magnitude, 1e-5f);
        }

        [Test]
        public void Fly_EasesSpeedToItsTarget_AndGripsTheCourseToTheNose()
        {
            Vector3 pos = Vector3.zero, course = Vector3.right;
            float speed = 0f;
            OmegaMath.Fly(ref pos, ref course, ref speed, Vector3.forward, 60f, 1f);
            Assert.AreEqual(60f * (1f - Mathf.Exp(-1.5f)), speed, 1e-4f, "the page's rate 1.5");
            Assert.Greater(course.z, 0.99f, "grip 30/s for a second puts the course on the nose");
            Assert.Greater(pos.z, 40f, "it moved along the course");
        }

        [Test]
        public void Formation_IsRigid_AttackerRightDefenderLeft_WhicheverMemberTheHullIs()
        {
            Near(Vector3.zero, OmegaMath.FormationOffset(OmegaMath.Role.Mobility, 18f, 10f), 0f, "the mobility vessel leads");
            Near(new Vector3(18f, 0f, -10f), OmegaMath.FormationOffset(OmegaMath.Role.Attack, 18f, 10f), 0f, "the attacker right and behind");
            Near(new Vector3(-18f, 0f, -10f), OmegaMath.FormationOffset(OmegaMath.Role.Defence, 18f, 10f), 0f, "the defender left and behind");
            // the person sits in the attacker: the leader is 18 left and 10 ahead of the hull, the defender 36 left
            Near(new Vector3(-18f, 0f, 10f), OmegaMath.FormationOffsetFrom(1, 0, 18f, 10f), 0f, "leader from the attacker");
            Near(new Vector3(-36f, 0f, 0f), OmegaMath.FormationOffsetFrom(1, 2, 18f, 10f), 0f, "defender from the attacker");
            Near(Vector3.zero, OmegaMath.FormationOffsetFrom(2, 2, 18f, 10f), 0f, "a member from itself");
            // the distances between the three never change with the hull
            for (int hull = 0; hull < 3; hull++)
            {
                float ad = Vector3.Distance(OmegaMath.FormationOffsetFrom(hull, 1, 18f, 10f), OmegaMath.FormationOffsetFrom(hull, 2, 18f, 10f));
                Assert.AreEqual(36f, ad, 1e-5f, "attacker to defender, hull " + hull);
            }
        }
    }
}
