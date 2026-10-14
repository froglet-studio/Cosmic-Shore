#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The CPU half of prism movement's three-level ladder and its level-1 COLLISION BURST
    /// (Docs/PRISM_MOTION.md, Docs/PRISM_ANIMATION.md §4.11). The GPU half is proven by
    /// Tools/Shaders/verify_prism_collision_burst.py, which executes the shipped HLSL. This file holds
    /// the CPU to the promises that proof assumes — the culling envelope uses the SAME formula the HLSL
    /// does, the unwelded mesh is the same solid with untearable triangles, the ladder has hysteresis —
    /// plus the shipped config's sanity, its wiring to the shader, and the triangle budget.
    /// </summary>
    public class PrismCollisionBurstTests
    {
        const string ShaderPath = "Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.shader";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.hlsl";
        const string ConfigPath = "Assets/Resources/PrismMotionConfig.asset";

        // ---------------------------------------------------------------- the ladder

        [Test]
        public void TheLadderClimbsAtEachCeiling()
        {
            Assert.AreEqual(PrismMotionLevel.Showcase, PrismMotion.NextLevel(PrismMotionLevel.Showcase, 64, 64, 256));
            Assert.AreEqual(PrismMotionLevel.Plain, PrismMotion.NextLevel(PrismMotionLevel.Showcase, 65, 64, 256));
            Assert.AreEqual(PrismMotionLevel.Plain, PrismMotion.NextLevel(PrismMotionLevel.Plain, 256, 64, 256));
            Assert.AreEqual(PrismMotionLevel.Bulk, PrismMotion.NextLevel(PrismMotionLevel.Plain, 257, 64, 256));
            Assert.AreEqual(PrismMotionLevel.Bulk, PrismMotion.NextLevel(PrismMotionLevel.Showcase, 1000, 64, 256));
        }

        [Test]
        public void TheLadderComesDownOnlyAtThreeQuarters()
        {
            // 256 -> 192: the setting the user named as the line to level three.
            Assert.AreEqual(PrismMotionLevel.Bulk, PrismMotion.NextLevel(PrismMotionLevel.Bulk, 193, 64, 256));
            Assert.AreEqual(PrismMotionLevel.Plain, PrismMotion.NextLevel(PrismMotionLevel.Bulk, 192, 64, 256));
            Assert.AreEqual(PrismMotionLevel.Plain, PrismMotion.NextLevel(PrismMotionLevel.Plain, 49, 64, 256));
            Assert.AreEqual(PrismMotionLevel.Showcase, PrismMotion.NextLevel(PrismMotionLevel.Plain, 48, 64, 256));
            // A collapse straight from many to few lands on level 1 in one step.
            Assert.AreEqual(PrismMotionLevel.Showcase, PrismMotion.NextLevel(PrismMotionLevel.Bulk, 3, 64, 256));
        }

        [Test]
        public void ACountHoveringAtALineDoesNotFlip()
        {
            var level = PrismMotionLevel.Plain;
            int flips = 0;
            for (int frame = 0; frame < 200; frame++)
            {
                int n = 256 + (frame % 2 == 0 ? 1 : -1) * (frame % 7);   // jitters 249..262
                var next = PrismMotion.NextLevel(level, n, 64, 256);
                if (next != level) flips++;
                level = next;
            }
            Assert.LessOrEqual(flips, 1, "the line at 256 must not flip back below 192");
        }

        [Test]
        public void LevelsCarryExplicitValues()
        {
            Assert.AreEqual(1, (int)PrismMotionLevel.Showcase);
            Assert.AreEqual(2, (int)PrismMotionLevel.Plain);
            Assert.AreEqual(3, (int)PrismMotionLevel.Bulk);
        }

        // ---------------------------------------------------------------- the burst's CPU stamps

        [Test]
        public void TheEnvelopeIsTheHlslsFormula()
        {
            string hlsl = File.ReadAllText(HlslPath);
            StringAssert.Contains("#define PRISM_BURST_ENVELOPE_BASE 3.5", hlsl);
            StringAssert.Contains("return PRISM_BURST_ENVELOPE_BASE + 1.5 * max(spread, 0.0);", hlsl);
            Assert.AreEqual(3.5f + 1.5f * 0.55f, PrismCollisionBurst.EnvelopeRadii(0.55f), 1e-6f);
            Assert.AreEqual(3.5f, PrismCollisionBurst.EnvelopeRadii(-1f), 1e-6f);
        }

        [Test]
        public void TheBoundsCoverTheEnvelopeAndTheWholeFlight()
        {
            var rotation = Quaternion.Euler(20f, 70f, -35f);
            var scale = new Vector3(6f, 0.5f, 2f);
            var flight = new Vector3(40f, -10f, 120f);
            PrismCollisionBurst.BuildBounds(rotation, scale, flight, 0.55f, out var c, out var e);
            float reach = PrismCollisionBurst.EnvelopeRadii(0.55f) * 0.5f * scale.magnitude;

            // Every world point within `reach` of the centre, at the start and at the end of the flight,
            // maps inside the object-space box.
            var model = Matrix4x4.TRS(Vector3.zero, rotation, scale);
            var inverse = model.inverse;
            var rng = new System.Random(7);
            for (int i = 0; i < 2000; i++)
            {
                var dir = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
                foreach (float along in new[] { 0f, 0.37f, 1f })
                {
                    Vector3 world = flight * along + dir * reach;
                    Vector3 os = inverse.MultiplyPoint3x4(world);
                    Assert.LessOrEqual(math.abs(os.x - c.x), e.x + 1e-3f);
                    Assert.LessOrEqual(math.abs(os.y - c.y), e.y + 1e-3f);
                    Assert.LessOrEqual(math.abs(os.z - c.z), e.z + 1e-3f);
                }
            }
        }

        [Test]
        public void KindsArePickedByWeight()
        {
            int[] counts = new int[PrismCollisionBurst.KindCount];
            for (int i = 0; i < 4000; i++)
                counts[PrismCollisionBurst.PickKind((i + 0.5f) / 4000f, 1f, 1f, 1f, 1f)]++;
            foreach (int c in counts) Assert.AreEqual(1000, c, 1);

            for (int i = 0; i < 100; i++)
                Assert.AreNotEqual(PrismCollisionBurst.Bloom, PrismCollisionBurst.PickKind(i / 100f, 1f, 0f, 1f, 1f));
            Assert.AreEqual(PrismCollisionBurst.Glitter, PrismCollisionBurst.PickKind(1f, 1f, 1f, 1f, 1f));
            Assert.AreEqual(PrismCollisionBurst.Unravel, PrismCollisionBurst.PickKind(1f, 1f, 1f, 1f, 0f));
            Assert.AreEqual(PrismCollisionBurst.Shatter, PrismCollisionBurst.PickKind(0.5f, 0f, 0f, 0f, 0f));
        }

        // ---------------------------------------------------------------- the mesh

        [Test]
        public void TheUnweldedMeshIsTheSameSolidWithUntearableTriangles()
        {
            HighPolyPrismMesh.InvalidateCache();
            var welded = HighPolyPrismMesh.Get(10);
            var mesh = HighPolyPrismMesh.GetUnwelded(10);
            Assert.AreSame(mesh, HighPolyPrismMesh.GetUnwelded(10), "shared and cached");
            Assert.IsTrue(HighPolyPrismMesh.IsHighPoly(mesh));
            Assert.AreEqual(welded.triangles.Length, mesh.triangles.Length, "triangle for triangle");
            Assert.AreEqual(mesh.triangles.Length, mesh.vertexCount, "three vertices per triangle");
            Assert.AreEqual(welded.bounds.extents, mesh.bounds.extents);

            var verts = mesh.vertices;
            var normals = mesh.normals;
            var tris = new System.Collections.Generic.List<Vector3>();
            mesh.GetUVs(HighPolyPrismMesh.TriangleUVChannel, tris);
            var tangents = mesh.tangents;
            for (int t = 0; t < verts.Length; t += 3)
            {
                // Same piece for every vertex of a triangle: the burst can never tear one.
                Assert.AreEqual(tris[t], tris[t + 1]);
                Assert.AreEqual(tris[t], tris[t + 2]);
                // On its face, with the face's hard normal, wound outward.
                Vector3 n = normals[t];
                Assert.AreEqual(HighPolyPrismMesh.HalfExtent, Vector3.Dot(verts[t], n), 1e-5f);
                Vector3 wound = Vector3.Cross(verts[t + 1] - verts[t], verts[t + 2] - verts[t]);
                Assert.Greater(Vector3.Dot(wound, n), 0f, "outward winding");
                // The centroid the shader reads is the triangle's own, in face coordinates.
                Vector3 u = tangents[t];
                Vector3 v = Vector3.Cross(n, u);
                Vector3 centroid = (verts[t] + verts[t + 1] + verts[t + 2]) / 3f;
                Assert.AreEqual(Vector3.Dot(centroid, u) + 0.5f, tris[t].x, 1e-5f);
                Assert.AreEqual(Vector3.Dot(centroid, v) + 0.5f, tris[t].y, 1e-5f);
                Assert.That(tris[t].z, Is.InRange(0f, 1f));
            }
        }

        // ---------------------------------------------------------------- the shipped config

        [Test]
        public void TheShippedConfigIsSaneAndWired()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<PrismMotionConfigSO>(ConfigPath);
            Assert.IsNotNull(cfg, ConfigPath);
            Assert.IsTrue(cfg.IsSane);
            Assert.AreEqual(256, cfg.BoxMaxMovers, "the level-3 line the user set");
            Assert.LessOrEqual(cfg.ShowcaseMaxMovers, cfg.BoxMaxMovers);
            Assert.IsTrue(cfg.BurstEnabled);
            Assert.IsNotNull(cfg.BurstMaterial);
            Assert.AreEqual("CosmicShore/PrismCollisionBurst", cfg.BurstMaterial.shader.name);
            Assert.LessOrEqual(cfg.WorstCaseTriangles, 80_000, "the burst's budget");
            Assert.LessOrEqual(cfg.Spread, 1f, "a cloud wider than the prism stops squinting as the explosion");
        }

        [Test]
        public void TheShaderReadsTheStampsTheCarrierWrites()
        {
            string shader = File.ReadAllText(ShaderPath);
            foreach (var prop in new[] { "_SliceTiming", "_SliceDrift", "_BrightColor", "_DarkColor" })
                Assert.IsTrue(Regex.IsMatch(shader, $@"UNITY_DOTS_INSTANCED_PROP\(float4, {prop}\)"), prop);
            foreach (var prop in new[] { "_BurstShape", "_BurstCurl", "_BurstFade" })
                StringAssert.Contains(prop, shader);
            StringAssert.Contains("#include \"PrismCollisionBurst.hlsl\"", shader);
        }
    }
}
#endif
