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
    /// keys and mouths, and the 100 fps budgets. I4 is the GAUNTLET (SWITCHBACK.md "The I4 gauntlet"): zig-zag keys,
    /// plugged tunnels, sealed eyes, no bore, the rays; the I4 of before it (gauntlet off, Scarab Rally) is held too.
    /// Headless outside Unity: <c>bash Tools/Build/switchback_sim_harness/run.sh tests</c>.
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
            var s = SwitchbackCourseSettings.ForIntensity(intensity);
            s.GateCount = Gates;
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            s.FirstGateDistance = 620f;
            return SwitchbackCourse.Generate(seed, s);
        }

        static SwitchbackFeatureLayout Layout(List<RaceGate> course, int intensity, bool gauntlet = true) =>
            SwitchbackBlastRun.Layout(course, intensity, Vector3.zero, ClearRadius, gauntlet);

        static IEnumerable<int> SeedsFor(int intensity)
        {
            for (int s = 1; s <= Seeds; s++) yield return s;
            yield return SwitchbackBlastRun.FixedSeed(intensity);   // the launch panel's FIXED seed
        }

        static IEnumerable<int> WorldSeeds()
        {
            for (int i = 1; i <= 4; i++) yield return SwitchbackBlastRun.FixedSeed(i);
        }

        [Test]
        public void FixedSeedsAreTheKnownCourses()
        {
            // The launch panel's SEED: FIXED courses kept their numbers when the fun types went (2026-10-29).
            for (int i = 1; i <= 4; i++) Assert.AreEqual(45_500 + i, SwitchbackBlastRun.FixedSeed(i));
            Assert.AreEqual(45_501, SwitchbackBlastRun.FixedSeed(0), "clamped low");
            Assert.AreEqual(45_504, SwitchbackBlastRun.FixedSeed(9), "clamped high");
        }

        [Test]
        public void TheLayoutIsDeterministic([Values(1, 2, 3, 4)] int intensity)
        {
            foreach (int seed in new[] { 11, SwitchbackBlastRun.FixedSeed(intensity) })
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
            Assert.Greater(stations[3], stations[1], "I4: two or three keys a gate");
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
                    // On the leg's line; an I4 gauntlet's earlier keys step off it by their zig-zag.
                    float off = SwitchbackBlastRun.GauntletOffset(st.Stage, st.Stages) * SwitchbackBlastRun.GauntletZigZag;
                    Assert.Less(SwitchbackFeatures.DistanceToSegment(st.Key, a, b), Mathf.Abs(off) + 0.01f, $"seed {seed}: key off its leg");
                    if (st.Stage == 0)
                        Assert.That(st.KeyLead, Is.InRange(SwitchbackBlastRun.KeyMinLead, SwitchbackBlastRun.KeyLeadMax));
                    else
                        Assert.That(st.KeyLead, Is.InRange(SwitchbackBlastRun.GauntletFinalLead - 0.01f, SwitchbackBlastRun.KeyLeadMax));
                    // The blast's shortest reach (400 u) passes the door from the key, and its target (the door, or the next key).
                    Assert.Less((st.Door - st.Key).magnitude, 400f, $"seed {seed}: door beyond the blast");
                    Assert.Less((st.Target - st.Key).magnitude, 400f, $"seed {seed}: target beyond the blast");
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
            Assert.AreEqual(DoorCount(intensity) * Doored(L) + world, L.Prisms.Count, "doors, then the fields");
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
                int doorPrisms = DoorCount(intensity) * Doored(L);
                for (int i = doorPrisms; i < L.Prisms.Count; i++)
                    for (int g = 0; g < c.Count; g++)
                        Assert.GreaterOrEqual((L.Prisms[i].Position - c[g].Position).magnitude, c[g].Radius,
                                              $"seed {seed}: prism {i} inside gate {g}'s ring");
            }
        }

        /// <summary>No field prism - laid, or anywhere on its mover's cycle - comes inside the bore round the leg's line,
        /// so the eye is always reachable down a straight line (the AI's lane). I1-I3, and I4 without the gauntlet (Scarab
        /// Rally's): the gauntlet has no bore - its way through is blasted (<see cref="TheGauntletRouteIsPluggedNotFilled"/>).</summary>
        [Test]
        public void TheBoreStaysClear([Values(1, 2, 3, 4)] int intensity)
        {
            bool gauntlet = intensity < 4;
            float bore = SwitchbackBlastRun.BoreRadius(intensity, gauntlet);
            Assert.GreaterOrEqual(bore, BreakwaterCourseSettings.EyeRadius);
            foreach (int seed in SeedsFor(intensity))
            {
                var c = Course(intensity, seed);
                var L = Layout(c, intensity, gauntlet);
                for (int step = 0; step < 8; step++)
                {
                    var posed = PosedAt(L, step * 0.37f);
                    int first = DoorCount(intensity, gauntlet) * Doored(L);
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
            foreach (int seed in new[] { 3, 7, SwitchbackBlastRun.FixedSeed(intensity) })
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
                int single = SwitchbackBlastRun.DoorPrisms(intensity);
                var clips = ClipPairs(L.Prisms, single, DoorCount(intensity) * Doored(L));
                Assert.IsEmpty(clips, $"seed {seed}: {clips.Count} clipping pairs, first {(clips.Count > 0 ? clips[0].ToString() : "")}");
            }
        }

        /// <summary>Movers never touch anything over their cycle: every moving prism, posed at 16 times through its
        /// period, against everything else posed at the same time.</summary>
        [Test]
        public void MoversNeverClip([Values(2, 3, 4)] int intensity)
        {
            foreach (int seed in new[] { 1, 2, 5, SwitchbackBlastRun.FixedSeed(intensity) })
            {
                var L = Layout(Course(intensity, seed), intensity);
                if (L.Movers.Count == 0) continue;
                float T = 0f;
                foreach (var m in L.Movers) T = Mathf.Max(T, Period(m));
                int single = SwitchbackBlastRun.DoorPrisms(intensity);
                for (int k = 0; k < 16; k++)
                {
                    var posed = PosedAt(L, k * T / 16f + 0.11f);
                    var clips = ClipPairs(posed, single, DoorCount(intensity) * Doored(L));
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
            bool frame = false, jack = false, hoop = false, ziggurat = false;
            foreach (int seed in SeedsFor(intensity))
            {
                var L = Layout(Course(intensity, seed), intensity);
                int at = DoorCount(intensity) * Doored(L);
                foreach (var st in L.Stations)
                {
                    // The field only: past the station's tunnel (I4).
                    for (int i = at + st.TunnelPrisms; i < at + st.WorldPrisms; i++)
                    {
                        Vector3 s = L.Prisms[i].Scale;
                        float lo = Mathf.Min(s.x, Mathf.Min(s.y, s.z)), hi = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                        float mid = s.x + s.y + s.z - lo - hi;
                        if (Mathf.Approximately(lo, hi) && hi <= 14f && hi >= 8f) smallCube = true;
                        if (Mathf.Approximately(lo, hi) && hi >= 40f) bigCube = true;
                        if (hi - lo > 10f) cuboid = true;
                        if (Mathf.Approximately(lo, 6.2f) && Mathf.Approximately(hi, 6.2f)) sphere = true;
                        if (Mathf.Approximately(lo, 8.4f) && Mathf.Approximately(hi, 8.4f)) bigSphere = true;
                        if (Mathf.Approximately(s.z, 6f) && hi >= 69f) beam = true;   // beams run across the leg
                        if (Mathf.Approximately(lo, 4f) && Mathf.Approximately(mid, 4f) && Mathf.Approximately(hi, 46f)) frame = true;
                        if (Mathf.Approximately(lo, 5f) && Mathf.Approximately(mid, 5f) && Mathf.Approximately(hi, 18f)) jack = true;
                        if (Mathf.Approximately(lo, 4f) && Mathf.Approximately(mid, 6f) && hi > 7f && hi < 8f) hoop = true;
                        if (Mathf.Approximately(lo, 4f) && Mathf.Approximately(mid, 34f) && Mathf.Approximately(hi, 34f)) ziggurat = true;
                    }
                    at += st.WorldPrisms;
                }
            }
            Assert.IsTrue(smallCube && bigCube && cuboid && sphere, $"I{intensity}: {smallCube} {bigCube} {cuboid} {sphere}");
            Assert.AreEqual(intensity >= 2, bigSphere, "big spheres from I2");
            Assert.AreEqual(intensity >= 3, beam, "beams from I3");
            Assert.AreEqual(intensity >= 4, frame && jack && hoop && ziggurat,
                            $"I{intensity}: frames {frame}, jacks {jack}, hoops {hoop}, ziggurats {ziggurat} (I4 only)");
        }

        // ── the I4 gauntlet (SWITCHBACK.md "The I4 gauntlet") ─────────────

        /// <summary>An I4 blocked gate is a row of keys (two or three; one only where no zig-zag fits): consecutive stations
        /// upstream first, each aimed at the next key and the last at the door, the last on the leg's line
        /// <see cref="SwitchbackBlastRun.GauntletFinalLead"/> out, and every key's segment turned from the one before it -
        /// so each blast wants the nose swung onto the next segment as the key is taken (a drift). Every key has a tunnel.</summary>
        [Test]
        public void TheGauntletZigZagsKeyToKeyToTheDoor()
        {
            int gates = 0, keys = 0, oneKey = 0;
            float minTurn = float.MaxValue;
            foreach (int seed in SeedsFor(4))
            {
                var c = Course(4, seed);
                var L = Layout(c, 4);
                var st = L.Stations;
                for (int s = 0; s < st.Count; s++)
                {
                    Assert.AreNotEqual(BlastRunTunnel.None, st[s].Tunnel, $"seed {seed}: an I4 key without a tunnel");
                    Assert.AreEqual(s > 0 && st[s - 1].Gate == st[s].Gate ? st[s - 1].Stage + 1 : 0, st[s].Stage, $"seed {seed}: keys out of order");
                    if (!st[s].Final)
                    {
                        Assert.AreEqual(st[s].Gate, st[s + 1].Gate);
                        Assert.IsTrue(st[s].Target.Equals(st[s + 1].Key), $"seed {seed}: a key not aimed at the next");
                        Assert.Greater(st[s].KeyLead, st[s + 1].KeyLead, $"seed {seed}: keys not in order down the leg");
                        continue;
                    }
                    gates++;
                    keys += st[s].Stages;
                    if (st[s].Stages == 1) { oneKey++; continue; }
                    Assert.IsTrue(st[s].Target.Equals(st[s].Door), $"seed {seed}: the last key not aimed at the door");
                    Assert.AreEqual(SwitchbackBlastRun.GauntletFinalLead, st[s].KeyLead, 0.01f);
                    Vector3 a = c[st[s].Gate - 1].Position, b = c[st[s].Gate].Position;
                    Assert.Less(SwitchbackFeatures.DistanceToSegment(st[s].Key, a, b), 0.01f, $"seed {seed}: the last key off the line");
                    // Every turn at a key: the leg (or the segment before) into it, against its own segment.
                    for (int j = s - st[s].Stage; j <= s; j++)
                    {
                        Vector3 into = st[j].Stage == 0 ? (b - a).normalized : (st[j].Key - st[j - 1].Key).normalized;
                        minTurn = Mathf.Min(minTurn, Vector3.Angle(into, st[j].Target - st[j].Key));
                    }
                }
            }
            Assert.Greater(gates, 0);
            Assert.GreaterOrEqual(keys, 2 * gates, "about two keys a gate or more");
            Assert.Less(oneKey, 0.05f * gates, $"{oneKey} of {gates} gauntlet gates fell back to one key");
            Assert.Greater(minTurn, SwitchbackBlastPlanner.DriftFreeDegrees * 0.5f,
                           $"a gauntlet key turns only {minTurn:F1} deg: no drift needed there");
        }

        /// <summary>The way through is blasted, never open: every tunnel's walls stand outside the flight tube
        /// (<see cref="SwitchbackBlastRun.TunnelRadius"/>), plug walls fill it (most tunnels at least two), and no field
        /// prism enters any route segment's tube - so the field fills round the route, and the route holds only plugs.</summary>
        [Test]
        public void TheGauntletRouteIsPluggedNotFilled()
        {
            int tunnels = 0, plugged = 0;
            foreach (int seed in SeedsFor(4))
            {
                var c = Course(4, seed);
                var L = Layout(c, 4);
                int at = DoorCount(4) * Doored(L);
                foreach (var st in L.Stations)
                {
                    Vector3 ax = (st.Target - st.Key).normalized;
                    var plugDepths = new HashSet<int>();
                    for (int i = at; i < at + st.TunnelPrisms; i++)
                    {
                        var p = L.Prisms[i];
                        float along = Vector3.Dot(p.Position - st.Key, ax);
                        Across(p, st.Key, ax, out float near, out float far);
                        if (far < SwitchbackBlastRun.TunnelRadius)
                            plugDepths.Add(Mathf.RoundToInt(along));   // a plug cube: wholly inside the tube
                        else Assert.GreaterOrEqual(near, SwitchbackBlastRun.TunnelRadius - 0.05f,
                                                   $"seed {seed} gate {st.Gate}: a {st.Tunnel} wall inside the flight tube");
                    }
                    if (st.TunnelPrisms > 0) { tunnels++; if (plugDepths.Count >= 2 || st.Final && plugDepths.Count >= 1) plugged++; }
                    at += st.WorldPrisms;
                }
                // The field (every prism past a station's tunnel) keeps out of every route segment's tube.
                at = DoorCount(4) * Doored(L);
                foreach (var st in L.Stations)
                {
                    for (int i = at + st.TunnelPrisms; i < at + st.WorldPrisms; i++)
                        foreach (var rt in L.Stations)
                            Assert.GreaterOrEqual(SwitchbackFeatures.DistanceToSegment(L.Prisms[i].Position, rt.Key, rt.Target),
                                                  SwitchbackBlastRun.TunnelRadius, $"seed {seed}: field prism {i} in gate {rt.Gate}'s route");
                    at += st.WorldPrisms;
                }
            }
            Assert.Greater(tunnels, 0);
            Assert.GreaterOrEqual(plugged, 0.8f * tunnels, $"only {plugged} of {tunnels} tunnels are plugged");
        }

        /// <summary>Every I4 door's eye is SEALED: a 3 x 3 of cubes inside the 18 u eye, its middle a Danger heart, clear of the
        /// door's own bars and collar; I4 without the gauntlet (Scarab Rally's) hangs no door at all.</summary>
        [Test]
        public void TheEyesAreSealed()
        {
            float port = SwitchbackBlastRun.PortFor(4);
            Assert.AreEqual(0, SwitchbackBlastRun.DoorPrisms(4, false), "I4 without the gauntlet hangs no door (the sight line)");
            int open4 = 0;
            SwitchbackBlastRun.EmitDoorPrisms(Vector3.zero, Vector3.forward, port, false, (p, r, s, k) => open4++);
            Assert.AreEqual(open4 + SwitchbackBlastRun.SealPrisms, SwitchbackBlastRun.DoorPrisms(4));
            Assert.AreEqual(SwitchbackBlastRun.DoorPrisms(3), SwitchbackBlastRun.DoorPrisms(3, false), "no seal below I4");
            var door = new List<SwitchbackFeaturePrism>();
            Vector3 at = new(10f, -40f, 300f), axis = new Vector3(0.3f, 0.2f, 1f).normalized;
            SwitchbackBlastRun.EmitDoorPrisms(at, axis, port, true, (p, r, s, k) => door.Add(new SwitchbackFeaturePrism(p, r, s, k)));
            int bars = door.Count - SwitchbackBlastRun.SealPrisms, danger = 0;
            for (int i = bars; i < door.Count; i++)
            {
                var seal = door[i];
                if (seal.Kind == PrismKind.Danger) danger++;
                Across(seal, at, axis, out _, out float far);
                Assert.Less(far, BreakwaterCourseSettings.EyeRadius, "a seal cube reaches out of the eye");
                for (int j = 0; j < bars; j++)
                    Assert.IsFalse(Overlap(seal, door[j], 0.05f), $"seal cube {i - bars} clips door prism {j}");
            }
            // The seal is plain since 2026-11-13: its Danger heart made every missed blast two dead stops a gate
            // (Docs/ElementalLoop/DOLPHIN_TIME_LADDER.md). Ramming it still costs a plain hit per cube.
            Assert.AreEqual(0, danger, "a seal has no Danger heart");
        }

        [Test]
        public void TheTunnelsMixKinds()
        {
            var seen = new HashSet<BlastRunTunnel>();
            foreach (int seed in SeedsFor(4))
                foreach (var st in Layout(Course(4, seed), 4).Stations)
                    if (st.TunnelPrisms > 0) seen.Add(st.Tunnel);
            foreach (BlastRunTunnel k in System.Enum.GetValues(typeof(BlastRunTunnel)))
                if (k != BlastRunTunnel.None) Assert.IsTrue(seen.Contains(k), $"no {k} tunnel was laid");
        }

        /// <summary>The rays light the gauntlet's way and nothing else: a lead-in to each gate's first key, every key to its
        /// target, the door on through the ring; nothing below I4, nothing without the gauntlet.</summary>
        [Test]
        public void TheRaysFollowTheRoute([Values(1, 2, 3, 4)] int intensity)
        {
            var c = Course(intensity, SwitchbackBlastRun.FixedSeed(intensity));
            var L = Layout(c, intensity);
            var rays = new List<Vector3>();
            SwitchbackBlastRun.RaySegments(L, c, rays);
            if (intensity < 4)
            {
                Assert.IsEmpty(rays, "rays below I4");
                return;
            }
            SwitchbackBlastRun.RaySegments(Layout(c, 4, false), c, rays);
            Assert.IsEmpty(rays, "rays without the gauntlet");
            SwitchbackBlastRun.RaySegments(L, c, rays);
            Assert.AreEqual(0, rays.Count % 2);
            foreach (var st in L.Stations)
            {
                bool found = false;
                for (int i = 0; i + 1 < rays.Count && !found; i += 2)
                    found = rays[i].Equals(st.Key) && rays[i + 1].Equals(st.Target);
                Assert.IsTrue(found, $"gate {st.Gate} key {st.Stage}: no ray to its target");
            }
            for (int i = 0; i + 1 < rays.Count; i += 2)
                Assert.Greater((rays[i + 1] - rays[i]).magnitude, 1f, "a ray of no length");
        }

        /// <summary>A ray is a lit, faceted volume, never a flat line: <see cref="SwitchbackRayMesh.Sides"/> flat side faces
        /// and two caps, every triangle wound to face out along its own (flat) normal, nothing for a ray of no length.</summary>
        [Test]
        public void ARayIsAFacetedVolumeFacingOut()
        {
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var tris = new List<int>();
            Vector3 a = new(5f, -20f, 300f), b = new(60f, 10f, 180f);
            int n = SwitchbackRayMesh.AppendBeam(a, b, 1.2f, verts, normals, tris);
            Assert.AreEqual(SwitchbackRayMesh.Sides * 4, n, "two triangles a face and one a cap at each end, per facet");
            Assert.AreEqual(3 * n, tris.Count);
            Vector3 axis = (b - a).normalized;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 p0 = verts[tris[t]], p1 = verts[tris[t + 1]], p2 = verts[tris[t + 2]];
                Vector3 nrm = normals[tris[t]];
                Assert.IsTrue(nrm.Equals(normals[tris[t + 1]]) && nrm.Equals(normals[tris[t + 2]]), "a facet is flat-shaded");
                Assert.Greater(Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), nrm), 0f, "a triangle winds away from its normal");
                Vector3 c = (p0 + p1 + p2) / 3f, w = c - a;
                Vector3 outward = w - axis * Vector3.Dot(w, axis);
                bool cap = Mathf.Abs(Vector3.Dot(nrm, axis)) > 0.99f;
                if (cap) Assert.Greater(Vector3.Dot(nrm, Vector3.Dot(w, axis) > 0.5f * (b - a).magnitude ? axis : -axis), 0.99f, "a cap faces in");
                else Assert.Greater(Vector3.Dot(nrm, outward.normalized), 0.8f, "a side faces in");
            }
            Assert.AreEqual(0, SwitchbackRayMesh.AppendBeam(a, a + Vector3.up * 0.1f, 1.2f, verts, normals, tris), "a ray of no length");
            Assert.AreEqual(0, SwitchbackRayMesh.AppendBeam(a, b, 0f, verts, normals, tris), "a ray of no width");
        }

        /// <summary>Without the gauntlet (a mode reusing Switchback's course for a hull without the key blast: Scarab
        /// Rally) I4 is the Fortress of before 2026-11-03: one key a gate on the line, open eyes, the 18 u bore, its own
        /// budgets. I1-I3 do not depend on the switch.</summary>
        /// <summary>Scarab Rally's I4 (no gauntlet, its own course): from anywhere inside the ring you leave, every point of
        /// the next ring's rim is in plain sight - no prism, door or field, laid or on its mover's cycle, crosses the line
        /// (Garrett, 2026-11-14: "respect sight lines from one ring to another so that prisms are not occluding which ring
        /// is lime green from the perspective of leaving a ring").</summary>
        [Test]
        public void WithoutTheGauntlet_TheNextRingIsInSightFromTheRingYouLeave()
        {
            int blocked = 0, rays = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var s = ScarabRallyCourse.ForIntensity(4);
                s.GateCount = 24; s.InnerRadius = Inner; s.OuterRadius = Outer;
                s.FirstGateDirection = Vector3.up; s.FirstGateDistance = 620f;
                var c = SwitchbackCourse.Generate(seed, s);
                if (c == null || c.Count < 3) continue;
                var L = Layout(c, 4, false);
                for (int step = 0; step < 4; step++)
                {
                    var posed = PosedAt(L, step * 0.61f);
                    for (int g = 1; g < c.Count; g++)
                    {
                        RaceGate from = c[g - 1], to = c[g];
                        SwitchbackBlastRun.Frame(from.Axis, out Vector3 a1, out Vector3 a2);
                        SwitchbackBlastRun.Frame(to.Axis, out Vector3 b1, out Vector3 b2);
                        for (int i = 0; i < 5; i++)
                        {
                            float ai = i * 0.4f * Mathf.PI;
                            Vector3 eye = i == 4 ? from.Position
                                : from.Position + (a1 * Mathf.Cos(ai) + a2 * Mathf.Sin(ai)) * (0.5f * from.Radius);
                            for (int j = 0; j < 8; j++)
                            {
                                float bj = j * 0.25f * Mathf.PI;
                                Vector3 rim = to.Position + (b1 * Mathf.Cos(bj) + b2 * Mathf.Sin(bj)) * to.Radius;
                                rays++;
                                foreach (var p in posed)
                                    if (SegmentHitsBox(eye, rim, p, 0.5f)) { blocked++; break; }
                            }
                        }
                    }
                }
            }
            Assert.Greater(rays, 1000);
            Assert.AreEqual(0, blocked, $"{blocked} of {rays} sight lines from a ring to the next one's rim are blocked");
        }

        /// <summary>Every door hangs wholly BEHIND its ring (Garrett, 2026-11-14, Docs/Reports/SIGHT_LINES.md): each door
        /// prism's box lies past the ring's plane, so no line from upstream to the ring's rim, which lies in that plane, can
        /// pass through a door. In front, the plug (wider than the ring) hid the lime ring on 12-67% of legs at I2-I4.</summary>
        [Test]
        public void TheDoorsHangBehindTheirRing([Values(1, 2, 3, 4)] int intensity)
        {
            int doors = 0;
            foreach (int seed in SeedsFor(intensity))
            {
                var c = Course(intensity, seed);
                if (c == null) continue;
                var L = Layout(c, intensity);
                int world = 0;
                foreach (var st in L.Stations) world += st.WorldPrisms;
                int doorPrisms = L.Prisms.Count - world;
                for (int k = 0; k < doorPrisms; k++)
                {
                    var p = L.Prisms[k];
                    // the nearest ring: a door hangs DoorSetback (or SecondDoorSetback) past its own
                    BlastRunStation own = default; float best = float.MaxValue;
                    foreach (var st in L.Stations)
                    {
                        float dd = (p.Position - c[st.Gate].Position).sqrMagnitude;
                        if (dd < best) { best = dd; own = st; }
                    }
                    Vector3 axis = own.FlowAxis, ring = c[own.Gate].Position;
                    Vector3 h = p.Scale * 0.5f;
                    float reach = Mathf.Abs(Vector3.Dot(p.Rotation * new Vector3(h.x, 0f, 0f), axis))
                                + Mathf.Abs(Vector3.Dot(p.Rotation * new Vector3(0f, h.y, 0f), axis))
                                + Mathf.Abs(Vector3.Dot(p.Rotation * new Vector3(0f, 0f, h.z), axis));
                    Assert.Greater(Vector3.Dot(p.Position - ring, axis) - reach, 0f,
                                   $"I{intensity} seed {seed}: door prism {k} reaches in front of gate {own.Gate}'s ring");
                    doors++;
                }
            }
            Assert.Greater(doors, 0, $"I{intensity}: no doors laid");
        }

        /// <summary>Whether segment a-b passes through prism <paramref name="p"/>'s box shrunk by <paramref name="shrink"/>
        /// on every side (slab test in the box's frame).</summary>
        static bool SegmentHitsBox(Vector3 a, Vector3 b, SwitchbackFeaturePrism p, float shrink)
        {
            Quaternion inv = Quaternion.Inverse(p.Rotation);
            Vector3 la = inv * (a - p.Position), lb = inv * (b - p.Position), d = lb - la;
            Vector3 h = p.Scale * 0.5f - Vector3.one * shrink;
            if (h.x <= 0f || h.y <= 0f || h.z <= 0f) return false;
            float t0 = 0f, t1 = 1f;
            for (int k = 0; k < 3; k++)
            {
                if (Mathf.Abs(d[k]) < 1e-6f) { if (Mathf.Abs(la[k]) > h[k]) return false; continue; }
                float u = (-h[k] - la[k]) / d[k], v = (h[k] - la[k]) / d[k];
                if (u > v) (u, v) = (v, u);
                t0 = Mathf.Max(t0, u); t1 = Mathf.Min(t1, v);
                if (t0 > t1) return false;
            }
            return true;
        }

        [Test]
        public void WithoutTheGauntlet_I4IsTheFortressOfBefore()
        {
            Assert.AreEqual(18f, SwitchbackBlastRun.BoreRadius(4, false));
            Assert.AreEqual(3400, SwitchbackBlastRun.TotalCap(4, false));
            Assert.AreEqual(400, SwitchbackBlastRun.ColliderCap(4, false));
            Assert.AreEqual(140, SwitchbackBlastRun.MovingPrismCap(4, false));
            Assert.AreEqual(5100, SwitchbackBlastRun.TotalCap(4), "the gauntlet's I4: 1.5x");
            Assert.AreEqual(600, SwitchbackBlastRun.ColliderCap(4));
            foreach (int seed in new[] { 3, 11, SwitchbackBlastRun.FixedSeed(4) })
            {
                var c = Course(4, seed);
                var L = Layout(c, 4, false);
                Assert.LessOrEqual(L.Prisms.Count, SwitchbackBlastRun.TotalCap(4, false));
                foreach (var st in L.Stations)
                {
                    Assert.AreEqual(1, st.Stages);
                    Assert.AreEqual(BlastRunTunnel.None, st.Tunnel);
                    Assert.IsTrue(st.Target.Equals(st.Door));
                }
                for (int i = 1; i <= 3; i++)
                {
                    var ci = Course(i, seed);
                    Assert.AreEqual(Layout(ci, i).Prisms.Count, Layout(ci, i, false).Prisms.Count, $"I{i} depends on the switch");
                }
            }
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

        /// <summary>The prisms a blocked gate's door(s) lay: two doors at I4, each sealed where the gauntlet is.</summary>
        static int DoorCount(int intensity, bool gauntlet = true) =>
            SwitchbackBlastRun.DoorPrisms(intensity, gauntlet) * (intensity >= 4 ? 2 : 1);

        /// <summary>The gates that carry doors: one per gate, on its last station.</summary>
        static int Doored(SwitchbackFeatureLayout L)
        {
            int n = 0;
            foreach (var st in L.Stations) if (st.Final) n++;
            return n;
        }

        /// <summary>The nearest and farthest a prism's box comes to the line through <paramref name="origin"/> along
        /// <paramref name="axis"/>, for a box whose own z runs along that line (every tunnel piece, plug and seal cube):
        /// exact, from its cross-section rectangle.</summary>
        static void Across(in SwitchbackFeaturePrism p, Vector3 origin, Vector3 axis, out float near, out float far)
        {
            Assert.Greater(Mathf.Abs(Vector3.Dot(p.Rotation * Vector3.forward, axis)), 0.999f, "the piece is not square to its axis");
            Vector3 w = p.Position - origin;
            w -= axis * Vector3.Dot(w, axis);
            float x = Mathf.Abs(Vector3.Dot(w, p.Rotation * Vector3.right)), y = Mathf.Abs(Vector3.Dot(w, p.Rotation * Vector3.up));
            float hx = 0.5f * p.Scale.x, hy = 0.5f * p.Scale.y;
            float qx = Mathf.Max(x - hx, 0f), qy = Mathf.Max(y - hy, 0f);
            near = Mathf.Sqrt(qx * qx + qy * qy);
            far = Mathf.Sqrt((x + hx) * (x + hx) + (y + hy) * (y + hy));
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
