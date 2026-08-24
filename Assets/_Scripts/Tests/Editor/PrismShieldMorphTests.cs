#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the GPU shield morph (Docs/PRISM_ANIMATION.md §5 B4) — the
    /// migration that retired the last sanctioned per-frame prism ticker.
    ///
    /// Three things can silently break it, and each has a test here:
    ///
    ///   1. THE BAKED FACE CENTROIDS. The morph's whole premise is that the ONE piece of
    ///      information a vertex shader cannot derive — which face a vertex belongs to —
    ///      rides in TEXCOORD1 on the cache-shared settled mesh. If a generator edit drops
    ///      or misplaces that channel, every shield collapses toward the object origin
    ///      instead of blooming, with no compile error anywhere.
    ///   2. THE GRAPH WIRING. An unwired graph means a snapped morph; the runtime screams
    ///      once per material, which is easy to miss in a busy console and impossible to
    ///      see in a screenshot.
    ///   3. THE TICKER COMING BACK. The migration's whole point is that no CPU code
    ///      advances a shield morph. A re-added Update()/coroutine would restore the cost
    ///      while everything still looked correct on screen.
    ///
    /// All assertions run from assets alone — no play mode.
    /// </summary>
    public class PrismShieldMorphTests
    {
        static readonly string[] WiredGraphPaths =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl";
        const string FunctionName = "PrismShieldMorph";

        static readonly string[] PerInstanceProps =
        {
            "_ShieldMorphStartTime", "_ShieldMorphDuration",
            "_ShieldMorphDirection", "_ShieldMorphOffset",
        };

        const string OctahedronShieldPath = "Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs";
        const string StellatedShieldPath = "Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs";
        const string RetiredTickerPath = "Assets/_Scripts/Controller/Managers/PrismOctahedronShieldManager.cs";

        // ── 1. the baked morph data ──────────────────────────────────────────

        [Test]
        public void BothGeneratorsAgreeOnTheFaceCentroidChannel()
        {
            Assert.AreEqual(OctahedronMeshGenerator.FaceCentroidUVChannel,
                StellatedOctahedronMeshGenerator.FaceCentroidUVChannel,
                "The two shield tiers must bake their face centroids into the SAME UV channel — " +
                "one shader path (PrismShieldMorph) animates both, and it reads one channel.");
        }

        [Test]
        public void OctahedronMesh_CarriesPerFaceCentroidsInTheMorphChannel()
        {
            var mesh = OctahedronMeshGenerator.Generate(new Vector3(0.5f, 1.25f, 2f));
            try
            {
                AssertPerFaceCentroids(mesh, expectedVertexCount: 24,
                    OctahedronMeshGenerator.FaceCentroidUVChannel);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void StellatedMesh_CarriesPerFaceCentroidsInTheMorphChannel()
        {
            var mesh = StellatedOctahedronMeshGenerator.Generate(new Vector3(0.5f, 1.25f, 2f));
            try
            {
                AssertPerFaceCentroids(mesh, StellatedOctahedronMeshGenerator.VERTEX_COUNT,
                    StellatedOctahedronMeshGenerator.FaceCentroidUVChannel);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        /// <summary>
        /// The centroid a vertex carries must be its OWN face's centroid — the exact value
        /// the retired CPU morph computed inline, (v0 + v1 + v2) / 3 over each consecutive
        /// vertex triple. Flat shading means every face owns its 3 vertices, so all three
        /// carry the same centroid.
        /// </summary>
        static void AssertPerFaceCentroids(Mesh mesh, int expectedVertexCount, int channel)
        {
            var verts = mesh.vertices;
            Assert.AreEqual(expectedVertexCount, verts.Length,
                "flat-shaded shield meshes own 3 vertices per face");

            var centroids = new System.Collections.Generic.List<Vector3>();
            mesh.GetUVs(channel, centroids);
            Assert.AreEqual(verts.Length, centroids.Count,
                $"UV{channel} must carry one centroid per vertex — the GPU morph reads it per vertex. " +
                "An empty channel means every shield morphs about the object origin instead of its faces.");

            for (int f = 0; f * 3 < verts.Length; f++)
            {
                int i0 = f * 3;
                Vector3 expected = (verts[i0] + verts[i0 + 1] + verts[i0 + 2]) / 3f;
                for (int k = 0; k < 3; k++)
                {
                    Assert.That(Vector3.Distance(centroids[i0 + k], expected), Is.LessThan(1e-4f),
                        $"face {f} vertex {k}: baked centroid {centroids[i0 + k]} != face centroid {expected}");
                }
            }
        }

        [Test]
        public void SettledShieldMeshes_AreCacheShared_SoSameSizeShieldsBatch()
        {
            // The morph runs on the SETTLED shared mesh, so sharing is what keeps a whole
            // batch of morphing shields in one draw. Cache-owned meshes are deliberately
            // not destroyed here.
            var extents = new Vector3(0.375f, 0.75f, 1.5f);
            Assert.AreSame(OctahedronMeshGenerator.GetSharedShieldMesh(extents),
                OctahedronMeshGenerator.GetSharedShieldMesh(extents),
                "same geometry must resolve to ONE octahedron mesh instance");
            Assert.AreSame(StellatedOctahedronMeshGenerator.GetSharedShieldMesh(extents),
                StellatedOctahedronMeshGenerator.GetSharedShieldMesh(extents),
                "same geometry must resolve to ONE stellation instance");

            var shared = OctahedronMeshGenerator.GetSharedShieldMesh(extents);
            var centroids = new System.Collections.Generic.List<Vector3>();
            shared.GetUVs(OctahedronMeshGenerator.FaceCentroidUVChannel, centroids);
            Assert.AreEqual(shared.vertexCount, centroids.Count,
                "the SHARED mesh is the morph mesh — it must carry the centroid channel too");
        }

        // ── 2. the graph wiring ──────────────────────────────────────────────

        [Test]
        public void MorphHlsl_ExistsAndDeclaresTheFunction()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing — the morph has no GPU half.");
            string hlsl = File.ReadAllText(HlslPath);
            Assert.IsTrue(hlsl.Contains($"void {FunctionName}_float("),
                $"{HlslPath} does not declare {FunctionName}_float — ShaderGraph appends the precision " +
                "suffix, so the name must match exactly or every prism graph fails to compile.");
        }

        [Test]
        public void EveryWiredGraph_DeclaresThePerInstancePropsAndTheMorphNode()
        {
            foreach (var graphPath in WiredGraphPaths)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                // Normalise CRLF first: a Windows checkout otherwise collapses the whole
                // file into one block and every block-scoped check reads the wrong property.
                string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

                foreach (var prop in PerInstanceProps)
                {
                    var block = blocks.FirstOrDefault(b =>
                        b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") && b.Contains("ShaderProperty"));
                    Assert.IsNotNull(block,
                        $"{Path.GetFileName(graphPath)} does not declare {prop} — run " +
                        "Tools/Shaders/wire_prism_shield_morph.py.");
                    Assert.IsTrue(block.Contains("\"hlslDeclarationOverride\": 3"),
                        $"{Path.GetFileName(graphPath)}: {prop} is not Hybrid Per Instance, so a per-prism " +
                        "stamp can never reach the shader and every shield morph snaps.");
                    Assert.IsTrue(block.Contains("\"m_GeneratePropertyBlock\": true"),
                        $"{Path.GetFileName(graphPath)}: {prop} must be exposed.");
                }

                Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{FunctionName}\""),
                    $"{Path.GetFileName(graphPath)} has no {FunctionName} Custom Function node.");

                // The per-vertex centroid feed. The wiring script asserts the structural
                // edge; this asserts the CHANNEL agrees with what the generators bake —
                // the one number that lives in both a C# constant and a graph node.
                int channel = OctahedronMeshGenerator.FaceCentroidUVChannel;
                Assert.IsTrue(blocks.Any(b => b.Contains("\"UnityEditor.ShaderGraph.UVNode\"") &&
                                              b.Contains($"\"m_OutputChannel\": {channel}")),
                    $"{Path.GetFileName(graphPath)} has no UV node reading UV{channel} — the shield " +
                    "morph would read whatever UV0 happens to hold instead of the face centroids.");
            }
        }

        [Test]
        public void EveryWiredGraph_IsAcyclicAndItsPropertyNodesAreTypeConsistent()
        {
            // Both halves of this test exist because both failures SHIPPED, from one
            // blind spot: the wiring script proved everything it BUILT and nothing about
            // what it built it INTO.
            //
            //  * a cycle — splicing a node in front of something already downstream of it
            //    closes a loop, ShaderGraph rejects the whole asset, and every material on
            //    it renders magenta. Local per-node checks cannot see it.
            //  * a property node whose output slot type does not match its property — a
            //    Vector1 slot on a Vector3 property delivers no vector, silently, and the
            //    animation just looks like it never got its stamp.
            foreach (var graphPath in WiredGraphPaths)
            {
                string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

                // objectId -> block, for the two lookups below.
                var byId = new Dictionary<string, string>();
                foreach (var b in blocks)
                {
                    var m = Regex.Match(b, "\"m_ObjectId\": \"([0-9a-f]{32})\"");
                    if (m.Success && !byId.ContainsKey(m.Groups[1].Value))
                        byId[m.Groups[1].Value] = b;
                }

                var graph = blocks.First(b => b.Contains("\"m_Type\": \"UnityEditor.ShaderGraph.GraphData\""));

                // --- acyclicity over every edge in the graph ---------------------
                var upstream = new Dictionary<string, List<string>>();
                foreach (Match e in Regex.Matches(graph,
                    "\"m_OutputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\": \"([0-9a-f]{32})\"[^}]*\\}[^}]*\\}," +
                    "\\s*\"m_InputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\": \"([0-9a-f]{32})\""))
                {
                    string from = e.Groups[1].Value, to = e.Groups[2].Value;
                    if (!upstream.TryGetValue(to, out var list))
                        upstream[to] = list = new List<string>();
                    list.Add(from);
                }
                Assert.Greater(upstream.Count, 0,
                    $"{Path.GetFileName(graphPath)}: parsed no edges — the edge shape changed and " +
                    "this test would silently pass on anything.");

                var state = new Dictionary<string, int>();   // 0 unvisited, 1 on stack, 2 done
                foreach (var node in upstream.Keys.ToList())
                    AssertNoCycleFrom(node, upstream, state, new List<string>(),
                                      Path.GetFileName(graphPath));

                // --- property nodes carry their property's concrete type ---------
                foreach (var node in blocks.Where(b =>
                             b.Contains("\"m_Type\": \"UnityEditor.ShaderGraph.PropertyNode\"")))
                {
                    var propId = Regex.Match(node, "\"m_Property\":\\s*\\{\\s*\"m_Id\": \"([0-9a-f]{32})\"");
                    var slotId = Regex.Match(node, "\"m_Slots\":[^]]*\"m_Id\": \"([0-9a-f]{32})\"");
                    if (!propId.Success || !slotId.Success) continue;
                    if (!byId.TryGetValue(propId.Groups[1].Value, out var prop)) continue;
                    if (!byId.TryGetValue(slotId.Groups[1].Value, out var slot)) continue;

                    var kind = Regex.Match(prop, "Internal\\.(Vector[1-4])ShaderProperty");
                    if (!kind.Success) continue;   // colors/textures/booleans: other families
                    string expected = kind.Groups[1].Value + "MaterialSlot";
                    Assert.IsTrue(slot.Contains(expected),
                        $"{Path.GetFileName(graphPath)}: the property node for " +
                        $"{Regex.Match(prop, "\"m_Name\": \"([^\"]*)\"").Groups[1].Value} is a " +
                        $"{kind.Groups[1].Value} property but its output slot is not a {expected}. " +
                        "No value of that width can reach the shader.");
                }
            }
        }

        static void AssertNoCycleFrom(string node, Dictionary<string, List<string>> upstream,
                                      Dictionary<string, int> state, List<string> stack, string graphName)
        {
            if (state.TryGetValue(node, out var s) && s != 0) return;
            state[node] = 1;
            stack.Add(node);
            if (upstream.TryGetValue(node, out var parents))
            {
                foreach (var parent in parents)
                {
                    state.TryGetValue(parent, out var ps);
                    Assert.AreNotEqual(1, ps,
                        $"{graphName} contains an edge CYCLE through node {parent}. ShaderGraph " +
                        "rejects a cyclic graph, so every material on it renders magenta. Something " +
                        "was spliced in FRONT of a node that was already DOWNSTREAM of it.");
                    if (ps == 0) AssertNoCycleFrom(parent, upstream, state, stack, graphName);
                }
            }
            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
        }

        [Test]
        public void EveryWiredGraph_MatchesTheHlslSignatureExactly()
        {
            // The gap neither the wiring script nor a code review can see: the script
            // asserts the graph is internally consistent and the HLSL compiles on its own,
            // but NOTHING checks that the node's slots still describe the function they
            // call. ShaderGraph passes slots positionally (inputs in list order, then
            // outputs), so a signature change on one side and not the other silently
            // shifts every argument — Velocity would arrive as Position and the shield
            // would morph toward a garbage point with no error anywhere.
            var (hlslInputs, hlslOutputs) = ReadHlslSignature();

            foreach (var graphPath in WiredGraphPaths)
            {
                string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

                var node = blocks.FirstOrDefault(b => b.Contains($"\"m_FunctionName\": \"{FunctionName}\""));
                Assert.IsNotNull(node, $"{Path.GetFileName(graphPath)} has no {FunctionName} node.");

                var slotIds = Regex.Matches(node.Substring(node.IndexOf("\"m_Slots\"", System.StringComparison.Ordinal)),
                                            "\"m_Id\": \"([0-9a-f]{32})\"")
                                   .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

                var inputs = new List<string>();
                var outputs = new List<string>();
                foreach (var id in slotIds)
                {
                    var slot = blocks.FirstOrDefault(b => b.Contains($"\"m_ObjectId\": \"{id}\"") &&
                                                          b.Contains("MaterialSlot"));
                    Assert.IsNotNull(slot, $"{Path.GetFileName(graphPath)}: slot {id} is missing.");
                    string name = Regex.Match(slot, "\"m_DisplayName\": \"([^\"]*)\"").Groups[1].Value;
                    bool isOutput = Regex.Match(slot, "\"m_SlotType\": (\\d+)").Groups[1].Value == "1";
                    (isOutput ? outputs : inputs).Add(name);
                }

                CollectionAssert.AreEqual(hlslInputs, inputs,
                    $"{Path.GetFileName(graphPath)}: the {FunctionName} node's INPUT slots no longer match " +
                    $"{Path.GetFileName(HlslPath)}'s parameter order. Re-run Tools/Shaders/wire_prism_shield_morph.py.");
                CollectionAssert.AreEqual(hlslOutputs, outputs,
                    $"{Path.GetFileName(graphPath)}: the {FunctionName} node's OUTPUT slots no longer match " +
                    $"{Path.GetFileName(HlslPath)}'s out-parameter order.");
            }
        }

        /// <summary>Parameter names of PrismShieldMorph_float, split into ins and outs.</summary>
        static (List<string> inputs, List<string> outputs) ReadHlslSignature()
        {
            string hlsl = File.ReadAllText(HlslPath);
            int start = hlsl.IndexOf($"void {FunctionName}_float(", System.StringComparison.Ordinal);
            Assert.Greater(start, -1, $"{HlslPath} does not declare {FunctionName}_float.");
            start = hlsl.IndexOf('(', start) + 1;
            int end = hlsl.IndexOf(')', start);
            Assert.Greater(end, start, "unterminated parameter list");

            var inputs = new List<string>();
            var outputs = new List<string>();
            foreach (var raw in hlsl.Substring(start, end - start).Split(','))
            {
                var parts = raw.Split(new[] { ' ', '\t', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                Assert.GreaterOrEqual(parts.Length, 2, $"unparsable parameter '{raw}'");
                bool isOut = parts[0] == "out";
                (isOut ? outputs : inputs).Add(parts[parts.Length - 1]);
            }
            return (inputs, outputs);
        }

        [Test]
        public void BothGenerators_BakeTheDebrisAttributeSet()
        {
            // A shield SHATTERS as ordinary prism-explosion debris on ExplodingBlockGraph
            // (Docs/PRISM_ANIMATION.md §4.8.1), whose vertex chain and fade are pure
            // functions of the mesh's own attributes. The port therefore lives HERE, in
            // the mesh: UV0 is the frame PrismErosionFade wipes across (an empty channel
            // makes each face POP instead of eroding), and the TANGENT is one of
            // RotateFacesAlongAxis' two rotation axes (a missing tangent hands the
            // subgraph a zero axis and the per-face rotation silently degenerates —
            // exactly the "faces do not rotate away" regression this task was about).
            foreach (var (mesh, verts) in new[]
                     {
                         (OctahedronMeshGenerator.Generate(new Vector3(0.5f, 1.25f, 2f)), 24),
                         (StellatedOctahedronMeshGenerator.Generate(new Vector3(0.5f, 1.25f, 2f)),
                          StellatedOctahedronMeshGenerator.VERTEX_COUNT),
                     })
            {
                try
                {
                    var uvs = new List<Vector2>();
                    mesh.GetUVs(OctahedronMeshGenerator.ErosionUVChannel, uvs);
                    Assert.AreEqual(verts, uvs.Count,
                        "UV0 must carry one erosion-frame coordinate per vertex — an empty " +
                        "channel makes every face pop instead of eroding.");

                    var tangents = mesh.tangents;
                    var normals = mesh.normals;
                    var positions = mesh.vertices;
                    Assert.AreEqual(verts, tangents.Length,
                        "the debris pipeline's per-face rotation reads mesh tangents — " +
                        "an empty channel degenerates the rotation to nothing");

                    for (int f = 0; f < verts; f += 3)
                    {
                        // A real 2D frame: three distinct, non-collinear coordinates. Any
                        // degenerate face would give the wipe no direction to run in.
                        Vector2 a = uvs[f], b = uvs[f + 1], c = uvs[f + 2];
                        float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y));
                        Assert.Greater(area, 0.1f,
                            $"face {f / 3}'s UV0 triangle is degenerate — the erosion front " +
                            "would have no frame to sweep across.");
                        foreach (var uv in new[] { a, b, c })
                        {
                            Assert.That(uv.x, Is.InRange(0f, 1f), "UV0 must stay inside the unit square");
                            Assert.That(uv.y, Is.InRange(0f, 1f), "UV0 must stay inside the unit square");
                        }

                        // The tangent: unit length, in the face plane (the standard dP/dU an
                        // imported mesh carries — v0→v1 IS the U axis of the frame above).
                        Vector3 t = tangents[f];
                        Assert.That(t.magnitude, Is.EqualTo(1f).Within(1e-3f),
                            $"face {f / 3}'s tangent is not unit length");
                        Assert.That(Mathf.Abs(Vector3.Dot(t, normals[f])), Is.LessThan(1e-3f),
                            $"face {f / 3}'s tangent is not in the face plane");
                        Assert.That(Vector3.Dot(t, (positions[f + 1] - positions[f]).normalized),
                            Is.EqualTo(1f).Within(1e-3f),
                            $"face {f / 3}'s tangent is not dP/dU of its UV frame");
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        // ── 3. no CPU ticker, now or ever again ──────────────────────────────

        [Test]
        public void TheShieldMorphTicker_IsRetired()
        {
            Assert.IsFalse(File.Exists(RetiredTickerPath),
                "PrismOctahedronShieldManager is back. The shield morphs are GPU-clocked " +
                "(Docs/PRISM_ANIMATION.md §5 B4) and nothing may tick them; its active set is " +
                "empty by construction because no shield registers any more.");
        }

        [Test]
        public void NeitherShield_DrivesItsMorphFromTheCpu()
        {
            foreach (var path in new[] { OctahedronShieldPath, StellatedShieldPath })
            {
                Assert.IsTrue(File.Exists(path), $"{path} is missing.");
                string src = File.ReadAllText(path);

                // A per-frame driver of any flavour. The morph is one stamp; anything that
                // advances it over time on the CPU is the regression this migration removed.
                foreach (var forbidden in new[]
                         {
                             "void Update(", "void LateUpdate(", "void FixedUpdate(",
                             "StartCoroutine", "DOTween", "PrismOctahedronShieldManager",
                             "PopulateMeshFaceScale", "PopulateMeshFaceShatter",
                         })
                {
                    Assert.IsFalse(src.Contains(forbidden),
                        $"{Path.GetFileName(path)} contains '{forbidden}'. Shield morphs are " +
                        "f(clock, stamp) on the GPU — see Docs/PRISM_ANIMATION.md §5 B4 and the " +
                        "clock-material law in CLAUDE.md ▸ Anti-Patterns.");
                }
            }
        }

        [Test]
        public void TheCpuMorphMeshRebuilders_AreGone()
        {
            // Their absence is what makes the shared-mesh batching guarantee hold: a
            // per-prism morph mesh is a per-prism draw call, whatever else is true.
            foreach (var path in new[]
                     {
                         "Assets/_Scripts/Utility/OctahedronMeshGenerator.cs",
                         "Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs",
                     })
            {
                string src = File.ReadAllText(path);
                Assert.IsFalse(src.Contains("public static void PopulateMeshFaceScale"),
                    $"{Path.GetFileName(path)} still offers a CPU per-face morph rebuild.");
                Assert.IsFalse(src.Contains("public static void PopulateMeshFaceShatter"),
                    $"{Path.GetFileName(path)} still offers a CPU per-face shatter rebuild.");
            }
        }

        [Test]
        public void ShedPalette_DistinguishesCapturedFromResolved()
        {
            // The sentinel that routes RequestShatter: default means "nobody captured the
            // outgoing look", which is the ONLY case allowed to fall back to the renderer.
            Assert.IsFalse(default(PrismShedPalette).HasValue,
                "A default PrismShedPalette must read as 'not captured'.");
            Assert.IsTrue(new PrismShedPalette(Color.red, Color.blue).HasValue,
                "A constructed PrismShedPalette must read as captured.");

            // A resolved palette is never the black an uninitialised struct carries — a
            // shatter that falls back must still be visible.
            var noMaterial = PrismShedPalette.FromMaterial(null);
            Assert.IsTrue(noMaterial.HasValue);
            Assert.AreEqual(Color.white, noMaterial.Bright);
            Assert.AreEqual(Color.white, noMaterial.Dark);
        }

        [Test]
        public void EveryShieldDisengage_IsHandedACapturedPalette()
        {
            // Docs/PRISM_ANIMATION.md §4.8.1: a state change binds its END-STATE material
            // first, so PrismStateManager's renderer already wears the incoming tier by the
            // time it disengages. Any Disengage call there that does not carry the capture
            // paints its shards in the tier the prism is BECOMING — which is exactly the
            // defect this rule exists to prevent, and it is invisible without a playtest.
            const string path = "Assets/_Scripts/Controller/Managers/PrismStateManager.cs";
            string src = File.ReadAllText(path);

            Assert.IsTrue(Regex.IsMatch(src, @"CaptureShedPalette\s*\(\s*\)\s*=>"),
                "PrismStateManager must own the capture helper (CaptureShedPalette).");

            var calls = Regex.Matches(src, @"\.Disengage\(([^)]*)\)")
                             .Cast<Match>()
                             .Select(m => m.Groups[1].Value)
                             .ToList();
            Assert.IsNotEmpty(calls, "Expected PrismStateManager to disengage shields.");

            foreach (var args in calls)
                Assert.IsTrue(args.TrimEnd().EndsWith("shed"),
                    $"PrismStateManager calls Disengage({args}) without the captured shed " +
                    "palette as its last argument — those shards will wear the INCOMING " +
                    "tier's colours (Docs/PRISM_ANIMATION.md §4.8.1).");
        }
    }
}
#endif
