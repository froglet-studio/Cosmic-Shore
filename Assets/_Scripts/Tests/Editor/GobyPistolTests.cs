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

        static readonly PistolPlane.Hills Shipped = new(3.5f, 60f, 1.6f, 10f);

        static PistolPlane.Frame Frame(int dir, float slope = 22f, float amp = 1f)
            => PistolPlane.For(new PlaneSegment(A, B, Vector3.up), dir, slope,
                new PistolPlane.Hills(Shipped.RollAmplitude * amp, Shipped.RollWavelength, Shipped.FineAmplitude * amp, Shipped.FineWavelength));

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
                var f = PistolPlane.For(seg, dir, 22f, Shipped);
                for (float u = 0f; u <= seg.L; u += seg.L / 23f)
                {
                    Vector2 g = PistolPlane.GroundPoint(f, u);
                    Vector3 world = f.ToWorld(g.x, g.y), trail = seg.Ground(PistolPlane.LineS(f, u), Shipped);
                    Assert.That(Vector3.Distance(world, trail), Is.LessThan(0.01f));
                    Assert.That(PistolPlane.Ground(f, g.x), Is.EqualTo(g.y).Within(0.05f), "the ground's inverse finds the same point");
                }
            }
        }

        [Test]
        public void Ground_KeepsTheLabsWiggles([Values(140f, 380f, 600f)] float length)
        {
            // two scales of hill (Garrett, 2026-10-22: "make more wiggles along the path"): the humps (10 u) and the rolls
            // (60 u). Count the crests the ground has along a dart: the humps give one per 10 u right up to the perches,
            // where the kit's first single wave faded out over the dart
            int crests = 0;
            float prev = PistolPlane.HillSlope(0f, length, Shipped);
            for (float s = 0.5f; s <= length; s += 0.5f)
            {
                float m = PistolPlane.HillSlope(s, length, Shipped);
                if (prev > 0f && m <= 0f) crests++;
                prev = m;
            }
            Assert.That(crests, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(length / Shipped.FineWavelength) - 1), $"a {length} u dart keeps its humps");
            float hi = 0f;
            for (float s = 0f; s <= length; s += 1f) hi = Mathf.Max(hi, Mathf.Abs(PistolPlane.Hill(s, length, Shipped)));
            Assert.That(hi, Is.GreaterThan(Shipped.FineAmplitude + 0.5f * Shipped.RollAmplitude), "the rolls stand above the zigzag");
            Assert.That(PistolPlane.Hill(0f, length, Shipped), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(PistolPlane.Hill(length - 1e-3f, length, Shipped), Is.EqualTo(0f).Within(0.05f), "both perches are met");
            // the control: a flat ground has none
            Assert.That(PistolPlane.Hill(length * 0.37f, length, default), Is.EqualTo(0f));
        }

        [Test]
        public void Ground_NeverFolds_EvenOnAShortDart([Values(6f, 20f, 60f, 300f)] float length)
        {
            // a dart blocked a few u out fits one half-wave of each scale: the hills calm down so the ground still runs
            // left to right on the Pistol's screen, and the inverse finds every point
            var seg = new PlaneSegment(Vector3.zero, Vector3.forward * length, Vector3.up);
            var f = PistolPlane.For(seg, 1, 22f, Shipped);
            float steepest = 0f, lastX = -1f;
            for (float u = 0f; u <= length; u += length / 200f)
            {
                steepest = Mathf.Max(steepest, Mathf.Abs(PistolPlane.HillSlope(u, length, Shipped)));
                Vector2 g = PistolPlane.GroundPoint(f, u);
                Assert.That(g.x, Is.GreaterThan(lastX), "the ground never folds back");
                lastX = g.x;
                Assert.That(PistolPlane.Ground(f, g.x), Is.EqualTo(g.y).Within(0.05f));
            }
            Assert.That(steepest, Is.LessThanOrEqualTo(PistolPlane.MaxHillSlope + 1e-3f));
        }

        [Test]
        public void SideLens_LooksAheadOfTheCameraBeside()
        {
            // the camera sits beside the Pistol, 160 u off the plane; its window is 700 u across and 140 u ahead: the
            // frustum's near-plane bounds, scaled back to the plane, are exactly that window
            PistolPlane.OffAxis(new Vector2(140f, 30f), 160f, 700f, 700f, 0.5f, out float l, out float r, out float b, out float t);
            float k = 160f / 0.5f;
            Assert.That(l * k, Is.EqualTo(140f - 350f).Within(1e-3f));
            Assert.That(r * k, Is.EqualTo(140f + 350f).Within(1e-3f));
            Assert.That(b * k, Is.EqualTo(30f - 350f).Within(1e-3f));
            Assert.That(t * k, Is.EqualTo(30f + 350f).Within(1e-3f));
            // the control: a centred window is symmetric
            PistolPlane.OffAxis(Vector2.zero, 160f, 700f, 400f, 0.5f, out l, out r, out b, out t);
            Assert.That(l, Is.EqualTo(-r).Within(1e-6f)); Assert.That(b, Is.EqualTo(-t).Within(1e-6f));
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

        /// <summary>
        /// Garrett, 2026-10-22: "it should have a 15 to 30 second journey from perch to perch ... launching should only get
        /// big enough to clear a few humps that are a small fraction of the journey". The SHIPPED config, ridden from a
        /// standstill by the Tiny Wings rider the Pistol AI is (dive on the downslopes, let go on the upslopes), over the
        /// shortest and the longest dart; the control: the same rider at the kit's first numbers took 4 to 9 s.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Ride_IsAFifteenToThirtySecondJourney_WithShortFlights(bool shipped)
        {
            var config = AssetDatabase.LoadAssetAtPath<CosmicShore.ScriptableObjects.GobyPistolConfigSO>("Assets/_SO_Assets/VesselActions/GobyPistol/GobyPistolConfig.asset");
            Assert.That(config, Is.Not.Null);
            var s = config.Body;
            var hills = config.Hills;
            if (!shipped)
            {
                s.pGravity = 95f; s.pDiveMul = 2.6f; s.pMinSpeed = 22f; s.pDrag = 0.35f;
                hills = new PistolPlane.Hills(18f, 290f, 10f, 64f);
            }
            foreach (float length in new[] { config.DartMinRange, config.DartRange })
            {
                var f = PistolPlane.For(new PlaneSegment(Vector3.zero, Vector3.forward * length, Vector3.up), 1, config.SlopeDegrees, hills);
                var p = new PistolBodyMath.State();
                PistolBodyMath.PlaceAtStart(p, f, s.pMinSpeed);
                float t = 0f, dt = 1f / 60f, takeoff = 0f, longest = 0f; bool wasGrounded = true;
                while (p.x < f.EndX - 1f && t < 120f)
                {
                    PistolPlane.Ground(f, p.x, out float slope);
                    PistolPlane.Ground(f, p.x + 2f, out float ahead);
                    float dive = p.grounded ? (slope < -0.02f ? 1f : 0f) : (p.vy < 0f && ahead < -0.02f ? 1f : 0f);
                    PistolBodyMath.Step(p, f, t, dt, new PistolBodyMath.Input { Dive = dive }, s);
                    if (wasGrounded && !p.grounded) takeoff = p.x;
                    if (!wasGrounded && p.grounded) longest = Mathf.Max(longest, p.x - takeoff);
                    wasGrounded = p.grounded; t += dt;
                }
                if (!shipped) { Assert.That(t, Is.LessThan(12f), $"the control: the kit's first ride crossed {length} u in {t:F1} s"); continue; }
                Assert.That(t, Is.InRange(12f, 32f), $"a {length} u dart is a {t:F1} s journey");
                Assert.That(longest, Is.LessThan(0.15f * length), $"the longest flight, {longest:F0} u, is a small share of {length} u");
                Assert.That(longest, Is.LessThan(6f * hills.FineWavelength), "a flight clears a few humps, not a stretch");
            }
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
