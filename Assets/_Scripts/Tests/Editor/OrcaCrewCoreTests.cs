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
    /// where its thumb turns it, each seat's magazine takes and refills, the fast gun fires at its rate and overheats, its
    /// pen lays clean strokes (round 6), and the shield leaves its forward cone open. The same core
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
        public void Magazine_PerSeat_TakesToEmpty_OmniAddsOneUpToItsSize()
        {
            var mag = new OrcaMagazine(start: 2, max: 4);   // round 5: each gunner seat's own ("not a shared pool")
            mag.Take(); mag.Take();
            Assert.IsFalse(mag.CanTake, "an empty seat cannot launch");
            Assert.AreEqual(1, mag.AddOmni(1), "an omni crystal gives the seat one missile");
            Assert.IsTrue(mag.CanTake);
            mag.Count = 4;
            Assert.AreEqual(0, mag.AddOmni(1), "a full seat takes none");
        }

        [Test]
        public void FastGun_FiresAtItsRate_WhileHeld()
        {
            var gun = new OrcaFastGun();
            int rounds = 0;
            for (int i = 0; i < 60; i++) rounds += gun.Step(hold: true, Dt, rate: 30f, heatSeconds: 6f, coolSeconds: 5f);
            Assert.AreEqual(30, rounds, 1, "30 rounds a second");
            Assert.IsFalse(gun.Hot);
        }

        [Test]
        public void FastGun_Overheats_ThenLocksOutUntilCool()
        {
            var gun = new OrcaFastGun();
            int i = 0;
            for (; i < 60 * 10 && !gun.Hot; i++) gun.Step(true, Dt, 30f, 6f, 5f);
            Assert.AreEqual(6f, i * Dt, 0.05f, "it overheats after six seconds of fire");
            int locked = 0, steps = 0;
            for (; gun.Hot && steps < 60 * 10; steps++) locked += gun.Step(true, Dt, 30f, 6f, 5f);
            Assert.AreEqual(0, locked, "no rounds while overheated, even held");
            Assert.AreEqual(5f, steps * Dt, 0.05f, "it cools from hot in five seconds");
            Assert.Greater(gun.Step(true, 0.1f, 30f, 6f, 5f), 0, "a press after the lockout fires at once");
        }

        [Test]
        public void FastGun_Released_CoolsAndTheNextPressFiresAtOnce()
        {
            var gun = new OrcaFastGun();
            for (int i = 0; i < 60; i++) gun.Step(true, Dt, 30f, 6f, 5f);
            float warm = gun.Heat;
            gun.Step(false, 1f, 30f, 6f, 5f);
            Assert.Less(gun.Heat, warm, "released, it cools");
            Assert.AreEqual(1, gun.Step(true, Dt, 30f, 6f, 5f), "the first frame of a press fires");
        }

        // a pen landing every 0.1 s at x = 0, step, 2 step, ... along +x at 120 u out
        static List<OrcaPenMark> Sweep(OrcaPen pen, int landings, float step, float spacing = 5f, float start = 0f, float t0 = 0f)
        {
            var laid = new List<OrcaPenMark>();
            var marks = new List<OrcaPenMark>();
            var up = V3.UnitY;
            for (int i = 0; i < landings; i++)
            {
                pen.Lay(new V3(start + i * step, 0f, 120f), V3.UnitZ, V3.Normalize(V3.UnitX + up), t0 + i * 0.1f, spacing, 0.35f, 2, marks);
                laid.AddRange(marks);
            }
            return laid;
        }

        [Test]
        public void Pen_Sweep_LaysOneUnbrokenStroke_ExactlySpacedAlongIt()
        {
            var laid = Sweep(new OrcaPen(), 20, 3f);   // 3 u per landing: slower than the 5 u spacing
            Assert.Greater(laid.Count, 8, "a 57 u sweep lays a stroke");
            for (int k = 1; k < laid.Count; k++)
            {
                Assert.AreEqual(5f, V3.Distance(laid[k - 1].Position, laid[k].Position), 1e-3f, "every prism one spacing from the last");
                Assert.Greater(laid[k].Along.X, 0.999f, "laid along the stroke, not along the aim");
            }
            Assert.Greater(laid[0].Along.X, 0.999f, "the first prism waited for the second landing and lies along the stroke too");
        }

        [Test]
        public void Pen_StillAim_NeverBlots()
        {
            var laid = Sweep(new OrcaPen(), 30, 0f);
            Assert.AreEqual(0, laid.Count, "a pen held still lays nothing while the first landing waits for a direction");
            var pen = new OrcaPen();
            Sweep(pen, 30, 0f);
            Assert.IsTrue(pen.Flush(3f + 1f, 0.35f, out var dot), "a pause lays the waiting landing as one dot");
            Assert.AreEqual(1f, V3.Dot(dot.Along, V3.Normalize(V3.UnitX + V3.UnitY)), 1e-4f, "a dot lies across the view at the nib's 45 degrees");
            Assert.IsFalse(pen.Flush(5f, 0.35f, out _), "and only once");
        }

        [Test]
        public void Pen_LongJump_LiftsThePen_QuickStrokeFillsUpToItsFill()
        {
            var pen = new OrcaPen();
            var laid = Sweep(pen, 3, 12f);   // 12 u per landing: two prisms fill each gap (fill 2), the stroke stays unbroken
            Assert.AreEqual(5, laid.Count, "the first landing plus two per step");
            int strokes = pen.Strokes;
            Sweep(pen, 2, 0f, start: 100f, t0: 0.3f);   // 76 u away: more than (fill + 1) spacings
            Assert.AreEqual(strokes + 1, pen.Strokes, "a long jump starts a new stroke");
        }

        [Test]
        public void Pen_Landing_ReachesThePaperOnTheRoundsEasedPath()
        {
            var (d, t) = OrcaPen.Landing(135f, 4.5f, 120f);
            Assert.AreEqual(120f, d, 1e-4f);
            float covered = 135f * 2f * 4.5f / MathF.PI * MathF.Sin(MathF.PI * t / (2f * 4.5f));
            Assert.AreEqual(120f, covered, 1e-2f, "v x (2T / pi) x sin(pi t / 2T) is 120 u at that time");
            var (dFar, tFar) = OrcaPen.Landing(10f, 4.5f, 120f);
            Assert.AreEqual(10f * 2f * 4.5f / MathF.PI, dFar, 1e-3f, "a round that never reaches the paper lays at its end");
            Assert.AreEqual(4.5f, tFar, 1e-6f);
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
