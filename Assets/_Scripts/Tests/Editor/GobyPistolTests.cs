#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Goby &amp; Pistol's own kit (Garrett, 2026-10-22; .claude/skills/duo-vessel/references/designs/goby-and-pistol.md
    /// §11): the Pistol's ground from perch to perch is downhill whichever way it rides and meets both perches; the dens
    /// never clip and only the Mass upgrade brings danger and super-shielded prisms; the den grows with the distance
    /// travelled; the Tiny Wings ride gathers speed downhill; a homing ring turns onto its target; and the Goby's ability
    /// map is its own four cards, not the Squirrel's.
    /// </summary>
    public class GobyPistolTests
    {
        static readonly Vector3 A = new(10f, -20f, 30f), B = new(310f, 60f, -170f);

        static PistolPlane.Frame Frame(int dir, float slope = 22f, float amp = 10f, float wave = 70f)
            => PistolPlane.For(new PlaneSegment(A, B, Vector3.up), dir, slope, amp, wave);

        [Test]
        public void Ground_MeetsBothPerches([Values(1, -1)] int dir)
        {
            var f = Frame(dir);
            Vector3 start = f.ToWorld(0f, PistolPlane.Ground(f, 0f)), end = f.ToWorld(f.EndX, PistolPlane.Ground(f, f.EndX));
            Assert.That(Vector3.Distance(start, dir > 0 ? A : B), Is.LessThan(0.01f), "the ground starts at the perch the Pistol leaves");
            Assert.That(Vector3.Distance(end, dir > 0 ? B : A), Is.LessThan(0.01f), "the ground ends at the perch it rides to");
        }

        [Test]
        public void Ground_IsDownhill_EitherWay([Values(1, -1)] int dir)
        {
            var f = Frame(dir);
            Assert.That(PistolPlane.Ground(f, f.EndX), Is.LessThan(PistolPlane.Ground(f, 0f) - f.L * 0.3f),
                "the far perch sits well below the near one on the Pistol's screen");
            // a flat stretch (no hills) is downhill at every point; the control: no slope, no fall
            var flat = Frame(dir, amp: 0f);
            for (float x = 1f; x < flat.EndX; x += 7f) { PistolPlane.Ground(flat, x, out float m); Assert.That(m, Is.LessThan(0f)); }
            var level = Frame(dir, slope: 0f, amp: 0f);
            Assert.That(PistolPlane.Ground(level, level.EndX), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Ground_IsTheDartsTrail()
        {
            // every point the frame says is ground lies on the 3D trail the dart laid (PlaneSegment.Ground)
            var seg = new PlaneSegment(A, B, Vector3.up);
            foreach (int dir in new[] { 1, -1 })
            {
                var f = PistolPlane.For(seg, dir, 22f, 10f, 70f);
                for (float u = 0f; u <= seg.L; u += seg.L / 23f)
                {
                    Vector2 g = PistolPlane.GroundPoint(f, u);
                    Vector3 world = f.ToWorld(g.x, g.y), trail = seg.Ground(PistolPlane.LineS(f, u), 10f, 70f);
                    Assert.That(Vector3.Distance(world, trail), Is.LessThan(0.01f));
                    Assert.That(PistolPlane.Ground(f, g.x), Is.EqualTo(g.y).Within(0.05f), "the ground's inverse finds the same point");
                }
            }
        }

        [Test]
        public void Frame_IsOrthonormal_AndHoldsTheDart([Values(1, -1)] int dir)
        {
            var f = Frame(dir);
            Assert.That(Vector3.Dot(f.R, f.Up), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(f.M.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Mathf.Abs(Vector3.Dot(B - A, f.M)), Is.LessThan(1e-3f), "the dart's line lies in the plane: the plane is flat and holds it");
        }

        [Test]
        public void Ride_GathersSpeedDownhill()
        {
            var f = Frame(1, amp: 0f);
            var s = new PistolBodyMath.Settings { pGravity = 95f, pDiveMul = 2.6f, pMinSpeed = 22f, pDrag = 0.35f, pGoodLand = 28f, pLaunchCharges = 1f, pLaunchCd = 9f, pLaunchRange = 220f, pLaunchSpeed = 160f, pGhostCd = 8f, pGhostTime = 2.5f, pVortexCd = 4f };
            var p = new PistolBodyMath.State { launch = 1 };
            PistolBodyMath.PlaceAtStart(p, f, s.pMinSpeed);
            float travelled = 0f;
            for (int i = 0; i < 120; i++) travelled += PistolBodyMath.Step(p, f, i / 60f, 1f / 60f, new PistolBodyMath.Input { Dive = 1f }, s).Travelled;
            Assert.That(p.v, Is.GreaterThan(s.pMinSpeed + 20f), "two seconds of diving downhill builds speed");
            Assert.That(travelled, Is.GreaterThan(30f));
        }

        [Test]
        public void Den_NoTwoPrismsClip()
        {
            float spacing = GobyDen.Spacing(4f, 1.1f);
            var pts = new List<Vector3>();
            for (int i = 0; i < 200; i++) pts.Add(GobyDen.Local(i, 10f, spacing));
            float least = float.MaxValue;
            for (int i = 0; i < pts.Count; i++)
                for (int j = i + 1; j < pts.Count; j++) least = Mathf.Min(least, Vector3.Distance(pts[i], pts[j]));
            Assert.That(least, Is.GreaterThanOrEqualTo(spacing * 0.999f));
            Assert.That(pts[0], Is.EqualTo(GobyDen.Local(0, 10f, spacing)), "a placed prism never moves when more are added");
        }

        [Test]
        public void Den_KindsFollowTheMassUpgrade()
        {
            int danger = 0, super = 0, shielded = 0;
            for (int i = 0; i < 100; i++)
            {
                var k = GobyDen.KindFor(i, false, 0.4f, 0.15f, 0.12f);
                Assert.That(k, Is.Not.EqualTo(PrismKind.Danger).And.Not.EqualTo(PrismKind.SuperShielded), "below the Mass upgrade a den is plain and shielded");
                if (k == PrismKind.Shielded) shielded++;
                var u = GobyDen.KindFor(i, true, 0.4f, 0.15f, 0.12f);
                if (u == PrismKind.Danger) danger++;
                if (u == PrismKind.SuperShielded) super++;
            }
            Assert.That(shielded, Is.InRange(30, 50));
            Assert.That(danger, Is.InRange(8, 22), "with the Mass upgrade a den grows danger prisms");
            Assert.That(super, Is.InRange(6, 18), "and super-shielded prisms");
        }

        [Test]
        public void Den_GrowsWithDistanceAndMass()
        {
            Assert.That(GobyDen.PrismsFor(400f, 40f, 1f, 0, 80), Is.EqualTo(10));
            Assert.That(GobyDen.PrismsFor(400f, 40f, 2.5f, 0, 80), Is.EqualTo(25), "Mass scales a rest's prisms");
            Assert.That(GobyDen.PrismsFor(400f, 40f, 2.5f, 70, 80), Is.EqualTo(10), "never past the den's cap");
            Assert.That(GobyDen.PrismsFor(0f, 40f, 2.5f, 0, 80), Is.EqualTo(0), "no travel, no prisms");
        }

        [Test]
        public void Ring_TurnsOntoItsTarget()
        {
            var ring = new VortexRing(Vector3.zero, Vector3.forward, 2f);
            Vector3 target = new(200f, 0f, 0f);
            for (int i = 0; i < 60; i++) { ring.Steer(target, 240f * Mathf.Deg2Rad / 60f); ring.Advance(1f / 60f, new VortexRing.Settings { pVortexSpeed = 120f }); }
            Assert.That(Vector3.Angle(ring.N, target - ring.C), Is.LessThan(5f), "a second of homing points the ring at its target");
            var straight = new VortexRing(Vector3.zero, Vector3.forward, 2f);
            straight.Advance(1f, new VortexRing.Settings { pVortexSpeed = 120f });
            Assert.That(Vector3.Angle(straight.N, Vector3.forward), Is.LessThan(0.01f), "the control: an unsteered ring flies straight");
        }

        [Test]
        public void AbilityMap_IsTheGobysOwn()
        {
            var map = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/ElementalAbilityMaps/Goby.asset");
            Assert.That(map, Is.Not.Null);
            var so = new SerializedObject(map);
            var entries = so.FindProperty("entries");
            var labels = new List<string>();
            for (int i = 0; i < entries.arraySize; i++) labels.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("AbilityLabel").stringValue);
            CollectionAssert.AreEquivalent(new[] { "Vortex Sting", "Dens", "Undertow", "Quick Jump" }, labels);
            CollectionAssert.DoesNotContain(labels, "Boost Ring", "the Squirrel's kit is gone");
        }
    }
}
#endif
