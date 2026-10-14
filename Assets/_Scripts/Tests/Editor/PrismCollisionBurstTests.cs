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
    /// The CPU half of prism movement's three-level ladder and of the prism's interaction DETAIL LEVELS —
    /// destruction (explosion, shatter, glitter) and stealing (the grade, or a bloom / unravel / flake shed)
    /// (Docs/PRISM_MOTION.md, Docs/PRISM_ANIMATION.md §4.11). The GPU half is proven by
    /// Tools/Shaders/verify_prism_collision_burst.py, which executes the shipped HLSL. This file holds
    /// the CPU to the promises that proof assumes — the culling envelope and the dissolve overshoot are the
    /// HLSL's, the unwelded mesh is the same solid with untearable triangles, the ladder has hysteresis, a
    /// shed's life ends with the grade, the level is picked by screen size in order — plus the shipped
    /// config's sanity, its wiring to the shader, and the triangle budget.
    /// </summary>
    public class PrismCollisionBurstTests
    {
        const string ShaderPath = "Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.shader";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.hlsl";
        const string MotionConfigPath = "Assets/Resources/PrismMotionConfig.asset";
        const string LodConfigPath = "Assets/Resources/PrismInteractionLodConfig.asset";

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
        public void TheOvershootIsTheHlsls()
        {
            string hlsl = File.ReadAllText(HlslPath);
            string value = PrismCollisionBurst.DissolveOvershoot.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            StringAssert.Contains($"#define PRISM_BURST_DISSOLVE_OVERSHOOT {value}", hlsl);
        }

        [Test]
        public void AShedEndsWithTheGradeItStandsFor()
        {
            // The fade reaches 1 at life / (1 + overshoot): exactly the grade's length.
            float life = PrismCollisionBurst.ShedLife(MaterialPropertyAnimator.DefaultTransitionSeconds);
            Assert.AreEqual(MaterialPropertyAnimator.DefaultTransitionSeconds,
                life / (1f + PrismCollisionBurst.DissolveOvershoot), 1e-6f);
        }

        [Test]
        public void TheSkinSitsOutsideThePrismOnEverySide()
        {
            var scale = new Vector3(4f, 0.6f, 2f);
            var skin = PrismCollisionBurst.SkinScale(scale, 0.02f);
            float grow = 2f * 0.02f * 0.5f * scale.magnitude;
            Assert.AreEqual(scale.x + grow, skin.x, 1e-5f);
            Assert.AreEqual(scale.y + grow, skin.y, 1e-5f, "the thin side gets the same margin, not 2% of itself");
            Assert.AreEqual(scale.z + grow, skin.z, 1e-5f);
            Assert.AreEqual(-scale.x - grow, PrismCollisionBurst.SkinScale(new Vector3(-4f, 0.6f, 2f), 0.02f).x, 1e-5f);
            Assert.AreEqual(scale, PrismCollisionBurst.SkinScale(scale, 0f));
        }

        [Test]
        public void ShedsArePickedByWeight()
        {
            int[] counts = new int[PrismCollisionBurst.KindCount];
            for (int i = 0; i < 3000; i++)
                counts[PrismCollisionBurst.PickShed((i + 0.5f) / 3000f, 1f, 1f, 1f)]++;
            Assert.AreEqual(1000, counts[PrismCollisionBurst.Bloom], 1);
            Assert.AreEqual(1000, counts[PrismCollisionBurst.Unravel], 1);
            Assert.AreEqual(1000, counts[PrismCollisionBurst.Flake], 1);
            Assert.AreEqual(0, counts[PrismCollisionBurst.Shatter] + counts[PrismCollisionBurst.Glitter],
                "a destruction kind is never a shed");

            for (int i = 0; i < 100; i++)
                Assert.AreNotEqual(PrismCollisionBurst.Unravel, PrismCollisionBurst.PickShed(i / 100f, 1f, 0f, 1f));
            Assert.AreEqual(PrismCollisionBurst.Flake, PrismCollisionBurst.PickShed(1f, 1f, 1f, 1f));
            Assert.AreEqual(PrismCollisionBurst.Unravel, PrismCollisionBurst.PickShed(1f, 1f, 1f, 0f));
            Assert.AreEqual(PrismCollisionBurst.Bloom, PrismCollisionBurst.PickShed(0.5f, 0f, 0f, 0f));
        }

        // ---------------------------------------------------------------- the detail levels

        [Test]
        public void ScreenFractionIsTheDiameterOverTheScreenHeight()
        {
            // A prism of radius 1 at distance d through a 90° camera: the screen is 2d tall, the prism 2 wide.
            Assert.AreEqual(1f / 10f, PrismInteractionLod.ScreenFraction(1f, 10f, 90f), 1e-5f);
            Assert.AreEqual(2f / 10f, PrismInteractionLod.ScreenFraction(2f, 10f, 90f), 1e-5f);
            Assert.AreEqual(1f / 20f, PrismInteractionLod.ScreenFraction(1f, 20f, 90f), 1e-5f);
            // Narrower field of view, bigger on screen.
            Assert.Greater(PrismInteractionLod.ScreenFraction(1f, 10f, 40f), PrismInteractionLod.ScreenFraction(1f, 10f, 90f));
            // Orthographic: the half-height is the unit.
            Assert.AreEqual(0.25f, PrismInteractionLod.ScreenFraction(1f, 999f, 60f, true, 4f), 1e-6f);
        }

        [Test]
        public void DestructionGetsFinerAsThePrismGrowsOnScreen()
        {
            Assert.AreEqual(PrismInteractionLod.DestructionLevel.Explosion, PrismInteractionLod.PickDestruction(0.01f, 0.05f, 0.15f));
            Assert.AreEqual(PrismInteractionLod.DestructionLevel.Shatter, PrismInteractionLod.PickDestruction(0.05f, 0.05f, 0.15f));
            Assert.AreEqual(PrismInteractionLod.DestructionLevel.Shatter, PrismInteractionLod.PickDestruction(0.149f, 0.05f, 0.15f));
            Assert.AreEqual(PrismInteractionLod.DestructionLevel.Glitter, PrismInteractionLod.PickDestruction(0.15f, 0.05f, 0.15f));
            Assert.AreEqual(PrismInteractionLod.DestructionLevel.Glitter, PrismInteractionLod.PickDestruction(5f, 0.05f, 0.15f));

            // Monotone: walking the camera in never steps back to a coarser level.
            var previous = PrismInteractionLod.DestructionLevel.Explosion;
            for (float d = 400f; d > 1f; d *= 0.97f)
            {
                var level = PrismInteractionLod.PickDestruction(PrismInteractionLod.ScreenFraction(2.25f, d, 60f), 0.05f, 0.15f);
                Assert.GreaterOrEqual((int)level, (int)previous);
                previous = level;
            }
            Assert.AreEqual(PrismInteractionLod.DestructionLevel.Glitter, previous);
        }

        [Test]
        public void LevelsCarryExplicitValues_Destruction()
        {
            Assert.AreEqual(0, (int)PrismInteractionLod.DestructionLevel.Explosion);
            Assert.AreEqual(1, (int)PrismInteractionLod.DestructionLevel.Shatter);
            Assert.AreEqual(2, (int)PrismInteractionLod.DestructionLevel.Glitter);
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
        public void TheShippedConfigsAreSaneAndWired()
        {
            var motion = AssetDatabase.LoadAssetAtPath<PrismMotionConfigSO>(MotionConfigPath);
            Assert.IsNotNull(motion, MotionConfigPath);
            Assert.IsTrue(motion.IsSane);
            Assert.AreEqual(256, motion.BoxMaxMovers, "the level-3 line the user set");

            var cfg = AssetDatabase.LoadAssetAtPath<PrismInteractionLodConfigSO>(LodConfigPath);
            Assert.IsNotNull(cfg, LodConfigPath);
            Assert.IsTrue(cfg.IsSane);
            Assert.IsTrue(cfg.DestructionEnabled);
            Assert.IsTrue(cfg.ShedEnabled);
            Assert.IsNotNull(cfg.BurstMaterial);
            Assert.AreEqual("CosmicShore/PrismCollisionBurst", cfg.BurstMaterial.shader.name);
            Assert.LessOrEqual(cfg.WorstCaseTriangles, 80_000, "the detail levels' budget");
            Assert.LessOrEqual(cfg.Spread, 1f, "a cloud wider than the prism stops squinting as the explosion");
            Assert.Less(cfg.ShatterMinScreen, cfg.GlitterMinScreen, "the spectrum runs coarse to fine as the prism nears");
            Assert.Greater(cfg.ShedInflate, 0f, "a skin at the prism's own depth would fight it");
        }

        [Test]
        public void TheShaderReadsTheStampsTheCarrierWrites()
        {
            string shader = File.ReadAllText(ShaderPath);
            foreach (var prop in new[] { "_SliceTiming", "_SliceDrift", "_SlicePlane", "_SlicePivot", "_BrightColor", "_DarkColor" })
                Assert.IsTrue(Regex.IsMatch(shader, $@"UNITY_DOTS_INSTANCED_PROP\(float4, {prop}\)"), prop);
            StringAssert.Contains("PRISM_BURST_PROP(float4, _SlicePlane).x", shader, "the fade curve");
            StringAssert.Contains("PRISM_BURST_PROP(float4, _SlicePivot)", shader, "the inside colour");
            foreach (var prop in new[] { "_BurstShape", "_BurstCurl" })
                StringAssert.Contains(prop, shader);
            StringAssert.Contains("#include \"PrismCollisionBurst.hlsl\"", shader);
        }

        [Test]
        public void NoWhiteAccentsRemain()
        {
            // The designer, 2026-10-14: no white edge on the dissolves or the glitter; the inside colour is the
            // only contrast.
            string shader = File.ReadAllText(ShaderPath);
            foreach (var retired in new[] { "_HotColor", "_EmberGlow", "_EmberWhite", "_BurstFade" })
                StringAssert.DoesNotContain(retired, shader, retired);
        }
    }
}
#endif
