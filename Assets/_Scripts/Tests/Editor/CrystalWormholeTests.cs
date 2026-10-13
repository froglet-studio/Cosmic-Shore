#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The crystal wormhole (Docs/CRYSTAL_WORMHOLE.md): an attractor and a repulsor joined by a wormhole,
    /// one smoothly graded warping of space with no interface anywhere, which forms out of nothing and
    /// annihilates back into it. Every way it can break is silent — a crease in a field, a pole that does
    /// not cancel, a curve that pops, a cell the selector never lists — so each is asserted from the
    /// shipped code and assets alone.
    /// </summary>
    public class CrystalWormholeTests
    {
        const string ConfigPath = "Assets/_SO_Assets/Cell Configs/Crystal Wormhole Cell/Crystal Wormhole Cell Config.asset";
        const string PrefabPath = "Assets/_Prefabs/Spawnables/SpawnableCrystalWormhole.prefab";
        const string MenuScenePath = "Assets/_Scenes/Menu_Main.unity";
        const string SeamlessMaterialPath = "Assets/_Graphics/Materials/WormholeSeamless.mat";
        const string FoldMaterialPath = "Assets/_Graphics/Materials/Wormhole.mat";

        static CellConfigDataSO Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<CellConfigDataSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} is missing.");
            return config;
        }

        static SpawnableCrystalWormhole Environment()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"{PrefabPath} is missing.");
            Assert.IsTrue(prefab.TryGetComponent<SpawnableCrystalWormhole>(out var env), "the prefab has no SpawnableCrystalWormhole.");
            return env;
        }

        static BlackHolePhysics.Well SmoothWell(float gm, float core) => new()
        {
            Position = float3.zero,
            GM = gm,
            Horizon = BlackHolePhysics.Horizon.Of(core * 0.5f),
            InfluenceRadius = 200f,
            SpinAxis = new float3(0f, 1f, 0f),
            FrameDrag = 0f,
            Softening = core,
        };

        // ---------------- The cell ----------------

        [Test]
        public void Cell_IsTheCrystalWormholeAndNothingElse_AndTheSelectorListsIt()
        {
            var config = Config();
            Assert.AreEqual("Crystal Wormhole", config.CellName);
            Assert.IsInstanceOf<SpawnableCrystalWormhole>(config.EnvironmentPrefab, "the cell's environment is not the crystal wormhole.");
            Assert.IsInstanceOf<RadialWarp>(config.WarpField, "the cell carries no radial warp field.");
            Assert.IsNull(config.NucleusPrefab, "the cell carries a nucleus — the attractor is its centre.");
            Assert.IsFalse(config.BootDefault, "the cell must stay opt-in, never the freestyle boot world.");
            Assert.IsNotNull(config.SpawnProfile);
            Assert.IsEmpty(config.SpawnProfile.SupportedFloras);
            Assert.IsEmpty(config.SpawnProfile.SupportedFaunas);

            string guid = AssetDatabase.AssetPathToGUID(ConfigPath);
            StringAssert.Contains($"guid: {guid}", File.ReadAllText(MenuScenePath),
                "Menu_Main's Cell.CellConfigs does not list the cell, so the Cell Selector never offers it.");
        }

        [Test]
        public void ScaleModel_IsABallPerPole_UnderTheSignatureFilter()
        {
            var env = Environment();
            env.InvalidateCache();
            var trails = env.GetTrailData();
            env.InvalidateCache();
            var centres = new[] { Vector3.zero, env.RepulsorOffset };
            Assert.AreEqual(2, trails.Length, "the scale model does not draw one ball per pole.");
            int total = 0;
            for (int t = 0; t < trails.Length; t++)
            {
                total += trails[t].Points.Length;
                float radius = (trails[t].Points[0].Position - centres[t]).magnitude;
                foreach (var point in trails[t].Points)
                    Assert.AreEqual(radius, (point.Position - centres[t]).magnitude, radius * 1e-3f, $"a plate of ball {t} is off its sphere.");
            }
            Assert.That(total, Is.InRange(16, 63), "the plate count left the band the signature filter keeps whole.");
        }

        [Test]
        public void Poles_ClearTheToysAndEachOther()
        {
            var env = Environment();
            var warp = (RadialWarp)Config().WarpField;
            float felt = env.VesselFeltReach * env.ThroatRadius;
            // The toys ring the cell horizontally at ~0.82 of a ~1200 membrane; the pole switches sit
            // straight up and down at the same radius.
            const float ToyRadius = 984f;
            var r = env.RepulsorOffset;
            float nearestToy = Mathf.Min(
                Mathf.Sqrt(r.y * r.y + Mathf.Pow(ToyRadius - new Vector2(r.x, r.z).magnitude, 2f)),
                Mathf.Min((r - Vector3.up * ToyRadius).magnitude, (r + Vector3.up * ToyRadius).magnitude));
            foreach (float reach in new[] { felt, warp.ReferenceRadius })
            {
                Assert.Greater(nearestToy, reach, $"the repulsor's reach ({reach:F0} u) covers a toy or a pole switch.");
                Assert.Greater(ToyRadius, reach, $"the attractor's reach ({reach:F0} u) covers the toy ring.");
            }
            Assert.Greater(r.magnitude, 2f * BlackHoleRegistry.Config.InfluenceRadius(env.Strength, env.ThroatRadius * 0.5f),
                "the poles' gravity spheres overlap — the repulsor would push mass back into the attractor.");
        }

        // ---------------- Smooth wells: no interface ----------------

        [Test]
        public void SmoothWell_IsFiniteAndContinuousThroughItsCentre_AndTheRepulsorIsItsNegation()
        {
            const float gm = 30000f, core = 26f;
            var attract = SmoothWell(gm, core);
            var repulse = SmoothWell(-gm, core);
            Assert.AreEqual(0f, math.length(BlackHolePhysics.Acceleration(new float3(1e-3f, 0f, 0f), attract)), 1e-2f,
                "a smooth well is not zero at its centre.");
            float peak = 0f, worstJump = 0f, prev = 0f;
            for (float d = 0.1f; d < 300f; d += 0.1f)
            {
                var p = new float3(d, 0f, 0f);
                float a = math.length(BlackHolePhysics.Acceleration(p, attract));
                peak = math.max(peak, a);
                if (d > 0.1f) worstJump = math.max(worstJump, math.abs(a - prev));
                prev = a;
                Assert.AreEqual(0f, math.length(BlackHolePhysics.Acceleration(p, attract) + BlackHolePhysics.Acceleration(p, repulse)), 1e-3f);
            }
            Assert.Less(peak, gm / (core * core), "the smooth well's pull is not bounded by its core.");
            Assert.Less(worstJump, 0.02f * peak, "the smooth well's pull jumps somewhere — an interface.");
        }

        [Test]
        public void Repulsor_NeverCaptures_AttractorCarriesAtItsCore()
        {
            var prm = new BlackHolePhysics.StepParams { ReleaseDamping = 1.5f, ReleaseSpeed = 0.75f, MaxSubsteps = 8 };
            var wells = new BlackHolePhysics.NativeWells();
            wells.Add(SmoothWell(-30000f, 26f));
            var p = new float3(1f, 0f, 0f);
            var v = new float3(-5f, 0f, 0f);
            for (int i = 0; i < 240; i++)
                Assert.AreNotEqual(BlackHolePhysics.Verdict.Captured,
                    BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _), "a repulsor captured a body.");
            Assert.Greater(math.length(p), 26f, "the repulsor did not drive a body out of its core.");

            wells = new BlackHolePhysics.NativeWells();
            wells.Add(SmoothWell(30000f, 26f));
            p = new float3(60f, 0f, 0f);
            v = float3.zero;
            bool captured = false;
            for (int i = 0; i < 1200 && !captured; i++)
                captured = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out _) == BlackHolePhysics.Verdict.Captured;
            Assert.IsTrue(captured, "the attractor never took a body at rest into its core (to carry it through).");
        }

        [Test]
        public void FeltLaw_IsSmoothThroughTheThroat_InverseSquareFarOut_AndSigned()
        {
            const float k = 4f, cruise = 60f, throat = 26f;
            float At(float d, float sign, float warp) =>
                math.length(BlackHoleVesselPull.FeltAcceleration(new float3(d, 0f, 0f), float3.zero, sign, k, cruise, throat, warp));
            Assert.AreEqual(0f, At(0f, 1f, 1f), 1e-4f, "the felt pull is not zero at the centre.");
            Assert.AreEqual(4f, At(1000f, 1f, 1f) / At(2000f, 1f, 1f), 0.01f, "far out, unwarped, the felt pull is not inverse-square.");
            float worstJump = 0f, prev = 0f, peak = 0f;
            for (float d = 0.05f; d < 120f; d += 0.05f)
            {
                float a = At(d, 1f, 1f);
                if (d > 0.05f) worstJump = Mathf.Max(worstJump, Mathf.Abs(a - prev));
                prev = a;
                peak = Mathf.Max(peak, a);
            }
            Assert.Less(worstJump, 0.01f * peak, "the felt pull jumps somewhere — an interface.");
            var pull = BlackHoleVesselPull.FeltAcceleration(new float3(100f, 0f, 0f), float3.zero, +1f, k, cruise, throat, 1f);
            var push = BlackHoleVesselPull.FeltAcceleration(new float3(100f, 0f, 0f), float3.zero, -1f, k, cruise, throat, 1f);
            Assert.Less(pull.x, 0f, "the attractor's felt law does not pull in.");
            Assert.AreEqual(0f, math.length(pull + push), 1e-4f, "the repulsor's felt law is not the attractor's negated.");
        }

        /// <summary>
        /// The playtest contract on the SHIPPED law and numbers: a pilot flying straight at a pole under the
        /// cell's warp. The attractor carries a hull at cruise in faster than it flies; the repulsor turns
        /// back a hull at cruise and lets a boosting one through — for a slow, a middling and a fast hull.
        /// </summary>
        [Test]
        public void FeltLaw_TheAttractorCarriesYouIn_TheRepulsorMustBeBoostedThrough()
        {
            var env = Environment();
            var warp = (RadialWarp)Config().WarpField;
            foreach (float cruise in new[] { 35f, 60f, 180f })
            {
                var attract = Approach(+1f, cruise, cruise, env, warp);
                Assert.IsTrue(attract.through, $"a hull at cruise {cruise} never reached the attractor's mouth.");
                Assert.Greater(attract.topSpeed, 1.8f * cruise, $"the attractor did not carry a hull at cruise {cruise} in.");
                Assert.IsFalse(Approach(-1f, cruise, cruise, env, warp).through,
                    $"a hull at cruise {cruise} flew into the repulsor without boosting — it is not a challenge.");
                Assert.IsTrue(Approach(-1f, 2.5f * cruise, cruise, env, warp).through,
                    $"a hull boosting at 2.5x cruise ({cruise}) could not get through the repulsor.");
            }
        }

        static (bool through, float topSpeed) Approach(float sign, float engine, float cruise, SpawnableCrystalWormhole env, RadialWarp warp)
        {
            const float dt = 1f / 120f;
            float throat = env.ThroatRadius;
            float reach = env.VesselFeltReach * throat;
            float r = Mathf.Min(reach * 1.2f, warp.ReferenceRadius), vg = 0f, top = 0f;
            for (float t = 0f; t < 90f; t += dt)
            {
                if (r <= throat) return (true, top);
                float s = warp.ScaleAt(new Vector3(r, 0f, 0f));
                if (r < reach)
                    vg -= BlackHoleVesselPull.FeltAcceleration(new float3(r, 0f, 0f), float3.zero, sign,
                        env.VesselFeltStrength, cruise, throat, s).x * dt;   // radial, inward positive
                else
                    vg *= Mathf.Exp(-1.5f * dt);
                vg = Mathf.Clamp(vg, -env.VesselFeltCap * cruise, env.VesselFeltCap * cruise);
                top = Mathf.Max(top, engine + vg);
                r -= (engine + vg) * s * dt;
                if (r > reach * 3f) break;
            }
            return (false, top);
        }

        // ---------------- Formation and annihilation ----------------

        [Test]
        public void Annihilation_StartsWhole_EndsInNothing_BeatsInAntiPhase_AndNeverPops()
        {
            var env = Environment();
            CrystalWormhole.Curve(0f, env.SpiralTurns, env.BeatCycles, env.BeatDepth,
                out float sep0, out _, out float env0, out float a0, out float r0);
            Assert.AreEqual(1f, sep0, 1e-5f); Assert.AreEqual(1f, env0, 1e-5f);
            Assert.AreEqual(1f, a0, 1e-5f, "the attractor does not start at full strength.");
            Assert.AreEqual(1f, r0, 1e-5f, "the repulsor does not start at full strength.");

            CrystalWormhole.Curve(1f, env.SpiralTurns, env.BeatCycles, env.BeatDepth,
                out float sep1, out _, out float env1, out float a1, out float r1);
            Assert.AreEqual(0f, sep1, 1e-5f, "the poles do not meet.");
            Assert.AreEqual(0f, a1 + r1 + env1, 1e-5f, "something is left when the pair annihilates.");

            float prevSep = 1f, prevEnv = 1f, prevA = 1f, prevR = 1f, worstJump = 0f;
            bool antiPhase = false;
            const int N = 4000;
            for (int i = 1; i <= N; i++)
            {
                float p = i / (float)N;
                CrystalWormhole.Curve(p, env.SpiralTurns, env.BeatCycles, env.BeatDepth,
                    out float sep, out _, out float e, out float a, out float r);
                Assert.LessOrEqual(sep, prevSep + 1e-6f, "the poles drew apart during the annihilation.");
                Assert.LessOrEqual(e, prevEnv + 1e-6f, "the envelope grew during the annihilation.");
                Assert.AreEqual(2f * e, a + r, 1e-4f, "the beats are not anti-phase about the envelope.");
                if (a > e * 1.2f && r < e * 0.8f) antiPhase = true;
                worstJump = Mathf.Max(worstJump, Mathf.Max(Mathf.Abs(a - prevA), Mathf.Abs(r - prevR)));
                prevSep = sep; prevEnv = e; prevA = a; prevR = r;
            }
            Assert.IsTrue(antiPhase, "the poles never swung against each other — no convolution.");
            Assert.Less(worstJump, 0.02f, "an amplitude jumps between frames — a pop.");
        }

        // ---------------- The warp field ----------------

        [Test]
        public void WarpField_PolesComposeAsAProduct_AmplitudeZeroIsFlat_AndAFrozenPoleHolds()
        {
            var field = ScriptableObject.CreateInstance<RadialWarp>();
            var so = new SerializedObject(field);
            so.FindProperty("referenceRadius").floatValue = 100f;
            so.FindProperty("minScale").floatValue = 0.01f;
            so.FindProperty("easeSeconds").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var cellCentre = new GameObject("Cell").transform;
            var a = new GameObject("Attractor").transform;
            var r = new GameObject("Repulsor").transform;
            r.position = new Vector3(0f, 500f, 0f);
            float ampA = 1f, ampR = 1f;
            var owner = new object();
            try
            {
                WarpFieldRuntime.Activate(owner, field, cellCentre);
                WarpFieldRuntime.AddPole(a, () => ampA);
                WarpFieldRuntime.AddPole(r, () => ampR);
                var near = new Vector3(0f, 480f, 0f);
                float expect = field.ScaleAt(near - a.position) * field.ScaleAt(near - r.position);
                Assert.AreEqual(expect, WarpFieldRuntime.ScaleAt(near), 1e-4f, "the poles do not compose as a product.");

                ampA = 0f; ampR = 0f;
                Assert.AreEqual(1f, WarpFieldRuntime.ScaleAt(near), 1e-5f, "poles at amplitude 0 still warp space.");
                ampR = 1f;
                float before = WarpFieldRuntime.ScaleAt(near);
                Object.DestroyImmediate(r.gameObject);
                Assert.AreEqual(before, WarpFieldRuntime.ScaleAt(near), 1e-5f,
                    "a destroyed pole stopped shaping the field — every vessel near it would pop to full size.");
            }
            finally
            {
                WarpFieldRuntime.Release(owner);
                _ = WarpFieldRuntime.IsActive;
                if (r) Object.DestroyImmediate(r.gameObject);
                Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(cellCentre.gameObject);
                Object.DestroyImmediate(field);
            }
        }

        // ---------------- Seamless visuals ----------------

        [Test]
        public void Materials_TheMouthIsSeamless_TheFoldIsUnchanged()
        {
            var seamless = AssetDatabase.LoadAssetAtPath<Material>(SeamlessMaterialPath);
            Assert.IsNotNull(seamless, $"{SeamlessMaterialPath} is missing.");
            Assert.Greater(seamless.GetFloat("_SoftEdge"), 0f, "the crystal wormhole's mouth has a hard edge.");
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.SrcAlpha, seamless.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, seamless.GetFloat("_DstBlend"));
            Assert.AreEqual(0f, seamless.GetFloat("_ZWrite"), "the seamless mouth writes depth — the lens would treat it as a wall.");
            Assert.GreaterOrEqual(seamless.renderQueue, 3000, "the seamless mouth is not drawn with the transparents the lens bends.");
            Assert.AreEqual(seamless, ((SpawnableCrystalWormhole)Config().EnvironmentPrefab).BuildSettings().MouthMaterial);

            var fold = AssetDatabase.LoadAssetAtPath<Material>(FoldMaterialPath);
            Assert.AreEqual(0f, fold.GetFloat("_SoftEdge"), "the Butterfly fold's mouth lost its hard sphere.");
            Assert.AreEqual(1f, fold.GetFloat("_ZWrite"));
        }

        [Test]
        public void Shaders_SumTheSmoothWells_AndSoftenTheTides()
        {
            string lens = File.ReadAllText("Assets/_Graphics/Materials/Graphs/BlackHoleLens.shader");
            Assert.IsTrue(lens.Contains("_SmoothWellCentre") && lens.Contains("BlackHoleSmoothLensDeflection("),
                "the lens no longer sums every smooth well — overlapping poles would leave a seam and never cancel.");
            string warp = File.ReadAllText("Assets/_Graphics/Materials/Graphs/PrismGravityWarp.hlsl");
            Assert.IsTrue(warp.Contains("PrismGravityWarpTideSoft(") && warp.Contains("abs(k)"),
                "the tidal warp lost the soft core or the sign — a smooth well's tide would have an edge.");
        }
    }
}
#endif
