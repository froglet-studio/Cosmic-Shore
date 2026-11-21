#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the Rhino sword's slice death (Docs/PRISM_ANIMATION.md §4.10).
    /// The GPU half is proven by Tools/Shaders/verify_prism_slice.py (it compiles and runs the
    /// shipped HLSL and front-end-compiles every pass). This file holds the CPU half to the
    /// promises that proof ASSUMES: the stamps <see cref="PrismSliceGeometry"/> builds are the
    /// stamps the shader was proven against — a centre strictly inside each half, a hinge on each
    /// half's trailing edge, a plane whose depth is a world distance — plus the shipped config's
    /// sanity, its wiring to the shader, and the triangle budget.
    /// </summary>
    public class PrismSliceTests
    {
        const string ShaderPath = "Assets/_Graphics/Materials/Graphs/PrismSlice.shader";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismSlice.hlsl";
        const string ConfigPath = "Assets/Resources/PrismSliceConfig.asset";

        /// <summary>The worst case the budget may cost, in triangles. The cradle's ceiling is ~74k
        /// for 24 residents; a slice lives a second, so it is allowed more, but not unbounded.</summary>
        const long TriangleCeiling = 160_000;

        static PrismSliceGeometry.Settings Settings(PrismSliceConfigSO cfg) => new()
        {
            MaxCutOffsetFraction = cfg.MaxCutOffsetFraction,
            SeparationFraction = cfg.SeparationFraction,
            MinSeparation = cfg.MinSeparation,
            OpenAngleMin = cfg.OpenAngleMinRadians,
            OpenAngleMax = cfg.OpenAngleMaxRadians,
            OpenAngleFullSpeed = cfg.OpenAngleFullSpeed,
            DriftFraction = cfg.DriftFraction,
            MaxDriftSpeed = cfg.MaxDriftSpeed,
            DriftDragSeconds = cfg.DriftDragSeconds,
        };

        static PrismSliceConfigSO LoadConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<PrismSliceConfigSO>(ConfigPath);
            Assert.IsNotNull(cfg, $"no slice config at {ConfigPath}");
            return cfg;
        }

        [Test]
        public void Config_IsSaneWiredAndInsideTheTriangleBudget()
        {
            var cfg = LoadConfig();
            Assert.IsTrue(cfg.IsSane, "the shipped slice config fails its own predicate");
            Assert.IsTrue(cfg.Enabled, "the shipped slice config is switched off");
            Assert.IsNotNull(cfg.Material, "the slice config has no material — every blade kill would explode");
            Assert.IsNotNull(cfg.Material.shader, "the slice material has no shader");
            Assert.AreEqual("CosmicShore/PrismSlice", cfg.Material.shader.name,
                "the slice material is not drawn with PrismSlice.shader");
            Assert.LessOrEqual(cfg.WorstCaseTriangles, TriangleCeiling,
                $"the slice budget can draw {cfg.WorstCaseTriangles} triangles");
            Assert.AreEqual(cfg.Material, Resources.Load<PrismSliceConfigSO>(PrismSlice.ConfigResourcePath).Material,
                "Resources/PrismSliceConfig is not the asset the runtime loads");
        }

        [Test]
        public void Shader_DeclaresEveryStampTheRenderServiceWrites()
        {
            string shader = File.ReadAllText(ShaderPath);
            foreach (var name in new[] { "_SliceTiming", "_SlicePlane", "_SliceCentre", "_SlicePivot",
                                         "_SliceAxis", "_SliceDrift", "_BrightColor", "_DarkColor", "_Spread" })
            {
                Assert.IsTrue(Regex.IsMatch(shader, $@"UNITY_DOTS_INSTANCED_PROP\(\w+,\s*{name}\)"),
                    $"{name} is not DOTS-instanced — Entities Graphics would silently drop the stamp");
            }
            StringAssert.Contains("#include \"PrismSlice.hlsl\"", shader);
            StringAssert.Contains("float _PrismClock;", shader,
                "the slice must read the platform clock, never _Time");
            Assert.IsTrue(File.Exists(HlslPath));
        }

        [Test]
        public void Geometry_StampsKeepEveryPromiseTheShaderWasProvenAgainst()
        {
            var cfg = LoadConfig();
            var settings = Settings(cfg);
            var rng = new System.Random(20260926);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 Dir() => new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)).normalized;

            int built = 0;
            for (int trial = 0; trial < 500; trial++)
            {
                var pos = new Vector3(R(-200, 200), R(-200, 200), R(-200, 200));
                var rot = Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360));
                var scale = new Vector3(R(0.3f, 6f), R(0.3f, 6f), R(0.3f, 8f));
                var n = Dir();
                var cutPoint = pos + Dir() * R(0, 10);
                var velocity = Vector3.Cross(n, Dir()) * R(0, 400);

                if (!PrismSliceGeometry.TryBuild(pos, rot, scale, cutPoint, n, velocity, in settings,
                        out var a, out var b))
                    continue;
                built++;

                var model = Matrix4x4.TRS(pos, rot, scale);
                foreach (var (half, outward) in new[] { (a, n), (b, -n) })
                {
                    var m = (Vector3)half.Plane;
                    float d = half.Plane.w;

                    // The plane's depth is a WORLD distance: dot(m, x) - d == n_out · (Mx - Q).
                    var probe = new Vector3(R(-0.5f, 0.5f), R(-0.5f, 0.5f), R(-0.5f, 0.5f));
                    var cutOnPlane = (Vector3)half.Pivot;
                    float objectDepth = Vector3.Dot(m, probe) - d;
                    float worldDepth = Vector3.Dot(outward, model.MultiplyPoint3x4(probe) - cutOnPlane);
                    Assert.AreEqual(worldDepth, objectDepth, 1e-3f * (1f + Mathf.Abs(worldDepth)),
                        "the object-space plane does not measure world distance");

                    // The projection's precondition: the centre strictly inside the kept half.
                    var c = (Vector3)half.Centre;
                    Assert.Less(Vector3.Dot(m, c) - d, 0f, "projection centre is not inside the kept half");
                    Assert.LessOrEqual(Mathf.Abs(c.x), 0.5f + 1e-5f);
                    Assert.LessOrEqual(Mathf.Abs(c.y), 0.5f + 1e-5f);
                    Assert.LessOrEqual(Mathf.Abs(c.z), 0.5f + 1e-5f);
                    Assert.Greater(half.Centre.w, 0f, "a half with no depth");

                    // The hinge is on the cut, on the trailing edge: no kept corner trails it.
                    var travel = (velocity - n * Vector3.Dot(n, velocity));
                    var axis = (Vector3)half.Axis;
                    Assert.AreEqual(1f, axis.magnitude, 1e-4f, "hinge axis is not unit");
                    Assert.AreEqual(0f, Vector3.Dot(axis, outward), 1e-4f, "hinge axis is not in the cut");
                    Assert.LessOrEqual(half.Axis.w, Mathf.PI * 0.5f + 1e-5f, "opening past 90 degrees");
                    if (travel.sqrMagnitude > 1e-4f)
                    {
                        var t = travel.normalized;
                        Assert.AreEqual(0f, Vector3.Dot(axis, t), 1e-3f, "hinge axis is not across the travel");
                        for (int i = 0; i < 8; i++)
                        {
                            var corner = new Vector3((i & 1) != 0 ? 0.5f : -0.5f, (i & 2) != 0 ? 0.5f : -0.5f, (i & 4) != 0 ? 0.5f : -0.5f);
                            if (Vector3.Dot(m, corner) - d > 0f) continue;          // not in this half
                            Assert.GreaterOrEqual(Vector3.Dot(t, model.MultiplyPoint3x4(corner) - cutOnPlane), -1e-3f,
                                "a corner of the half trails the hinge — the opening could pass through its twin");
                        }
                    }

                    // The culling envelope covers the half at rest.
                    var lo = half.BoundsCenter - half.BoundsExtents;
                    var hi = half.BoundsCenter + half.BoundsExtents;
                    Assert.IsTrue(c.x >= lo.x && c.y >= lo.y && c.z >= lo.z && c.x <= hi.x && c.y <= hi.y && c.z <= hi.z,
                        "the culling bounds do not contain the half");
                }

                // The two halves are twins: opposite planes, the same drift, the same angle.
                Assert.AreEqual(-a.Plane.w, b.Plane.w, 1e-4f);
                Assert.AreEqual(a.Axis.w, b.Axis.w, 1e-6f);
                Assert.AreEqual((Vector3)a.Drift, (Vector3)b.Drift);
            }

            Assert.Greater(built, 400, "most random cuts should produce two halves");
        }

        [Test]
        public void Geometry_RefusesDegenerateCuts()
        {
            var settings = Settings(LoadConfig());
            Assert.IsFalse(PrismSliceGeometry.TryBuild(Vector3.zero, Quaternion.identity, Vector3.one,
                Vector3.zero, Vector3.zero, Vector3.forward, in settings, out _, out _), "zero normal");
            Assert.IsFalse(PrismSliceGeometry.TryBuild(Vector3.zero, Quaternion.identity, new Vector3(1, 0, 1),
                Vector3.zero, Vector3.up, Vector3.forward, in settings, out _, out _), "zero-thickness prism");
        }

        [Test]
        public void HighPolyMesh_IsTheSolidTheHalvesDraw()
        {
            var cfg = LoadConfig();
            var mesh = HighPolyPrismMesh.Get(cfg.Subdivision);
            Assert.IsTrue(HighPolyPrismMesh.IsHighPoly(mesh));
            Assert.AreEqual(12 * cfg.Subdivision * cfg.Subdivision, mesh.triangles.Length / 3);
            Assert.AreEqual(Vector3.one, mesh.bounds.size, "not the unit prism the plane is stamped against");
        }
    }
}
#endif
