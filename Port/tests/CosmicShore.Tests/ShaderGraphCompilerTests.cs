using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content.Shaders;
using CosmicShore.Engine;
using CosmicShore.Render;

namespace CosmicShore.Tests;

// C2: the Shader Graph compiler over the REAL project. These are the coverage gates: a graph that
// stops parsing, a node type the project starts using, or a custom function nobody ported fails
// here (with its name) instead of silently drawing through a fallback.
public class ShaderGraphCompilerTests
{
    static IEnumerable<string> GraphPaths(bool subGraphs = true)
    {
        var root = ContentYamlTests.ProjectRoot;
        if (root == null) yield break;
        foreach (var p in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.shadergraph", SearchOption.AllDirectories)) yield return p;
        if (subGraphs)
            foreach (var p in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.shadersubgraph", SearchOption.AllDirectories)) yield return p;
    }

    static ShaderGraphCatalog Catalog() => new(ContentYamlTests.Db);

    [Fact]
    public void EveryProjectGraphAndSubGraph_Parses()
    {
        var failures = new List<string>();
        int parsed = 0;
        foreach (var p in GraphPaths())
        {
            try { ShaderGraphAsset.Load(p); parsed++; }
            catch (InvalidDataException e) { failures.Add($"{p}: {e.Message}"); }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        if (ContentYamlTests.ProjectRoot != null) Assert.True(parsed >= 85, $"only {parsed} graphs parsed");
    }

    [Fact]
    public void EveryNodeTypeTheProjectUses_HasAnEmitter()
    {
        var supported = new HashSet<string>(ShaderGraphCompiler.SupportedNodeTypes.Concat(ShaderGraphCompiler.StructuralNodeTypes));
        var missing = new SortedDictionary<string, string>();
        foreach (var p in GraphPaths())
        {
            ShaderGraphAsset g;
            try { g = ShaderGraphAsset.Load(p); } catch (InvalidDataException) { continue; }
            foreach (var n in g.Nodes)
                if (!supported.Contains(n.Type)) missing.TryAdd(n.Type, Path.GetFileName(p));
        }
        Assert.True(missing.Count == 0, "node types with no emitter: " + string.Join(", ", missing.Select(kv => $"{kv.Key} ({kv.Value})")));
    }

    [Fact]
    public void EveryCustomFunctionTheProjectCalls_IsInTheGlslLibrary()
    {
        var missing = new SortedSet<string>();
        int calls = 0;
        foreach (var p in GraphPaths())
        {
            ShaderGraphAsset g;
            try { g = ShaderGraphAsset.Load(p); } catch (InvalidDataException) { continue; }
            foreach (var n in g.Nodes.Where(n => n.Type == "CustomFunctionNode" && n.Int("m_SourceType") == 0))
            {
                calls++;
                var fn = n.Str("m_FunctionName") + "_float";
                if (!ShaderGraphLibrary.Defines(fn)) missing.Add($"{fn} ({Path.GetFileName(p)})");
            }
        }
        Assert.True(missing.Count == 0, "custom functions not ported to ShaderGraphLibrary.glsl: " + string.Join(", ", missing));
        if (ContentYamlTests.ProjectRoot != null) Assert.True(calls >= 70, $"only {calls} custom function calls found");
    }

    [Fact]
    public void EveryProjectGraph_Compiles()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        var catalog = Catalog();
        var db = ContentYamlTests.Db;
        var failures = new List<string>();
        int compiled = 0;
        foreach (var p in GraphPaths(subGraphs: false))
        {
            var guid = db.GuidOf(p);
            var prog = catalog.For(guid);
            if (prog == null) continue; // a v1 graph
            if (!prog.Ok) failures.Add($"{db.ProjectRelative(p)}: {prog.Error}");
            else
            {
                compiled++;
                Assert.Contains("void sg_vertex(", prog.Vertex);
                Assert.Contains("SgSurface sg_surface()", prog.Fragment);
            }
        }
        Assert.True(failures.Count == 0, "graphs that did not compile:\n" + string.Join("\n", failures));
        Assert.True(compiled >= 50, $"only {compiled} graphs compiled");
    }

    [Fact]
    public void CompiledGlsl_IsAscii()
    {
        // GlProgram hands the driver a character count; a non-ASCII character truncates the source.
        Assert.DoesNotContain(ShaderGraphLibrary.Source, c => c > 127);
        if (ContentYamlTests.ProjectRoot == null) return;
        var catalog = Catalog();
        foreach (var p in GraphPaths(subGraphs: false))
            if (catalog.For(ContentYamlTests.Db.GuidOf(p)) is { Ok: true } prog)
            {
                var (vs, fs) = GraphProgramCache.Sources(prog);
                Assert.True(vs.All(c => c <= 127) && fs.All(c => c <= 127), $"{p}: non-ASCII GLSL");
            }
    }

    [Fact]
    public void HandTunedFamilies_AreKeyedByTheGuidOfTheAssetTheyWereWrittenFor()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        foreach (var (guid, (kind, path)) in MaterialFamilies.ByGuid)
        {
            Assert.Equal(guid, ContentYamlTests.Db.GuidOf(path));
            Assert.NotEqual(MaterialFamilies.Kind.None, kind);
        }
    }

    [Fact]
    public void RenamedShader_KeepsItsFamily()
    {
        var s = Shader.Find("Shader Graphs/BlockGraph (renamed for the test)", "bf8c159f627e64b439094797bff88611", "Assets/x.shadergraph");
        Assert.Equal(MaterialFamilies.Kind.BlockGraph, MaterialFamilies.For(s));
    }

    [Fact]
    public void UnknownShader_WarnsOnce_BuiltinsNever()
    {
        var warnings = new List<string>();
        var previous = MaterialFamilies.Warn;
        MaterialFamilies.Warn = warnings.Add;
        try
        {
            var unknown = Shader.Find("Test/UnknownForWarnOnce", "0123456789abcdef0123456789abcdef", "Assets/Test/Unknown.shader");
            Assert.True(MaterialFamilies.WarnUntranslated(unknown, "Unlit"));
            Assert.False(MaterialFamilies.WarnUntranslated(unknown, "Unlit"));
            Assert.False(MaterialFamilies.WarnUntranslated(Shader.Find("Universal Render Pipeline/Lit"), "Lit"));
            var w = Assert.Single(warnings);
            Assert.Contains("Test/UnknownForWarnOnce", w);
            Assert.Contains("Assets/Test/Unknown.shader", w);
        }
        finally { MaterialFamilies.Warn = previous; }
    }

    [Fact]
    public void Cast_FollowsShaderGraphConversions()
    {
        Assert.Equal("vec4(x, 1.0)", ShaderGraphCompiler.ConvertVector("x", 3, 4));
        Assert.Equal("vec4(x, 0.0, 1.0)", ShaderGraphCompiler.ConvertVector("x", 2, 4));
        Assert.Equal("vec3(x)", ShaderGraphCompiler.ConvertVector("x", 1, 3));
        Assert.Equal("x.xy", ShaderGraphCompiler.ConvertVector("x", 4, 2));
        Assert.Equal("x.x", ShaderGraphCompiler.ConvertVector("x", 3, 1));
    }

    [Fact]
    public void SmallGraph_EmitsTheNodeFormulas()
    {
        // Property(_Tint) * Fresnel(power 2) -> BaseColor; Time -> Alpha via Sine.
        var graph = ShaderGraphAsset.Parse(TinyGraph);
        var prog = new ShaderGraphCompiler(_ => null).Compile(graph, "Test/Tiny");
        Assert.True(prog.Ok, prog.Error);
        Assert.Contains("uniform vec4 _Tint;", prog.Uniforms);
        Assert.Contains("pow(1.0 - clamp(dot(normalize(normalize(sg_NrmWS)), normalize(normalize((sg_CamPos - sg_PosWS)))), 0.0, 1.0), 2.0)", prog.Fragment);
        Assert.Contains("sin(sg_Time.x)", prog.Fragment);
        Assert.Contains("s.BaseColor = ", prog.Fragment);
        Assert.True(prog.Transparent);
        Assert.Equal(0, prog.Cull);
    }

    const string TinyGraph = @"{
    ""m_SGVersion"": 3, ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"", ""m_ObjectId"": ""g"",
    ""m_Properties"": [{ ""m_Id"": ""p"" }], ""m_Keywords"": [],
    ""m_Nodes"": [{ ""m_Id"": ""prop"" }, { ""m_Id"": ""fres"" }, { ""m_Id"": ""mul"" }, { ""m_Id"": ""time"" }, { ""m_Id"": ""sin"" }, { ""m_Id"": ""bc"" }, { ""m_Id"": ""al"" }],
    ""m_Edges"": [
        { ""m_OutputSlot"": { ""m_Node"": { ""m_Id"": ""prop"" }, ""m_SlotId"": 0 }, ""m_InputSlot"": { ""m_Node"": { ""m_Id"": ""mul"" }, ""m_SlotId"": 0 } },
        { ""m_OutputSlot"": { ""m_Node"": { ""m_Id"": ""fres"" }, ""m_SlotId"": 3 }, ""m_InputSlot"": { ""m_Node"": { ""m_Id"": ""mul"" }, ""m_SlotId"": 1 } },
        { ""m_OutputSlot"": { ""m_Node"": { ""m_Id"": ""mul"" }, ""m_SlotId"": 2 }, ""m_InputSlot"": { ""m_Node"": { ""m_Id"": ""bc"" }, ""m_SlotId"": 0 } },
        { ""m_OutputSlot"": { ""m_Node"": { ""m_Id"": ""time"" }, ""m_SlotId"": 0 }, ""m_InputSlot"": { ""m_Node"": { ""m_Id"": ""sin"" }, ""m_SlotId"": 0 } },
        { ""m_OutputSlot"": { ""m_Node"": { ""m_Id"": ""sin"" }, ""m_SlotId"": 1 }, ""m_InputSlot"": { ""m_Node"": { ""m_Id"": ""al"" }, ""m_SlotId"": 0 } }
    ],
    ""m_VertexContext"": { ""m_Blocks"": [] },
    ""m_FragmentContext"": { ""m_Blocks"": [{ ""m_Id"": ""bc"" }, { ""m_Id"": ""al"" }] },
    ""m_OutputNode"": { ""m_Id"": """" },
    ""m_ActiveTargets"": [{ ""m_Id"": ""t"" }]
}
{ ""m_Type"": ""UnityEditor.ShaderGraph.Internal.ColorShaderProperty"", ""m_ObjectId"": ""p"", ""m_Name"": ""Tint"", ""m_DefaultReferenceName"": ""Color_1"", ""m_OverrideReferenceName"": ""_Tint"", ""m_Value"": { ""r"": 1.0, ""g"": 0.5, ""b"": 0.25, ""a"": 1.0 } }
{ ""m_Type"": ""UnityEditor.ShaderGraph.PropertyNode"", ""m_ObjectId"": ""prop"", ""m_Property"": { ""m_Id"": ""p"" }, ""m_Slots"": [{ ""m_Id"": ""s_prop"" }] }
{ ""m_Type"": ""UnityEditor.ShaderGraph.Vector4MaterialSlot"", ""m_ObjectId"": ""s_prop"", ""m_Id"": 0, ""m_SlotType"": 1, ""m_ShaderOutputName"": ""Out"" }
{ ""m_Type"": ""UnityEditor.ShaderGraph.FresnelNode"", ""m_ObjectId"": ""fres"", ""m_Slots"": [{ ""m_Id"": ""f0"" }, { ""m_Id"": ""f1"" }, { ""m_Id"": ""f2"" }, { ""m_Id"": ""f3"" }] }
{ ""m_Type"": ""UnityEditor.ShaderGraph.NormalMaterialSlot"", ""m_ObjectId"": ""f0"", ""m_Id"": 0, ""m_SlotType"": 0, ""m_ShaderOutputName"": ""Normal"", ""m_Space"": 2 }
{ ""m_Type"": ""UnityEditor.ShaderGraph.ViewDirectionMaterialSlot"", ""m_ObjectId"": ""f1"", ""m_Id"": 1, ""m_SlotType"": 0, ""m_ShaderOutputName"": ""ViewDir"", ""m_Space"": 2 }
{ ""m_Type"": ""UnityEditor.ShaderGraph.Vector1MaterialSlot"", ""m_ObjectId"": ""f2"", ""m_Id"": 2, ""m_SlotType"": 0, ""m_ShaderOutputName"": ""Power"", ""m_Value"": 2.0 }
{ ""m_Type"": ""UnityEditor.ShaderGraph.Vector1MaterialSlot"", ""m_ObjectId"": ""f3"", ""m_Id"": 3, ""m_SlotType"": 1, ""m_ShaderOutputName"": ""Out"" }
{ ""m_Type"": ""UnityEditor.ShaderGraph.MultiplyNode"", ""m_ObjectId"": ""mul"", ""m_Slots"": [{ ""m_Id"": ""m0"" }, { ""m_Id"": ""m1"" }, { ""m_Id"": ""m2"" }] }
{ ""m_Type"": ""UnityEditor.ShaderGraph.DynamicValueMaterialSlot"", ""m_ObjectId"": ""m0"", ""m_Id"": 0, ""m_SlotType"": 0, ""m_ShaderOutputName"": ""A"" }
{ ""m_Type"": ""UnityEditor.ShaderGraph.DynamicValueMaterialSlot"", ""m_ObjectId"": ""m1"", ""m_Id"": 1, ""m_SlotType"": 0, ""m_ShaderOutputName"": ""B"" }
{ ""m_Type"": ""UnityEditor.ShaderGraph.DynamicValueMaterialSlot"", ""m_ObjectId"": ""m2"", ""m_Id"": 2, ""m_SlotType"": 1, ""m_ShaderOutputName"": ""Out"" }
{ ""m_Type"": ""UnityEditor.ShaderGraph.TimeNode"", ""m_ObjectId"": ""time"", ""m_Slots"": [{ ""m_Id"": ""t0"" }] }
{ ""m_Type"": ""UnityEditor.ShaderGraph.Vector1MaterialSlot"", ""m_ObjectId"": ""t0"", ""m_Id"": 0, ""m_SlotType"": 1, ""m_ShaderOutputName"": ""Time"" }
{ ""m_Type"": ""UnityEditor.ShaderGraph.SineNode"", ""m_ObjectId"": ""sin"", ""m_Slots"": [{ ""m_Id"": ""si0"" }, { ""m_Id"": ""si1"" }] }
{ ""m_Type"": ""UnityEditor.ShaderGraph.DynamicVectorMaterialSlot"", ""m_ObjectId"": ""si0"", ""m_Id"": 0, ""m_SlotType"": 0, ""m_ShaderOutputName"": ""In"" }
{ ""m_Type"": ""UnityEditor.ShaderGraph.DynamicVectorMaterialSlot"", ""m_ObjectId"": ""si1"", ""m_Id"": 1, ""m_SlotType"": 1, ""m_ShaderOutputName"": ""Out"" }
{ ""m_Type"": ""UnityEditor.ShaderGraph.BlockNode"", ""m_ObjectId"": ""bc"", ""m_SerializedDescriptor"": ""SurfaceDescription.BaseColor"", ""m_Slots"": [{ ""m_Id"": ""b0"" }] }
{ ""m_Type"": ""UnityEditor.ShaderGraph.ColorRGBMaterialSlot"", ""m_ObjectId"": ""b0"", ""m_Id"": 0, ""m_SlotType"": 0, ""m_ShaderOutputName"": ""BaseColor"", ""m_Value"": { ""x"": 0.5, ""y"": 0.5, ""z"": 0.5 } }
{ ""m_Type"": ""UnityEditor.ShaderGraph.BlockNode"", ""m_ObjectId"": ""al"", ""m_SerializedDescriptor"": ""SurfaceDescription.Alpha"", ""m_Slots"": [{ ""m_Id"": ""a0"" }] }
{ ""m_Type"": ""UnityEditor.ShaderGraph.Vector1MaterialSlot"", ""m_ObjectId"": ""a0"", ""m_Id"": 0, ""m_SlotType"": 0, ""m_ShaderOutputName"": ""Alpha"", ""m_Value"": 1.0 }
{ ""m_Type"": ""UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"", ""m_ObjectId"": ""t"", ""m_ActiveSubTarget"": { ""m_Id"": ""st"" }, ""m_SurfaceType"": 1, ""m_AlphaMode"": 0, ""m_RenderFace"": 0, ""m_AlphaClip"": false }
{ ""m_Type"": ""UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget"", ""m_ObjectId"": ""st"" }
";
}
