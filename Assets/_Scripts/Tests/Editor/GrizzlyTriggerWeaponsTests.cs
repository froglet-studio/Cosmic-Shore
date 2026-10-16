using System.IO;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Grizzly's triggers follow the active weapon (design ask, 2026-10-16): bombs in
    /// Explosives, a left-front and right-front plasma claw sized by pressure, and the sniper's
    /// scope on LT and fire on RT. These hold the shipped wiring and the claw's pressure curve.
    /// </summary>
    [TestFixture]
    public class GrizzlyTriggerWeaponsTests
    {
        const string Prefab = "Assets/_Prefabs/Spacevessels/Grizzly.prefab";
        const string Assets = "Assets/_SO_Assets/VesselActions/Grizzly/";
        const string ClawGuid = "a6a49ba11f7b4b04a075230bda3d3306";
        const string SniperGuid = "52f08890cabc4e48891138d622f0ba57";
        const string CycleWeaponGuid = "33158a486e2e4b37b7b85ccf5c6dea72";

        GrizzlyFlamethrowerActionSO _claw;
        GrizzlySniperShotActionSO _sniper;

        [SetUp]
        public void SetUp()
        {
            _claw = ScriptableObject.CreateInstance<GrizzlyFlamethrowerActionSO>();
            _sniper = ScriptableObject.CreateInstance<GrizzlySniperShotActionSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_claw);
            Object.DestroyImmediate(_sniper);
        }

        [Test]
        public void ClawConeGrowsWithPressureAndFullIsTheAuthoredCone()
        {
            Assert.AreEqual(_claw.Range, _claw.RangeForPressure(1f), 1e-4f);
            Assert.AreEqual(_claw.ConeHalfAngle, _claw.HalfAngleForPressure(1f), 1e-4f);
            Assert.Less(_claw.RangeForPressure(0f), _claw.RangeForPressure(1f), "a feathered pull must be a smaller cone");
            Assert.Less(_claw.HalfAngleForPressure(0f), _claw.HalfAngleForPressure(1f));
            float prev = -1f;
            for (float p = 0f; p <= 1.0001f; p += 0.05f)
            {
                float r = _claw.RangeForPressure(p);
                Assert.GreaterOrEqual(r, prev);
                prev = r;
            }
        }

        [Test]
        public void ClawCellEmptiesHalfAgainFasterForTheLargestFlame()
        {
            // Design ask 2026-10-16: each cone's energy cell empties 1.5x faster for larger flames.
            Assert.AreEqual(1.5f, _claw.CellDrainPerSecond(1f) / _claw.CellDrainPerSecond(0f), 1e-4f);
            Assert.Greater(_claw.CellDrainPerSecond(0.5f), _claw.CellDrainPerSecond(0f));
            Assert.Less(_claw.CellDrainPerSecond(0.5f), _claw.CellDrainPerSecond(1f));
            Assert.Greater(_claw.CellRechargePerSecond, 0f, "an idle cell must refill");
        }

        [Test]
        public void ClawConesSpreadApart()
        {
            Assert.Greater(_claw.SplayDegrees, 0f, "the two cones turn outward, LT left and RT right");
            Assert.Less(_claw.SplayDegrees, _claw.ConeHalfAngle, "splayed past its half-angle a cone no longer reads as forward");
        }

        [Test]
        public void ClawIsFourRigidRedWhipsPerSide()
        {
            // Design ask 2026-10-16: the Brittle Star's whips, "more rigid and red with four on each side".
            Assert.AreEqual(4, _claw.WhipsPerSide);
            Assert.Greater(_claw.WhipRigidity, 0.5f, "the claw's whips are stiffer than the Brittle Star's loose whip");
            var rim = _claw.WhipRimColor;
            Assert.Greater(rim.r, 2f * Mathf.Max(rim.g, rim.b), "the whips glow RED");

            // At that rigidity a whip held on a still line stays on it: a claw finger, not a rope.
            var cord = new BrittleStarCord(_claw.WhipLinks);
            var settings = new BrittleStarCord.Settings
            {
                LooseWave = _claw.WhipLooseWave, RigidWave = _claw.WhipRigidWave,
                LooseDamping = _claw.WhipLooseDamping, RigidDamping = _claw.WhipRigidDamping, Pose = _claw.WhipPose,
            };
            cord.Step(Vector3.zero, Vector3.right, _claw.Range, _claw.WhipRigidity, settings, 1f / 60f);   // seeds straight
            for (int f = 0; f < 60; f++)
                cord.Step(Vector3.zero, Vector3.forward, _claw.Range, _claw.WhipRigidity, settings, 1f / 60f);
            Assert.Less((cord.Tip - Vector3.forward * _claw.Range).magnitude, _claw.Range * 0.1f,
                "a rigid claw whip must settle onto its aim within a second of a 90 degree turn");
        }

        [Test]
        public void ClawIsAlwaysTheShortestReach()
        {
            float sniperReach = _sniper.ProjectileSpeed * _sniper.ProjectileTime;
            Assert.Less(_claw.Range, sniperReach, "the claw must out-range nothing: it is the close-range weapon");
            Assert.Greater(_claw.SideOffset, 0f, "LT and RT must spray from their own sides of the front");
        }

        [Test]
        public void BothTriggersCarryTheClawAndTheSniper()
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                var yaml = File.ReadAllText(Assets + "GrizzlyBomb" + side + "Action.asset");
                StringAssert.Contains("claw: {fileID: 11400000, guid: " + ClawGuid, yaml, side + " trigger has no claw");
                StringAssert.Contains("sniper: {fileID: 11400000, guid: " + SniperGuid, yaml, side + " trigger has no sniper");
            }
        }

        [Test]
        public void XFiresNothingAndLeftBumperCyclesWeapons()
        {
            var yaml = File.ReadAllText(Prefab);
            StringAssert.DoesNotContain("- InputEvent: 8\n", yaml.Replace("\r\n", "\n"),
                "X / Q must not fire on the Grizzly: its weapons are on the triggers");
            StringAssert.Contains("_gamepadActionOverrides:\n  - InputEvent: 4\n    ShipActions:\n    - {fileID: 11400000, guid: " + CycleWeaponGuid,
                yaml.Replace("\r\n", "\n"), "LB (IdleAction on the pad) must cycle the Grizzly's weapon");
        }
    }
}
