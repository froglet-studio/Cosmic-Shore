using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using CosmicShore.Engine;

namespace CosmicShore.Content.Shaders
{
    /// <summary>
    /// Shader Graph to GLSL. Walks back from the master-stack blocks of each stage and emits one
    /// typed GLSL statement per node, following Shader Graph's own generated HLSL (the node
    /// formulas are the package's, taken from com.unity.shadergraph 17.3). Sub-graphs are inlined
    /// per use; Custom Function nodes call the hand-ported library the renderer prepends
    /// (<c>CosmicShore.Render/Glsl/ShaderGraphLibrary.glsl</c>), so each of the project's HLSL
    /// functions is ported once.
    ///
    /// <para>The emitted code is written against the renderer's symbol contract, which the
    /// renderer's template provides in both stages:</para>
    /// <list type="bullet">
    /// <item>per-vertex: <c>sg_PosOS, sg_NrmOS, sg_TanOS (vec4), sg_PosWS, sg_NrmWS, sg_TanWS (vec4),
    /// sg_Uv0, sg_Uv1, sg_Color (vec4), sg_VertexID, sg_FrontFace, sg_ScreenPosRaw (vec4),
    /// sg_ObjectToWorld, sg_WorldToObject</c>;</item>
    /// <item>uniforms: <c>sg_View, sg_InvView, sg_Proj, sg_ViewProj, sg_CamPos, sg_CamDir, sg_Time
    /// (t, sin t, cos t, dt), sg_ScreenParams (w, h, 1+1/w, 1+1/h), sg_ProjectionParams (sign,
    /// near, far, 1/far), sg_OrthoParams</c> and the default textures <c>sg_White, sg_Black, sg_Grey,
    /// sg_Bump</c>;</item>
    /// <item>the struct <c>SgSurface</c> and <c>sg_defaultSurface()</c>, and the macro
    /// <c>SG_FRAGMENT</c> in the fragment stage.</item>
    /// </list>
    /// </summary>
    public sealed partial class ShaderGraphCompiler
    {
        readonly Func<string, ShaderGraphAsset> _subGraph;

        /// <param name="subGraphByGuid">Loads a sub-graph asset by guid (null when missing).</param>
        public ShaderGraphCompiler(Func<string, ShaderGraphAsset> subGraphByGuid) { _subGraph = subGraphByGuid; }

        /// <summary>Compiles a graph; never throws for a graph problem (the result carries <see cref="ShaderGraphProgram.Error"/>).</summary>
        public ShaderGraphProgram Compile(ShaderGraphAsset graph, string name)
        {
            var prog = new ShaderGraphProgram { Guid = graph.Guid, AssetPath = graph.Path, Name = name };
            try { CompileInto(graph, prog); }
            catch (SgException e) { prog.Error = e.Message; }
            return prog;
        }

        void CompileInto(ShaderGraphAsset graph, ShaderGraphProgram prog)
        {
            if (graph.IsSubGraph) throw new SgException("a sub-graph is not a shader");
            var t = graph.Target;
            if (t == null) throw new SgException("no active target");
            prog.Lit = t.Lit;
            prog.Transparent = t.SurfaceType == 1;
            prog.AlphaMode = t.AlphaMode;
            prog.AlphaClip = t.AlphaClip;
            prog.AllowMaterialOverride = t.AllowMaterialOverride;
            // URP RenderFace: Front = 2, Back = 1, Both = 0, the same numbers as the cull mode.
            prog.Cull = t.RenderFace == 2 ? 2 : t.RenderFace == 1 ? 1 : 0;
            prog.ZWrite = t.ZWriteControl switch { 1 => 1, 2 => 0, _ => -1 };
            if (graph.Keywords.Count > 0) prog.Approximations.Add($"Keywords: {string.Join(", ", graph.Keywords)} compiled with every keyword off");

            var unit = new Unit(this, prog);
            var uniforms = new StringBuilder();
            foreach (var p in graph.Properties)
                if (!string.IsNullOrEmpty(p.Reference)) unit.Declared.Add(UniformName(p.Reference));
            foreach (var p in graph.Properties) unit.DeclareProperty(p, uniforms);
            prog.Uniforms = uniforms.ToString();

            var top = new Scope { Graph = graph, Prefix = "" };

            var vs = new Stage(unit, fragment: false);
            string pos = "sg_PosOS", nrm = "sg_NrmOS", tan = "sg_TanOS.xyz";
            foreach (var b in graph.VertexBlocks)
            {
                var slot = b.Inputs.FirstOrDefault();
                if (slot == null) continue;
                switch (b.Str("m_SerializedDescriptor"))
                {
                    case "VertexDescription.Position": pos = vs.In(top, b, slot.Id, K.Vec3).E; break;
                    case "VertexDescription.Normal": nrm = vs.In(top, b, slot.Id, K.Vec3).E; break;
                    case "VertexDescription.Tangent": tan = vs.In(top, b, slot.Id, K.Vec3).E; break;
                    case var d: prog.Approximations.Add($"BlockNode: {d} ignored"); break;
                }
            }
            prog.Vertex = "void sg_vertex(out vec3 posOS, out vec3 nrmOS, out vec3 tanOS) {\n" + vs.Body
                + $"  posOS = {pos};\n  nrmOS = {nrm};\n  tanOS = {tan};\n}}\n";

            // Each stage expands the graph on its own: a value computed in one stage is not visible in the other.
            top = new Scope { Graph = graph, Prefix = "" };
            var fs = new Stage(unit, fragment: true);
            var assign = new StringBuilder();
            foreach (var b in graph.FragmentBlocks)
            {
                var slot = b.Inputs.FirstOrDefault();
                if (slot == null) continue;
                string Get(K k) => fs.In(top, b, slot.Id, k).E;
                switch (b.Str("m_SerializedDescriptor"))
                {
                    case "SurfaceDescription.BaseColor": assign.Append($"  s.BaseColor = {Get(K.Vec3)};\n"); break;
                    case "SurfaceDescription.Alpha": assign.Append($"  s.Alpha = {Get(K.Float)};\n"); break;
                    case "SurfaceDescription.AlphaClipThreshold": assign.Append($"  s.AlphaClipThreshold = {Get(K.Float)};\n"); break;
                    case "SurfaceDescription.Emission": assign.Append($"  s.Emission = {Get(K.Vec3)};\n"); break;
                    case "SurfaceDescription.NormalTS": assign.Append($"  s.Normal = {Get(K.Vec3)};\n  s.NormalSpace = 1;\n"); break;
                    case "SurfaceDescription.NormalOS": assign.Append($"  s.Normal = {Get(K.Vec3)};\n  s.NormalSpace = 2;\n"); break;
                    case "SurfaceDescription.NormalWS": assign.Append($"  s.Normal = {Get(K.Vec3)};\n  s.NormalSpace = 3;\n"); break;
                    case "SurfaceDescription.Smoothness": assign.Append($"  s.Smoothness = {Get(K.Float)};\n"); break;
                    case "SurfaceDescription.Metallic": assign.Append($"  s.Metallic = {Get(K.Float)};\n"); break;
                    case "SurfaceDescription.Occlusion": assign.Append($"  s.Occlusion = {Get(K.Float)};\n"); break;
                    case "SurfaceDescription.Specular": break; // specular workflow tint: the template's lighting has no specular term
                    case var d: prog.Approximations.Add($"BlockNode: {d} ignored"); break;
                }
            }
            prog.Fragment = "SgSurface sg_surface() {\n  SgSurface s = sg_defaultSurface();\n" + fs.Body + assign + "  return s;\n}\n";

            var fn = new StringBuilder();
            foreach (var h in unit.HelperOrder) fn.Append(unit.Helpers[h]).Append('\n');
            prog.Functions = fn.ToString();
            var seen = new HashSet<string>();
            prog.Approximations.RemoveAll(a => !seen.Add(a));
        }

        // ── Values ──

        internal enum K { Float = 1, Vec2 = 2, Vec3 = 3, Vec4 = 4, Bool = 5, Mat4 = 6, Tex = 7, Sampler = 8, Gradient = 9 }

        /// <summary>A typed GLSL expression. <see cref="Data"/>: a texture's property reference, or a gradient's keys.</summary>
        internal readonly record struct V(string E, K K, object Data = null);

        internal static int Dim(K k) => k is >= K.Float and <= K.Vec4 ? (int)k : k == K.Bool ? 1 : 0;
        internal static K VecK(int dim) => (K)Math.Clamp(dim, 1, 4);

        internal static string Glsl(K k) => k switch
        {
            K.Float => "float", K.Vec2 => "vec2", K.Vec3 => "vec3", K.Vec4 => "vec4",
            K.Bool => "bool", K.Mat4 => "mat4", K.Tex => "sampler2D", _ => throw new SgException($"no GLSL type for {k}"),
        };

        internal static string Lit(float f)
        {
            if (float.IsNaN(f)) return "0.0";
            if (float.IsPositiveInfinity(f)) return "1e30";
            if (float.IsNegativeInfinity(f)) return "-1e30";
            var s = f.ToString("R", CultureInfo.InvariantCulture);
            if (s.IndexOfAny(new[] { '.', 'E', 'e' }) < 0) s += ".0";
            return s;
        }

        internal static string Vec(params float[] c) => c.Length == 1 ? Lit(c[0]) : $"vec{c.Length}({string.Join(", ", c.Select(Lit))})";

        /// <summary>Shader Graph's implicit slot conversions (truncate; pad 0, and w = 1; splat a scalar).</summary>
        internal static V Cast(V v, K to)
        {
            if (v.K == to) return v;
            if (to == K.Tex || to == K.Sampler || to == K.Gradient || v.K is K.Tex or K.Sampler or K.Gradient)
                throw new SgException($"cannot convert {v.K} to {to}");
            if (to == K.Mat4) throw new SgException($"cannot convert {v.K} to a matrix");
            if (v.K == K.Mat4) throw new SgException($"cannot convert a matrix to {to}");
            if (to == K.Bool) return new V(v.K == K.Float ? $"({v.E} != 0.0)" : $"({v.E}.x != 0.0)", K.Bool);
            if (v.K == K.Bool) return Cast(new V($"({v.E} ? 1.0 : 0.0)", K.Float), to);
            int from = Dim(v.K), dim = Dim(to);
            if (from == 1) return new V(dim == 1 ? v.E : $"{Glsl(to)}({v.E})", to);
            if (dim == 1) return new V($"{v.E}.x", to);
            if (dim < from) return new V($"{v.E}.{"xyzw".Substring(0, dim)}", to);
            return (from, dim) switch
            {
                (2, 3) => new V($"vec3({v.E}, 0.0)", to),
                (2, 4) => new V($"vec4({v.E}, 0.0, 1.0)", to),
                (3, 4) => new V($"vec4({v.E}, 1.0)", to),
                _ => throw new SgException($"cannot convert {v.K} to {to}"),
            };
        }

        /// <summary>A vector expression of width <paramref name="from"/> as width <paramref name="to"/>, by Shader Graph's rules.</summary>
        public static string ConvertVector(string expr, int from, int to) => Cast(new V(expr, VecK(from)), VecK(to)).E;

        internal static K SlotKind(SgSlot s) => s.Type switch
        {
            "Vector1" => K.Float, "Vector2" => K.Vec2, "Vector3" => K.Vec3, "Vector4" => K.Vec4,
            "Boolean" => K.Bool, "ColorRGB" => K.Vec3, "ColorRGBA" or "Color" => K.Vec4,
            "Matrix4" or "Matrix3" or "Matrix2" => K.Mat4,
            "Texture2D" or "Texture2DInput" => K.Tex, "SamplerState" => K.Sampler,
            "Gradient" or "GradientInput" => K.Gradient,
            "UV" => K.Vec2, "Position" or "Normal" or "Tangent" or "Bitangent" or "ViewDirection" => K.Vec3,
            "ScreenPosition" => K.Vec4, "VertexColor" => K.Vec4,
            "DynamicVector" or "DynamicValue" or "DynamicMatrix" => K.Float, // resolved by the node
            _ => throw new SgException($"unsupported slot type {s.Type}"),
        };

        internal static K PropertyKind(SgProperty p) => p.Type switch
        {
            "Vector1ShaderProperty" or "IntegerShaderProperty" or "SliderShaderProperty" => K.Float,
            "Vector2ShaderProperty" => K.Vec2, "Vector3ShaderProperty" => K.Vec3,
            "Vector4ShaderProperty" or "ColorShaderProperty" => K.Vec4,
            "BooleanShaderProperty" => K.Bool,
            "Texture2DShaderProperty" => K.Tex,
            "SamplerStateShaderProperty" => K.Sampler,
            "Matrix4ShaderProperty" or "Matrix3ShaderProperty" or "Matrix2ShaderProperty" => K.Mat4,
            "GradientShaderProperty" => K.Gradient,
            _ => throw new SgException($"unsupported property type {p.Type} ({p.Reference})"),
        };

        internal sealed class SgException : Exception { public SgException(string m) : base(m) { } }

        // ── Compilation state ──

        /// <summary>One program: its helper functions (shared by both stages) and its approximations.</summary>
        internal sealed class Unit
        {
            public readonly ShaderGraphCompiler Compiler;
            public readonly ShaderGraphProgram Prog;
            public readonly Dictionary<string, string> Helpers = new(StringComparer.Ordinal);
            public readonly List<string> HelperOrder = new();
            /// <summary>Every uniform name the graph's own properties declare.</summary>
            public readonly HashSet<string> Declared = new(StringComparer.Ordinal);
            readonly Dictionary<string, string> _assetTextures = new(StringComparer.Ordinal);

            public Unit(ShaderGraphCompiler c, ShaderGraphProgram p) { Compiler = c; Prog = p; }

            public void Helper(string name, string code)
            {
                if (Helpers.ContainsKey(name)) return;
                Helpers[name] = code;
                HelperOrder.Add(name);
            }

            public void Approx(string what) => Prog.Approximations.Add(what);

            /// <summary>A texture asset a node or slot names directly: a uniform the renderer binds by guid.</summary>
            public string AssetTexture(string guid)
            {
                if (string.IsNullOrEmpty(guid)) return "sg_White";
                if (!_assetTextures.TryGetValue(guid, out var u))
                {
                    _assetTextures[guid] = u = "sg_asset_" + guid;
                    Prog.AssetTextures[u] = guid;
                    Prog.Uniforms += $"uniform sampler2D {u};\n";
                }
                return u;
            }

            public void DeclareProperty(SgProperty p, StringBuilder sb)
            {
                if (string.IsNullOrEmpty(p.Reference)) return;
                var k = PropertyKind(p);
                var name = UniformName(p.Reference);
                switch (k)
                {
                    case K.Sampler: return;
                    case K.Gradient: return; // inlined from its value
                    case K.Tex:
                        sb.Append($"uniform sampler2D {name};\n");
                        // A graph may declare its own _ST / _TexelSize property (TMP's do): that one wins.
                        if (!Declared.Contains(name + "_ST")) sb.Append($"uniform vec4 {name}_ST;\n");
                        if (!Declared.Contains(name + "_TexelSize")) sb.Append($"uniform vec4 {name}_TexelSize;\n");
                        Prog.PropertyUniforms[name] = p.Reference;
                        Prog.PropertyUniforms[name + "_ST"] = p.Reference + "_ST";
                        Prog.PropertyUniforms[name + "_TexelSize"] = p.Reference + "_TexelSize";
                        Prog.TextureDefaults[p.Reference] = p.DefaultTextureType switch { 1 => "black", 2 => "grey", 3 => "bump", _ => "white" };
                        if (p.Value.ValueKind == JsonValueKind.Object && ShaderGraphAsset.NestedGuid(ShaderGraphAsset.Str(p.Value, "m_SerializedTexture")) is { } tg)
                            Prog.TextureAssetDefaults[p.Reference] = tg;
                        break;
                    case K.Bool:
                        sb.Append($"uniform float {name};\n");
                        Prog.PropertyUniforms[name] = p.Reference;
                        break;
                    default:
                        sb.Append($"uniform {Glsl(k)} {name};\n");
                        Prog.PropertyUniforms[name] = p.Reference;
                        break;
                }
                // The library declares the globals its functions read behind this guard.
                sb.Append($"#define SG_HAS{name}\n");
            }
        }

        /// <summary>A property reference as a legal GLSL identifier (GLSL reserves "__" and "gl_").</summary>
        internal static string UniformName(string reference)
        {
            var sb = new StringBuilder(reference.Length);
            foreach (char c in reference) sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            var s = sb.ToString();
            while (s.Contains("__")) s = s.Replace("__", "_x");
            if (s.Length == 0 || char.IsDigit(s[0])) s = "p" + s;
            if (s.StartsWith("gl_", StringComparison.Ordinal) || s.StartsWith("sg_", StringComparison.Ordinal)) s = "p_" + s;
            return s;
        }

        /// <summary>A graph being expanded: the top-level graph, or one use of a sub-graph.</summary>
        internal sealed class Scope
        {
            public ShaderGraphAsset Graph;
            public string Prefix;
            /// <summary>Sub-graph properties: the values its node's inputs supply. Null at the top level.</summary>
            public Dictionary<string, V> PropertyValues;
            public readonly Dictionary<string, Dictionary<int, V>> Outputs = new(StringComparer.Ordinal);
            public readonly HashSet<string> Visiting = new(StringComparer.Ordinal);
            public int Depth;
        }

        /// <summary>One shader stage's body.</summary>
        internal sealed partial class Stage
        {
            public readonly Unit Unit;
            public readonly bool Fragment;
            readonly StringBuilder _body = new();
            int _counter;
            int _scopes;

            public Stage(Unit unit, bool fragment) { Unit = unit; Fragment = fragment; }

            public string Body => _body.ToString();

            public string Tmp() => (Fragment ? "f" : "v") + (_counter++).ToString(CultureInfo.InvariantCulture);

            /// <summary>Declares a local holding <paramref name="expr"/> and returns it as a value.</summary>
            public V Let(K k, string expr)
            {
                var t = Tmp();
                _body.Append("  ").Append(Glsl(k)).Append(' ').Append(t).Append(" = ").Append(expr).Append(";\n");
                return new V(t, k);
            }

            /// <summary>Declares an uninitialised local (for a function's out parameter).</summary>
            public string Decl(K k)
            {
                var t = Tmp();
                _body.Append("  ").Append(Glsl(k)).Append(' ').Append(t).Append(";\n");
                return t;
            }

            public void Line(string s) => _body.Append("  ").Append(s).Append('\n');

            /// <summary>The value an input slot receives: its edge, else the slot's own default, converted to <paramref name="want"/>.</summary>
            public V In(Scope sc, SgNode n, int slotId, K want)
            {
                var raw = Raw(sc, n, slotId);
                if (raw is { } r) return Cast(r, want);
                var slot = n.Slot(slotId) ?? throw new SgException($"{n} has no slot {slotId}");
                return Cast(Default(sc, n, slot, want), want);
            }

            /// <summary>The connected value of an input slot, or null when nothing feeds it.</summary>
            public V? Raw(Scope sc, SgNode n, int slotId)
            {
                var (from, slot) = sc.Graph.Source(n, slotId);
                if (from == null) return null;
                return Output(sc, from, slot.Id);
            }

            public V Output(Scope sc, SgNode n, int slotId)
            {
                if (!sc.Outputs.TryGetValue(n.Id, out var outs))
                {
                    if (!sc.Visiting.Add(n.Id)) throw new SgException($"cycle through {n}");
                    outs = new Dictionary<int, V>();
                    EmitNode(sc, n, outs);
                    sc.Visiting.Remove(n.Id);
                    sc.Outputs[n.Id] = outs;
                }
                if (!outs.TryGetValue(slotId, out var v)) throw new SgException($"{n} produced no value for slot {slotId}");
                return v;
            }

            /// <summary>Shader Graph's dynamic-vector rule: the smallest connected width, a scalar promoting; nothing connected = a scalar.</summary>
            public K Dynamic(Scope sc, SgNode n, params int[] slots)
            {
                var kinds = new List<K>();
                foreach (var id in slots)
                    if (Raw(sc, n, id) is { } v) kinds.Add(v.K == K.Bool ? K.Float : v.K);
                var distinct = kinds.Distinct().ToList();
                if (distinct.Count == 0) return K.Float;
                if (distinct.Count == 1) return distinct[0] == K.Mat4 ? K.Mat4 : distinct[0];
                distinct.RemoveAll(k => k == K.Float);
                distinct.RemoveAll(k => k == K.Mat4);
                return distinct.Count == 0 ? K.Float : distinct.OrderBy(Dim).First();
            }

            /// <summary>An unconnected slot's value.</summary>
            V Default(Scope sc, SgNode n, SgSlot s, K want)
            {
                var v = s.Value;
                switch (s.Type)
                {
                    case "Vector1": return new V(Lit(Num(v)), K.Float);
                    case "Boolean": return new V(v.ValueKind == JsonValueKind.True ? "true" : "false", K.Bool);
                    case "Vector2": return new V(Vec(Comp(v, "x"), Comp(v, "y")), K.Vec2);
                    case "Vector3": case "ColorRGB": return new V(Vec(Comp(v, "x"), Comp(v, "y"), Comp(v, "z")), K.Vec3);
                    case "Vector4": case "ColorRGBA": case "Color": return new V(Vec(Comp(v, "x"), Comp(v, "y"), Comp(v, "z"), Comp(v, "w")), K.Vec4);
                    case "DynamicVector":
                    case "DynamicValue":
                    {
                        if (want == K.Mat4) return new V("mat4(1.0)", K.Mat4);
                        // A dynamic value slot stores a matrix; as a vector it is the first row.
                        float x = v.TryGetProperty("e00", out _) ? Comp(v, "e00") : Comp(v, "x");
                        float y = v.TryGetProperty("e01", out _) ? Comp(v, "e01") : Comp(v, "y");
                        float z = v.TryGetProperty("e02", out _) ? Comp(v, "e02") : Comp(v, "z");
                        float w = v.TryGetProperty("e03", out _) ? Comp(v, "e03") : Comp(v, "w");
                        return new V(Vec(x, y, z, w), K.Vec4);
                    }
                    case "Matrix4": case "Matrix3": case "Matrix2": return new V("mat4(1.0)", K.Mat4);
                    case "UV": return Uv(s.Channel);
                    case "Position": return Position(s.Space);
                    case "Normal": return NormalVec(s.Space);
                    case "Tangent": return TangentVec(s.Space);
                    case "Bitangent": return BitangentVec(s.Space);
                    case "ViewDirection": return ViewDirection(s.Space, normalized: true);
                    case "ScreenPosition": return ScreenPosition(s.ScreenSpaceType);
                    case "VertexColor": return new V("sg_Color", K.Vec4);
                    case "Texture2DInput":
                    case "Texture2D":
                    {
                        string guid = s.Json.TryGetProperty("m_Texture", out var t) ? ShaderGraphAsset.NestedGuid(ShaderGraphAsset.Str(t, "m_SerializedTexture")) : null;
                        if (guid != null) return new V(Unit.AssetTexture(guid), K.Tex);
                        return new V(ShaderGraphAsset.Int(s.Json, "m_DefaultType") switch { 1 => "sg_Black", 2 => "sg_Grey", 3 => "sg_Bump", _ => "sg_White" }, K.Tex);
                    }
                    case "SamplerState": return new V("", K.Sampler);
                    case "GradientInput": return new V("", K.Gradient, GradientKeys.FromSlot(v));
                    default: throw new SgException($"{n}: no default for slot type {s.Type}");
                }
            }

            static float Num(JsonElement v) => v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : 0f;
            static float Comp(JsonElement v, string c) => v.ValueKind == JsonValueKind.Object && v.TryGetProperty(c, out var e) && e.ValueKind == JsonValueKind.Number ? (float)e.GetDouble() : 0f;

            // ── Sub-graphs: inlined per use ──

            void EmitSubGraph(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                var guid = ShaderGraphAsset.NestedGuid(n.Str("m_SerializedSubGraph"));
                var sub = guid == null ? null : Unit.Compiler._subGraph(guid);
                if (sub == null) throw new SgException($"{n}: sub-graph {guid ?? "(none)"} not found");
                if (sc.Depth > 16) throw new SgException($"{n}: sub-graphs nested deeper than 16");
                var inner = new Scope { Graph = sub, Prefix = sc.Prefix + "s" + (_scopes++) + "_", PropertyValues = new(StringComparer.Ordinal), Depth = sc.Depth + 1 };

                var guids = new List<string>();
                if (n.Json.TryGetProperty("m_PropertyGuids", out var pg) && pg.ValueKind == JsonValueKind.Array)
                    foreach (var e in pg.EnumerateArray()) guids.Add(e.GetString());
                var inputs = n.Inputs.ToList();
                foreach (var p in sub.Properties)
                {
                    var k = PropertyKind(p);
                    if (k == K.Sampler) { inner.PropertyValues[p.Id] = new V("", K.Sampler); continue; }
                    var slot = inputs.FirstOrDefault(s => s.ShaderOutputName == p.Reference)
                            ?? inputs.FirstOrDefault(s => s.DisplayName == p.Name);
                    if (slot == null)
                    {
                        int i = p.Guid != null ? guids.IndexOf(p.Guid) : -1;
                        if (i >= 0 && i < inputs.Count) slot = inputs[i];
                    }
                    if (slot == null)
                    {
                        inner.PropertyValues[p.Id] = PropertyDefault(p, k);
                        continue;
                    }
                    if (k == K.Gradient)
                    {
                        inner.PropertyValues[p.Id] = Raw(sc, n, slot.Id) ?? PropertyDefault(p, k);
                        continue;
                    }
                    // A value computed once in the caller, so the sub-graph reads a local.
                    var v = In(sc, n, slot.Id, k);
                    inner.PropertyValues[p.Id] = k is K.Tex or K.Mat4 ? v : (IsSimple(v.E) ? v : Let(k, v.E));
                }

                var output = sub.OutputNode ?? throw new SgException($"{n}: sub-graph has no output node");
                foreach (var o in n.Outputs)
                {
                    var inSlot = output.Slot(o.Id) ?? output.Inputs.FirstOrDefault(s => s.ShaderOutputName == o.ShaderOutputName || s.DisplayName == o.DisplayName);
                    if (inSlot == null) { outs[o.Id] = new V(ZeroOf(SlotKind(o)), SlotKind(o)); continue; }
                    var k = SlotKind(inSlot) is var sk && (inSlot.Type is "DynamicVector" or "DynamicValue") ? (Raw(inner, output, inSlot.Id)?.K ?? K.Float) : sk;
                    outs[o.Id] = In(inner, output, inSlot.Id, k);
                }
            }

            static bool IsSimple(string e)
            {
                foreach (char c in e) if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.')) return false;
                return true;
            }

            internal static string ZeroOf(K k) => k switch
            {
                K.Float => "0.0", K.Vec2 => "vec2(0.0)", K.Vec3 => "vec3(0.0)", K.Vec4 => "vec4(0.0)",
                K.Bool => "false", K.Mat4 => "mat4(1.0)", K.Tex => "sg_White", _ => "",
            };

            /// <summary>A property's own default value, as a literal (a sub-graph input nothing names).</summary>
            V PropertyDefault(SgProperty p, K k)
            {
                var v = p.Value;
                switch (k)
                {
                    case K.Float: return new V(Lit(Num(v)), k);
                    case K.Bool: return new V(v.ValueKind == JsonValueKind.True ? "true" : "false", k);
                    case K.Vec2: return new V(Vec(Comp(v, "x"), Comp(v, "y")), k);
                    case K.Vec3: return new V(Vec(Comp(v, "x"), Comp(v, "y"), Comp(v, "z")), k);
                    case K.Vec4:
                        return p.Type == "ColorShaderProperty"
                            ? new V(Vec(Comp(v, "r"), Comp(v, "g"), Comp(v, "b"), Comp(v, "a")), k)
                            : new V(Vec(Comp(v, "x"), Comp(v, "y"), Comp(v, "z"), Comp(v, "w")), k);
                    case K.Tex:
                        if (v.ValueKind == JsonValueKind.Object && ShaderGraphAsset.NestedGuid(ShaderGraphAsset.Str(v, "m_SerializedTexture")) is { } g)
                            return new V(Unit.AssetTexture(g), k);
                        return new V(p.DefaultTextureType switch { 1 => "sg_Black", 2 => "sg_Grey", 3 => "sg_Bump", _ => "sg_White" }, k);
                    case K.Mat4: return new V("mat4(1.0)", k);
                    case K.Gradient: return new V("", k, GradientKeys.FromProperty(v));
                    default: return new V(ZeroOf(k), k);
                }
            }

            /// <summary>A Property node: the uniform at the top level, the supplied value inside a sub-graph.</summary>
            void EmitProperty(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                var pid = n.Json.TryGetProperty("m_Property", out var pr) ? ShaderGraphAsset.Str(pr, "m_Id") : null;
                if (pid == null || !sc.Graph.PropertyById.TryGetValue(pid, out var p)) throw new SgException($"{n}: property {pid} not on the blackboard");
                var k = PropertyKind(p);
                V v;
                if (sc.PropertyValues != null)
                    v = sc.PropertyValues.TryGetValue(p.Id, out var pv) ? pv : PropertyDefault(p, k);
                else
                {
                    var u = UniformName(p.Reference);
                    v = k switch
                    {
                        K.Bool => new V($"({u} > 0.5)", K.Bool),
                        K.Tex => new V(u, K.Tex, p.Reference),
                        K.Sampler => new V("", K.Sampler),
                        K.Gradient => PropertyDefault(p, k),
                        _ => new V(u, k),
                    };
                }
                foreach (var o in n.Outputs) outs[o.Id] = v;
            }

            // ── Custom functions: a call into the library (file) or an inlined body (string) ──

            void EmitCustomFunction(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                var fname = n.Str("m_FunctionName");
                if (string.IsNullOrEmpty(fname)) throw new SgException($"{n}: custom function has no name");
                var args = new List<string>();
                var inputs = n.Inputs.ToList();
                var outputs = n.Outputs.ToList();
                foreach (var s in inputs)
                {
                    var k = SlotKind(s);
                    if (k == K.Sampler) continue; // GLSL samplers carry their own state
                    if (k == K.Gradient) throw new SgException($"{n}: gradient argument");
                    args.Add(In(sc, n, s.Id, k).E);
                }
                var outNames = new List<(SgSlot, string)>();
                foreach (var s in outputs)
                {
                    var k = SlotKind(s);
                    var t = Decl(k);
                    args.Add(t);
                    outNames.Add((s, t));
                }
                string glslName;
                if (n.Int("m_SourceType") == 0)
                {
                    glslName = fname + "_float";
                    if (!Unit.Prog.LibraryCalls.Contains(glslName)) Unit.Prog.LibraryCalls.Add(glslName);
                }
                else
                {
                    glslName = "sgfn_" + UniformName(fname);
                    var sig = new StringBuilder();
                    sig.Append("void ").Append(glslName).Append('(');
                    bool first = true;
                    foreach (var s in inputs)
                    {
                        var k = SlotKind(s);
                        if (k == K.Sampler) continue;
                        if (!first) sig.Append(", ");
                        first = false;
                        sig.Append(Glsl(k)).Append(' ').Append(UniformName(s.ShaderOutputName ?? s.DisplayName));
                    }
                    foreach (var s in outputs)
                    {
                        if (!first) sig.Append(", ");
                        first = false;
                        sig.Append("out ").Append(Glsl(SlotKind(s))).Append(' ').Append(UniformName(s.ShaderOutputName ?? s.DisplayName));
                    }
                    sig.Append(") {\n").Append(HlslToGlsl(n.Str("m_FunctionBody") ?? "")).Append("\n}\n");
                    Unit.Helper(glslName + "#" + n.Id, sig.ToString());
                    Unit.Approx($"CustomFunctionNode: inline body of {fname} translated from HLSL text");
                }
                Line($"{glslName}({string.Join(", ", args)});");
                foreach (var (s, t) in outNames) outs[s.Id] = new V(t, SlotKind(s));
            }

            /// <summary>The textual HLSL→GLSL a custom function's inline body needs (types and intrinsics the library's macros do not cover).</summary>
            internal static string HlslToGlsl(string body)
            {
                var s = body;
                foreach (var (a, b) in new[]
                {
                    ("float4x4", "mat4"), ("float3x3", "mat3"), ("float2x2", "mat2"),
                    ("half4x4", "mat4"), ("half3x3", "mat3"),
                    ("float4", "vec4"), ("float3", "vec3"), ("float2", "vec2"),
                    ("half4", "vec4"), ("half3", "vec3"), ("half2", "vec2"), ("half", "float"),
                    ("int4", "ivec4"), ("int3", "ivec3"), ("int2", "ivec2"),
                    ("uint4", "uvec4"), ("uint3", "uvec3"), ("uint2", "uvec2"),
                })
                    s = System.Text.RegularExpressions.Regex.Replace(s, $@"\b{a}\b", b);
                return s;
            }
        }
    }

    /// <summary>A gradient's keys, unrolled into GLSL where it is sampled.</summary>
    internal sealed class GradientKeys
    {
        public readonly List<(float R, float G, float B, float T)> Colors = new();
        public readonly List<(float A, float T)> Alphas = new();
        public int Mode;

        public static GradientKeys FromNode(JsonElement node)
        {
            var g = new GradientKeys { Mode = ShaderGraphAsset.Int(node, "m_SerializableMode") };
            if (node.TryGetProperty("m_SerializableColorKeys", out var ck) && ck.ValueKind == JsonValueKind.Array)
                foreach (var k in ck.EnumerateArray()) g.Colors.Add((ShaderGraphAsset.F(k, "x"), ShaderGraphAsset.F(k, "y"), ShaderGraphAsset.F(k, "z"), ShaderGraphAsset.F(k, "w")));
            if (node.TryGetProperty("m_SerializableAlphaKeys", out var ak) && ak.ValueKind == JsonValueKind.Array)
                foreach (var k in ak.EnumerateArray()) g.Alphas.Add((ShaderGraphAsset.F(k, "x"), ShaderGraphAsset.F(k, "y")));
            return g.Fix();
        }

        /// <summary>A slot's or property's serialized UnityEngine.Gradient (key0..7, ctime0..7 in 0..65535).</summary>
        public static GradientKeys FromSlot(JsonElement v)
        {
            var g = new GradientKeys { Mode = ShaderGraphAsset.Int(v, "m_Mode") };
            if (v.ValueKind != JsonValueKind.Object) return g.Fix();
            int nc = ShaderGraphAsset.Int(v, "m_NumColorKeys", 2), na = ShaderGraphAsset.Int(v, "m_NumAlphaKeys", 2);
            for (int i = 0; i < Math.Min(nc, 8); i++)
                if (v.TryGetProperty("key" + i, out var key))
                    g.Colors.Add((ShaderGraphAsset.F(key, "r"), ShaderGraphAsset.F(key, "g"), ShaderGraphAsset.F(key, "b"), ShaderGraphAsset.Int(v, "ctime" + i) / 65535f));
            for (int i = 0; i < Math.Min(na, 8); i++)
                if (v.TryGetProperty("key" + i, out var key))
                    g.Alphas.Add((ShaderGraphAsset.F(key, "a"), ShaderGraphAsset.Int(v, "atime" + i) / 65535f));
            return g.Fix();
        }

        public static GradientKeys FromProperty(JsonElement v)
        {
            if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("m_SerializedGradient", out var s) && s.ValueKind == JsonValueKind.String)
            {
                try
                {
                    using var doc = JsonDocument.Parse(s.GetString());
                    foreach (var p in doc.RootElement.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.Object) return FromSlot(p.Value.Clone());
                }
                catch (JsonException) { }
            }
            return FromSlot(v);
        }

        GradientKeys Fix()
        {
            if (Colors.Count == 0) { Colors.Add((1, 1, 1, 0)); Colors.Add((1, 1, 1, 1)); }
            if (Alphas.Count == 0) { Alphas.Add((1, 0)); Alphas.Add((1, 1)); }
            return this;
        }
    }
}
