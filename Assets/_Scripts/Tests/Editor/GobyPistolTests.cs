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

        /// <summary>The shipped hills (Tiny Wings', Docs/PISTOL_TINY_WINGS_STUDY.md): half-hills 10-15 u on, 3.6-7.2 u up or down.</summary>
        static readonly PistolPlane.HillSpec Shipped = new(10f, 15f, 3.6f, 7.2f, 9f, 0.08f, 1.5f);

        static PistolPlane.HillTrack Track(float length, float amp = 1f, uint seed = 7u, int island = 0)
            => PistolPlane.HillTrack.Build(length, seed, new PistolPlane.HillSpec(Shipped.HalfLengthMin, Shipped.HalfLengthMax, Shipped.HalfHeightMin * amp,
                Shipped.HalfHeightMax * amp, Shipped.Band, Shipped.GrowthPerIsland, Shipped.GrowthMax), island);

        static PistolPlane.Frame Frame(int dir, float slope = 22f, float amp = 1f)
            => PistolPlane.For(new PlaneSegment(A, B, Vector3.up), dir, slope, Track((B - A).magnitude, amp));

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
            var track = Track(seg.L);
            foreach (int dir in new[] { 1, -1 })
            {
                var f = PistolPlane.For(seg, dir, 22f, track);
                for (float u = 0f; u <= seg.L; u += seg.L / 23f)
                {
                    Vector2 g = PistolPlane.GroundPoint(f, u);
                    Vector3 world = f.ToWorld(g.x, g.y), trail = seg.Ground(PistolPlane.LineS(f, u), track);
                    Assert.That(Vector3.Distance(world, trail), Is.LessThan(0.01f));
                    Assert.That(PistolPlane.Ground(f, g.x), Is.EqualTo(g.y).Within(0.05f), "the ground's inverse finds the same point");
                }
            }
        }

        [Test]
        public void Ground_IsTinyWingsKeyPointHills([Values(140f, 900f, 1800f)] float length, [Values(1u, 2u, 3u)] uint seed)
        {
            // Tiny Wings' ground (Garrett, 2026-10-24: "the right sinusoidal curve"; the study): key points, alternately a crest
            // and a trough, each a random 10-15 u on and 3.6-7.2 u up or down, a half-cosine between; the island starts on a
            // crest at the perch (its first half-hill runs down) and ends on a crest at the next (its last half-hill climbs)
            var t = PistolPlane.HillTrack.Build(length, seed, Shipped);
            Assert.That(t.KeyCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(t.KeyS(0), Is.EqualTo(0f)); Assert.That(t.KeyH(0), Is.EqualTo(0f));
            Assert.That(t.KeyS(t.KeyCount - 1), Is.EqualTo(length)); Assert.That(t.KeyH(t.KeyCount - 1), Is.EqualTo(0f));
            Assert.That(t.KeyH(1), Is.LessThan(0f), "the first half-hill runs down: the perch is a crest, the ride starts with a dive");
            Assert.That(t.KeyH(t.KeyCount - 2), Is.LessThan(0f), "the last half-hill climbs: the ride lands on the perch off a ramp");
            float rises = 0f;
            for (int i = 0; i + 1 < t.KeyCount; i++)
            {
                float dx = t.KeyS(i + 1) - t.KeyS(i), dy = t.KeyH(i + 1) - t.KeyH(i);
                Assert.That(dx, Is.GreaterThan(0f));
                if (i + 3 < t.KeyCount) Assert.That(dx, Is.InRange(Shipped.HalfLengthMin - 1e-3f, Shipped.HalfLengthMax + 1e-3f), "a half-hill's length (the last two fit the perch)");
                if (i > 0 && i + 1 < t.KeyCount) Assert.That(Mathf.Sign(dy), Is.Not.EqualTo(Mathf.Sign(t.KeyH(i) - t.KeyH(i - 1))), "crest, trough, crest: the steps alternate");
                Assert.That(Mathf.Abs(dy), Is.LessThanOrEqualTo(Shipped.HalfHeightMax + 1e-3f));
                Assert.That(Mathf.Abs(t.KeyH(i + 1)), Is.LessThanOrEqualTo(Shipped.Band + 1e-3f), "the ground keeps near the dart");
                Assert.That(t.Slope(t.KeyS(i) + 1e-4f), Is.EqualTo(0f).Within(1e-2f), "every crest and trough is level");
                rises += Mathf.Abs(dy);
            }
            Assert.That(rises / (t.KeyCount - 1), Is.InRange(0.6f * Shipped.MeanHeight, 1.3f * Shipped.MeanHeight), "the mean rise is the game's");
            if (length >= 900f) Assert.That((t.KeyCount - 1) / 2, Is.InRange(Mathf.FloorToInt(length / (2f * Shipped.HalfLengthMax)) - 1, Mathf.CeilToInt(length / (2f * Shipped.HalfLengthMin))));
            // the cosine pieces meet the keys and the perches, and the same seed builds the same island everywhere
            for (int i = 0; i < t.KeyCount; i++) Assert.That(t.Height(t.KeyS(i) + 1e-5f), Is.EqualTo(t.KeyH(i)).Within(1e-3f));
            Assert.That(t.Height(0f), Is.EqualTo(0f)); Assert.That(t.Height(length - 1e-3f), Is.EqualTo(0f).Within(0.01f));
            var again = PistolPlane.HillTrack.Build(length, seed, Shipped);
            for (int i = 0; i < t.KeyCount; i++) Assert.That(again.KeyH(i), Is.EqualTo(t.KeyH(i)));
            Assert.That(PistolPlane.HillTrack.Build(length, seed + 100u, Shipped).KeyH(2), Is.Not.EqualTo(t.KeyH(2)).Within(1e-4f), "another seed, another island");
            // the controls: a flat track is flat; later islands are taller
            Assert.That(PistolPlane.HillTrack.Flat(length).Height(0.37f * length), Is.EqualTo(0f));
            Assert.That(Shipped.Growth(4), Is.EqualTo(1.32f).Within(1e-4f)); Assert.That(Shipped.Growth(40), Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void Ground_NeverFolds_EvenOnAShortDart([Values(6f, 20f, 60f, 300f)] float length)
        {
            // on a leaning screen (the knob; the shipped lean is 0) the ground still runs left to right, and the inverse finds
            // every point: the hills' steepest slope stays under the lean's cotangent (MaxHillSlope)
            var seg = new PlaneSegment(Vector3.zero, Vector3.forward * length, Vector3.up);
            var track = Track(length);
            var f = PistolPlane.For(seg, 1, 22f, track);
            float lastX = -1f;
            for (float u = 0f; u <= length; u += length / 200f)
            {
                Vector2 g = PistolPlane.GroundPoint(f, u);
                Assert.That(g.x, Is.GreaterThan(lastX), "the ground never folds back");
                lastX = g.x;
                Assert.That(PistolPlane.Ground(f, g.x), Is.EqualTo(g.y).Within(0.05f));
            }
            Assert.That(track.SteepestSlope(), Is.LessThanOrEqualTo(PistolPlane.MaxHillSlope + 1e-3f));
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
        struct Run { public float Seconds, Longest, FirstBigAt, EarlySpeed, LateSpeed, Top; public int Flights, Smooth; }

        /// <summary>Ride the SHIPPED config over a <paramref name="length"/> u dart from a standstill at
        /// <paramref name="fps"/>, the rider choosing the dive each frame (null: the Tiny Wings rider the Pistol AI is).</summary>
        static Run RideIt(CosmicShore.ScriptableObjects.GobyPistolConfigSO config, float length, float fps, System.Func<PistolBodyMath.State, float, float> rider = null)
        {
            var s = config.Body;
            var f = PistolPlane.For(new PlaneSegment(Vector3.zero, Vector3.forward * length, Vector3.up), 1, config.SlopeDegrees,
                PistolPlane.HillTrack.Build(length, 1u, config.HillSpec));
            var p = new PistolBodyMath.State();
            PistolBodyMath.PlaceAtStart(p, f, s.pMinSpeed);
            var run = new Run { FirstBigAt = -1f };
            float dt = 1f / fps, takeoff = 0f, wave = config.HillSpec.MeanWavelength, early = 0f, late = 0f; int nEarly = 0, nLate = 0; bool wasGrounded = true;
            while (p.x < f.EndX - 1f && run.Seconds < 240f)
            {
                float play = PistolAI.TinyWingsDive(p, f, s), dive = rider != null ? rider(p, play) : play;
                var ev = PistolBodyMath.Step(p, f, run.Seconds, dt, new PistolBodyMath.Input { Dive = dive }, s);
                if (wasGrounded && !p.grounded) takeoff = p.x;
                if (ev.Landed)
                {
                    run.Flights++; run.Longest = Mathf.Max(run.Longest, p.x - takeoff); if (ev.SmoothLanding) run.Smooth++;
                    if (run.FirstBigAt < 0f && p.x - takeoff >= 3f * wave) run.FirstBigAt = takeoff / wave;
                }
                float speed = p.grounded ? p.v : Mathf.Sqrt(p.vx * p.vx + p.vy * p.vy), at = p.x / f.EndX;
                run.Top = Mathf.Max(run.Top, speed);
                if (at > 0.05f && at < 0.33f) { early += speed; nEarly++; } else if (at > 0.67f) { late += speed; nLate++; }
                wasGrounded = p.grounded; run.Seconds += dt;
            }
            run.EarlySpeed = nEarly > 0 ? early / nEarly : 0f; run.LateSpeed = nLate > 0 ? late / nLate : 0f;
            return run;
        }

        /// <summary>A rider who plays it but gets it WRONG for 0.3 s of every second (dives up a slope, lets go down one),
        /// starting <paramref name="phase"/> s in: the forgiveness check.</summary>
        static System.Func<PistolBodyMath.State, float, float> Sloppy(float phase)
        {
            float t = phase;
            return (p, play) => { t += 1f / 60f; return t % 1f < 0.3f ? 1f - play : play; };
        }

        /// <summary>The sloppy rider's time, averaged over four phases of its mistakes: a long ride is chaotic, and one
        /// frame's shift in when the mistakes fall moves a single run by half again.</summary>
        static float SloppySeconds(CosmicShore.ScriptableObjects.GobyPistolConfigSO config, float length)
        {
            float sum = 0f;
            for (int i = 0; i < 4; i++) sum += RideIt(config, length, 60f, Sloppy(0.25f * i)).Seconds;
            return sum / 4f;
        }

        /// <summary>
        /// Tiny Wings' ride on the SHIPPED config (Garrett, 2026-10-24: "understand tiny wings better ... properly scaled, have
        /// the right sinusoidal curve, speed, accelerations, camera"; the study, Docs/PISTOL_TINY_WINGS_STUDY.md, measured
        /// the game's physics from its remakes). The rider who dives the slopes and lets go up them crosses an island at the
        /// game's pace (one to two screens a second), flies off crest after crest, lands most flights as perfect slides,
        /// flies over several hills at a time, and reaches its top speed well above its early speed. The controls: never
        /// diving dawdles at the floor speed (the game's), never letting go stays glued, and a sloppy rider is slower but
        /// still rides (the game punishes a wrong hold, never a landing).
        /// </summary>
        [Test]
        public void Ride_IsTinyWings()
        {
            var config = ShippedConfig();
            float d = 2f * config.BodyR, screen = config.ViewWidth;
            foreach (float length in new[] { config.IslandMinLength, config.IslandMaxLength })
            {
                var play = RideIt(config, length, 60f);
                float hills = length / config.HillSpec.MeanWavelength, screensPerSecond = length / play.Seconds / screen;
                Assert.That(screensPerSecond, Is.InRange(0.9f, 2.5f), $"a {length} u island ({hills:F0} hills) in {play.Seconds:F1} s: {screensPerSecond:F2} screens a second (the game: one to two)");
                Assert.That(play.Flights, Is.GreaterThanOrEqualTo(hills / 4f), "it flies off crest after crest");
                Assert.That(play.Smooth, Is.GreaterThanOrEqualTo(Mathf.CeilToInt(0.7f * play.Flights)), "most landings are perfect slides");
                Assert.That(play.Longest, Is.GreaterThanOrEqualTo(3f * config.HillSpec.MeanWavelength), "a flight over several hills");
                Assert.That(play.Longest, Is.LessThan(0.25f * length), "but never a stretch");
                Assert.That(play.FirstBigAt, Is.InRange(1f, 0.7f * hills), "it rides a few hills, then flies over several");
                Assert.That(play.Top, Is.GreaterThan(1.4f * play.EarlySpeed), "the speed builds");
                Assert.That(play.Top / d, Is.InRange(25f, 60f), $"top speed {play.Top / d:F0} Pistol lengths a second (the game: about 30-46)");

                var never = RideIt(config, length, 60f, (p, dive) => 0f);
                Assert.That(never.Seconds, Is.GreaterThan(2f * play.Seconds), "the control: never diving builds no speed");
                Assert.That(never.Top, Is.LessThan(0.4f * play.Top));
                var always = RideIt(config, length, 60f, (p, dive) => 1f);
                Assert.That(always.Seconds, Is.GreaterThan(1.5f * play.Seconds), "never letting go: glued to the ground");
                Assert.That(always.Flights, Is.LessThan(play.Flights / 3), "and it hardly flies");
                Assert.That(SloppySeconds(config, length), Is.LessThan(2.6f * play.Seconds), "a sloppy rider is slower, never stuck");
            }
        }

        /// <summary>
        /// Tiny Wings' proportions on the SHIPPED config (Garrett, 2026-10-23: "it should feel much closer to tiny wings in
        /// relative scale of piston and hill height, apparent speed, length of level compared to perch distance"), in the
        /// Pistol's own length as Tiny Wings' are in the bird's (estimates from the game, the brief's 2026-10-23 entries): a
        /// hill about 15 lengths crest to crest and 3-5 tall, a resting screen a little over one hill wide, and a stretch
        /// from perch to perch an island of 30-75 hills crossed at one to three hills a second. The control: the numbers before
        /// this pass (18 u hills under an 80 u screen) fail the screen and the hill-height checks.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Proportions_AreTinyWings(bool shipped)
        {
            var config = ShippedConfig();
            float length = 2f * config.BodyR, wave = config.HillSpec.MeanWavelength, tall = config.HillSpec.MeanHeight, view = config.ViewWidth;
            if (!shipped) { length = 1f; wave = 18f; tall = 1.8f; view = 80f; }
            bool ok = wave / length >= 11f && wave / length <= 17f
                      && tall / length >= 2f && tall / length <= 4.5f
                      && view / wave >= 0.9f && view / wave <= 1.6f;
            if (!shipped) { Assert.That(ok, Is.False, "the control: the earlier proportions are not Tiny Wings'"); return; }
            // the game: key points 160-240 pt on and 60-120 pt up or down for a 14 pt bird, on a 480 pt screen
            Assert.That(wave / length, Is.InRange(11f, 17f), "a hill is 11-17 Pistol lengths crest to crest");
            Assert.That(tall / length, Is.InRange(2f, 4.5f), "and 2-4 of them from trough to crest");
            Assert.That(view / wave, Is.InRange(0.9f, 1.6f), "the resting screen shows a little over one hill");
            Assert.That(length / view, Is.InRange(1f / 25f, 1f / 12f), "the Pistol is about a seventeenth of the screen");
            foreach (float dart in new[] { config.IslandMinLength, config.IslandMaxLength })
            {
                float hills = dart / wave;
                Assert.That(hills, Is.InRange(30f, 75f), $"a {dart} u stretch is an island of {hills:F0} hills (Garrett: \"ride about 3 times longer before the perch\")");
                float rate = hills / RideIt(config, dart, 60f).Seconds;
                Assert.That(rate, Is.InRange(1.2f, 3f), $"crossed at {rate:F2} hills a second");
                Assert.That(config.SlopeDegrees, Is.EqualTo(0f), "level, as the game is");
            }
        }

        [Test]
        public void Ride_IsTheSame_AtThirtyAndSixtyFps()
        {
            // the ride steps at 1/120 s whatever the frame (the liftoff and the landing used to depend on it: at 120 steps a
            // second the old rule never left the ground at all)
            var config = ShippedConfig();
            foreach (System.Func<PistolBodyMath.State, float, float> rider in new System.Func<PistolBodyMath.State, float, float>[] { (p, d) => 0f, (p, d) => 1f })
            {
                Run a = RideIt(config, config.IslandMaxLength, 60f, rider), b = RideIt(config, config.IslandMaxLength, 30f, rider);
                Assert.That(b.Seconds, Is.EqualTo(a.Seconds).Within(0.02f * a.Seconds + 1f / 30f));
                Assert.That(b.Flights, Is.EqualTo(a.Flights));
            }
        }

        [Test]
        public void TinyWingsView_KeepsTheGroundOnScreen_AndZoomsOutToFly()
        {
            var f = PistolPlane.For(new PlaneSegment(Vector3.zero, Vector3.forward * 400f, Vector3.up), 1, 0f, Track(400f));
            float x = 120f, gy = PistolPlane.Ground(f, x);
            // grounded and slow: the game's close view, the ground under the Pistol in the lower part, the Pistol an eighth
            // to a sixth of the way in
            Vector2 c = PistolPlane.TinyWingsView(f, x, gy, 0f, 29f, 150f, 0.15f, -0.35f, 0.4f, 0.2f, 16f / 9f, out float w);
            float h = w / (16f / 9f);
            Assert.That(w, Is.EqualTo(29f).Within(1e-3f));
            Assert.That((gy - (c.y - 0.5f * h)) / h, Is.InRange(0.05f, 0.6f), "the ground sits in the screen's lower part");
            Assert.That((x - (c.x - 0.5f * w)) / w, Is.EqualTo(0.15f).Within(1e-3f));
            // high in the air: wider, the Pistol under the headroom and the ground under it still on screen
            float high = gy + 25f;   // a big flight: over three hill heights up
            c = PistolPlane.TinyWingsView(f, x, high, 40f, 29f, 150f, 0.15f, -0.35f, 0.4f, 0.2f, 16f / 9f, out float wHigh);
            h = wHigh / (16f / 9f);
            Assert.That(wHigh, Is.GreaterThan(2f * w), "it zooms out to fly");
            Assert.That(high, Is.LessThanOrEqualTo(c.y + 0.5f * h - 0.2f * h + 1e-3f), "the Pistol stays under the headroom (the game: four fifths)");
            Assert.That(gy, Is.GreaterThanOrEqualTo(c.y - 0.5f * h), "the ground below it is still on screen");
            // the control: far above the widest view, the window rises with the Pistol instead
            c = PistolPlane.TinyWingsView(f, x, gy + 900f, 0f, 29f, 150f, 0.15f, -0.35f, 0.4f, 0.2f, 16f / 9f, out float wTop);
            Assert.That(wTop, Is.EqualTo(150f).Within(1e-3f));
            Assert.That(gy + 900f, Is.LessThanOrEqualTo(c.y + 0.5f * (wTop / (16f / 9f)) + 1e-3f));
        }

        [Test]
        public void Islands_AreLaidAtTheirLength_AndKeepThePairTogether()
        {
            // Garrett, 2026-10-23: "the goby is barely cruising around at all". The Goby cruises; a dart's end is a perch only
            // once it is an island's length from the last perch; a longer gap is several islands; and no new perch is laid
            // while the Pistol has its islands ahead
            Vector3 a = Vector3.zero;
            Assert.That(GobyIslands.Reaches(a, Vector3.forward * 899f, 900f), Is.False, "the control: a dart short of an island is a dash");
            Assert.That(GobyIslands.Reaches(a, new Vector3(600f, 0f, 700f), 900f), Is.True);
            Assert.That(GobyIslands.Pieces(a, Vector3.forward * 1800f, 1800f), Is.EqualTo(1));
            Assert.That(GobyIslands.Pieces(a, Vector3.forward * 1801f, 1800f), Is.EqualTo(2));
            Assert.That(GobyIslands.Pieces(a, Vector3.forward * 4000f, 1800f), Is.EqualTo(3), "a long cruise is laid as islands the Pistol can ride");
            Assert.That(GobyIslands.Ahead(perches: 4, resting: true, at: 3, segment: -1, dir: 1), Is.EqualTo(0), "resting at the head: nothing ahead");
            Assert.That(GobyIslands.Ahead(perches: 4, resting: false, at: 0, segment: 0, dir: 1), Is.EqualTo(2), "riding the first: two more wait");
            Assert.That(GobyIslands.Ahead(perches: 4, resting: false, at: 2, segment: 1, dir: -1), Is.EqualTo(2), "riding back to perch 1");
        }

        /// <summary>
        /// The pair together (Garrett, 2026-10-23: "take a pass at tuning the two together"): the Goby AI, cruising and
        /// wandering, makes an island faster than the Pistol AI rides one, so the Pistol never waits at the head perch; the
        /// island queue then keeps the Goby from running away. The control: the old Goby, which only darted and waited up to
        /// 45 s for the Pistol, kept it waiting at every perch.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Pair_TheGobyKeepsAheadOfThePistol(bool shipped)
        {
            var config = ShippedConfig();
            const float wander = 0.7f;   // net progress per u flown by the wandering AI (simulated over 200 seeds: median 0.88, one in ten 0.56)
            float island = 0.5f * (config.IslandMinLength + config.IslandMaxLength);
            float goby = island / (wander * config.AiCruiseSpeed) + config.PerchSeconds + config.DartSeconds;
            float pistol = RideIt(config, island, 60f).Seconds + config.RestSeconds;
            if (!shipped) { goby = config.PerchSeconds + 45f; Assert.That(goby, Is.GreaterThan(pistol), "the control: the waiting Goby"); return; }
            Assert.That(goby, Is.LessThan(pistol), $"the Goby makes an island in {goby:F1} s, the Pistol rides one in {pistol:F1} s");
            Assert.That(goby, Is.GreaterThan(0.4f * pistol), "and it is not so far ahead that it only circles");
            Assert.That(config.IslandQueue, Is.InRange(2, 4));
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

        [Test]
        public void Paths_EveryDenEitherWay_ThenTheHeadWaits()
        {
            // Garrett, 2026-10-25: "the pistol should be able to travel either direction from a den"
            var o = new List<PathChoice>();
            PistolPaths.Options(3, 2, o);
            var want = new[] { (0, 1), (1, -1), (1, 1), (2, -1), (2, 1) };
            Assert.That(o.Count, Is.EqualTo(want.Length));
            for (int i = 0; i < want.Length; i++) { Assert.That(o[i].Perch, Is.EqualTo(want[i].Item1)); Assert.That(o[i].Dir, Is.EqualTo(want[i].Item2)); }
            Assert.That(PistolPaths.Ready(o[4], 2), Is.False, "the head's ahead waits for the Goby's next island");
            Assert.That(o[3].Segment, Is.EqualTo(1), "back from perch 2 rides island 1");
            PistolPaths.Options(1, 0, o);
            Assert.That(o.Count, Is.EqualTo(1));
            Assert.That(PistolPaths.Ready(o[0], 0), Is.False, "the control: one perch, nothing to ride yet");
            PistolPaths.Options(3, 2, o);
            Assert.That(PistolPaths.Step(o, new PathChoice(0, 1), -1).Same(new PathChoice(0, 1)), "the cursor stops at the chain's end");
            Assert.That(PistolPaths.Step(o, new PathChoice(1, -1), 2).Same(new PathChoice(2, -1)));
        }

        [Test]
        public void Paths_TheAiTakesTheIslandWithTheMostOpposingPrisms()
        {
            var o = new List<PathChoice>();
            PistolPaths.Options(4, 3, o);
            int best = PistolPaths.Best(o, new[] { 2, 9, 4 }, 3, 3);
            Assert.That(o[best].Segment, Is.EqualTo(1), "island 1 holds the most");
            Assert.That(o[best].Perch, Is.EqualTo(2), "of its two ends, the one nearest the perch it is at");
            Assert.That(PistolPaths.Best(o, new[] { 0, 0, 0 }, 3, 3), Is.EqualTo(-1), "the control: every island empty, it keeps its own way");
            Assert.That(PistolPaths.InBand(new Vector3(50f, 10f, 30f), 100f, 0f, 40f, 60f), Is.True);
            Assert.That(PistolPaths.InBand(new Vector3(50f, 10f, 50f), 100f, 0f, 40f, 60f), Is.False, "beyond the undertow's reach");
            Assert.That(PistolPaths.InBand(new Vector3(120f, 10f, 0f), 100f, 0f, 40f, 60f), Is.False, "past the island's end");
        }

        [Test]
        public void Map_TouchFirst_DragSpins_TapPicks_AHeldFingerRidesOn()
        {
            // Garrett, 2026-10-25: "the control priority for 2d characters is mobile, gamepad, mouse/keyboard ... the player
            // needs to be able to spin the map"
            var p = new MapPointer();
            p.Open(false, Vector2.zero);
            p.Step(true, true, new Vector2(100f, 100f), 0.016f, 12f, 0.35f, out var drag, out bool tap, out _, out _);
            p.Step(false, true, new Vector2(140f, 100f), 0.016f, 12f, 0.35f, out drag, out tap, out _, out _);
            Assert.That(drag.x, Is.EqualTo(40f).Within(1e-3f), "past the slop a press drags: the map spins");
            p.Step(false, false, new Vector2(140f, 100f), 0.016f, 12f, 0.35f, out _, out tap, out _, out _);
            Assert.That(tap, Is.False, "a drag that lifts is no tap");

            p.Step(true, true, new Vector2(300f, 200f), 0.016f, 12f, 0.35f, out _, out _, out _, out _);
            p.Step(false, true, new Vector2(305f, 202f), 0.05f, 12f, 0.35f, out drag, out _, out _, out _);
            Assert.That(drag, Is.EqualTo(Vector2.zero), "inside the slop it does not spin");
            p.Step(false, false, new Vector2(305f, 202f), 0.05f, 12f, 0.35f, out _, out tap, out var at, out _);
            Assert.That(tap, Is.True, "a quick, still press is a tap");
            Assert.That(at, Is.EqualTo(new Vector2(300f, 200f)), "where it went down");

            p.Step(true, true, Vector2.zero, 0.016f, 12f, 0.35f, out _, out _, out _, out _);
            p.Step(false, true, Vector2.zero, 0.5f, 12f, 0.35f, out _, out _, out _, out _);
            p.Step(false, false, Vector2.zero, 0.016f, 12f, 0.35f, out _, out tap, out _, out _);
            Assert.That(tap, Is.False, "the control: a long press is no tap");

            p.Open(true, new Vector2(50f, 50f));   // the finger diving through the perch
            p.Step(false, true, new Vector2(51f, 50f), 0.3f, 12f, 0.35f, out _, out _, out _, out bool still);
            Assert.That(still, Is.True, "held still it asks to ride on");
            p.Step(false, false, new Vector2(51f, 50f), 0.016f, 12f, 0.35f, out _, out tap, out _, out _);
            Assert.That(tap, Is.False, "lifting a carried press is not a tap");
        }

        [Test]
        public void GobyAi_LaysItsIslandThroughTheOpposingPrisms()
        {
            // Garrett, 2026-10-25: "the goby AI was doing a bad job of taking paths that would give the shrimp plenty to destroy"
            var pts = new List<Vector3>();
            var rng = new System.Random(3);
            Vector3 band = new Vector3(Mathf.Cos(17f * Mathf.Deg2Rad), 0.1f, Mathf.Sin(17f * Mathf.Deg2Rad)).normalized;
            float J() => (float)rng.NextDouble() * 40f - 20f;
            for (int i = 0; i < 40; i++) pts.Add(band * (400f + i * 12.5f) + new Vector3(J(), J(), J()));
            for (int i = 0; i < 12; i++) pts.Add(new Vector3(J() * 40f, J() * 40f, J() * 40f));
            Assert.That(PistolPaths.BestHeading(Vector3.zero, 1000f, 60f, pts, Vector3.zero, 4000f, Vector3.forward, 0f, 1u, out var h, out int c), Is.True);
            Assert.That(Vector3.Angle(h, band), Is.LessThan(6f), "the island runs through the band of opposing prisms");
            Assert.That(c, Is.GreaterThanOrEqualTo(35));
            int fixedBest = 0;
            foreach (var d in PistolPaths.Headings) fixedBest = Mathf.Max(fixedBest, PistolPaths.CorridorCount(Vector3.zero, d, 1000f, 60f, pts));
            Assert.That(fixedBest, Is.LessThan(c), "the control: the fixed headings alone miss most of the band");
            Assert.That(PistolPaths.BestHeading(Vector3.zero, 1000f, 60f, pts, Vector3.zero, 500f, Vector3.forward, 0f, 1u, out _, out _), Is.False,
                "never past the home radius");
        }

        [Test]
        public void Pistol_HasItsOwnModel()
        {
            // Garrett, 2026-10-24: "give the pistol a model" (specs/pistol.py, Docs/VesselModels/Pistol.md)
            var cfg = AssetDatabase.LoadAssetAtPath<CosmicShore.ScriptableObjects.GobyPistolConfigSO>("Assets/_SO_Assets/VesselActions/GobyPistol/GobyPistolConfig.asset");
            Assert.That(cfg, Is.Not.Null);
            Assert.That(cfg.PistolModel, Is.Not.Null, "the seat draws a model, not the capsule");
            Assert.That(AssetDatabase.GetAssetPath(cfg.PistolModel), Does.EndWith("Pistol.fbx"));
            var bones = new HashSet<string>();
            foreach (var t in cfg.PistolModel.GetComponentsInChildren<Transform>(true)) bones.Add(t.name);
            Assert.That(bones, Does.Contain("claw"), "the hammer claw the Snap shuts");
            Assert.That(bones, Does.Contain("tail.1"), "the tail the dive curls");
            Assert.That(cfg.PistolModel.GetComponentInChildren<SkinnedMeshRenderer>(true), Is.Not.Null);
        }

        [Test]
        public void Hud_EachSeatHasItsOwnCards()
        {
            // Garrett, 2026-10-24: "give both of its seats separate UI ability icons, glyphs, etc." (author_goby_hud.py)
            var hull = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Spacevessels/Goby.prefab");
            Assert.That(hull, Is.Not.Null);
            Assert.That(hull.TryGetComponent(out CosmicShore.UI.GobyPistolHUDController controller), Is.True,
                "the hull's HUD controller is the Goby & Pistol's, not the Squirrel's");
            Assert.That(hull.GetComponent<VesselStatus>().VesselHUDController, Is.SameAs(controller), "VesselStatus drives that controller");
            var view = hull.GetComponentInChildren<CosmicShore.UI.GobyPistolHUDView>(true);
            Assert.That(view, Is.Not.Null, "the HUD under ShipHUDContainer is the GobyHUDVariant");
            Assert.That(hull.GetComponentInChildren<CosmicShore.UI.SquirrelVesselHUDView>(true), Is.Null, "the control: no Squirrel HUD left");
            foreach (var element in CosmicShore.UI.VesselHUDView.AbilityDisplayOrder)
            {
                Assert.That(view.TryGetAbilityIcon(element, out var icon) && icon, Is.True, $"{element}: the card binds an icon");
                Assert.That(view.HasBothSeatIcons(element), Is.True, $"{element}: the Goby and the Pistol each have their own icon");
            }
        }
    }
}
#endif
