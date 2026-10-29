using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// BLAST RUN (<see cref="SwitchbackBlastRun"/>, SWITCHBACK.md "Blast Run"): blocked gates with a key crystal, a
    /// Breakwater door and a closed-form world on the leg in; sweepers (I2+), snap-trap jaws (I3+), a second door and
    /// hunter packs (I4). Swept over the shipped shell and the picker's own seeds, like SwitchbackFeaturesTests.
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

        /// <summary>Breakwater's densest 200 u neighbourhood per intensity, plus 5 %: Blast Run is never busier.</summary>
        static int DensityCap(int intensity) => new[] { 596, 487, 338, 290 }[intensity - 1];

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
            var c = Course(intensity, 11);
            var a = Layout(c, intensity);
            var b = Layout(c, intensity);
            Assert.AreEqual(a.Prisms.Count, b.Prisms.Count);
            for (int i = 0; i < a.Prisms.Count; i++)
                Assert.AreEqual(a.Prisms[i].Position, b.Prisms[i].Position, $"prism {i}");
            Assert.AreEqual(a.MovingClusters.Count, b.MovingClusters.Count);
            Assert.AreEqual(a.Packs.Count, b.Packs.Count);
        }

        [Test]
        public void StationsFollowTheLadder([Values(1, 2, 3, 4)] int intensity)
        {
            int[] minStations = { 3, 5, 12, 8 };   // I4 loses some to StationSpacing on tight hairpins
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
                    Assert.LessOrEqual(st.WorldPrisms, SwitchbackBlastRun.WorldCap(intensity));
                    Assert.GreaterOrEqual(st.Port, c[st.Gate].Radius, "the plug must cover the ring");
                }
                Assert.AreEqual(intensity >= 4, L.Packs.Count > 0 || L.Stations.Count <= SwitchbackBlastRun.PackOffset,
                                $"seed {seed}: packs {L.Packs.Count}");
            }
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
        public void BudgetsStayUnderBreakwaters([Values(1, 2, 3, 4)] int intensity)
        {
            foreach (int seed in SeedsFor(intensity))
            {
                var L = Layout(Course(intensity, seed), intensity);
                Assert.LessOrEqual(L.Prisms.Count, SwitchbackBlastRun.TotalCap(intensity), $"seed {seed}");
                Assert.LessOrEqual(L.Prisms.Count, SwitchbackBlastRun.LayCapacity, $"seed {seed}");
                Assert.LessOrEqual(MaxWithin(L.Prisms, 200f), DensityCap(intensity), $"seed {seed}");
                int worldDanger = 0, world = 0;
                int tail = L.Prisms.Count;
                foreach (var st in L.Stations) world += st.WorldPrisms;
                for (int i = tail - world; i < tail; i++) if (L.Prisms[i].Kind == PrismKind.Danger) worldDanger++;
                Assert.LessOrEqual(worldDanger, 0.15f * Mathf.Max(1, world), $"seed {seed}: world danger share");
            }
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

        [Test]
        public void JawsSnapShutAndOpenClearOfTheEye([Values(3, 4)] int intensity)
        {
            var c = Course(intensity, 5);
            var L = Layout(c, intensity);
            int jaws = 0;
            foreach (var m in L.MovingClusters)
            {
                if (m.Omega <= 0f) continue;
                jaws++;
                Assert.AreEqual(SwitchbackBlastRun.JawPrisms, m.Count);
                // Over a period the jaw's distance from the axis runs from JawClosed to JawOpen.
                var p0 = L.Prisms[m.FirstPrism];
                float lo = float.MaxValue, hi = float.MinValue;
                Vector3 axisPoint = FindAxisPoint(L, c, m.FirstPrism, out Vector3 sideUnit);
                for (int k = 0; k < 48; k++)
                {
                    Vector3 p = p0.Position + m.DisplacementAt(k * SwitchbackBlastRun.JawPeriod / 48f);
                    float d = Mathf.Abs(Vector3.Dot(p - axisPoint, sideUnit));
                    lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d);
                }
                Assert.AreEqual(SwitchbackBlastRun.JawClosed, lo, 0.2f);
                Assert.AreEqual(SwitchbackBlastRun.JawOpen, hi, 0.2f);
                Assert.Greater(hi - SwitchbackBlastRun.JawPrismScale.x * 0.5f, BreakwaterCourseSettings.EyeRadius,
                               "wide open, the jaws clear the eye");
            }
            Assert.AreEqual(2 * L.Stations.Count, jaws);
        }

        [Test]
        public void HunterPacksAreDangerBodiesAtTheirDens()
        {
            foreach (int seed in SeedsFor(4))
            {
                var c = Course(4, seed);
                var L = Layout(c, 4);
                foreach (var pack in L.Packs)
                {
                    Assert.AreEqual(SwitchbackBlastRun.PackMembers, pack.Members);
                    Assert.AreEqual(SwitchbackBlastRun.PackBodyPrisms, pack.PerMember);
                    for (int i = 0; i < pack.Members * pack.PerMember; i++)
                    {
                        var p = L.Prisms[pack.FirstPrism + i];
                        Assert.AreEqual(PrismKind.Danger, p.Kind);
                        Assert.Less((p.Position - pack.Den).magnitude, 30f, $"seed {seed}: body far from its den");
                    }
                    for (int k = 0; k + 1 < c.Count; k++)
                        Assert.Greater(SwitchbackFeatures.DistanceToSegment(pack.Den, c[k].Position, c[k + 1].Position),
                                       SwitchbackBlastRun.DenClearance, $"seed {seed}: den on leg {k}");
                }
            }
            for (int i = 1; i <= 3; i++) Assert.IsEmpty(Layout(Course(i, 3), i).Packs);
        }

        [Test]
        public void NoPrismClipsAnother([Values(1, 2, 3, 4)] int intensity)
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var L = Layout(Course(intensity, seed), intensity);
                int perStation = DoorCount(intensity);
                int single = perStation / (intensity >= 4 ? 2 : 1);
                var clips = ClipPairs(L.Prisms, single, perStation * L.Stations.Count);
                Assert.IsEmpty(clips, $"seed {seed}: {clips.Count} clipping pairs, first {(clips.Count > 0 ? clips[0].ToString() : "")}");
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

        [Test]
        public void HuntersHoldTheirDenUntilTheLeaderComesNear()
        {
            Vector3 den = new(100, 0, 0);
            // No leader, or a leader beyond sense: home (and already there, so no motion).
            Assert.AreEqual(den, BlastRunHunters.Step(den, den, false, default, 0.1f));
            Vector3 far = den + Vector3.forward * (SwitchbackBlastRun.PackSense + 50f);
            Assert.AreEqual(den, BlastRunHunters.Step(den, den, true, far, 0.1f));
            // A leader in sense: toward it, at PackSpeed.
            Vector3 near = den + Vector3.forward * 200f;
            Vector3 next = BlastRunHunters.Step(den, den, true, near, 0.1f);
            Assert.AreEqual(SwitchbackBlastRun.PackSpeed * 0.1f, Vector3.Distance(den, next), 1e-3f);
            Assert.Greater(Vector3.Dot(next - den, Vector3.forward), 0f);
            // Displaced and the leader gone: back toward the den.
            Vector3 away = den + Vector3.up * 100f;
            Assert.Less(Vector3.Distance(BlastRunHunters.Step(away, den, false, default, 0.5f), den), 100f);
        }

        [Test]
        public void HuntersNeverRoamPastTheirLeash()
        {
            Vector3 den = Vector3.zero;
            Vector3 leader = Vector3.right * (SwitchbackBlastRun.PackSense - 1f);   // in sense, past the leash
            Vector3 pos = den;
            for (int i = 0; i < 2000; i++) pos = BlastRunHunters.Step(pos, den, true, leader, 0.1f);
            Assert.AreEqual(SwitchbackBlastRun.PackLeash, Vector3.Distance(pos, den), 1e-2f);
            Assert.Less(SwitchbackBlastRun.PackLeash, SwitchbackBlastRun.PackSense);
        }

        [Test]
        public void AHunterIsDownWhenTheBlastLeavesTwoPrisms()
        {
            Assert.IsFalse(BlastRunHunters.IsDown(SwitchbackBlastRun.PackBodyPrisms));
            Assert.IsFalse(BlastRunHunters.IsDown(3));
            Assert.IsTrue(BlastRunHunters.IsDown(2));
            Assert.IsTrue(BlastRunHunters.IsDown(0));
            Assert.IsFalse(BlastRunHunters.AllDown(0b011, 3));
            Assert.IsTrue(BlastRunHunters.AllDown(0b111, 3));
            Assert.IsFalse(BlastRunHunters.AllDown(0, 0));
            // The drawn pack closes on the replicated one without overshooting.
            Vector3 shown = BlastRunHunters.Follow(Vector3.zero, Vector3.right * 10f, 0.1f);
            Assert.Greater(shown.x, 0f);
            Assert.Less(shown.x, 10f);
        }

        // ── helpers ──────────────────────────────────────────────────────

        static int DoorCount(int intensity)
        {
            int n = 0;
            BreakwaterStationBuilder.BuildDoor(new BreakwaterStation(Vector3.zero, Vector3.forward, SwitchbackBlastRun.PortFor(intensity)),
                                               (p, r, s, k) => n++);
            return n * (intensity >= 4 ? 2 : 1);
        }

        static Vector3 FindAxisPoint(SwitchbackFeatureLayout L, List<RaceGate> c, int first, out Vector3 sideUnit)
        {
            // The jaw clusters come in pairs; the axis point is the midpoint of the pair's first prisms at laid pose.
            int idx = L.MovingClusters.FindIndex(m => m.FirstPrism == first);
            var m0 = L.MovingClusters[idx];
            var mate = (idx % 2 == 0) ? L.MovingClusters[idx + 1] : L.MovingClusters[idx - 1];
            sideUnit = m0.Side.normalized;
            Vector3 a = L.Prisms[m0.FirstPrism].Position, b = L.Prisms[mate.FirstPrism].Position;
            // Both jaws share the phase, so their laid poses are mirror images about the axis.
            return (a + b) * 0.5f;
        }

        static int MaxWithin(List<SwitchbackFeaturePrism> prisms, float r)
        {
            int best = 0;
            float r2 = r * r;
            for (int i = 0; i < prisms.Count; i += 5)
            {
                int n = 0;
                for (int j = 0; j < prisms.Count; j++)
                    if ((prisms[j].Position - prisms[i].Position).sqrMagnitude < r2) n++;
                if (n > best) best = n;
            }
            return best;
        }

        /// <summary>Pairs of laid prisms whose oriented boxes overlap by more than 0.05 u (15-axis SAT). A door's own
        /// prisms are Breakwater's, flush by its own audit, so pairs inside one door are skipped.</summary>
        static List<(int, int)> ClipPairs(List<SwitchbackFeaturePrism> prisms, int doorGroup, int doorEnd)
        {
            var clips = new List<(int, int)>();
            const float cell = 40f;
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
