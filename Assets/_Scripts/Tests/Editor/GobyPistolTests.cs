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

        static readonly PistolPlane.Hills Shipped = new(3.5f, 26f, 0.5f, 156f);

        static PistolPlane.Frame Frame(int dir, float slope = 22f, float amp = 1f)
            => PistolPlane.For(new PlaneSegment(A, B, Vector3.up), dir, slope,
                new PistolPlane.Hills(Shipped.Height * amp, Shipped.Wavelength, Shipped.Swell, Shipped.SwellWavelength));

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
        public void Ground_IsATrainOfTinyWingsHills([Values(140f, 300f, 600f)] float length)
        {
            // Tiny Wings' hills (Garrett, 2026-10-22): one train of smooth hills, a crest every 26 u right up to the perches,
            // whose heights swell and fall (runs of tall hills and of short ones, like the game's islands)
            var crests = new List<float>();
            for (float s = 0.05f; s < length - 0.05f; s += 0.05f)
            {
                float a = PistolPlane.Hill(s - 0.05f, length, Shipped), b = PistolPlane.Hill(s, length, Shipped), c = PistolPlane.Hill(s + 0.05f, length, Shipped);
                if (b > a && b >= c && b > 0f) crests.Add(b);
            }
            Assert.That(crests.Count, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(length / Shipped.Wavelength) - 1), $"a {length} u dart is a train of hills");
            float tallest = 0f, shortest = float.MaxValue;
            foreach (float h in crests) { tallest = Mathf.Max(tallest, h); shortest = Mathf.Min(shortest, h); }
            Assert.That(tallest / shortest, Is.GreaterThan(1.5f), "the heights swell and fall");
            Assert.That(tallest, Is.LessThanOrEqualTo(Shipped.Height + 1e-3f));
            Assert.That(PistolPlane.Hill(0f, length, Shipped), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(PistolPlane.Hill(length - 1e-3f, length, Shipped), Is.EqualTo(0f).Within(0.05f), "both perches are met");
            // the control: no swell, every hill alike
            var even = new PistolPlane.Hills(Shipped.Height, Shipped.Wavelength, 0f, Shipped.SwellWavelength);
            Assert.That(PistolPlane.Hill(length * 0.37f, length, default), Is.EqualTo(0f));
            float evenTall = 0f, evenShort = float.MaxValue;
            for (float s = 0.05f; s < length - 0.05f; s += 0.05f)
            {
                float a = PistolPlane.Hill(s - 0.05f, length, even), b = PistolPlane.Hill(s, length, even), c = PistolPlane.Hill(s + 0.05f, length, even);
                if (b > a && b >= c && b > 0f) { evenTall = Mathf.Max(evenTall, b); evenShort = Mathf.Min(evenShort, b); }
            }
            Assert.That(evenTall / evenShort, Is.LessThan(1.05f));
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

        static CosmicShore.ScriptableObjects.GobyPistolConfigSO ShippedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<CosmicShore.ScriptableObjects.GobyPistolConfigSO>("Assets/_SO_Assets/VesselActions/GobyPistol/GobyPistolConfig.asset");
            Assert.That(config, Is.Not.Null);
            return config;
        }

        /// <summary>What one rider did over one stretch.</summary>
        struct Run { public float Seconds, Longest; public int Flights, Smooth; }

        /// <summary>Ride the SHIPPED config over a <paramref name="length"/> u dart from a standstill at
        /// <paramref name="fps"/>, the rider choosing the dive each frame (null: the Tiny Wings rider the Pistol AI is).</summary>
        static Run RideIt(CosmicShore.ScriptableObjects.GobyPistolConfigSO config, float length, float fps, System.Func<PistolBodyMath.State, float> rider = null)
        {
            var s = config.Body;
            var f = PistolPlane.For(new PlaneSegment(Vector3.zero, Vector3.forward * length, Vector3.up), 1, config.SlopeDegrees, config.Hills);
            var p = new PistolBodyMath.State();
            PistolBodyMath.PlaceAtStart(p, f, s.pMinSpeed);
            var run = new Run();
            float dt = 1f / fps, takeoff = 0f; bool wasGrounded = true;
            while (p.x < f.EndX - 1f && run.Seconds < 120f)
            {
                float dive = rider != null ? rider(p) : PistolAI.TinyWingsDive(p, f, s.pGravity, s.pDiveMul);
                var ev = PistolBodyMath.Step(p, f, run.Seconds, dt, new PistolBodyMath.Input { Dive = dive }, s);
                if (wasGrounded && !p.grounded) takeoff = p.x;
                if (ev.Landed) { run.Flights++; run.Longest = Mathf.Max(run.Longest, p.x - takeoff); if (ev.SmoothLanding) run.Smooth++; }
                wasGrounded = p.grounded; run.Seconds += dt;
            }
            return run;
        }

        /// <summary>
        /// Tiny Wings' loop on the SHIPPED config (Garrett, 2026-10-22: "the reference for the pistol is the game tiny wings";
        /// earlier the same day: "a 15 to 30 second journey ... launching should only get big enough to clear a few humps
        /// that are a small fraction of the journey"). The rider who dives the slopes and lets go up them flies off the
        /// crests again and again, lands most of them as smooth slides, and crosses the shortest and the longest dart in a
        /// journey; the controls, a rider who never dives and one who never lets go, are far slower and slide far less.
        /// </summary>
        [Test]
        public void Ride_IsTinyWings_DiveLetGoFlySlide()
        {
            var config = ShippedConfig();
            foreach (float length in new[] { config.DartMinRange, config.DartRange })
            {
                var play = RideIt(config, length, 60f);
                float hills = length / config.Hills.Wavelength;
                Assert.That(play.Seconds, Is.InRange(10f, 30f), $"a {length} u dart is a {play.Seconds:F1} s journey");
                Assert.That(play.Flights, Is.GreaterThanOrEqualTo(hills / 3f), "a crest throws it at least every three hills");
                Assert.That(play.Smooth, Is.GreaterThanOrEqualTo(Mathf.CeilToInt(0.6f * play.Flights)), "most landings are smooth slides on the back of a hill");
                Assert.That(play.Longest, Is.LessThan(3f * config.Hills.Wavelength), "a flight clears a few hills, each a small share of the journey");

                var never = RideIt(config, length, 60f, _ => 0f);
                Assert.That(never.Seconds, Is.GreaterThan(1.8f * play.Seconds), "never diving: the speed is never built");
                Assert.That(never.Smooth, Is.LessThanOrEqualTo(play.Smooth / 2), "and few smooth slides");
                var always = RideIt(config, length, 60f, _ => 1f);
                Assert.That(always.Seconds, Is.GreaterThan(1.2f * play.Seconds), "never letting go: no flights to slide out of");
                Assert.That(always.Smooth, Is.LessThanOrEqualTo(play.Smooth / 2));
            }
        }

        /// <summary>
        /// Tiny Wings' proportions on the SHIPPED config (Garrett, 2026-10-23: "it should feel much closer to tiny wings in
        /// relative scale of piston and hill height, apparent speed, length of level compared to perch distance"), in the
        /// Pistol's own length as Tiny Wings' are in the bird's (estimates from the game, the brief's 2026-10-23 entry): a
        /// hill about 15 lengths crest to crest and 3-5 tall, a resting screen a little over one hill wide, and a stretch
        /// from perch to perch an island of 10-25 hills crossed at about a hill a second. The control: the numbers before
        /// this pass (18 u hills under an 80 u screen) fail the screen and the hill-height checks.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Proportions_AreTinyWings(bool shipped)
        {
            var config = ShippedConfig();
            float length = 2f * config.BodyR, wave = config.Hills.Wavelength, tall = 2f * config.Hills.Height, view = config.ViewWidth;
            if (!shipped) { length = 1f; wave = 18f; tall = 3.6f; view = 80f; }
            bool ok = wave / length >= 12f && wave / length <= 18f
                      && tall / length >= 3f && tall / length <= 5f
                      && view / wave >= 0.9f && view / wave <= 1.6f;
            if (!shipped) { Assert.That(ok, Is.False, "the control: the earlier proportions are not Tiny Wings'"); return; }
            Assert.That(wave / length, Is.InRange(12f, 18f), "a hill is about fifteen Pistol lengths crest to crest");
            Assert.That(tall / length, Is.InRange(3f, 5f), "and three to five of them tall");
            Assert.That(view / wave, Is.InRange(0.9f, 1.6f), "the resting screen shows a little over one hill");
            Assert.That(length / view, Is.InRange(1f / 25f, 1f / 12f), "the Pistol is about a seventeenth of the screen");
            foreach (float dart in new[] { config.DartMinRange, config.DartRange })
            {
                float hills = dart / wave;
                Assert.That(hills, Is.InRange(10f, 25f), $"a {dart} u stretch is an island of {hills:F0} hills");
                float rate = hills / RideIt(config, dart, 60f).Seconds;
                Assert.That(rate, Is.InRange(0.6f, 1.6f), $"crossed at {rate:F2} hills a second");
            }
        }

        [Test]
        public void Ride_IsTheSame_AtThirtyAndSixtyFps()
        {
            // the ride steps at 1/120 s whatever the frame (the liftoff and the landing used to depend on it: at 120 steps a
            // second the old rule never left the ground at all)
            var config = ShippedConfig();
            foreach (System.Func<PistolBodyMath.State, float> rider in new System.Func<PistolBodyMath.State, float>[] { _ => 0f, _ => 1f })
            {
                Run a = RideIt(config, config.DartRange, 60f, rider), b = RideIt(config, config.DartRange, 30f, rider);
                Assert.That(b.Seconds, Is.EqualTo(a.Seconds).Within(0.02f * a.Seconds + 1f / 30f));
                Assert.That(b.Flights, Is.EqualTo(a.Flights));
            }
        }

        [Test]
        public void TinyWingsView_KeepsTheGroundOnScreen_AndZoomsOutToFly()
        {
            var f = PistolPlane.For(new PlaneSegment(Vector3.zero, Vector3.forward * 400f, Vector3.up), 1, 6f, Shipped);
            float x = 120f, gy = PistolPlane.Ground(f, x);
            // grounded and slow: the close view, the ground under the Pistol near the bottom, the Pistol a third of the way in
            Vector2 c = PistolPlane.TinyWingsView(f, x, gy, 0f, 30f, 120f, 0.6f, -0.2f, 0.35f, 0.15f, 16f / 9f, out float w);
            float h = w / (16f / 9f);
            Assert.That(w, Is.EqualTo(30f).Within(1e-3f));
            Assert.That((gy - (c.y - 0.5f * h)) / h, Is.InRange(0.05f, 0.4f), "the ground sits in the screen's lower part");
            Assert.That((x - (c.x - 0.5f * w)) / w, Is.EqualTo(0.3f).Within(1e-3f));
            // high in the air: wider, the Pistol under the headroom and the ground under it still on screen
            float high = gy + 25f;   // a big flight: over three hill heights up
            c = PistolPlane.TinyWingsView(f, x, high, 26f, 30f, 120f, 0.6f, -0.2f, 0.35f, 0.15f, 16f / 9f, out float wHigh);
            h = wHigh / (16f / 9f);
            Assert.That(wHigh, Is.GreaterThan(2f * w), "it zooms out to fly");
            Assert.That(high, Is.LessThanOrEqualTo(c.y + 0.5f * h - 0.15f * h + 1e-3f), "the Pistol stays under the headroom");
            Assert.That(gy, Is.GreaterThanOrEqualTo(c.y - 0.5f * h), "the ground below it is still on screen");
            // the control: far above the widest view, the window rises with the Pistol instead
            c = PistolPlane.TinyWingsView(f, x, gy + 900f, 0f, 30f, 120f, 0.6f, -0.2f, 0.35f, 0.15f, 16f / 9f, out float wTop);
            Assert.That(wTop, Is.EqualTo(120f).Within(1e-3f));
            Assert.That(gy + 900f, Is.LessThanOrEqualTo(c.y + 0.5f * (wTop / (16f / 9f)) + 1e-3f));
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
