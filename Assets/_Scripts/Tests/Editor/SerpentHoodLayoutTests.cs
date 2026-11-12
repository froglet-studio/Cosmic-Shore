using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Hood's arena (<see cref="SerpentHoodLayout"/>): the ladder gets harder in the ways SERPENTHOOD.md promises, no
    /// two covers touch, the covers stay clear of the crystal heart and the spawn sphere, the core is listed first (the
    /// core rule stops at the first non-core part), the exits stay open after the hood warp, and the weave warped into a
    /// hood still leaves no hull-sized hole anywhere but the exits. Pure geometry: also run outside Unity by
    /// <c>Tools/Build/serpent_hood_harness/run.sh</c>.
    /// </summary>
    [TestFixture]
    public class SerpentHoodLayoutTests
    {
        /// <summary>The scene's spawn sphere (ServerPlayerVesselInitializer.spawnRingRadiusFloor, author_serpent_hood_assets.py).</summary>
        const float SpawnRadius = 2200f;
        /// <summary>The crystal heart (SerpentHoodSettings.fieldRadius).</summary>
        const float FieldRadius = 380f;
        /// <summary>The weave's largest hole away from the exits, as a radius. Serpentine holds its flat weave to 6.5 u; the
        /// hood stretches it across by up to 1.6x, so the bound here is the stretched pitch's: still under a Serpent's
        /// half-width plus its danger margin. Measured 2026-11-09, see SERPENTHOOD.md.</summary>
        const float LargestHole = 9.5f;
        /// <summary>The smallest clear radius an exit keeps once warped (Serpentine's I4 exit is 12 u).</summary>
        const float SmallestExit = 16f;

        static List<SerpentHoodLayout.Cover> Covers(int intensity)
        {
            var list = new List<SerpentHoodLayout.Cover>();
            SerpentHoodLayout.Covers(Vector3.zero, intensity, list);
            return list;
        }

        [Test]
        public void The_ladder_gets_harder_up_the_intensities()
        {
            for (int i = 1; i < 4; i++)
            {
                var a = SerpentHoodLayout.RungFor(i);
                var b = SerpentHoodLayout.RungFor(i + 1);
                Assert.Greater(b.Perches, a.Perches, $"I{i + 1} has more perches than I{i}");
                Assert.Less(b.PerchRadius, a.PerchRadius, $"I{i + 1}'s shots are shorter than I{i}'s");
                Assert.GreaterOrEqual(b.Cup, a.Cup, $"I{i + 1}'s cup is at least as deep as I{i}'s");
                Assert.GreaterOrEqual(b.Shutters, a.Shutters, $"I{i + 1} has at least I{i}'s shutters");
            }
            Assert.AreEqual(0, SerpentHoodLayout.RungFor(2).Shutters, "I1-I2 leave the middle open");
            Assert.Greater(SerpentHoodLayout.RungFor(3).Shutters, 0, "I3 adds shutters");
            // Every shot across the hood is inside the rifle's 3,000 u reach.
            Assert.LessOrEqual(2f * SerpentHoodLayout.RungFor(1).PerchRadius, 3000f);
        }

        [Test]
        public void No_two_covers_touch_and_all_stay_inside_the_spawn_sphere_and_outside_the_heart()
        {
            for (int i = 1; i <= 4; i++)
            {
                var covers = Covers(i);
                var reach = new float[covers.Count];
                for (int c = 0; c < covers.Count; c++)
                    reach[c] = SerpentHoodLayout.Build(covers[c], i, new List<SerpentineGate.Part>());

                for (int c = 0; c < covers.Count; c++)
                {
                    float r = covers[c].Centre.magnitude;
                    Assert.Less(r + reach[c], SpawnRadius - 300f, $"I{i} cover {c} reaches within 300 u of the spawns");
                    Assert.Greater(r - reach[c], FieldRadius + 60f, $"I{i} cover {c} reaches into the crystal heart");
                    for (int d = c + 1; d < covers.Count; d++)
                    {
                        float gap = Vector3.Distance(covers[c].Centre, covers[d].Centre) - reach[c] - reach[d];
                        Assert.Greater(gap, 20f, $"I{i}: covers {c} and {d} touch ({gap:F0} u apart)");
                    }
                }
            }
        }

        [Test]
        public void Perches_face_the_heart_and_shutters_turn_across_it()
        {
            for (int i = 1; i <= 4; i++)
            foreach (var cover in Covers(i))
            {
                float facing = Vector3.Dot(cover.Axis.normalized, -cover.Centre.normalized);
                if (cover.Shutter) Assert.Less(Mathf.Abs(facing), 0.05f, $"I{i} shutter {cover.Index} is not turned across");
                else Assert.Greater(facing, 0.999f, $"I{i} perch {cover.Index} does not face the heart");
            }
        }

        [Test]
        public void The_core_comes_first_and_is_this_rungs_core()
        {
            for (int i = 1; i <= 4; i++)
            {
                var parts = new List<SerpentineGate.Part>();
                SerpentHoodLayout.Build(Covers(i)[0], i, parts);
                int cores = 0;
                while (cores < parts.Count && parts[cores].Core) cores++;
                for (int k = cores; k < parts.Count; k++) Assert.IsFalse(parts[k].Core, $"I{i}: a core part after the first non-core");
                Assert.AreEqual(SerpentineGate.CoreFor(i).Blocks, cores, $"I{i}: core block count");
            }
        }

        [Test]
        public void The_weave_is_danger_the_rest_plain()
        {
            for (int i = 1; i <= 4; i++)
            {
                var parts = new List<SerpentineGate.Part>();
                SerpentHoodLayout.Build(Covers(i)[1], i, parts);
                foreach (var p in parts)
                    Assert.AreEqual(p.Weave ? PrismKind.Danger : PrismKind.Plain, p.Kind);
            }
        }

        static void Segment(in SerpentineGate.Part p, out Vector3 a, out Vector3 b)
        {
            Vector3 along = p.Rotation * Vector3.forward * (p.Scale.z * 0.5f);
            a = p.Position - along;
            b = p.Position + along;
        }

        static float Clearance(Vector3 point, List<SerpentineGate.Part> parts)
        {
            float clear = float.MaxValue;
            foreach (var part in parts)
            {
                if (!part.Weave) continue;
                Segment(part, out var a, out var b);
                a.z = 0f; b.z = 0f;
                clear = Mathf.Min(clear, SerpentineGate.SegmentDistance(new Vector2(point.x, point.y), new Vector2(a.x, a.y),
                    new Vector2(b.x, b.y)) - part.Scale.x * 0.5f);
            }
            return clear;
        }

        [Test]
        public void Every_exit_stays_open_through_the_warp()
        {
            const float R = SerpentHoodLayout.CoverRingRadius;
            for (int i = 1; i <= 4; i++)
            {
                var rung = SerpentHoodLayout.RungFor(i);
                float reach = SerpentineGate.OuterReach(R, i);
                var ring = new RaceGate(Vector3.zero, Vector3.forward, R);
                for (int cover = 0; cover < 4; cover++)
                {
                    var parts = new List<SerpentineGate.Part>();
                    SerpentHoodLayout.BuildLocal(cover, i, parts);
                    for (int e = 0; e < SerpentineGate.ExitCount(i); e++)
                    {
                        var exit = SerpentHoodLayout.Warp(SerpentineGate.Exit(ring, cover, i, e).Position, rung, reach);
                        Assert.GreaterOrEqual(Clearance(exit, parts), SmallestExit,
                            $"I{i} cover {cover} exit {e}: the warp closed it");
                    }
                }
            }
        }

        [Test]
        public void The_warped_weave_leaves_no_hull_sized_hole_but_the_exits()
        {
            const float R = SerpentHoodLayout.CoverRingRadius;
            for (int i = 1; i <= 4; i++)
            {
                var rung = SerpentHoodLayout.RungFor(i);
                float reach = SerpentineGate.OuterReach(R, i);
                float coreReach = SerpentineGate.CoreFor(i).Reach;
                var ring = new RaceGate(Vector3.zero, Vector3.forward, R);
                var parts = new List<SerpentineGate.Part>();
                SerpentHoodLayout.BuildLocal(0, i, parts);
                var exits = new List<Vector3>();
                var exitRadii = new List<float>();
                for (int e = 0; e < SerpentineGate.ExitCount(i); e++)
                {
                    var exit = SerpentineGate.Exit(ring, 0, i, e);
                    exits.Add(exit.Position);
                    exitRadii.Add(exit.Radius);
                }

                // Sample the UNWARPED mouth (Serpentine's own test grid), carry each point through the warp, and measure
                // the hole there; a point whose unwarped hole touches an exit is part of that exit.
                for (float x = -R; x <= R; x += 2f)
                for (float y = -R; y <= R; y += 2f)
                {
                    var p = new Vector3(x, y, 0f);
                    float fromCentre = p.magnitude;
                    if (fromCentre <= coreReach + 2f || fromCentre >= R - 6f) continue;
                    bool besideAnExit = false;
                    for (int e = 0; e < exits.Count; e++)
                        if ((p - exits[e]).magnitude <= exitRadii[e] + 8f) besideAnExit = true;
                    if (besideAnExit) continue;

                    float clear = Clearance(SerpentHoodLayout.Warp(p, rung, reach), parts);
                    Assert.LessOrEqual(clear, LargestHole, $"I{i}: a {clear:F1} u hole at {p} (unwarped) outside the exits");
                }
            }
        }

        [Test]
        public void The_prism_budget_rises_and_stays_light()
        {
            int previous = 0;
            for (int i = 1; i <= 4; i++)
            {
                int count = 0;
                foreach (var cover in Covers(i))
                {
                    var parts = new List<SerpentineGate.Part>();
                    SerpentHoodLayout.Build(cover, i, parts);
                    count += parts.Count;
                }
                Assert.Greater(count, previous, $"I{i} lays more than I{i - 1}");
                Assert.LessOrEqual(count, 8000, $"I{i} lays {count} prisms");
                previous = count;
            }
        }

        [Test]
        public void The_crystal_heart_fills_its_ball()
        {
            for (int k = 0; k < 24; k++)
            {
                float r = SerpentHoodLayout.FieldSlot(Vector3.zero, k, 24, FieldRadius).magnitude;
                Assert.LessOrEqual(r, FieldRadius + 1e-3f);
            }
        }
    }
}
