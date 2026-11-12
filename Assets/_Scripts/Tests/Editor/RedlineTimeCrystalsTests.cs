using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Redline's Time crystals (<see cref="RedlineTimeCrystals"/>, 2026-11-04): none at I1, then one, two and three a lap,
    /// on the line at I2-I3 and inside the apexes at I4, every one clear of every mouth and inside the shell. Swept over
    /// the same 400 seeds as <see cref="RedlineCourseTests"/>.
    /// </summary>
    public class RedlineTimeCrystalsTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        /// <summary>The cell's nucleus body (Nucleus.prefab at scale 400, 391.9 u): a crystal on the line may cross the
        /// course's 480 u keep-out (a chord between two gates dips inside it), never the body.</summary>
        const float NucleusBody = 392f;

        static List<RaceGate> Circuit(int intensity, int seed)
        {
            var s = RedlineCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        static float CornerRadius(List<RaceGate> c, int i)
        {
            int n = c.Count;
            return RaceCourseGeometry.CornerRadius(c[(i - 1 + n) % n].Position, c[i].Position, c[(i + 1) % n].Position);
        }

        [Test]
        public void One_more_crystal_a_level_and_none_at_level_one()
        {
            var into = new List<Vector3>();
            int[] expected = { 0, 0, 1, 2, 3 };
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                Assert.AreEqual(expected[intensity], RedlineTimeCrystals.PerLap(intensity));
                for (int seed = 1; seed <= 20; seed++)
                {
                    RedlineTimeCrystals.Place(Circuit(intensity, seed), intensity, into);
                    Assert.AreEqual(expected[intensity], into.Count, $"i{intensity} seed {seed}");
                }
            }
        }

        [Test]
        public void Every_crystal_is_clear_of_every_mouth_and_inside_the_cell()
        {
            var into = new List<Vector3>();
            for (int intensity = 2; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    RedlineTimeCrystals.Place(c, intensity, into);
                    foreach (var p in into)
                    {
                        foreach (var g in c)
                            Assert.Greater((p - g.Position).magnitude, g.Radius + 40f,
                                $"i{intensity} seed {seed}: a crystal sits in or beside a mouth");
                        float r = p.magnitude;
                        Assert.Greater(r, NucleusBody + 20f, $"i{intensity} seed {seed}: a crystal inside the nucleus");
                        Assert.Less(r, Outer, $"i{intensity} seed {seed}: a crystal outside the membrane");
                    }
                    for (int a = 0; a < into.Count; a++)
                        for (int b = a + 1; b < into.Count; b++)
                            Assert.Greater((into[a] - into[b]).magnitude, 100f, $"i{intensity} seed {seed}: two crystals together");
                }
        }

        [Test]
        public void Levels_two_and_three_lie_on_the_leg_into_a_costing_corner()
        {
            var into = new List<Vector3>();
            for (int intensity = 2; intensity <= 3; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    int n = c.Count;
                    RedlineTimeCrystals.Place(c, intensity, into);
                    var order = new List<int>();
                    for (int i = 0; i < n; i++) order.Add(i);
                    order.Sort((x, y) => CornerRadius(c, x).CompareTo(CornerRadius(c, y)));
                    for (int k = 0; k < into.Count; k++)
                    {
                        int corner = order[k];
                        Vector3 mid = Vector3.Lerp(c[(corner - 1 + n) % n].Position, c[corner].Position,
                                                   RedlineTimeCrystals.OnLineFraction);
                        Assert.Less((into[k] - mid).magnitude, 1e-2f,
                            $"i{intensity} seed {seed}: crystal {k} is not halfway down the leg into corner {corner}");
                    }
                }
        }

        [Test]
        public void Level_four_sits_inside_its_corner()
        {
            // Inside the turn: nearer the line between the gates before and after the corner than the exit leg is.
            var into = new List<Vector3>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Circuit(4, seed);
                int n = c.Count;
                RedlineTimeCrystals.Place(c, 4, into);
                var order = new List<int>();
                for (int i = 0; i < n; i++) order.Add(i);
                order.Sort((x, y) => CornerRadius(c, x).CompareTo(CornerRadius(c, y)));
                for (int k = 0; k < into.Count; k++)
                {
                    int corner = order[k];
                    Vector3 prev = c[(corner - 1 + n) % n].Position, at = c[corner].Position, next = c[(corner + 1) % n].Position;
                    Vector3 onExit = at + (next - at) * RedlineTimeCrystals.ApexAlongExit;
                    Vector3 inside = 0.5f * (prev + next);
                    Assert.Less((into[k] - inside).magnitude, (onExit - inside).magnitude,
                        $"i4 seed {seed}: crystal {k} is set off its exit leg AWAY from the inside of corner {corner}");
                    Assert.AreEqual(RedlineTimeCrystals.ApexInset, (into[k] - onExit).magnitude, 0.5f);
                }
            }
        }

        [Test]
        public void The_field_is_a_pure_function_of_the_course()
        {
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            for (int intensity = 2; intensity <= 4; intensity++)
            {
                RedlineTimeCrystals.Place(Circuit(intensity, 777), intensity, a);
                RedlineTimeCrystals.Place(Circuit(intensity, 777), intensity, b);
                Assert.AreEqual(a.Count, b.Count);
                for (int i = 0; i < a.Count; i++) Assert.AreEqual(0f, (a[i] - b[i]).magnitude, 1e-5f);
            }
        }
    }
}
