using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The shape at every Serpentine ring (<see cref="SerpentineGate"/>): its exits are real holes inside the mouth and
    /// clear of the core, nothing of the weave reaches into them, the weave leaves no hull-sized hole anywhere else, the
    /// parts are the kinds the design says (plain core and shell, danger weave), a gate stays within its prism budget,
    /// the build is deterministic, and two gates' shapes never meet on the real circuits.
    /// </summary>
    public class SerpentineGateTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 60;
        static readonly float[] Radii = { 72f, 60f, 50f, 42f };

        static RaceGate Ring(float radius) =>
            new RaceGate(new Vector3(10f, 800f, -30f), new Vector3(0.3f, 0.2f, 0.93f).normalized, radius);

        static List<SerpentineGate.Part> Build(in RaceGate ring, int gate, int intensity)
        {
            var parts = new List<SerpentineGate.Part>();
            SerpentineGate.Build(ring, gate, intensity, parts);
            return parts;
        }

        static void Segment(in SerpentineGate.Part p, out Vector3 a, out Vector3 b)
        {
            Vector3 half = p.Rotation * Vector3.forward * (p.Scale.z * 0.5f);
            a = p.Position - half;
            b = p.Position + half;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        [Test]
        public void Coil_at_I1_I2_Maw_at_I3_Iris_at_I4()
        {
            Assert.AreEqual(SerpentineGate.Shape.Coil, SerpentineGate.ShapeFor(1));
            Assert.AreEqual(SerpentineGate.Shape.Coil, SerpentineGate.ShapeFor(2));
            Assert.AreEqual(SerpentineGate.Shape.Maw, SerpentineGate.ShapeFor(3));
            Assert.AreEqual(SerpentineGate.Shape.Iris, SerpentineGate.ShapeFor(4));
        }

        [Test]
        public void Exits_lie_inside_the_mouth_and_clear_of_the_core()
        {
            for (int i = 0; i < 4; i++)
            {
                float r = Radii[i];
                float d = SerpentineGate.ExitDistance(r), e = SerpentineGate.ExitRadius(r);
                Assert.Greater(e, 10f, $"R {r}: an exit a Serpent can thread");
                Assert.LessOrEqual(d + e, r, $"R {r}: an exit inside the ring's mouth, so threading it threads the gate");
                Assert.GreaterOrEqual(d - e, SerpentineGate.CoreFor(i + 1).Reach, $"R {r}: an exit clear of the core");
            }
        }

        [Test]
        public void Nothing_of_the_weave_reaches_into_an_exit()
        {
            for (int i = 0; i < 4; i++)
            {
                for (int gate = 0; gate < 8; gate++)
                {
                    var ring = Ring(Radii[i]);
                    var parts = Build(ring, gate, i + 1);
                    for (int e = 0; e < SerpentineGate.ExitCount(i + 1); e++)
                    {
                        var exit = SerpentineGate.Exit(ring, gate, i + 1, e);
                        foreach (var p in parts)
                        {
                            if (!p.Weave) continue;
                            Segment(p, out var a, out var b);
                            Assert.GreaterOrEqual(SegmentDistance(exit.Position, a, b), exit.Radius + p.Scale.x * 0.5f - 0.05f,
                                $"I{i + 1} gate {gate}: a weave bar reaches into exit {e}");
                        }
                    }
                }
            }
        }

        [Test]
        public void The_weave_leaves_no_hull_sized_hole_but_the_exits()
        {
            const float largestHole = 6.5f;   // radius: the weave's own pitch (12 u, 3 u bars) leaves about 4.5
            for (int i = 0; i < 4; i++)
            {
                float r = Radii[i];
                float coreReach = SerpentineGate.CoreFor(i + 1).Reach;
                var ring = new RaceGate(Vector3.zero, Vector3.forward, r);
                for (int gate = 0; gate < 4; gate++)
                {
                    var parts = Build(ring, gate, i + 1);
                    var exits = new List<RaceGate>();
                    for (int e = 0; e < SerpentineGate.ExitCount(i + 1); e++) exits.Add(SerpentineGate.Exit(ring, gate, i + 1, e));

                    for (float x = -r; x <= r; x += 1.5f)
                    for (float y = -r; y <= r; y += 1.5f)
                    {
                        var p = new Vector3(x, y, 0f);
                        float fromCentre = p.magnitude;
                        if (fromCentre <= coreReach || fromCentre >= r - 2f) continue;

                        float clear = Mathf.Min(fromCentre - coreReach, r - 2f - fromCentre + 2f);
                        foreach (var part in parts)
                        {
                            if (!part.Weave) continue;
                            Segment(part, out var a, out var b);
                            a.z = 0f; b.z = 0f;   // the Maw's layers sit a bar either side of the plane
                            clear = Mathf.Min(clear, SegmentDistance(p, a, b) - part.Scale.x * 0.5f);
                        }

                        bool besideAnExit = false;
                        foreach (var exit in exits)
                            if ((p - exit.Position).magnitude - exit.Radius <= clear) besideAnExit = true;
                        if (besideAnExit) continue;
                        Assert.LessOrEqual(clear, largestHole, $"I{i + 1} gate {gate}: a {clear:F1} u hole at {p} outside the exits");
                    }
                }
            }
        }

        [Test]
        public void Core_and_shell_are_plain_the_weave_is_danger()
        {
            for (int i = 0; i < 4; i++)
            {
                var ring = Ring(Radii[i]);
                var parts = Build(ring, 3, i + 1);
                int core = 0;
                for (int k = 0; k < parts.Count; k++)
                {
                    var p = parts[k];
                    if (p.Core)
                    {
                        Assert.AreEqual(k, core++, "the core is listed first (the controller watches it there)");
                        Assert.AreEqual(PrismKind.Plain, p.Kind);
                    }
                    else if (p.Weave)
                    {
                        Assert.AreEqual(PrismKind.Danger, p.Kind);
                        Assert.Less((p.Position - ring.Position).magnitude, ring.Radius, "the weave is across the mouth");
                    }
                    else
                    {
                        Assert.AreEqual(PrismKind.Plain, p.Kind);
                        Vector3 off = p.Position - ring.Position;
                        Vector3 inPlane = off - ring.Axis * Vector3.Dot(off, ring.Axis);
                        Assert.Greater(inPlane.magnitude, ring.Radius, "the shell stands outside the rim");
                    }
                }
                Assert.AreEqual(SerpentineGate.CoreFor(i + 1).Blocks, core, $"I{i + 1}: the core's blocks");
            }
        }

        [Test]
        public void A_gate_stays_within_its_prism_budget_and_inside_its_reach()
        {
            for (int i = 0; i < 4; i++)
            {
                var ring = Ring(Radii[i]);
                float reach = SerpentineGate.OuterReach(ring.Radius, i + 1);
                for (int gate = 0; gate < 8; gate++)
                {
                    var parts = Build(ring, gate, i + 1);
                    Assert.LessOrEqual(parts.Count, 260, $"I{i + 1} gate {gate}: {parts.Count} prisms");
                    foreach (var p in parts)
                        Assert.LessOrEqual((p.Position - ring.Position).magnitude, reach, $"I{i + 1}: a part past OuterReach");
                }
            }
        }

        [Test]
        public void The_core_shrinks_as_the_intensity_rises()
        {
            for (int i = 2; i <= 4; i++)
            {
                var hi = SerpentineGate.CoreFor(i);
                var lo = SerpentineGate.CoreFor(i - 1);
                Assert.LessOrEqual(hi.Reach, lo.Reach, $"I{i}: a core no bigger than I{i - 1}'s");
                Assert.IsTrue(hi.Blocks < lo.Blocks || hi.Cube < lo.Cube, $"I{i}: a smaller core than I{i - 1}'s");
            }
            Assert.AreEqual(7, SerpentineGate.CoreFor(1).Blocks);
            Assert.AreEqual(1, SerpentineGate.CoreFor(4).Blocks);
        }

        [Test]
        public void The_build_is_deterministic()
        {
            var ring = Ring(60f);
            var a = Build(ring, 5, 2);
            var b = Build(ring, 5, 2);
            Assert.AreEqual(a.Count, b.Count);
            for (int k = 0; k < a.Count; k++)
            {
                Assert.AreEqual(a[k].Position, b[k].Position);
                Assert.AreEqual(a[k].Scale, b[k].Scale);
            }
        }

        [Test]
        public void Two_gates_shapes_never_meet_on_the_real_circuits()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var s = SerpentTimeCourse.ForIntensity(intensity);
                    s.InnerRadius = Inner;
                    s.OuterRadius = Outer;
                    s.FirstGateDirection = Vector3.up;
                    var course = HeadlongCircuit.Generate(seed, s);
                    if (course == null || course.Count == 0) continue;
                    for (int a = 0; a < course.Count; a++)
                    for (int b = a + 1; b < course.Count; b++)
                    {
                        float need = SerpentineGate.OuterReach(course[a].Radius, intensity) +
                                     SerpentineGate.OuterReach(course[b].Radius, intensity);
                        Assert.Greater(Vector3.Distance(course[a].Position, course[b].Position), need,
                            $"I{intensity} seed {seed}: gates {a + 1} and {b + 1}'s shapes meet");
                    }
                }
            }
        }
    }
}
