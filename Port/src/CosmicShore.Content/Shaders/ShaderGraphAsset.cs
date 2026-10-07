using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CosmicShore.Content.Shaders
{
    /// <summary>
    /// A .shadergraph / .shadersubgraph read into its graph: nodes with their slots, the edges
    /// between slots, the blackboard properties, the master-stack blocks and the active target's
    /// render settings. Shader Graph v2+ stores a stream of JSON objects, each addressed by
    /// <c>m_ObjectId</c> and referenced elsewhere as <c>{"m_Id": ...}</c>; this resolves those
    /// references. Read-only: nothing here writes an asset.
    /// </summary>
    public sealed class ShaderGraphAsset
    {
        public string Path;
        public string Guid;
        public bool IsSubGraph;
        public readonly List<SgNode> Nodes = new();
        public readonly Dictionary<string, SgNode> NodeById = new(StringComparer.Ordinal);
        public readonly List<SgProperty> Properties = new();
        public readonly Dictionary<string, SgProperty> PropertyById = new(StringComparer.Ordinal);
        public readonly List<SgEdge> Edges = new();
        public readonly List<SgNode> VertexBlocks = new();
        public readonly List<SgNode> FragmentBlocks = new();
        public readonly List<string> Keywords = new();
        /// <summary>A sub-graph's output node (its input slots are the sub-graph's outputs).</summary>
        public SgNode OutputNode;
        public SgTarget Target;

        readonly Dictionary<(string, int), (SgNode Node, SgSlot Slot)> _incoming = new();

        /// <summary>The output slot feeding an input slot, or (null, null) when it is unconnected.</summary>
        public (SgNode Node, SgSlot Slot) Source(SgNode node, int inputSlot)
            => _incoming.TryGetValue((node.Id, inputSlot), out var s) ? s : (null, null);

        public static ShaderGraphAsset Load(string path, string guid = null)
        {
            var g = Parse(File.ReadAllText(path));
            g.Path = path;
            g.Guid = guid;
            g.IsSubGraph = path.EndsWith(".shadersubgraph", StringComparison.OrdinalIgnoreCase);
            return g;
        }

        public static ShaderGraphAsset Parse(string text)
        {
            var objects = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            JsonElement? graphData = null;
            var bytes = Encoding.UTF8.GetBytes(text);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, AllowMultipleValues = true });
            while (true)
            {
                JsonElement root;
                try
                {
                    if (!reader.Read()) break;
                    if (reader.TokenType != JsonTokenType.StartObject) continue;
                    using var doc = JsonDocument.ParseValue(ref reader);
                    root = doc.RootElement.Clone();
                }
                catch (JsonException) { break; } // a truncated tail: keep what parsed
                var id = Str(root, "m_ObjectId");
                if (id != null) objects[id] = root;
                if (Str(root, "m_Type") == "UnityEditor.ShaderGraph.GraphData") graphData = root;
            }
            var g = new ShaderGraphAsset();
            if (graphData is not { } gd) throw new InvalidDataException("no GraphData object (not a Shader Graph v2+ file)");

            foreach (var r in Refs(gd, "m_Properties"))
                if (objects.TryGetValue(r, out var p)) { var prop = SgProperty.From(r, p); g.Properties.Add(prop); g.PropertyById[r] = prop; }
            foreach (var r in Refs(gd, "m_Keywords"))
                if (objects.TryGetValue(r, out var k)) g.Keywords.Add(Str(k, "m_OverrideReferenceName") is { Length: > 0 } o ? o : Str(k, "m_DefaultReferenceName"));
            foreach (var r in Refs(gd, "m_Nodes"))
            {
                if (!objects.TryGetValue(r, out var n)) continue;
                var node = SgNode.From(r, n, objects);
                g.Nodes.Add(node);
                g.NodeById[r] = node;
            }
            if (gd.TryGetProperty("m_Edges", out var edges) && edges.ValueKind == JsonValueKind.Array)
                foreach (var e in edges.EnumerateArray())
                {
                    var edge = new SgEdge
                    {
                        OutputNode = Str(e.GetProperty("m_OutputSlot").GetProperty("m_Node"), "m_Id"),
                        OutputSlot = e.GetProperty("m_OutputSlot").GetProperty("m_SlotId").GetInt32(),
                        InputNode = Str(e.GetProperty("m_InputSlot").GetProperty("m_Node"), "m_Id"),
                        InputSlot = e.GetProperty("m_InputSlot").GetProperty("m_SlotId").GetInt32(),
                    };
                    g.Edges.Add(edge);
                    if (g.NodeById.TryGetValue(edge.OutputNode, out var from) && from.Slot(edge.OutputSlot) is { } slot)
                        g._incoming[(edge.InputNode, edge.InputSlot)] = (from, slot);
                }
            foreach (var (ctx, list) in new[] { ("m_VertexContext", g.VertexBlocks), ("m_FragmentContext", g.FragmentBlocks) })
                if (gd.TryGetProperty(ctx, out var c))
                    foreach (var r in Refs(c, "m_Blocks"))
                        if (g.NodeById.TryGetValue(r, out var b)) list.Add(b);
            if (gd.TryGetProperty("m_OutputNode", out var on) && Str(on, "m_Id") is { Length: > 0 } oid)
                g.NodeById.TryGetValue(oid, out g.OutputNode);
            foreach (var r in Refs(gd, "m_ActiveTargets"))
                if (objects.TryGetValue(r, out var t) && SgTarget.From(t, objects) is { } target) { g.Target = target; break; }
            return g;
        }

        internal static IEnumerable<string> Refs(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) yield break;
            foreach (var item in arr.EnumerateArray())
                if (Str(item, "m_Id") is { Length: > 0 } id) yield return id;
        }

        internal static string Str(JsonElement e, string name)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        internal static int Int(JsonElement e, string name, int fallback = 0)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

        internal static bool Bool(JsonElement e, string name, bool fallback = false)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
                ? v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => fallback }
                : fallback;

        internal static float F(JsonElement e, string name, float fallback = 0f)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : fallback;

        /// <summary>A guid inside one of Shader Graph's nested serialized-reference strings (sub-graph, texture).</summary>
        internal static string NestedGuid(string serialized)
        {
            if (string.IsNullOrEmpty(serialized)) return null;
            try
            {
                using var doc = JsonDocument.Parse(serialized);
                foreach (var p in doc.RootElement.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.Object && Str(p.Value, "guid") is { Length: > 0 } guid) return guid;
            }
            catch (JsonException) { }
            return null;
        }
    }

    public sealed class SgEdge
    {
        public string OutputNode, InputNode;
        public int OutputSlot, InputSlot;
    }

    public sealed class SgNode
    {
        public string Id;
        /// <summary>The short type name, e.g. "MultiplyNode".</summary>
        public string Type;
        public string Name;
        public JsonElement Json;
        public readonly List<SgSlot> Slots = new();

        public SgSlot Slot(int id)
        {
            foreach (var s in Slots) if (s.Id == id) return s;
            return null;
        }

        public IEnumerable<SgSlot> Inputs { get { foreach (var s in Slots) if (!s.IsOutput) yield return s; } }
        public IEnumerable<SgSlot> Outputs { get { foreach (var s in Slots) if (s.IsOutput) yield return s; } }

        public string Str(string name) => ShaderGraphAsset.Str(Json, name);
        public int Int(string name, int fallback = 0) => ShaderGraphAsset.Int(Json, name, fallback);
        public bool Bool(string name, bool fallback = false) => ShaderGraphAsset.Bool(Json, name, fallback);

        internal static SgNode From(string id, JsonElement e, Dictionary<string, JsonElement> objects)
        {
            var full = ShaderGraphAsset.Str(e, "m_Type") ?? "";
            var n = new SgNode { Id = id, Json = e, Type = full.Substring(full.LastIndexOf('.') + 1), Name = ShaderGraphAsset.Str(e, "m_Name") };
            foreach (var r in ShaderGraphAsset.Refs(e, "m_Slots"))
                if (objects.TryGetValue(r, out var s)) n.Slots.Add(SgSlot.From(s));
            return n;
        }

        public override string ToString() => $"{Type} '{Name}'";
    }

    public sealed class SgSlot
    {
        public int Id;
        /// <summary>The short slot type without "MaterialSlot", e.g. "DynamicVector", "Vector3", "UV".</summary>
        public string Type;
        public string DisplayName;
        public string ShaderOutputName;
        public bool IsOutput;
        public JsonElement Json;

        public JsonElement Value => Json.TryGetProperty("m_Value", out var v) ? v : default;
        public int Space => ShaderGraphAsset.Int(Json, "m_Space");
        public int Channel => ShaderGraphAsset.Int(Json, "m_Channel");
        public int ScreenSpaceType => ShaderGraphAsset.Int(Json, "m_ScreenSpaceType");

        internal static SgSlot From(JsonElement e)
        {
            var full = ShaderGraphAsset.Str(e, "m_Type") ?? "";
            var t = full.Substring(full.LastIndexOf('.') + 1);
            if (t.EndsWith("MaterialSlot", StringComparison.Ordinal)) t = t.Substring(0, t.Length - "MaterialSlot".Length);
            return new SgSlot
            {
                Id = ShaderGraphAsset.Int(e, "m_Id"),
                Type = t,
                DisplayName = ShaderGraphAsset.Str(e, "m_DisplayName"),
                ShaderOutputName = ShaderGraphAsset.Str(e, "m_ShaderOutputName"),
                IsOutput = ShaderGraphAsset.Int(e, "m_SlotType") == 1,
                Json = e,
            };
        }

        public override string ToString() => $"{Type} {Id} '{ShaderOutputName}'";
    }

    public sealed class SgProperty
    {
        public string Id;
        /// <summary>Short type, e.g. "ColorShaderProperty".</summary>
        public string Type;
        public string Name;
        /// <summary>The name materials and globals use: the override reference if set, else the default one.</summary>
        public string Reference;
        public string Guid;
        public JsonElement Value;
        /// <summary>A Texture2D property's default when unset: 0 white, 1 black, 2 grey, 3 bump.</summary>
        public int DefaultTextureType;
        public bool Exposed;

        internal static SgProperty From(string id, JsonElement e)
        {
            var full = ShaderGraphAsset.Str(e, "m_Type") ?? "";
            var reference = ShaderGraphAsset.Str(e, "m_OverrideReferenceName");
            if (string.IsNullOrEmpty(reference)) reference = ShaderGraphAsset.Str(e, "m_DefaultReferenceName");
            return new SgProperty
            {
                Id = id,
                Type = full.Substring(full.LastIndexOf('.') + 1),
                Name = ShaderGraphAsset.Str(e, "m_Name"),
                Reference = reference,
                Guid = e.TryGetProperty("m_Guid", out var g) ? ShaderGraphAsset.Str(g, "m_GuidSerialized") : null,
                Value = e.TryGetProperty("m_Value", out var v) ? v : default,
                DefaultTextureType = ShaderGraphAsset.Int(e, "m_DefaultType"),
                Exposed = ShaderGraphAsset.Bool(e, "m_GeneratePropertyBlock", true),
            };
        }
    }

    /// <summary>The active target's settings that decide render state (URP's UniversalTarget).</summary>
    public sealed class SgTarget
    {
        public string Type, SubTarget;
        public int SurfaceType, AlphaMode, RenderFace, ZWriteControl, ZTestMode;
        public bool AlphaClip, AllowMaterialOverride;

        public bool Lit => SubTarget != null && SubTarget.Contains("Lit") && !SubTarget.Contains("Unlit");

        internal static SgTarget From(JsonElement t, Dictionary<string, JsonElement> objects)
        {
            var type = ShaderGraphAsset.Str(t, "m_Type") ?? "";
            var target = new SgTarget { Type = type.Substring(type.LastIndexOf('.') + 1) };
            if (t.TryGetProperty("m_ActiveSubTarget", out var sub) && ShaderGraphAsset.Str(sub, "m_Id") is { } sid && objects.TryGetValue(sid, out var s))
            {
                var st = ShaderGraphAsset.Str(s, "m_Type") ?? "";
                target.SubTarget = st.Substring(st.LastIndexOf('.') + 1);
            }
            target.SurfaceType = ShaderGraphAsset.Int(t, "m_SurfaceType");
            target.AlphaMode = ShaderGraphAsset.Int(t, "m_AlphaMode");
            target.RenderFace = ShaderGraphAsset.Int(t, "m_RenderFace", 2);
            target.ZWriteControl = ShaderGraphAsset.Int(t, "m_ZWriteControl");
            target.ZTestMode = ShaderGraphAsset.Int(t, "m_ZTestMode", 4);
            target.AlphaClip = ShaderGraphAsset.Bool(t, "m_AlphaClip");
            target.AllowMaterialOverride = ShaderGraphAsset.Bool(t, "m_AllowMaterialOverride");
            // HDRP's targets keep these in a SystemData object.
            foreach (var r in ShaderGraphAsset.Refs(t, "m_Datas"))
                if (objects.TryGetValue(r, out var d) && (ShaderGraphAsset.Str(d, "m_Type") ?? "").EndsWith("SystemData", StringComparison.Ordinal))
                {
                    target.SurfaceType = ShaderGraphAsset.Int(d, "m_SurfaceType");
                    target.AlphaClip = ShaderGraphAsset.Bool(d, "m_AlphaTest");
                    target.RenderFace = ShaderGraphAsset.Int(d, "m_DoubleSidedMode") != 0 ? 0 : 2;
                }
            return target;
        }
    }
}
