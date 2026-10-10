using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CosmicShore.Engine;
using CosmicShore.Engine.Rendering;
using Silk.NET.OpenGL;
using Shader = CosmicShore.Engine.Shader;
using Texture = CosmicShore.Engine.Texture;

namespace CosmicShore.Render
{
    /// <summary>The project's custom-function library, ported once from HLSL (Glsl/ShaderGraphLibrary.glsl).</summary>
    public static class ShaderGraphLibrary
    {
        static string s_source;

        /// <summary>The library's GLSL, as embedded in this assembly.</summary>
        public static string Source
        {
            get
            {
                if (s_source != null) return s_source;
                using var s = typeof(ShaderGraphLibrary).Assembly.GetManifestResourceStream("CosmicShore.Render.Glsl.ShaderGraphLibrary.glsl")
                    ?? throw new InvalidOperationException("ShaderGraphLibrary.glsl is not embedded");
                using var r = new StreamReader(s);
                return s_source = r.ReadToEnd();
            }
        }

        /// <summary>True when the library defines <c>void name(</c>.</summary>
        public static bool Defines(string glslFunction) => Source.Contains("void " + glslFunction + "(", StringComparison.Ordinal);
    }

    /// <summary>The per-frame inputs every compiled graph reads (Unity's camera and time built-ins).</summary>
    public struct GraphFrame
    {
        public System.Numerics.Matrix4x4 View, InvView, Proj, ViewProj;
        public Vector3 CamPos, CamDir;
        public float Time, DeltaTime;
        public float Width, Height, Near, Far;
        public bool Orthographic;
        public float OrthoSize, Aspect;
        public Vector3 LightDir, LightColor, Ambient;
        public Vector4 FogColor, Fog;
    }

    /// <summary>
    /// Compiled Shader Graphs as GL programs: one per graph (keyed by the graph's guid), built from
    /// the compiler's GLSL inside a template that supplies the symbol contract, linked once, and
    /// bound per draw from the material (then the renderer's property block, then Shader globals,
    /// then the graph's declared default). A graph that fails to compile or link is reported once
    /// and draws through the generic family instead.
    /// </summary>
    public sealed class GraphProgramCache : IDisposable
    {
        internal sealed class Uniform
        {
            public string Name;      // GLSL name (array names without "[0]")
            public string Reference; // material / global property name
            public int Id;
            public int Loc;
            public UniformType Type;
            public int Size;
            public int Role;         // 0 property, 1 _ST, 2 _TexelSize, 3 asset texture, 4 template-owned
            public string AssetGuid;
            public int Unit;         // texture unit for samplers
        }

        public sealed class Entry
        {
            public ShaderGraphProgram Graph;
            public GlProgram Program;
            internal List<Uniform> Uniforms;
            internal int Frame = -1;
            public string Error;
        }

        readonly GL _gl;
        readonly TextureCache _textures;
        readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        readonly Dictionary<string, Texture> _assetTextures = new(StringComparer.Ordinal);
        readonly float[] _scratch = new float[4 * 64];
        GraphFrame _frame;
        int _frameNo;

        /// <summary>Receives a one-line report for each graph that failed to compile (once per graph).</summary>
        public static Action<string> Report = msg => Debug.LogWarning(msg);

        public GraphProgramCache(GL gl, TextureCache textures) { _gl = gl; _textures = textures; }

        public void BeginFrame(in GraphFrame frame) { _frame = frame; _frameNo++; }

        /// <summary>The linked program for a compiled graph; null (reported once) when it cannot be built.</summary>
        public Entry Get(ShaderGraphProgram graph)
        {
            if (graph == null || !graph.Ok) return null;
            var key = graph.Guid ?? graph.Name;
            if (!_entries.TryGetValue(key, out var e))
            {
                _entries[key] = e = new Entry { Graph = graph };
                try
                {
                    var (vs, fs) = Sources(graph);
                    e.Program = new GlProgram(_gl, vs, fs, "graph:" + Path.GetFileNameWithoutExtension(graph.AssetPath ?? graph.Name));
                    e.Uniforms = Introspect(e.Program, graph);
                }
                catch (Exception ex)
                {
                    e.Error = ex.Message;
                    e.Program?.Dispose();
                    e.Program = null;
                    Report?.Invoke($"[Render] Shader Graph '{graph.Name}' ({graph.AssetPath}) did not compile; drawing it as the generic family. {FirstLine(ex.Message)}");
                }
            }
            return e.Program != null ? e : null;
        }

        static string FirstLine(string s)
        {
            int i = s.IndexOf('\n');
            return i < 0 ? s : s.Substring(0, i) + " ...";
        }

        /// <summary>Every program built so far (for the shader check).</summary>
        public IEnumerable<Entry> Entries => _entries.Values;

        // ── The template ──

        const string Common = @"
uniform mat4 sg_View;
uniform mat4 sg_InvView;
uniform mat4 sg_Proj;
uniform mat4 sg_ViewProj;
uniform vec3 sg_CamPos;
uniform vec3 sg_CamDir;
uniform vec4 sg_Time;             // t, sin t, cos t, dt
uniform vec4 sg_ScreenParams;     // w, h, 1 + 1/w, 1 + 1/h
uniform vec4 sg_ProjectionParams; // sign, near, far, 1/far
uniform vec4 sg_OrthoParams;      // ortho width, height, -, isOrtho
uniform sampler2D sg_White;
uniform sampler2D sg_Black;
uniform sampler2D sg_Grey;
uniform sampler2D sg_Bump;
struct SgSurface { vec3 BaseColor; float Alpha; float AlphaClipThreshold; vec3 Emission; vec3 Normal; int NormalSpace; float Smoothness; float Metallic; float Occlusion; };
SgSurface sg_defaultSurface() {
  SgSurface s;
  s.BaseColor = vec3(0.5); s.Alpha = 1.0; s.AlphaClipThreshold = 0.5; s.Emission = vec3(0.0);
  s.Normal = vec3(0.0, 0.0, 1.0); s.NormalSpace = 0; s.Smoothness = 0.5; s.Metallic = 0.0; s.Occlusion = 1.0;
  return s;
}
vec3 sg_PosOS; vec3 sg_NrmOS; vec4 sg_TanOS;
vec3 sg_PosWS; vec3 sg_NrmWS; vec4 sg_TanWS;
vec4 sg_Uv0; vec4 sg_Uv1; vec4 sg_Uv2; vec4 sg_Uv3; vec4 sg_Color;
float sg_VertexID; bool sg_FrontFace;
vec4 sg_ScreenPosRaw; vec2 sg_ScreenUV;
mat4 sg_ObjectToWorld; mat4 sg_WorldToObject;
mat3 sg_TBN() {
  vec3 n = normalize(sg_NrmWS);
  vec3 t = sg_TanWS.xyz;
  t = dot(t, t) > 1e-12 ? normalize(t - n * dot(n, t)) : (abs(n.y) < 0.999 ? normalize(cross(vec3(0.0, 1.0, 0.0), n)) : vec3(1.0, 0.0, 0.0));
  vec3 b = cross(n, t) * (sg_TanWS.w < 0.0 ? -1.0 : 1.0);
  return mat3(t, b, n);
}
vec4 sg_computeScreenPos(vec4 clip) {
  vec4 o = clip * 0.5;
  o.xy = vec2(o.x, o.y * sg_ProjectionParams.x) + o.w;
  o.zw = clip.zw;
  return o;
}
";

        const string VertexMain = @"
out vec3 vPosOS; out vec3 vNrmOS; out vec4 vTanOS;
out vec3 vPosWS; out vec3 vNrmWS; out vec4 vTanWS;
out vec4 vUv0; out vec4 vUv1; out vec4 vColor; out vec4 vScreenRaw;
flat out float vVertexID;
flat out vec4 vM0; flat out vec4 vM1; flat out vec4 vM2; flat out vec4 vM3;
void main() {
  mat4 M = mat4(iM0, iM1, iM2, iM3);
  if (uSkinned == 1)
    M = uBones[int(aBoneIdx.x)] * aBoneW.x + uBones[int(aBoneIdx.y)] * aBoneW.y + uBones[int(aBoneIdx.z)] * aBoneW.z + uBones[int(aBoneIdx.w)] * aBoneW.w;
  sg_ObjectToWorld = M;
  sg_WorldToObject = inverse(M);
  mat3 NM = transpose(mat3(sg_WorldToObject));
  sg_PosOS = aPos; sg_NrmOS = aNormal; sg_TanOS = aTangent;
  sg_Uv0 = vec4(aUv, 0.0, 0.0); sg_Uv1 = aUv1; sg_Uv2 = vec4(0.0); sg_Uv3 = vec4(0.0);
  sg_Color = uVertexColor == 1 ? aColor : vec4(1.0);
  sg_VertexID = float(gl_VertexID);
  sg_FrontFace = true;
  sg_PosWS = (M * vec4(aPos, 1.0)).xyz;
  sg_NrmWS = normalize(NM * aNormal);
  sg_TanWS = vec4(mat3(M) * aTangent.xyz, aTangent.w);
  vec4 clip0 = sg_ViewProj * vec4(sg_PosWS, 1.0);
  sg_ScreenPosRaw = sg_computeScreenPos(clip0);
  sg_ScreenUV = clip0.xy / max(abs(clip0.w), 1e-6) * 0.5 + 0.5;
  vec3 p, n, t;
  sg_vertex(p, n, t);
  vec4 w = M * vec4(p, 1.0);
  vPosOS = p; vNrmOS = n; vTanOS = vec4(t, aTangent.w);
  vPosWS = w.xyz; vNrmWS = NM * n; vTanWS = vec4(mat3(M) * t, aTangent.w);
  vUv0 = sg_Uv0; vUv1 = sg_Uv1; vColor = sg_Color;
  vVertexID = sg_VertexID;
  vM0 = M[0]; vM1 = M[1]; vM2 = M[2]; vM3 = M[3];
  gl_Position = sg_ViewProj * w;
  vScreenRaw = sg_computeScreenPos(gl_Position);
}
";

        const string FragmentMain = @"
in vec3 vPosOS; in vec3 vNrmOS; in vec4 vTanOS;
in vec3 vPosWS; in vec3 vNrmWS; in vec4 vTanWS;
in vec4 vUv0; in vec4 vUv1; in vec4 vColor; in vec4 vScreenRaw;
flat in float vVertexID;
flat in vec4 vM0; flat in vec4 vM1; flat in vec4 vM2; flat in vec4 vM3;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uAmbient;
uniform vec4 uFogColor;
uniform vec4 uFog;
uniform int uLit;
uniform int uTransparent;
uniform int uAlphaClip;
uniform int uAlphaMode;
out vec4 frag;
void main() {
  sg_ObjectToWorld = mat4(vM0, vM1, vM2, vM3);
  sg_WorldToObject = inverse(sg_ObjectToWorld);
  sg_PosOS = vPosOS; sg_NrmOS = vNrmOS; sg_TanOS = vTanOS;
  sg_PosWS = vPosWS; sg_NrmWS = normalize(vNrmWS); sg_TanWS = vTanWS;
  sg_Uv0 = vUv0; sg_Uv1 = vUv1; sg_Uv2 = vec4(0.0); sg_Uv3 = vec4(0.0); sg_Color = vColor;
  sg_VertexID = vVertexID;
  sg_FrontFace = gl_FrontFacing;
  sg_ScreenPosRaw = vScreenRaw;
  sg_ScreenUV = gl_FragCoord.xy / sg_ScreenParams.xy;
  SgSurface s = sg_surface();
  if (uAlphaClip == 1 && s.Alpha < s.AlphaClipThreshold) discard;
  vec3 N = sg_NrmWS;
  if (s.NormalSpace == 1) N = normalize(sg_TBN() * s.Normal);
  else if (s.NormalSpace == 2) N = normalize(transpose(mat3(sg_WorldToObject)) * s.Normal);
  else if (s.NormalSpace == 3) N = normalize(s.Normal);
  if (!gl_FrontFacing) N = -N;
  vec3 col = s.BaseColor;
  if (uLit == 1) {
    float ndl = max(dot(N, uLightDir), 0.0);
    col = s.BaseColor * (1.0 - 0.5 * s.Metallic) * (uLightColor * ndl + uAmbient * s.Occlusion);
  }
  col += s.Emission;
  float a = uTransparent == 1 ? s.Alpha : 1.0;
  if (uAlphaMode == 1) col *= a;
  else if (uAlphaMode == 3) col = mix(vec3(1.0), col, a);
  if (uFog.x > 0.5) {
    float d = length(sg_CamPos - sg_PosWS);
    float k = uFog.x < 1.5 ? clamp((uFog.w - d) / max(uFog.w - uFog.z, 1e-4), 0.0, 1.0)
            : uFog.x < 2.5 ? exp(-uFog.y * d) : exp(-(uFog.y * d) * (uFog.y * d));
    col = mix(uFogColor.rgb, col, k);
  }
  frag = vec4(col, a);
}
";

        const string Attributes = @"
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUv;
layout(location=3) in vec4 aColor;
layout(location=4) in vec4 iM0;
layout(location=5) in vec4 iM1;
layout(location=6) in vec4 iM2;
layout(location=7) in vec4 iM3;
layout(location=12) in vec4 aBoneIdx;
layout(location=13) in vec4 aBoneW;
layout(location=14) in vec4 aTangent;
layout(location=15) in vec4 aUv1;
uniform int uSkinned;
uniform mat4 uBones[128];
uniform int uVertexColor;
";

        /// <summary>
        /// Compiles and links a graph on <paramref name="gl"/> (as GLSL ES 3.00 when <paramref name="es"/>),
        /// then deletes the program: null when it links, else the driver's message.
        /// </summary>
        public static string TryLink(GL gl, ShaderGraphProgram g, bool es)
        {
            bool previous = GlCaps.ForceEsTranslation;
            GlCaps.ForceEsTranslation = es;
            try
            {
                var (vs, fs) = Sources(g);
                using var p = new GlProgram(gl, vs, fs, Path.GetFileNameWithoutExtension(g.AssetPath ?? g.Name) + (es ? ".es" : ""));
                return null;
            }
            catch (Exception e) { return e.Message.Trim(); }
            finally { GlCaps.ForceEsTranslation = previous; }
        }

        /// <summary>The two stages' full sources for a compiled graph (also what the shader check validates).</summary>
        public static (string Vertex, string Fragment) Sources(ShaderGraphProgram g)
        {
            var vs = new StringBuilder();
            vs.Append("#version 330 core\n").Append(Attributes).Append(Common)
              .Append(g.Uniforms).Append(ShaderGraphLibrary.Source).Append('\n').Append(g.Functions).Append(g.Vertex).Append(VertexMain);
            var fs = new StringBuilder();
            fs.Append("#version 330 core\n#define SG_FRAGMENT 1\n").Append(Common)
              .Append(g.Uniforms).Append(ShaderGraphLibrary.Source).Append('\n').Append(g.Functions).Append(g.Fragment).Append(FragmentMain);
            return (vs.ToString(), fs.ToString());
        }

        // ── Binding ──

        List<Uniform> Introspect(GlProgram p, ShaderGraphProgram g)
        {
            var list = new List<Uniform>();
            _gl.GetProgram(p.Handle, ProgramPropertyARB.ActiveUniforms, out int count);
            int unit = 2; // 0 and 1 belong to the scene program (base map, extended block)
            for (uint i = 0; i < count; i++)
            {
                string raw = _gl.GetActiveUniform(p.Handle, i, out int size, out UniformType type);
                string name = raw.EndsWith("[0]", StringComparison.Ordinal) ? raw.Substring(0, raw.Length - 3) : raw;
                var u = new Uniform { Name = name, Type = type, Size = size, Loc = _gl.GetUniformLocation(p.Handle, raw) };
                if (u.Loc < 0) continue;
                if (name.StartsWith("sg_asset_", StringComparison.Ordinal)) { u.Role = 3; u.AssetGuid = g.AssetTextures.TryGetValue(name, out var guid) ? guid : null; }
                else if (name.StartsWith("sg_", StringComparison.Ordinal) || (name.Length > 1 && name[0] == 'u' && char.IsUpper(name[1]))) u.Role = 4;
                else
                {
                    u.Reference = g.PropertyUniforms.TryGetValue(name, out var r) ? r : name;
                    u.Role = u.Reference.EndsWith("_ST", StringComparison.Ordinal) && type == UniformType.FloatVec4 && g.PropertyUniforms.ContainsKey(name) ? 1
                           : u.Reference.EndsWith("_TexelSize", StringComparison.Ordinal) && type == UniformType.FloatVec4 && g.PropertyUniforms.ContainsKey(name) ? 2 : 0;
                    u.Id = Shader.PropertyToID(u.Role == 0 ? u.Reference : u.Reference.Substring(0, u.Reference.LastIndexOf('_')));
                }
                if (type == UniformType.Sampler2D) u.Unit = unit++;
                list.Add(u);
            }
            p.Use();
            foreach (var u in list)
                if (u.Type == UniformType.Sampler2D) _gl.Uniform1(u.Loc, u.Unit);
            return list;
        }

        /// <summary>Makes the program current and sets this frame's camera, time, light and fog (once per frame).</summary>
        public void Use(Entry e)
        {
            var p = e.Program;
            p.Use();
            if (e.Frame == _frameNo) return;
            e.Frame = _frameNo;
            var f = _frame;
            p.Set("sg_View", f.View);
            p.Set("sg_InvView", f.InvView);
            p.Set("sg_Proj", f.Proj);
            p.Set("sg_ViewProj", f.ViewProj);
            p.Set("sg_CamPos", f.CamPos.x, f.CamPos.y, f.CamPos.z);
            p.Set("sg_CamDir", f.CamDir.x, f.CamDir.y, f.CamDir.z);
            p.Set("sg_Time", f.Time, MathF.Sin(f.Time), MathF.Cos(f.Time), f.DeltaTime);
            p.Set("sg_ScreenParams", f.Width, f.Height, 1f + 1f / Math.Max(f.Width, 1f), 1f + 1f / Math.Max(f.Height, 1f));
            p.Set("sg_ProjectionParams", 1f, f.Near, f.Far, 1f / Math.Max(f.Far, 1e-6f));
            float oh = f.OrthoSize, ow = oh * f.Aspect;
            p.Set("sg_OrthoParams", ow, oh, 0f, f.Orthographic ? 1f : 0f);
            p.Set("uLightDir", f.LightDir.x, f.LightDir.y, f.LightDir.z);
            p.Set("uLightColor", f.LightColor.x, f.LightColor.y, f.LightColor.z);
            p.Set("uAmbient", f.Ambient.x, f.Ambient.y, f.Ambient.z);
            p.Set("uFogColor", f.FogColor.x, f.FogColor.y, f.FogColor.z, f.FogColor.w);
            p.Set("uFog", f.Fog.x, f.Fog.y, f.Fog.z, f.Fog.w);
            p.Set("uLit", e.Graph.Lit ? 1 : 0);
        }

        /// <summary>Binds a draw's property values: the block (per renderer), else the material, else a global, else the default.</summary>
        public unsafe void Bind(Entry e, Material m, MaterialPropertyBlock block, bool transparent, bool alphaClip, int alphaMode)
        {
            var p = e.Program;
            p.Set("uTransparent", transparent ? 1 : 0);
            p.Set("uAlphaClip", alphaClip ? 1 : 0);
            p.Set("uAlphaMode", alphaMode);
            foreach (var u in e.Uniforms)
            {
                switch (u.Role)
                {
                    case 4:
                        if (u.Type == UniformType.Sampler2D)
                            BindUnit(u.Unit, u.Name switch
                            {
                                "sg_Black" => _textures.Solid(0, 0, 0, 255),
                                "sg_Grey" => _textures.Solid(128, 128, 128, 255),
                                "sg_Bump" => _textures.Solid(128, 128, 255, 255),
                                _ => _textures.White,
                            });
                        continue;
                    case 3:
                        BindUnit(u.Unit, AssetTexture(u.AssetGuid) is { } at ? _textures.Get(at) : _textures.White);
                        continue;
                    case 1:
                    {
                        var st = m.GetTextureScaleOffset(Shader.PropertyName(u.Id));
                        _gl.Uniform4(u.Loc, st.x, st.y, st.z, st.w);
                        continue;
                    }
                    case 2:
                    {
                        var t = TextureFor(e, u, m, block);
                        float w = t?.width ?? 1, h = t?.height ?? 1;
                        _gl.Uniform4(u.Loc, 1f / Math.Max(w, 1f), 1f / Math.Max(h, 1f), w, h);
                        continue;
                    }
                }
                switch (u.Type)
                {
                    case UniformType.Sampler2D:
                    {
                        var t = TextureFor(e, u, m, block);
                        BindUnit(u.Unit, t != null ? _textures.Get(t) : DefaultTexture(e, u.Reference));
                        break;
                    }
                    case UniformType.Float:
                    case UniformType.Int:
                    case UniformType.Bool:
                        _gl.Uniform1(u.Loc, FloatFor(u.Id, m, block));
                        break;
                    case UniformType.FloatVec2:
                    case UniformType.FloatVec3:
                    case UniformType.FloatVec4:
                        if (u.Size > 1) { UploadArray(u, m, block); break; }
                        var v = VectorFor(u.Id, m, block);
                        if (u.Type == UniformType.FloatVec2) _gl.Uniform2(u.Loc, v.x, v.y);
                        else if (u.Type == UniformType.FloatVec3) _gl.Uniform3(u.Loc, v.x, v.y, v.z);
                        else _gl.Uniform4(u.Loc, v.x, v.y, v.z, v.w);
                        break;
                    case UniformType.FloatMat4:
                    {
                        var mat = block != null && block.HasProperty(u.Id) ? block.GetMatrix(u.Id)
                                : Shader.GlobalMatrices.TryGetValue(u.Id, out var gm) ? gm : Matrix4x4.identity;
                        var n = SceneRenderer.ToNumerics(mat);
                        _gl.UniformMatrix4(u.Loc, 1, false, (float*)&n);
                        break;
                    }
                }
            }
            _gl.ActiveTexture(TextureUnit.Texture0);
        }

        void BindUnit(int unit, uint handle)
        {
            _gl.ActiveTexture(TextureUnit.Texture0 + unit);
            _gl.BindTexture(TextureTarget.Texture2D, handle);
        }

        /// <summary>A vec4 array: the renderer's block, else the material, else the global (a forcefield's impacts ride the block).</summary>
        unsafe void UploadArray(Uniform u, Material m, MaterialPropertyBlock block)
        {
            int n = Math.Min(u.Size, _scratch.Length / 4);
            Array.Clear(_scratch, 0, n * 4);
            var arr = block?.GetVectorArray(u.Id) ?? m?.GetVectorArray(u.Id) ?? Shader.GetGlobalVectorArray(u.Id);
            if (arr != null)
                for (int i = 0; i < arr.Length && i < n; i++)
                { _scratch[i * 4] = arr[i].x; _scratch[i * 4 + 1] = arr[i].y; _scratch[i * 4 + 2] = arr[i].z; _scratch[i * 4 + 3] = arr[i].w; }
            int comps = u.Type == UniformType.FloatVec4 ? 4 : u.Type == UniformType.FloatVec3 ? 3 : 2;
            if (comps != 4) return; // the library's arrays are all vec4
            fixed (float* p = _scratch) _gl.Uniform4(u.Loc, (uint)n, p);
        }

        static float FloatFor(int id, Material m, MaterialPropertyBlock block)
        {
            if (block != null && block.HasFloat(id)) return block.GetFloat(id);
            if (m.HasStoredProperty(id)) return m.GetFloat(id);
            if (Shader.GlobalFloats.TryGetValue(id, out var g)) return g;
            return m.GetFloat(id);
        }

        static Vector4 VectorFor(int id, Material m, MaterialPropertyBlock block)
        {
            if (block != null)
            {
                if (block.HasVector(id)) return block.GetVector(id);
                if (block.HasColor(id)) { var c = block.GetColor(id); return new Vector4(c.r, c.g, c.b, c.a); }
            }
            if (m.HasStoredProperty(id)) return m.GetVector(id);
            if (Shader.GlobalVectors.TryGetValue(id, out var gv)) return gv;
            if (Shader.GlobalColors.TryGetValue(id, out var gc)) return new Vector4(gc.r, gc.g, gc.b, gc.a);
            return m.GetVector(id);
        }

        Texture TextureFor(Entry e, Uniform u, Material m, MaterialPropertyBlock block)
        {
            int id = u.Id;
            return (block != null ? block.GetTexture(id) : null)
                ?? m.GetTexture(id)
                ?? Shader.GetGlobalTexture(id)
                ?? (e.Graph.TextureAssetDefaults.TryGetValue(Shader.PropertyName(id), out var guid) ? AssetTexture(guid) : null);
        }

        uint DefaultTexture(Entry e, string reference)
            => (e.Graph.TextureDefaults.TryGetValue(reference ?? "", out var d) ? d : "white") switch
            {
                "black" => _textures.Solid(0, 0, 0, 255),
                "grey" => _textures.Solid(128, 128, 128, 255),
                "bump" => _textures.Solid(128, 128, 255, 255),
                _ => _textures.White,
            };

        Texture AssetTexture(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            if (!_assetTextures.TryGetValue(guid, out var t))
                _assetTextures[guid] = t = Shader.TextureByGuid?.Invoke(guid);
            return t;
        }

        public void Dispose()
        {
            foreach (var e in _entries.Values) e.Program?.Dispose();
            _entries.Clear();
        }
    }
}
