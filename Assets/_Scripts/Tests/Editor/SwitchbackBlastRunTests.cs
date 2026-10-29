using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// BLAST RUN (<see cref="SwitchbackBlastRun"/>, SWITCHBACK.md "Blast Run"): blocked gates with a key crystal, a
    /// Breakwater door and a 3D block field on the leg in, with clock-driven sliders (I2+), rotors (I3+), opposed
    /// slider pairs and a second door (I4). Swept over the shipped shell and the picker's own seeds (45_501-45_504),
    /// like SwitchbackFeaturesTests: determinism, the open bore, no clipping (laid, and movers over a cycle), the
    /// keys and mouths, and the 100 fps budgets.
    /// </summary>
    public class SwitchbackBlastRunTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Gates = 20;
        const int Seeds = 60;
        const float ClearRadius = Inner / 1.22f + 30f;

        static List<RaceGate> Course(int intensity, int seed)
        {
            var s = SwitchbackCourseSettings.ForIntensity(intensity, SwitchbackFunType.BlastRun);
            s.GateCount = Gates;
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            s.FirstGateDistance = 620f;
            return SwitchbackCourse.Generate(seed, s);
        }

        static SwitchbackFeatureLayout Layout(List<RaceGate> course, int intensity) =>
            SwitchbackBlastRun.Layout(course, intensity, Vector3.zero, ClearRadius);

        static IEnumerable<int> SeedsFor(int intensity)
        {
            for (int s = 1; s <= Seeds; s++) yield return s;
            yield return SwitchbackFunTypes.WorldSeed(SwitchbackFunType.BlastRun, intensity);   // the picker's world
        }

        static IEnumerable<int> WorldSeeds()
        {
            for (int i = 1; i <= 4; i++) yield return SwitchbackFunTypes.WorldSeed(SwitchbackFunType.BlastRun, i);
        }

        [Test]
        public void TheCourseIsClassics([Values(1, 2, 3, 4)] int intensity)
        {
            var a = SwitchbackCourseSettings.ForIntensity(intensity, SwitchbackFunType.BlastRun);
            var b = SwitchbackCourseSettings.ForIntensity(intensity, SwitchbackFunType.Baseline);
            Assert.AreEqual(b.RingRadius, a.RingRadius);
            var ca = Course(intensity, 7);
            var s = SwitchbackCourseSettings.ForIntensity(intensity);
            s.GateCount = Gates; s.InnerRadius = Inner; s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up; s.FirstGateDistance = 620f;
            var cb = SwitchbackCourse.Generate(7, s);
            Assert.AreEqual(cb.Count, ca.Count);
            for (int g = 0; g < ca.Count; g++) Assert.AreEqual(cb[g].Position, ca[g].Position, $"gate {g}");
        }

        [Test]
        public void TheLayoutIsDeterministic([Values(1, 2, 3, 4)] int intensity)
        {
            foreach (int seed in new[] { 11, SwitchbackFunTypes.WorldSeed(SwitchbackFunType.BlastRun, intensity) })
            {
                var c = Course(intensity, seed);
                var a = Layout(c, intensity);
                var b = Layout(c, intensity);
                Assert.AreEqual(a.Prisms.Count, b.Prisms.Count);
                for (int i = 0; i < a.Prisms.Count; i++)
                {
                    Assert.IsTrue(a.Prisms[i].Position.Equals(b.Prisms[i].Position), $"seed {seed} prism {i}");
                    Assert.IsTrue(a.Prisms[i].Scale.Equals(b.Prisms[i].Scale), $"seed {seed} prism {i}");
                    Assert.AreEqual(a.Prisms[i].Kind, b.Prisms[i].Kind);
                }
                Assert.AreEqual(a.Movers.Count, b.Movers.Count);
                for (int i = 0; i < a.Movers.Count; i++)
                {
                    Assert.AreEqual(a.Movers[i].FirstPrism, b.Movers[i].FirstPrism);
                    Assert.AreEqual(a.Movers[i].Phase, b.Movers[i].Phase);
                    Assert.AreEqual(a.Movers[i].Omega, b.Movers[i].Omega);
                }
                Assert.AreEqual(0, a.MovingClusters.Count, "Blast Run has no Plan 2 sweep clusters");
            }
        }

        [Test]
        public void StationsFollowTheLadder([Values(1, 2, 3, 4)] int intensity)
        {
            int[] minStations = { 3, 5, 9, 9 };
            foreach (int seed in SeedsFor(intensity))
            {
                var c = Course(intensity, seed);
                var L = Layout(c, intensity);
                Assert.GreaterOrEqual(L.Stations.Count, minStations[intensity - 1], $"seed {seed}");
                foreach (var st in L.Stations)
                {
                    Assert.IsTrue(SwitchbackBlastRun.IsBlocked(st.Gate, intensity), $"seed {seed} gate {st.Gate}");
                    Assert.AreEqual(SwitchbackBlastRun.WorldFor(intensity), st.World);
                    Assert.AreEqual(intensity >= 4, st.DoubleDoor);
                    Assert.LessOrEqual(st.WorldPrisms, SwitchbackBlastRun.StationCap(intensity));
                    Assert.GreaterOrEqual(st.Port, c[st.Gate].Radius, "the plug must cover the ring");
                }
                foreach (var m in L.Movers)
                {
                    Assert.IsTrue(m.Motion != BlastRunMotion.Slide || SwitchbackBlastRun.HasSliders(intensity));
                    Assert.IsTrue(m.Motion != BlastRunMotion.Spin || SwitchbackBlastRun.HasRotors(intensity));
                }
                if (intensity == 1) Assert.IsEmpty(L.Movers, $"seed {seed}: I1 is static");
            }
        }

        /// <summary>Each step up the ladder is harder, averaged over the seeds: a narrower bore and a fuller grid, at
        /// least as many blocked gates as I2 from I3 on, more field prisms per run and more moving groups.</summary>
        [Test]
        public void EveryStepUpTheLadderIsHarder()
        {
            var stations = new float[4];
            var field = new float[4];
            var movers = new float[4];
            for (int i = 1; i <= 4; i++)
            {
                int n = 0;
                foreach (int seed in SeedsFor(i))
                {
                    var L = Layout(Course(i, seed), i);
                    stations[i - 1] += L.Stations.Count;
                    foreach (var st in L.Stations) field[i - 1] += st.WorldPrisms;
                    movers[i - 1] += L.Movers.Count;
                    n++;
                }
                stations[i - 1] /= n;
                field[i - 1] /= n;
                movers[i - 1] /= n;
            }
            for (int i = 1; i < 4; i++)
            {
                Assert.Less(SwitchbackBlastRun.BoreRadius(i + 1), SwitchbackBlastRun.BoreRadius(i));
                Assert.Greater(SwitchbackBlastRun.Fill(i + 1), SwitchbackBlastRun.Fill(i));
                Assert.Greater(field[i], field[i - 1], $"I{i + 1} field prisms per run");
                Assert.Greater(movers[i], movers[i - 1], $"I{i + 1} moving groups");
            }
            Assert.Greater(stations[1], stations[0]);
            Assert.Greater(stations[2], stations[1]);
            Assert.Greater(stations[3], stations[1], "I4 trades a few gates (station spacing) for double doors");
        }

        [Test]
        public void KeysSitOnTheLegInsideTheBlast([Values(1, 2, 3, 4)] int intensity)
        {
            foreach (int seed in SeedsFor(intensity))
            {
                var c = Course(intensity, seed);
                foreach (var st in Layout(c, intensity).Stations)
                {
                    Vector3 a = c[st.Gate - 1].Position, b = c[st.Gate].Position;
                    Assert.Less(SwitchbackFeatures.DistanceToSegment(st.Key, a, b), 0.01f, $"seed {seed}: key off its leg");
                    Assert.That(st.KeyLead, Is.InRange(SwitchbackBlastRun.KeyMinLead, SwitchbackBlastRun.KeyLeadMax));
                    // The blast's shortest reach (400 u) passes the door from the key.
                    Assert.Less((st.Door - st.Key).magnitude, 400f, $"seed {seed}: door beyond the blast");
                    for (int k = 0; k + 1 < c.Count; k++)
                        if (k != st.Gate - 1)
                            Assert.GreaterOrEqual(SwitchbackFeatures.DistanceToSegment(st.Key, c[k].Position, c[k + 1].Position),
                                                  SwitchbackBlastRun.KeyClearance - 0.01f, $"seed {seed}: key near leg {k}");
                }
            }
        }

        [Test]
        public void BudgetsHold([Values(1, 2, 3, 4)] int intensity)
        {
            foreach (int seed in SeedsFor(intensity))
                AssertBudgets(intensity, seed);
            foreach (int seed in WorldSeeds()) AssertBudgets(intensity, seed);
        }

        static void AssertBudgets(int intensity, int seed)
        {
            var L = Layout(Course(intensity, seed), intensity);
            Assert.LessOrEqual(L.Prisms.Count, SwitchbackBlastRun.TotalCap(intensity), $"seed {seed}");
            Assert.LessOrEqual(L.Prisms.Count, SwitchbackBlastRun.LayCapacity, $"seed {seed}");
            Assert.LessOrEqual(MaxWithin(L.Prisms, SwitchbackBlastRun.ColliderRadius), SwitchbackBlastRun.ColliderCap(intensity),
                               $"seed {seed}: live colliders");
            Assert.LessOrEqual(L.Movers.Count, SwitchbackBlastRun.MovingGroupCap(intensity), $"seed {seed}: moving groups");
            int moving = 0;
            foreach (var m in L.Movers) moving += m.Count;
            Assert.LessOrEqual(moving, SwitchbackBlastRun.MovingPrismCap(intensity), $"seed {seed}: moving prisms");
            int worldDanger = 0, world = 0;
            int tail = L.Prisms.Count;
            foreach (var st in L.Stations) world += st.WorldPrisms;
            Assert.AreEqual(DoorCount(intensity) * L.Stations.Count + world, L.Prisms.Count, "doors, then the fields");
            for (int i = tail - world; i < tail; i++) if (L.Prisms[i].Kind == PrismKind.Danger) worldDanger++;
            Assert.LessOrEqual(worldDanger, 0.15f * Mathf.Max(1, world), $"seed {seed}: field danger share");
        }

        [Test]
        public void NothingSitsInAMouthOrTheNucleus([Values(1, 2, 3, 4)] int intensity)
        {
            foreach (int seed in SeedsFor(intensity))
            {
                var c = Course(intensity, seed);
                var L = Layout(c, intensity);
                var own = new HashSet<int>();
                foreach (var p in L.Prisms)
                {
                    Assert.Greater(p.Position.magnitude, ClearRadius - 0.01f, $"seed {seed}: in the nucleus");
                }
                // Every non-door prism keeps out of every ring's mouth (a door hangs over its own ring by design).
                int doorPrisms = DoorCount(intensity) * L.Stations.Count;
                for (int i = doorPrisms; i < L.Prisms.Count; i++)
                    for (int g = 0; g < c.Count; g++)
                        Assert.GreaterOrEqual((L.Prisms[i].Position - c[g].Position).magnitude, c[g].Radius,
                                              $"seed {seed}: prism {i} inside gate {g}'s ring");
            }
        }

        /// <summary>No field prism - laid, or anywhere on its mover's cycle - comes inside the bore round the leg's line,
        /// so the eye is always reachable down a straight line (the AI's lane).</summary>
        [Test]
        public void TheBoreStaysClear([Values(1, 2, 3, 4)] int intensity)
        {
            float bore = SwitchbackBlastRun.BoreRadius(intensity);
            Assert.GreaterOrEqual(bore, BreakwaterCourseSettings.EyeRadius);
            foreach (int seed in SeedsFor(intensity))
            {
                var c = Course(intensity, seed);
                var L = Layout(c, intensity);
                for (int step = 0; step < 8; step++)
                {
                    var posed = PosedAt(L, step * 0.37f);
                    int first = DoorCount(intensity) * L.Stations.Count;
                    foreach (var st in L.Stations)
                    {
                        Vector3 gate = c[st.Gate].Position;
                        Vector3 dir = (gate - c[st.Gate - 1].Position).normalized;
                        SwitchbackBlastRun.Frame(dir, out Vector3 s1, out Vector3 s2);
                        for (int i = first; i < first + st.WorldPrisms; i++)
                        {
                            var p = posed[i];
                            Vector3 min = new(float.MaxValue, float.MaxValue, 0f), max = new(float.MinValue, float.MinValue, 0f);
                            for (int k = 0; k < 8; k++)
                            {
                                Vector3 corner = p.Position + p.Rotation * new Vector3(
                                    ((k & 1) == 0 ? -0.5f : 0.5f) * p.Scale.x, ((k & 2) == 0 ? -0.5f : 0.5f) * p.Scale.y,
                                    ((k & 4) == 0 ? -0.5f : 0.5f) * p.Scale.z);
                                float x = Vector3.Dot(corner - gate, s1), y = Vector3.Dot(corner - gate, s2);
                                min.x = Mathf.Min(min.x, x); min.y = Mathf.Min(min.y, y);
                                max.x = Mathf.Max(max.x, x); max.y = Mathf.Max(max.y, y);
                            }
                            Assert.IsTrue(SwitchbackBlastRun.OutsideBore(min, max, bore - 0.05f),
                                          $"seed {seed} gate {st.Gate}: prism {i} in the bore at step {step}");
                        }
                        first += st.WorldPrisms;
                    }
                }
            }
        }

        /// <summary>A mover is a pure function of the shared clock: at t = 0 it stands as laid, one period later it is
        /// back where it was, the same t gives the same pose, a slider stays on its track and a rotor's prisms keep
        /// their distance from its axis.</summary>
        [Test]
        public void MoversArePureFunctionsOfTime([Values(2, 3, 4)] int intensity)
        {
            int seen = 0;
            foreach (int seed in new[] { 3, 7, SwitchbackFunTypes.WorldSeed(SwitchbackFunType.BlastRun, intensity) })
            {
                var L = Layout(Course(intensity, seed), intensity);
                foreach (var m in L.Movers)
                {
                    seen++;
                    Assert.Greater(Mathf.Abs(m.Omega), 0f);
                    float T = Period(m);
                    for (int k = 0; k < 12; k++)
                    {
                        float t = 17.3f + k * T / 12f;
                        for (int j = 0; j < m.Count; j++)
                        {
                            var p = L.Prisms[m.FirstPrism + j];
                            m.PoseAt(0f, out Quaternion s0, out Vector3 o0);
                            m.Apply(s0, o0, p.Position, p.Rotation, out Vector3 at0, out _);
                            Assert.Less((at0 - p.Position).magnitude, 1e-3f, "t = 0 is the laid pose");
                            m.PoseAt(t, out Quaternion sa, out Vector3 oa);
                            m.Apply(sa, oa, p.Position, p.Rotation, out Vector3 a, out _);
                            m.PoseAt(t, out Quaternion sb, out Vector3 ob);
                            m.Apply(sb, ob, p.Position, p.Rotation, out Vector3 b, out _);
                            Assert.IsTrue(a.Equals(b), "same t, same pose");
                            m.PoseAt(t + T, out Quaternion sc, out Vector3 oc);
                            m.Apply(sc, oc, p.Position, p.Rotation, out Vector3 cyc, out _);
                            Assert.Less((cyc - a).magnitude, 0.05f, "one period later, the same pose");
                            if (m.Motion == BlastRunMotion.Slide)
                                Assert.LessOrEqual(Mathf.Abs(Vector3.Dot(a - m.Pivot, m.Axis)), m.Amplitude + 6f, "on its track");
                            else
                            {
                                float r0 = Vector3.ProjectOnPlane(p.Position - m.Pivot, m.Axis).magnitude;
                                float r1 = Vector3.ProjectOnPlane(a - m.Pivot, m.Axis).magnitude;
                                Assert.AreEqual(r0, r1, 0.01f, "a rotor keeps its radius");
                            }
                        }
                    }
                }
            }
            Assert.Greater(seen, 0, "movers exist from I2");
        }

        [Test]
        public void NoPrismClipsAnother([Values(1, 2, 3, 4)] int intensity)
        {
            var seeds = new List<int>();
            for (int seed = 1; seed <= 12; seed++) seeds.Add(seed);
            seeds.AddRange(WorldSeeds());
            foreach (int seed in seeds)
            {
                var L = Layout(Course(intensity, seed), intensity);
                int perStation = DoorCount(intensity);
                int single = perStation / (intensity >= 4 ? 2 : 1);
                var clips = ClipPairs(L.Prisms, single, perStation * L.Stations.Count);
                Assert.IsEmpty(clips, $"seed {seed}: {clips.Count} clipping pairs, first {(clips.Count > 0 ? clips[0].ToString() : "")}");
            }
        }

        /// <summary>Movers never touch anything over their cycle: every moving prism, posed at 16 times through its
        /// period, against everything else posed at the same time.</summary>
        [Test]
        public void MoversNeverClip([Values(2, 3, 4)] int intensity)
        {
            foreach (int seed in new[] { 1, 2, 5, SwitchbackFunTypes.WorldSeed(SwitchbackFunType.BlastRun, intensity) })
            {
                var L = Layout(Course(intensity, seed), intensity);
                if (L.Movers.Count == 0) continue;
                float T = 0f;
                foreach (var m in L.Movers) T = Mathf.Max(T, Period(m));
                int perStation = DoorCount(intensity);
                int single = perStation / (intensity >= 4 ? 2 : 1);
                for (int k = 0; k < 16; k++)
                {
                    var posed = PosedAt(L, k * T / 16f + 0.11f);
                    var clips = ClipPairs(posed, single, perStation * L.Stations.Count);
                    Assert.IsEmpty(clips, $"seed {seed} t {k}: {clips.Count} clipping pairs, first {(clips.Count > 0 ? clips[0].ToString() : "")}");
                }
            }
        }

        /// <summary>The fields carry the mix Yash asked for: small and big cubes, cuboids (pillars, slabs, panels,
        /// beams from I3) and voxel spheres (big ones from I2).</summary>
        [Test]
        public void TheFieldsMixShapesAndSizes([Values(1, 2, 3, 4)] int intensity)
        {
            bool smallCube = false, bigCube = false, cuboid = false, sphere = false, bigSphere = false, beam = false;
            foreach (int seed in SeedsFor(intensity))
            {
                var L = Layout(Course(intensity, seed), intensity);
                int first = DoorCount(intensity) * L.Stations.Count;
                for (int i = first; i < L.Prisms.Count; i++)
                {
                    Vector3 s = L.Prisms[i].Scale;
                    float lo = Mathf.Min(s.x, Mathf.Min(s.y, s.z)), hi = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                    if (Mathf.Approximately(lo, hi) && hi <= 14f && hi >= 8f) smallCube = true;
                    if (Mathf.Approximately(lo, hi) && hi >= 40f) bigCube = true;
                    if (hi - lo > 10f) cuboid = true;
                    if (Mathf.Approximately(lo, 6.2f) && Mathf.Approximately(hi, 6.2f)) sphere = true;
                    if (Mathf.Approximately(lo, 8.4f) && Mathf.Approximately(hi, 8.4f)) bigSphere = true;
                    if (Mathf.Approximately(s.z, 6f) && hi >= 69f) beam = true;   // beams run across the leg
                }
            }
            Assert.IsTrue(smallCube && bigCube && cuboid && sphere, $"I{intensity}: {smallCube} {bigCube} {cuboid} {sphere}");
            Assert.AreEqual(intensity >= 2, bigSphere, "big spheres from I2");
            Assert.AreEqual(intensity >= 3, beam, "beams from I3");
        }

        [Test]
        public void TheKeyBlastMultiplierOnlyTouchesKeys()
        {
            BlastRunKeyCrystals.Clear();
            Assert.AreEqual(1f, BlastRunKeyCrystals.CoreMultiplierFor(new Vector3(10, 0, 0)));
            BlastRunKeyCrystals.Register(new[] { new Vector3(10, 0, 0) });
            Assert.AreEqual(BlastRunKeyCrystals.KeyCoreMultiplier, BlastRunKeyCrystals.CoreMultiplierFor(new Vector3(11, 1, 0)));
            Assert.AreEqual(1f, BlastRunKeyCrystals.CoreMultiplierFor(new Vector3(40, 0, 0)));
            BlastRunKeyCrystals.Clear();
            Assert.AreEqual(1f, BlastRunKeyCrystals.CoreMultiplierFor(new Vector3(10, 0, 0)));
        }

        // ── helpers ──────────────────────────────────────────────────────

        static int DoorCount(int intensity)
        {
            int n = 0;
            BreakwaterStationBuilder.BuildDoor(new BreakwaterStation(Vector3.zero, Vector3.forward, SwitchbackBlastRun.PortFor(intensity)),
                                               (p, r, s, k) => n++);
            return n * (intensity >= 4 ? 2 : 1);
        }

        /// <summary>The most laid prisms within <paramref name="r"/> of any laid prism (itself included) - exact.</summary>
        public static int MaxWithin(List<SwitchbackFeaturePrism> prisms, float r)
        {
            var grid = new Dictionary<(int, int, int), List<int>>();
            for (int i = 0; i < prisms.Count; i++)
            {
                var k = Cell(prisms[i].Position, r);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(i);
            }
            int best = 0;
            float r2 = r * r;
            for (int i = 0; i < prisms.Count; i++)
            {
                var (cx, cy, cz) = Cell(prisms[i].Position, r);
                int n = 0;
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                    if (grid.TryGetValue((cx + dx, cy + dy, cz + dz), out var l))
                        foreach (int j in l)
                            if ((prisms[j].Position - prisms[i].Position).sqrMagnitude <= r2) n++;
                if (n > best) best = n;
            }
            return best;
        }

        static (int, int, int) Cell(Vector3 p, float size) =>
            (Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size), Mathf.FloorToInt(p.z / size));

        /// <summary>Every prism of every mover, posed at <paramref name="t"/>; the rest as laid.</summary>
        static List<SwitchbackFeaturePrism> PosedAt(SwitchbackFeatureLayout L, float t)
        {
            var posed = new List<SwitchbackFeaturePrism>(L.Prisms);
            foreach (var m in L.Movers)
            {
                m.PoseAt(t, out Quaternion spin, out Vector3 offset);
                for (int j = 0; j < m.Count; j++)
                {
                    var p = L.Prisms[m.FirstPrism + j];
                    m.Apply(spin, offset, p.Position, p.Rotation, out Vector3 pos, out Quaternion rot);
                    posed[m.FirstPrism + j] = new SwitchbackFeaturePrism(pos, rot, p.Scale, p.Kind);
                }
            }
            return posed;
        }

        static float Period(in BlastRunMover m) => 2f * Mathf.PI / Mathf.Abs(m.Omega);

        /// <summary>Pairs of laid prisms whose oriented boxes overlap by more than 0.05 u (15-axis SAT). A door's own
        /// prisms are Breakwater's, flush by its own audit, so pairs inside one door are skipped.</summary>
        static List<(int, int)> ClipPairs(List<SwitchbackFeaturePrism> prisms, int doorGroup, int doorEnd)
        {
            var clips = new List<(int, int)>();
            const float cell = 100f;   // over the widest pair reach: two 94 u beams
            var grid = new Dictionary<(int, int, int), List<int>>();
            for (int i = 0; i < prisms.Count; i++)
            {
                var k = (Mathf.FloorToInt(prisms[i].Position.x / cell), Mathf.FloorToInt(prisms[i].Position.y / cell),
                         Mathf.FloorToInt(prisms[i].Position.z / cell));
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(i);
            }
            for (int i = 0; i < prisms.Count; i++)
            {
                var p = prisms[i].Position;
                int cx = Mathf.FloorToInt(p.x / cell), cy = Mathf.FloorToInt(p.y / cell), cz = Mathf.FloorToInt(p.z / cell);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!grid.TryGetValue((cx + dx, cy + dy, cz + dz), out var l)) continue;
                    foreach (int j in l)
                    {
                        if (j <= i) continue;
                        if (i < doorEnd && j < doorEnd && i / doorGroup == j / doorGroup) continue;
                        if (Overlap(prisms[i], prisms[j], 0.05f)) clips.Add((i, j));
                    }
                }
            }
            return clips;
        }

        static bool Overlap(in SwitchbackFeaturePrism a, in SwitchbackFeaturePrism b, float slack)
        {
            Vector3 d = b.Position - a.Position;
            float ra0 = a.Scale.magnitude * 0.5f, rb0 = b.Scale.magnitude * 0.5f;
            if (d.magnitude > ra0 + rb0) return false;
            var A = new[] { a.Rotation * Vector3.right, a.Rotation * Vector3.up, a.Rotation * Vector3.forward };
            var B = new[] { b.Rotation * Vector3.right, b.Rotation * Vector3.up, b.Rotation * Vector3.forward };
            var ea = a.Scale * 0.5f; var eb = b.Scale * 0.5f;
            float[] EA = { ea.x, ea.y, ea.z }, EB = { eb.x, eb.y, eb.z };
            var axes = new List<Vector3>(15);
            axes.AddRange(A); axes.AddRange(B);
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                Vector3 x = Vector3.Cross(A[i], B[j]);
                if (x.sqrMagnitude > 1e-6f) axes.Add(x.normalized);
            }
            foreach (var ax in axes)
            {
                float ra = 0f, rb = 0f;
                for (int k = 0; k < 3; k++) { ra += EA[k] * Mathf.Abs(Vector3.Dot(A[k], ax)); rb += EB[k] * Mathf.Abs(Vector3.Dot(B[k], ax)); }
                if (Mathf.Abs(Vector3.Dot(d, ax)) >= ra + rb - slack) return false;
            }
            return true;
        }
    }
}
