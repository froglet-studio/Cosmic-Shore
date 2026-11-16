using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Spearline's seals (SPEARLINE.md): the ladder, the sealed rings, the armour, the connected-hole rule against the
    /// model of what a Swordfish cuts on the way through (<see cref="SpearlineSeals.HullReach"/>,
    /// <see cref="SpearlineSeals.ModelBladeLength"/>, <see cref="SpearlineSeals.ModelStrokeReach"/>), the read the AI
    /// makes, and the worn seal. The same file runs headless through <c>Tools/Build/spearline_harness</c>.
    /// </summary>
    public class SpearlineSealsTests
    {
        static readonly int[] Rungs = { 1, 2, 3, 4 };

        static RaceGate Gate(float radius) =>
            new(new Vector3(100f, -40f, 700f), new Vector3(0.3f, 0.2f, 1f).normalized, radius);

        /// <summary>One laid seal, its neighbours and its armour flags, as the controller lays them.</summary>
        sealed class Pane
        {
            public RaceGate Gate;
            public float Mouth;
            public readonly List<Vector3> At = new();
            public int[][] Adjacency;
            public bool[] Armour;
            public Vector3 U, V;
            public int Count => At.Count;

            public Pane(int intensity, float armourAngle = 0f, bool armoured = false)
            {
                var s = SpearlineSeals.ForIntensity(intensity);
                Mouth = s.MouthRadius;
                Gate = SpearlineSealsTests.Gate(Mouth);
                SpearlineSeals.Lay(Gate, Mouth, At);
                Adjacency = SpearlineSeals.Adjacency(At, 0, At.Count);
                SpearlineSeals.Basis(Gate.Axis, out U, out V);
                Armour = new bool[At.Count];
                for (int i = 0; i < At.Count; i++)
                {
                    Vector3 d = At[i] - Gate.Position;
                    Armour[i] = armoured && SpearlineSeals.InArmour(Vector3.Dot(d, U), Vector3.Dot(d, V), armourAngle,
                                                                    s.ArmourDegrees);
                }
            }

            public Vector3 Point(float x, float y) => Gate.Position + U * x + V * y;
            public Vector3 Dir(float angle) => U * Mathf.Cos(angle) + V * Mathf.Sin(angle);
            public float SealRadius => Mouth - SpearlineSeals.RimInset - 0.5f * SpearlineSeals.PrismSize;

            /// <summary>The hole a crossing at <paramref name="c"/> makes with one stroke along <paramref name="band"/>
            /// (zero: none) through this pane as laid (nothing gone yet).</summary>
            public int Hole(Vector3 c, Vector3 band, bool[] goneBefore = null)
            {
                var gone = new bool[Count];
                var breaks = new bool[Count];
                for (int i = 0; i < Count; i++)
                {
                    bool was = goneBefore != null && goneBefore[i];
                    gone[i] = was || (!Armour[i] && SpearlineSeals.StrokeCuts(At[i], c, Gate.Axis.normalized, band,
                                          SpearlineSeals.ModelBladeLength, SpearlineSeals.ModelStrokeReach));
                    breaks[i] = gone[i] || !Armour[i];
                }
                return SpearlineSeals.HoleSize(c, Gate.Axis, At, 0, Adjacency, gone, breaks, new bool[Count], new List<int>());
            }
        }

        /// <summary>Crossings spread over the seal out to <paramref name="share"/> of its radius (centre, then rings).</summary>
        static IEnumerable<(float x, float y, float angle)> Crossings(Pane pane, float share)
        {
            yield return (0f, 0f, 0f);
            for (int ring = 1; ring <= 6; ring++)
            {
                float rho = pane.SealRadius * share * ring / 6f;
                for (int a = 0; a < 24; a++)
                {
                    float ang = 2f * Mathf.PI * (a + 0.37f * ring) / 24f;
                    yield return (rho * Mathf.Cos(ang), rho * Mathf.Sin(ang), ang);
                }
            }
        }

        [Test]
        public void Ladder_is_ordered_toward_harder_in_even_mouth_steps()
        {
            for (int i = 2; i <= 4; i++)
            {
                var a = SpearlineSeals.ForIntensity(i - 1);
                var b = SpearlineSeals.ForIntensity(i);
                Assert.Less(b.MouthRadius, a.MouthRadius, $"I{i} mouth not smaller than I{i - 1}");
                Assert.LessOrEqual(1f - b.MouthRadius / a.MouthRadius, 0.15f, $"I{i - 1} -> I{i} mouth step over 15%");
                Assert.GreaterOrEqual(b.SealedPerLap, a.SealedPerLap, $"I{i} seals fewer rings than I{i - 1}");
                Assert.GreaterOrEqual(b.ArmourDegrees, a.ArmourDegrees, $"I{i} armour narrower than I{i - 1}");
                Assert.LessOrEqual(b.RegrowSeconds, a.RegrowSeconds, $"I{i} regrows slower than I{i - 1}");
            }
            Assert.AreEqual(0f, SpearlineSeals.ForIntensity(1).ArmourDegrees, "I1 is the intro: no armour");
        }

        [Test]
        public void The_hull_alone_never_opens_a_seal()
        {
            foreach (int i in Rungs)
            {
                var pane = new Pane(i);
                int need = SpearlineSeals.ForIntensity(i).NeedPrisms;
                foreach (var c in Crossings(pane, 0.95f))
                    Assert.Less(pane.Hole(pane.Point(c.x, c.y), Vector3.zero), need,
                        $"I{i}: the hull alone opened a seal at ({c.x:0.0}, {c.y:0.0})");
            }
        }

        [Test]
        public void One_stroke_into_a_plain_pane_always_opens_it()
        {
            foreach (int i in Rungs)
            {
                var pane = new Pane(i);
                int need = SpearlineSeals.ForIntensity(i).NeedPrisms;
                foreach (var c in Crossings(pane, 0.75f))
                {
                    // Into the pane: toward the centre, or any way at all from the centre itself.
                    Vector3 band = c.x == 0f && c.y == 0f ? pane.Dir(0.4f) : -pane.Dir(c.angle);
                    Assert.GreaterOrEqual(pane.Hole(pane.Point(c.x, c.y), band), need,
                        $"I{i}: a stroke into the pane at ({c.x:0.0}, {c.y:0.0}) did not open it");
                }
            }
        }

        [Test]
        public void A_stroke_out_of_the_pane_near_its_rim_can_fail()
        {
            // The read at I1 already: swing INTO the pane. Near the rim, a stroke toward the rim cuts little.
            var pane = new Pane(1);
            int need = SpearlineSeals.ForIntensity(1).NeedPrisms;
            int fails = 0, n = 0;
            for (int a = 0; a < 24; a++)
            {
                float ang = 2f * Mathf.PI * a / 24f;
                float rho = pane.SealRadius * 0.92f;
                n++;
                if (pane.Hole(pane.Point(rho * Mathf.Cos(ang), rho * Mathf.Sin(ang)), pane.Dir(ang)) < need) fails++;
            }
            Assert.Greater(fails, n / 3, $"only {fails}/{n} outward strokes at the rim failed - swinging out is as good as in");
        }

        static List<Vector3> Strokes(Pane pane, float roll) =>
            new() { -pane.Dir(roll), pane.Dir(roll), pane.Dir(roll + 0.5f * Mathf.PI) };

        [Test]
        public void The_read_opens_every_armoured_seal_and_a_fixed_habit_does_not()
        {
            foreach (int i in new[] { 2, 3, 4 })
            {
                var s = SpearlineSeals.ForIntensity(i);
                int read = 0, habit = 0, n = 0;
                for (int k = 0; k < 24; k++)
                {
                    float armourAngle = 2f * Mathf.PI * k / 24f + 0.11f;
                    float roll = 0.7f * k;
                    var pane = new Pane(i, armourAngle, armoured: true);
                    var gone = new bool[pane.Count];
                    var breaks = new bool[pane.Count];
                    var cuttable = new bool[pane.Count];
                    for (int p = 0; p < pane.Count; p++) { breaks[p] = !pane.Armour[p]; cuttable[p] = !pane.Armour[p]; }
                    var strokes = Strokes(pane, roll);
                    var plan = SpearlineSeals.Read(pane.Gate, pane.Mouth, pane.Gate.Position, pane.At, 0, pane.Adjacency,
                                                   gone, breaks, cuttable, strokes, s.NeedPrisms, 1,
                                                   SpearlineSeals.ModelBladeLength, SpearlineSeals.ModelStrokeReach,
                                                   new bool[pane.Count], new bool[pane.Count], new List<int>());
                    n++;
                    var band = plan.Stroke >= 0 ? strokes[plan.Stroke] : Vector3.zero;
                    if (plan.Opens && pane.Hole(plan.Crossing, band) >= s.NeedPrisms) read++;
                    // The habit: the centre, the right-hand stroke.
                    if (pane.Hole(pane.Gate.Position, strokes[1]) >= s.NeedPrisms) habit++;
                }
                Assert.AreEqual(n, read, $"I{i}: the read opened only {read}/{n} armoured seals");
                Assert.LessOrEqual(habit, n * 0.7f, $"I{i}: the centre-and-right habit opened {habit}/{n} - nothing to read");
            }
        }

        [Test]
        public void Armour_cracks_under_the_hull_and_does_not_count_until_it_is_gone()
        {
            var pane = new Pane(4, 0f, armoured: true);
            // A crossing deep in the armour: nothing breaks under the hull (every shield stands).
            var c = pane.Point(0.5f * pane.SealRadius, 0f);
            Assert.Less(pane.Hole(c, Vector3.zero), 1, "an intact shield broke under the hull");
            Assert.Less(pane.Hole(c, pane.Dir(Mathf.PI)), SpearlineSeals.ForIntensity(4).NeedPrisms,
                "a stroke through armour opened the seal");
        }

        [Test]
        public void The_worn_seal_lets_anyone_through_in_a_bounded_number_of_passes()
        {
            foreach (int i in Rungs)
            {
                var s = SpearlineSeals.ForIntensity(i);
                int max = SpearlineSeals.MaxPasses(s);
                Assert.LessOrEqual(max, 6, $"I{i}: a pilot could need {max} passes at one seal");
                Assert.AreEqual(0, SpearlineSeals.NeedAfter(s, max - 1), $"I{i}: the need has not worn out by pass {max}");
                for (int p = 1; p < max; p++)
                    Assert.Less(SpearlineSeals.NeedAfter(s, p), SpearlineSeals.NeedAfter(s, p - 1), $"I{i}: pass {p} did not wear");
                // Hull-only passes through the armour's own centre still get through.
                var pane = new Pane(i, 0f, armoured: s.ArmourEvery > 0);
                Assert.GreaterOrEqual(pane.Hole(pane.Gate.Position, Vector3.zero), SpearlineSeals.NeedAfter(s, max - 1));
            }
        }

        static List<RaceGate> Lap(int intensity, int seed)
        {
            var settings = HeadlongCircuitSettings.ForIntensity(intensity);
            settings.RingRadius = SpearlineSeals.ForIntensity(intensity).MouthRadius;
            settings.InnerRadius = 480f;
            settings.OuterRadius = 1080f;
            settings.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, settings);
        }

        [Test]
        public void Sealed_rings_are_the_gentlest_never_ring_zero_and_a_pure_function_of_the_lap()
        {
            foreach (int i in Rungs)
            {
                var s = SpearlineSeals.ForIntensity(i);
                for (int seed = 1; seed <= 40; seed++)
                {
                    var lap = Lap(i, seed);
                    var sealedRings = SpearlineSeals.SealedRings(lap, s);
                    CollectionAssert.AreEqual(sealedRings, SpearlineSeals.SealedRings(Lap(i, seed), s), "not deterministic");
                    Assert.IsFalse(sealedRings[0], "ring 0 is sealed");
                    int count = 0;
                    float hardestSealed = 0f, gentlestOpen = 999f;
                    for (int r = 0; r < lap.Count; r++)
                    {
                        float turn = SpearlineSeals.TurnDegrees(lap, r);
                        if (sealedRings[r]) { count++; hardestSealed = Mathf.Max(hardestSealed, turn); }
                        else if (r != 0) gentlestOpen = Mathf.Min(gentlestOpen, turn);
                    }
                    Assert.AreEqual(s.SealedPerLap, count, $"I{i} seed {seed}: {count} sealed");
                    Assert.LessOrEqual(hardestSealed, gentlestOpen + 1e-3f, $"I{i} seed {seed}: a harder corner was sealed");
                }
            }
        }

        [Test]
        public void The_prism_budget_holds()
        {
            foreach (int i in Rungs)
            {
                var s = SpearlineSeals.ForIntensity(i);
                var pane = new Pane(i);
                Assert.LessOrEqual(pane.Count * s.SealedPerLap, 900, $"I{i}: {pane.Count * s.SealedPerLap} seal prisms a lap");
            }
        }

        [Test]
        public void Armour_faces_a_ring_by_hash_the_same_every_time()
        {
            var at = new Vector3(812f, -133f, 405f);
            Assert.AreEqual(SpearlineSeals.ArmourAngle(3, at), SpearlineSeals.ArmourAngle(3, at));
            var seen = new HashSet<int>();
            for (int r = 0; r < 8; r++) seen.Add(Mathf.FloorToInt(SpearlineSeals.ArmourAngle(r, at) / (Mathf.PI / 4f)));
            Assert.GreaterOrEqual(seen.Count, 4, "the armour faces the same few ways on every ring");
            Assert.IsFalse(SpearlineSeals.InArmour(0f, 0f, 0f, 240f), "the centre is armour");
            Assert.IsTrue(SpearlineSeals.InArmour(10f, 0f, 0f, 120f));
            Assert.IsFalse(SpearlineSeals.InArmour(-10f, 0f, 0f, 120f));
            Assert.IsTrue(SpearlineSeals.InArmour(-10f, 0.1f, Mathf.PI, 10f), "the sector does not wrap at pi");
        }

        [Test]
        public void Neighbours_are_the_hex_ring_and_symmetric()
        {
            var pane = new Pane(1);
            int interior = 0;
            for (int i = 0; i < pane.Count; i++)
            {
                foreach (int j in pane.Adjacency[i])
                    CollectionAssert.Contains(pane.Adjacency[j], i, "adjacency is not symmetric");
                Assert.LessOrEqual(pane.Adjacency[i].Length, 6);
                if (pane.Adjacency[i].Length == 6) interior++;
            }
            Assert.Greater(interior, pane.Count / 2, "most prisms should have six neighbours");
        }
    }
}
