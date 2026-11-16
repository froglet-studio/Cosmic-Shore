using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.ECS;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// E-76248f9266: "Out of memory in the Entities Graphics chunk metadata buffer". Prism show/hide used the
    /// DisableRendering TAG; Entities Graphics strips its chunk component from hidden chunks and re-adds it to whole
    /// chunks on show, so every frame that showed something minted a rendered chunk no later entity joined, and
    /// long-lived prisms pinned ~one entity per chunk until the 1M-element metadata buffer (~30k chunks) ran out.
    /// Visibility is now MaterialMeshInfo's enable bit (PrismRenderService VisibilityFragmentationNote).
    /// </summary>
    public class PrismRenderVisibilityChunkTests
    {
        const string RenderServicePath = "Assets/_Scripts/Controller/ECS/Rendering/PrismRenderService.cs";

        static string CodeOnly(string source)
        {
            // Drop // comments (incl. ///) so the doc notes that EXPLAIN the old tag do not trip the guard.
            return string.Join("\n", source.Split('\n').Select(l =>
            {
                int i = l.IndexOf("//", System.StringComparison.Ordinal);
                return i >= 0 ? l.Substring(0, i) : l;
            }));
        }

        [Test]
        public void Service_NeverTogglesTheDisableRenderingTag()
        {
            var code = CodeOnly(File.ReadAllText(RenderServicePath));
            Assert.IsFalse(Regex.IsMatch(code, @"\bDisableRendering\b"),
                "PrismRenderService code names DisableRendering again. A tag toggle is a structural move that " +
                "fragments Entities Graphics chunks until the chunk-metadata buffer runs out (E-76248f9266). " +
                "Show/hide through SetComponentEnabled<MaterialMeshInfo>.");
        }

        [Test]
        public void Prototype_IsBornHiddenThroughTheMaterialMeshInfoEnableBit()
        {
            var code = CodeOnly(File.ReadAllText(RenderServicePath));
            StringAssert.Contains("em.SetComponentEnabled<MaterialMeshInfo>(prototype, false);", code,
                "GetPrototype must disable MaterialMeshInfo so clones are born hidden without a tag.");
            int enables = Regex.Matches(code, @"SetComponentEnabled<MaterialMeshInfo>\(entity, true\)").Count;
            Assert.AreEqual(3, enables,
                "Each debris spawner (explosion, slice, implosion) shows its clones by enabling MaterialMeshInfo.");
        }

        [Test]
        public void VisibilityChurn_DoesNotGrowTheRenderedChunkCount()
        {
            PrismRenderService.SetRuntimeOverride(true);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material material = shader ? new Material(shader) : null;
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
            };
            const int n = 256;
            var matrices = new NativeArray<float4x4>(n, Allocator.Temp);
            var handles = new NativeArray<PrismRenderHandle>(n, Allocator.Temp);
            try
            {
                Assume.That(material != null, "URP Unlit shader unavailable in this editor.");
                for (int i = 0; i < n; i++) matrices[i] = float4x4.Translate(new float3(i, 0f, 0f));
                bool created = PrismRenderService.CreateBatch(mesh, material, 0, matrices, handles);
                Assume.That(created, "Entities Graphics is unavailable here (no GPU / batchmode); nothing to measure.");

                var world = World.DefaultGameObjectInjectionWorld;
                var structure = world.GetExistingSystemManaged<UpdateHybridChunksStructure>();
                Assume.That(structure != null, "No UpdateHybridChunksStructure in the default world.");
                var em = world.EntityManager;
                using var query = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<MaterialMeshInfo>(), ComponentType.ReadOnly<PrismBrightColorOverride>() },
                    Options = EntityQueryOptions.IgnoreComponentEnabledState,
                });

                for (int i = 0; i < n; i++) PrismRenderService.SetVisible(handles[i], true);
                structure.Update();
                int baseline = query.CalculateChunkCount();

                // A frame per show, as the prism lifecycle and the swarms do: hide one, show one, run the structure system.
                for (int frame = 0; frame < 300; frame++)
                {
                    int k = frame % n;
                    PrismRenderService.SetVisible(handles[k], false);
                    structure.Update();
                    PrismRenderService.SetVisible(handles[k], true);
                    structure.Update();
                }

                for (int i = 0; i < n; i++) Assert.IsTrue(PrismRenderService.IsVisible(handles[i]));
                int after = query.CalculateChunkCount();
                Assert.LessOrEqual(after, baseline,
                    $"Show/hide churn grew the rendered chunks {baseline} -> {after}: visibility is moving entities " +
                    "between chunks again (E-76248f9266).");
            }
            finally
            {
                for (int i = 0; i < n; i++) { var h = handles[i]; PrismRenderService.Destroy(ref h); }
                matrices.Dispose();
                handles.Dispose();
                Object.DestroyImmediate(mesh);
                if (material) Object.DestroyImmediate(material);
                PrismRenderService.SetRuntimeOverride(null);
            }
        }
    }
}
