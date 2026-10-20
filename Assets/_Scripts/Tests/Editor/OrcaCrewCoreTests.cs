#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using V3 = System.Numerics.Vector3;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Orca's crew rules (the 4-seat vessel, Crew of Four Lab round 4; <c>Docs/Studios/CREW_OF_FOUR_LAB.md</c> §4): a
    /// stabilized turret holds its aim while the hull turns, its view's horizon eases back to the hull's up, a missile flies
    /// where its thumb turns it, the magazine takes and refills, and the shield leaves its forward cone open. The same core
    /// is held to the lab page step by step by <c>Tools/Build/orca_crew_lab/parity.cjs</c>; these tests pin the rules a
    /// reader can state in a sentence.
    /// </summary>
    public class OrcaCrewCoreTests
    {
        const float Dt = 1f / 60f;
        static float AngleDeg(V3 a, V3 b) => MathF.Atan2(V3.Cross(a, b).Length(), V3.Dot(a, b)) / OrcaCrewCore.Deg2Rad;

        [Test]
        public void Turret_HullTurnsUnderIt_AimHoldsStillInTheWorld()
        {
            var hull = new OrcaFrame(V3.UnitZ, V3.UnitY);
            var t = new OrcaTurret();
            t.Reset(hull, V3.UnitZ);
            for (int i = 0; i < 120; i++)
            {
                hull.Turn(80f * OrcaCrewCore.Deg2Rad * Dt, 30f * OrcaCrewCore.Deg2Rad * Dt, 60f * OrcaCrewCore.Deg2Rad * Dt);
                t.HoldHorizon(hull.U, Dt, 3f);
            }
            Assert.Less(AngleDeg(t.Aim, V3.UnitZ), 1e-3f, "a stabilized aim lives in the world; the hull's turns never reach it");
            Assert.Greater(AngleDeg(hull.F, V3.UnitZ), 60f, "negative control: the hull really did turn");
        }

        [Test]
        public void Turret_Horizon_HeldThenEasedToTheHullsUp()
        {
            var hull = new OrcaFrame(V3.UnitZ, V3.UnitY);
            var t = new OrcaTurret();
            t.Reset(hull, V3.UnitZ);
            hull.Turn(0f, 0f, 90f * OrcaCrewCore.Deg2Rad);   // the hull rolls a quarter turn at once
            float first = t.HoldHorizon(hull.U, Dt, 3f);
            Assert.Less(first, 1f, "one step later the view has barely moved: the horizon is held, not locked");
            for (int i = 0; i < 60 * 15; i++) t.HoldHorizon(hull.U, Dt, 3f);
            Assert.Less(AngleDeg(t.Up, hull.U), 1f, "after five time constants the view's up matches the hull's");
        }

        [Test]
        public void Turret_PersonsAim_TurnsAboutTheViewsOwnAxes()
        {
            var hull = new OrcaFrame(V3.UnitZ, V3.UnitY);
            var t = new OrcaTurret();
            t.Reset(hull, V3.UnitZ);
            t.TurnBy(90f * OrcaCrewCore.Deg2Rad, 0f);
            Assert.Greater(t.Aim.X, 0.999f, "positive az turns the aim to starboard (+x, Unity's right)");
            t.TurnBy(0f, 30f * OrcaCrewCore.Deg2Rad);
            Assert.AreEqual(MathF.Sin(30f * OrcaCrewCore.Deg2Rad), t.Aim.Y, 1e-4f, "positive el raises it");
        }

        [Test]
        public void Turret_AISlew_IsCappedAtTheTurretRate()
        {
            var t = new OrcaTurret();
            t.Reset(new OrcaFrame(V3.UnitZ, V3.UnitY), V3.UnitZ);
            t.SlewToward(V3.UnitX, 1f, 45f);
            Assert.AreEqual(45f, AngleDeg(t.Aim, V3.UnitZ), 1e-2f);
            t.SlewToward(V3.UnitX, 1f, 90f);
            Assert.AreEqual(0f, AngleDeg(t.Aim, V3.UnitX), 1e-2f, "within reach it lands on the target");
        }

        [Test]
        public void Turret_Release_PersonsIsHeldAcrossTheReload_AnAIsIsNot()
        {
            var t = new OrcaTurret { Cooldown = 0.3f };
            t.Release(human: true, Dt);
            bool fired = false;
            for (int i = 0; i < 30 && !fired; i++) fired = t.Step(Dt, canTake: true);
            Assert.IsTrue(fired, "a person's Release during the 0.3 s reload fires when it ends (held 0.5 s)");

            var ai = new OrcaTurret { Cooldown = 0.3f };
            ai.Release(human: false, Dt);
            fired = false;
            for (int i = 0; i < 30 && !fired; i++) fired = ai.Step(Dt, canTake: true);
            Assert.IsFalse(fired, "an AI's Release lasts one step");
        }

        [Test]
        public void Turret_Release_EmptyMagazineDoesNotFire()
        {
            var t = new OrcaTurret();
            t.Release(human: true, Dt);
            Assert.IsFalse(t.Step(Dt, canTake: false));
        }

        [Test]
        public void Missile_NoStick_FliesStraightAtItsSpeed()
        {
            var m = new OrcaMissile();
            m.Launch(V3.Zero, V3.UnitZ, V3.UnitY, V3.UnitX);
            for (int i = 0; i < 60; i++) m.Step(Dt, 180f, 150f);
            Assert.AreEqual(OrcaCrewCore.LaunchStandoff + 180f, m.Position.Z, 1e-2f);
            Assert.AreEqual(0f, m.Position.X, 1e-4f);
            Assert.IsFalse(m.FuseOut(5f));
            for (int i = 0; i < 60 * 4; i++) m.Step(Dt, 180f, 150f);
            Assert.IsTrue(m.FuseOut(5f), "it bursts five seconds after launch");
        }

        [Test]
        public void Missile_FullStick_TurnsAtItsTurnRate()
        {
            var m = new OrcaMissile();
            m.Launch(V3.Zero, V3.UnitZ, V3.UnitY, V3.UnitX);
            m.StickYaw = 1f;
            for (int i = 0; i < 60; i++) m.Step(Dt, 180f, 90f);
            Assert.AreEqual(90f, AngleDeg(m.Frame.F, V3.UnitZ), 0.1f);
            Assert.Greater(m.Frame.F.X, 0.99f, "a full right stick turns it to starboard");
        }

        [Test]
        public void Missile_SecondRelease_OnlyAfterItHasFlownAMoment()
        {
            var m = new OrcaMissile();
            m.Launch(V3.Zero, V3.UnitZ, V3.UnitY, V3.UnitX);
            m.Step(0.1f, 180f, 150f);
            Assert.IsFalse(m.CanBurstByHand, "the press that launched it must not also burst it");
            m.Step(0.2f, 180f, 150f);
            Assert.IsTrue(m.CanBurstByHand);
        }

        [Test]
        public void Magazine_TakesAboveTheReserve_OmniRefillsToItsSize()
        {
            var mag = new OrcaMagazine(start: 2, max: 12);
            Assert.IsTrue(mag.CanTake(reserve: 1));
            mag.Take();
            Assert.IsFalse(mag.CanTake(reserve: 1), "AI gunners leave one missile for each human gunner aboard");
            Assert.IsTrue(mag.CanTake(reserve: 0), "a person may take the last one");
            Assert.AreEqual(3, mag.AddOmni(3));
            mag.Count = 11;
            Assert.AreEqual(1, mag.AddOmni(3), "an omni crystal fills only up to the magazine's size");
            Assert.AreEqual(12, mag.Count);
        }

        [Test]
        public void Shield_LeavesTheForwardConeOpen()
        {
            var dirs = new List<V3>();
            OrcaCrewCore.ShieldDirections(144, 45f, dirs);
            Assert.AreEqual(144, dirs.Count, "the DangerHemisphereConfig's 12 x 12 blocks, all placed");
            float open = MathF.Cos(45f * OrcaCrewCore.Deg2Rad);
            foreach (var d in dirs) Assert.LessOrEqual(d.Z, open + 1e-5f, "no prism inside the open forward cone");
            int behind = 0; foreach (var d in dirs) if (d.Z < -0.7f) behind++;
            Assert.Greater(behind, 10, "negative control: the shell does cover the stern");
        }

        [Test]
        public void ElementMul_MatchesTheLab()
        {
            Assert.AreEqual(1f, OrcaCrewCore.ElementMul(0f, 0.8f), 1e-6f);
            Assert.AreEqual(1.8f, OrcaCrewCore.ElementMul(15f, 0.8f), 1e-6f, "an element's effect at level 15 is 1 + k");
        }

        [Test]
        public void Mounts_SitOutsideTheHull()
        {
            for (int s = 1; s <= 3; s++)
                Assert.Greater(OrcaCrewCore.Mounts[s].Length(), 1.5f, $"turret {s} sits on an outrigger, clear of the hull's radius");
            Assert.Less(OrcaCrewCore.Mounts[2].X, 0f, "gunner 2 to port");
            Assert.Greater(OrcaCrewCore.Mounts[3].X, 0f, "gunner 3 to starboard");
        }

        [Test]
        public void Steer_PointsTheNoseAtTheTarget()
        {
            var body = new OrcaFrame(V3.UnitZ, V3.UnitY);
            var (yaw, pitch, _) = OrcaCrewCore.SteerToward(body, V3.Normalize(new V3(1f, 0f, 1f)), 0f);
            Assert.AreEqual(1f, yaw, 1e-4f, "a target 45 degrees to starboard: full right stick");
            Assert.AreEqual(0f, pitch, 1e-4f);
        }
    }
}
#endif
