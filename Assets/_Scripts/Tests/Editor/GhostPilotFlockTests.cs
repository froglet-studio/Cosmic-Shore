#if UNITY_EDITOR
using CosmicShore.Game;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The ghost pilots' pure step (Docs/GENOME_GHOST_PILOTS.md): deterministic for a seed, speed relaxes to cruise,
    /// pilots stay inside their soft bound, the turn cap holds, the meshes are volumes, and the two presets carry the
    /// lab genomes' numbers.
    /// </summary>
    public class GhostPilotFlockTests
    {
        const string ChasePreset = "Assets/_SO_Assets/Environment/GhostPilots/GhostPilots_ChaseNoHud.asset";
        const string OrbitPreset = "Assets/_SO_Assets/Environment/GhostPilots/GhostPilots_OrbitOverview.asset";
        const float Dt = GhostPilotMotion.StepSeconds;

        // The chase_nohud__tuned genome's motion, decoded (ag.spawn 15, ag.boids 16, ag.wander 17, ag.bound 18).
        static GhostPilotMotion Chase(float stiffness = 0.49444f) => Motion(29.68494f, 1.34138f, 0.29956f, 6.09687f,
                                                                             146.11873f, stiffness);

        // The orbit_overview__tuned genome's motion (ag.spawn 7, ag.boids 8, ag.wander 9, ag.bound 10).
        static GhostPilotMotion Orbit() => Motion(30.58309f, 2.52967f, 0.0322f, 3.45548f, 566.1626f, 0.49444f);

        static GhostPilotMotion Motion(float speed, float turn, float freq, float amp, float bound, float stiff) => new()
        {
            CruiseSpeed = speed,
            TurnCap = turn,
            SpeedRelax = 2f,
            WanderOmega = freq * 2f * math.PI,
            WanderAccel = amp * speed * GhostPilotMotion.WanderSpeedGain,
            BoundRadius = bound,
            BoundStiffness = stiff,
        };

        sealed class Flock : System.IDisposable
        {
            public NativeArray<float3> Pos, Vel, PhaseA, PhaseB;
            public Flock(int n, uint seed, float radius, float speed)
            {
                Pos = new NativeArray<float3>(n, Allocator.Temp);
                Vel = new NativeArray<float3>(n, Allocator.Temp);
                PhaseA = new NativeArray<float3>(n, Allocator.Temp);
                PhaseB = new NativeArray<float3>(n, Allocator.Temp);
                GhostPilotMotion.Seed(seed, GhostPilotSpawnShape.Ball, radius, speed, Pos, Vel, PhaseA, PhaseB);
            }
            public void Dispose() { Pos.Dispose(); Vel.Dispose(); PhaseA.Dispose(); PhaseB.Dispose(); }
        }

        /// <summary>Steps the flock, calling <paramref name="each"/> after every pilot step.</summary>
        static void Run(Flock f, in GhostPilotMotion m, int steps, System.Action<float3, float3, float3> each = null)
        {
            for (int s = 0; s < steps; s++)
            {
                float t = s * Dt;
                for (int i = 0; i < f.Pos.Length; i++)
                {
                    float3 p = f.Pos[i], v = f.Vel[i], before = v;
                    GhostPilotMotion.Step(ref p, ref v, f.PhaseA[i], f.PhaseB[i], t, Dt, m);
                    f.Pos[i] = p;
                    f.Vel[i] = v;
                    each?.Invoke(p, before, v);
                }
            }
        }

        [Test]
        public void SameSeed_SameFlock_DifferentSeed_DifferentFlock()
        {
            var m = Chase();
            using var a = new Flock(16, 7, 65f, m.CruiseSpeed);
            using var b = new Flock(16, 7, 65f, m.CruiseSpeed);
            using var c = new Flock(16, 8, 65f, m.CruiseSpeed);
            Run(a, m, 600);
            Run(b, m, 600);
            Run(c, m, 600);
            float diff = 0f;
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(a.Pos[i], b.Pos[i], "same seed must replay bit for bit");
                Assert.AreEqual(a.Vel[i], b.Vel[i]);
                diff += math.distance(a.Pos[i], c.Pos[i]);
            }
            Assert.Greater(diff, 1f, "a different seed must give a different flock");
        }

        [Test]
        public void SpeedRelaxesToCruise_WhenNothingSteers()
        {
            var m = Chase();
            m.WanderAccel = 0f;
            m.BoundStiffness = 0f;
            float3 p = float3.zero, v = new float3(0f, 0f, m.CruiseSpeed * 2f);
            for (int s = 0; s < 300; s++) GhostPilotMotion.Step(ref p, ref v, 0f, 0f, s * Dt, Dt, m);
            Assert.AreEqual(m.CruiseSpeed, math.length(v), m.CruiseSpeed * 0.02f, "5 s of relax at 2/s");
        }

        [Test]
        public void TunedChase_FliesAroundCruise()
        {
            // The lab's rule lets wander along the heading speed a pilot up or slow it down; measured in the numpy
            // port: median ~cruise, 1st-99th percentile ~4-57 u/s; the C# port's peak over 3 seeds was 69-71 u/s
            // (~2.4x cruise). Pin the median and an upper bound.
            var m = Chase();
            using var f = new Flock(16, 7, 65f, m.CruiseSpeed);
            var speeds = new System.Collections.Generic.List<float>();
            float max = 0f;
            Run(f, m, 60 * 60, (p, before, v) =>
            {
                float s = math.length(v);
                speeds.Add(s);
                max = math.max(max, s);
            });
            speeds.Sort();
            float median = speeds[speeds.Count / 2];
            Assert.AreEqual(m.CruiseSpeed, median, m.CruiseSpeed * 0.2f, "median speed stays near cruise");
            Assert.Less(max, m.CruiseSpeed * 3f, "no runaway speed");
        }

        [Test]
        public void TunedPresets_StayInsideTheirBound()
        {
            // 60 s each. Measured in the numpy port over 3 seeds: chase peaks at ~1.55-1.59 R, orbit at ~1.1-1.16 R.
            AssertMaxRadius(Chase(), 16, 65f, 1.75f);
            AssertMaxRadius(Orbit(), 24, 79.4f, 1.5f);
        }

        [Test]
        public void WithoutTheBound_PilotsEscape()
        {
            // Negative control: the bound is what holds them.
            var m = Chase(stiffness: 0f);
            using var f = new Flock(16, 7, 65f, m.CruiseSpeed);
            float maxR = 0f;
            Run(f, m, 60 * 60, (p, before, v) => maxR = math.max(maxR, math.length(p)));
            Assert.Greater(maxR, 1.75f * m.BoundRadius);
        }

        static void AssertMaxRadius(GhostPilotMotion m, int n, float spawnRadius, float limit)
        {
            using var f = new Flock(n, 11, spawnRadius, m.CruiseSpeed);
            float maxR = 0f;
            Run(f, m, 60 * 60, (p, before, v) => maxR = math.max(maxR, math.length(p)));
            Assert.LessOrEqual(maxR, limit * m.BoundRadius, $"max radius {maxR:F1} vs bound {m.BoundRadius:F1}");
        }

        [Test]
        public void TurnCap_IsNeverExceeded()
        {
            foreach (var (m, mustReach) in new[] { (Chase(), true), (Orbit(), false) })
            {
                using var f = new Flock(16, 3, 65f, m.CruiseSpeed);
                float worst = 0f;
                Run(f, m, 60 * 30, (p, before, v) =>
                {
                    float c = math.clamp(math.dot(math.normalize(before), math.normalize(v)), -1f, 1f);
                    worst = math.max(worst, math.acos(c) / Dt);
                });
                Assert.LessOrEqual(worst, m.TurnCap * 1.001f + 1e-3f, "turn rate");
                // The chase genome's wander asks for more than its cap (~3.6 rad/s), so the cap must bind there;
                // the orbit genome's slow wander rarely does (measured peak ~2.25 of 2.53 rad/s in the numpy port).
                if (mustReach) Assert.Greater(worst, m.TurnCap * 0.9f, "the cap is actually reached");
            }
        }

        [Test]
        public void CapTurn_TurnsExactlyTheCap_EvenForAReversedWish()
        {
            float3 cur = new float3(0f, 0f, 10f);
            float3 got = GhostPilotMotion.CapTurn(cur, new float3(0f, 0f, -20f), 0.1f);
            Assert.AreEqual(20f, math.length(got), 1e-3f, "keeps the wanted length");
            Assert.AreEqual(0.1f, math.acos(math.dot(math.normalize(cur), math.normalize(got))), 1e-3f);
            float3 small = new float3(0.01f, 0f, 10f);
            Assert.AreEqual(small, GhostPilotMotion.CapTurn(cur, small, 0.1f), "a turn inside the cap is untouched");
        }

        [Test]
        public void Job_BloomsFromNothing_AndLaysTrails()
        {
            const int n = 4, cap = 8;
            using var f = new Flock(n, 5, 30f, 30f);
            var trailPhase = new NativeArray<float>(n, Allocator.TempJob);
            var trailHead = new NativeArray<int>(n, Allocator.TempJob);
            var trailPos = new NativeArray<float3>(n * cap, Allocator.TempJob);
            var trailBasis = new NativeArray<float3x3>(n * cap, Allocator.TempJob);
            var trailBirth = new NativeArray<float>(n * cap, Allocator.TempJob);
            var body = new NativeArray<Matrix4x4>(n, Allocator.TempJob);
            var trail = new NativeArray<Matrix4x4>(n * cap, Allocator.TempJob);
            var pos = new NativeArray<float3>(f.Pos, Allocator.TempJob);
            var vel = new NativeArray<float3>(f.Vel, Allocator.TempJob);
            var pa = new NativeArray<float3>(f.PhaseA, Allocator.TempJob);
            var pb = new NativeArray<float3>(f.PhaseB, Allocator.TempJob);
            try
            {
                for (int j = 0; j < n * cap; j++) trailBirth[j] = float.NegativeInfinity;
                var job = new GhostPilotStepJob
                {
                    Motion = Chase(), Pos = pos, Vel = vel, PhaseA = pa, PhaseB = pb,
                    TrailPhase = trailPhase, TrailHead = trailHead, TrailPos = trailPos, TrailBasis = trailBasis,
                    TrailBirth = trailBirth, BodyMatrices = body, TrailMatrices = trail,
                    T0 = 0f, Dt = Dt, Steps = 30, DropTrails = true, TrailCap = cap,
                    TrailInterval = 0.058f, TrailLife = 9.77f, TrailTaper = 0.5f,
                    TrailSize = new float3(0.54f, 0.37f, 2.2f), BodySize = new float3(0.8f, 0.4f, 1.08f),
                    Envelope = 0f, LocalToWorld = float4x4.identity,
                };
                job.Run(n);
                for (int i = 0; i < n; i++)
                    Assert.AreEqual(0f, ((Vector3)body[i].GetColumn(0)).magnitude, 1e-6f, "envelope 0 draws nothing");

                job.T0 = 30 * Dt;
                job.Envelope = 1f;
                job.Run(n);
                for (int i = 0; i < n; i++)
                    Assert.Greater(((Vector3)body[i].GetColumn(2)).magnitude, 0.5f, "a bloomed body has its length");
                int laid = 0, drawn = 0;
                for (int j = 0; j < n * cap; j++)
                {
                    if (!float.IsNegativeInfinity(trailBirth[j])) laid++;
                    if (((Vector3)trail[j].GetColumn(2)).magnitude > 0f) drawn++;
                }
                Assert.AreEqual(laid, drawn, "every laid prism is drawn, every empty slot is not");
                Assert.Greater(laid, n * 3, "1 s at a 0.058 s interval lays several prisms per pilot");
            }
            finally
            {
                trailPhase.Dispose(); trailHead.Dispose(); trailPos.Dispose(); trailBasis.Dispose();
                trailBirth.Dispose(); body.Dispose(); trail.Dispose();
                pos.Dispose(); vel.Dispose(); pa.Dispose(); pb.Dispose();
            }
        }

        [Test]
        public void Meshes_AreFacetedVolumes_NotFlatSheets()
        {
            foreach (var mesh in new[] { GhostPilotMeshes.BuildDart(), GhostPilotMeshes.BuildPyramid() })
            {
                try
                {
                    Vector3 e = mesh.bounds.extents;
                    Assert.Greater(Mathf.Min(e.x, Mathf.Min(e.y, e.z)), 0.1f, $"{mesh.name} has depth on every axis");
                    var v = mesh.vertices;
                    var nrm = mesh.normals;
                    var tris = mesh.triangles;
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                        Vector3 face = Vector3.Cross(b - a, c - a).normalized;
                        Assert.Greater(Vector3.Dot(face, nrm[tris[t]]), 0.99f, "winding agrees with the face normal");
                        Assert.Greater(Vector3.Dot(face, a + b + c), 0f, "faces point outward");
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test]
        public void Presets_CarryTheLabGenomes()
        {
            var chase = AssetDatabase.LoadAssetAtPath<GhostPilotFlockProfileSO>(ChasePreset);
            var orbit = AssetDatabase.LoadAssetAtPath<GhostPilotFlockProfileSO>(OrbitPreset);
            Assert.IsNotNull(chase, ChasePreset);
            Assert.IsNotNull(orbit, OrbitPreset);

            Assert.AreEqual(16, chase.Count);
            Assert.AreEqual(29.68494f, chase.CruiseSpeed, 1e-4f);
            Assert.AreEqual(1.34138f, chase.TurnCap, 1e-4f);
            Assert.AreEqual(146.11873f, chase.BoundRadius, 1e-3f);
            Assert.AreEqual(0.05805f, chase.TrailInterval, 1e-5f);

            Assert.AreEqual(24, orbit.Count);
            Assert.AreEqual(2.52967f, orbit.TurnCap, 1e-4f);
            Assert.AreEqual(566.1626f, orbit.BoundRadius, 1e-3f);
            Assert.AreEqual(0.0322f, orbit.WanderFrequency, 1e-5f);
            Assert.AreEqual(20.21421f, orbit.TrailLifetime, 1e-4f);
            Assert.IsFalse(chase.AttractEnabled || orbit.AttractEnabled, "neither tuned genome has an ag.attract");

            // The motion struct reads the profile as the lab does.
            var m = GhostPilotMotion.From(chase);
            Assert.AreEqual(6.09687f * 29.68494f * 0.3f, m.WanderAccel, 1e-2f);
            Assert.AreEqual(chase.TrailCapacity, Mathf.CeilToInt(9.76585f / 0.05805f) + 2);
        }
    }
}
#endif
