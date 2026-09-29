using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CosmicShore.Engine;
using Silk.NET.OpenGL;
using EMatrix = CosmicShore.Engine.Matrix4x4;
using EVector3 = CosmicShore.Engine.Vector3;
using GlPrimitive = Silk.NET.OpenGL.PrimitiveType;
using Texture = CosmicShore.Engine.Texture;
using Shader = CosmicShore.Engine.Shader;
using Light = CosmicShore.Engine.Light;
using LightType = CosmicShore.Engine.LightType;

namespace CosmicShore.Render
{
    /// <summary>
    /// The 3D pass: every live <see cref="MeshRenderer"/>/<see cref="SkinnedMeshRenderer"/> the
    /// camera's culling mask admits, drawn with an uber-shader that reproduces the project's
    /// material FAMILIES rather than each Shader Graph node for node:
    ///
    ///   Fresnel pair — base face → rim, lerp(dark, bright, (1 − N·V)^p). The prism BlockGraph
    ///     (_DarkColor/_BrightColor, power 4 via FresnelPower4), the crystal graphs
    ///     (_DullCrystalColor/_BrightCrystalColor), spindles (_DullColor/_BrightColor),
    ///     vessels (_Color1/_Color2) and SpreadFresnel (_FresnelPower). Unlit, HDR.
    ///   Lit — URP Lit / Standard: base × texture × (N·L sun + ambient) + emission.
    ///   Unlit — anything else: base colour × texture.
    ///
    /// Opaque geometry is instanced per (mesh, submesh, material); transparent geometry is
    /// sorted back to front and drawn one renderer at a time. Colour properties are authored
    /// in gamma space and linearised here (Unity's linear colour space).
    /// </summary>
    public sealed class SceneRenderer : IDisposable
    {
        const int InstanceFloats = 32; // mat4 + dark + bright + grow + growFrac

        const string Vert = @"#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUv;
layout(location=3) in vec4 aColor;
layout(location=4) in vec4 iM0;
layout(location=5) in vec4 iM1;
layout(location=6) in vec4 iM2;
layout(location=7) in vec4 iM3;
layout(location=8) in vec4 iDark;
layout(location=9) in vec4 iBright;
layout(location=10) in vec4 iGrow;
layout(location=11) in vec4 iGrowFrac;
uniform mat4 uViewProj;
uniform float uClock;
out vec3 vWorld;
out vec3 vNormal;
out vec2 vUv;
out vec4 vColor;
flat out vec4 vDark;
flat out vec4 vBright;
void main(){
  mat4 M = mat4(iM0, iM1, iM2, iM3);
  vec3 p = aPos;
  // PrismGrowScale (PrismClockAnimation.hlsl): exponential approach from the stamped fraction.
  if (iGrow.y > 0.0) {
    float t = max(uClock - iGrow.x, 0.0);
    p *= max(vec3(1.0) - (vec3(1.0) - iGrowFrac.xyz) * exp(-iGrow.y * t), vec3(0.0));
  }
  vec4 w = M * vec4(p, 1.0);
  vWorld = w.xyz;
  vNormal = transpose(inverse(mat3(M))) * aNormal;
  vUv = aUv;
  vColor = aColor;
  vDark = iDark;
  vBright = iBright;
  gl_Position = uViewProj * w;
}";

        const string Frag = @"#version 330 core
in vec3 vWorld;
in vec3 vNormal;
in vec2 vUv;
in vec4 vColor;
flat in vec4 vDark;
flat in vec4 vBright;
uniform int uFamily;          // 0 unlit, 1 lit, 2 fresnel pair
uniform sampler2D uTex;
uniform vec4 uTexST;
uniform float uFresPow;
uniform vec3 uEmission;
uniform float uCutoff;
uniform int uVertexColor;
uniform vec3 uCamPos;
uniform vec3 uLightDir;       // toward the light
uniform vec3 uLightColor;
uniform vec3 uAmbient;
uniform vec4 uFogColor;
uniform vec4 uFog;            // mode, density, start, end (mode 0 = off)
out vec4 frag;
void main(){
  vec3 N = normalize(vNormal);
  if (!gl_FrontFacing) N = -N;
  vec3 V = normalize(uCamPos - vWorld);
  vec4 tex = texture(uTex, vUv * uTexST.xy + uTexST.zw);
  vec4 col;
  if (uFamily == 2) {
    float f = pow(1.0 - clamp(dot(N, V), 0.0, 1.0), uFresPow);
    col = mix(vDark, vBright, f) * tex;
  } else if (uFamily == 1) {
    vec4 base = vDark * tex;
    float ndl = max(dot(N, uLightDir), 0.0);
    col = vec4(base.rgb * (uLightColor * ndl + uAmbient) + uEmission, base.a);
  } else {
    col = vDark * tex;
    col.rgb += uEmission;
  }
  if (uVertexColor == 1) col *= vColor;
  if (col.a < uCutoff) discard;
  if (uFog.x > 0.5) {
    float d = length(uCamPos - vWorld);
    float k = uFog.x < 1.5 ? clamp((uFog.w - d) / max(uFog.w - uFog.z, 1e-4), 0.0, 1.0)
            : uFog.x < 2.5 ? exp(-uFog.y * d) : exp(-(uFog.y * d) * (uFog.y * d));
    col.rgb = mix(uFogColor.rgb, col.rgb, k);
  }
  frag = col;
}";

        sealed class MeshEntry
        {
            public uint Vao, Vbo, Ebo;
            public int[] SubmeshStart = Array.Empty<int>(), SubmeshCount = Array.Empty<int>();
            public object VertsRef, NormRef, UvRef, ColRef;
            public object[] SubRefs = Array.Empty<object>();
            public bool HasColors;
            public int Frame;
        }

        struct MatState
        {
            public int Family;
            public Color Dark, Bright; // linear
            public float FresPow;
            public Texture Tex;
            public Vector4 TexST;
            public EVector3 Emission;
            public float Cutoff;
            public bool Transparent;
            public BlendingFactor Src, Dst;
            public int Cull; // 0 off, 1 front, 2 back
            public bool ZWrite;
            public int Queue;
            public int DarkId, BrightId; // property ids the per-instance colours come from
        }

        struct Item
        {
            public Renderer Renderer;
            public Mesh Mesh;
            public int Submesh;
            public Material Material;
            public MatState State;
            public float Distance;
        }

        readonly GL _gl;
        readonly TextureCache _textures;
        readonly GlProgram _program;
        readonly uint _instanceVbo;
        readonly ConditionalWeakTable<Mesh, MeshEntry> _meshes = new();
        readonly List<Renderer> _renderers = new();
        readonly List<Item> _opaque = new(), _transparent = new();
        readonly Dictionary<Material, MatState> _mats = new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<(Mesh, int, Material), List<Item>> _batches = new();
        readonly List<List<Item>> _batchPool = new();
        float[] _instanceData = new float[InstanceFloats * 256];
        int _instanceCapacity;
        int _frame;

        static readonly int IdDark = Shader.PropertyToID("_DarkColor"), IdBright = Shader.PropertyToID("_BrightColor");
        static readonly int IdDull = Shader.PropertyToID("_DullCrystalColor"), IdBrightCrystal = Shader.PropertyToID("_BrightCrystalColor");
        static readonly int IdDullColor = Shader.PropertyToID("_DullColor");
        static readonly int IdColor1 = Shader.PropertyToID("_Color1"), IdColor2 = Shader.PropertyToID("_Color2");
        static readonly int IdFresPow = Shader.PropertyToID("_FresnelPower");
        static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor"), IdColor = Shader.PropertyToID("_Color");
        static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap"), IdMainTex = Shader.PropertyToID("_MainTex");
        static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");
        static readonly int IdSurface = Shader.PropertyToID("_Surface"), IdSrc = Shader.PropertyToID("_SrcBlend"), IdDst = Shader.PropertyToID("_DstBlend");
        static readonly int IdCull = Shader.PropertyToID("_Cull"), IdZWrite = Shader.PropertyToID("_ZWrite");
        static readonly int IdAlphaClip = Shader.PropertyToID("_AlphaClip"), IdCutoff = Shader.PropertyToID("_Cutoff");
        static readonly int IdColorMul = Shader.PropertyToID("_ColorMultiplier");
        static readonly int IdGrowStart = Shader.PropertyToID("_GrowStartTime"), IdGrowRate = Shader.PropertyToID("_GrowRate"), IdGrowFrac = Shader.PropertyToID("_GrowStartFrac");
        static readonly int IdPrismClock = Shader.PropertyToID("_PrismClock");

        public int DrawCalls { get; private set; }
        public int Instances { get; private set; }

        public SceneRenderer(GL gl, TextureCache textures)
        {
            _gl = gl;
            _textures = textures;
            _program = new GlProgram(gl, Vert, Frag, "scene");
            _instanceVbo = gl.GenBuffer();
        }

        /// <summary>Draws the scene as seen by <paramref name="camera"/> into the bound target.</summary>
        public void Render(Camera camera, int width, int height)
        {
            _frame++;
            DrawCalls = Instances = 0;
            var view = camera.worldToCameraMatrix;
            var proj = camera.projectionMatrix;
            var viewProj = proj * view;
            var camPos = camera.transform.position;
            int mask = camera.cullingMask;

            Collect(mask, camPos);

            _program.Use();
            _program.Set("uViewProj", ToNumerics(viewProj));
            _program.Set("uClock", Shader.GetGlobalFloat(IdPrismClock) is var clk && clk > 0 ? clk : Time.time);
            SetVec3("uCamPos", camPos);
            SetLighting();
            SetFog();
            _program.Set("uTex", 0);

            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Lequal);

            // Opaque: instanced batches, front-to-back by queue.
            _gl.Disable(EnableCap.Blend);
            foreach (var kv in _batches) { kv.Value.Clear(); _batchPool.Add(kv.Value); }
            _batches.Clear();
            foreach (var it in _opaque)
            {
                var key = (it.Mesh, it.Submesh, it.Material);
                if (!_batches.TryGetValue(key, out var list))
                {
                    if (_batchPool.Count > 0) { list = _batchPool[^1]; _batchPool.RemoveAt(_batchPool.Count - 1); }
                    else list = new List<Item>();
                    _batches[key] = list;
                }
                list.Add(it);
            }
            foreach (var kv in _batches)
                DrawBatch(kv.Value);

            // Transparent: back to front, one at a time.
            _transparent.Sort((a, b) => a.State.Queue != b.State.Queue ? a.State.Queue.CompareTo(b.State.Queue) : b.Distance.CompareTo(a.Distance));
            _gl.Enable(EnableCap.Blend);
            var one = new List<Item>(1) { default };
            foreach (var it in _transparent)
            {
                one[0] = it;
                DrawBatch(one);
            }

            _gl.DepthMask(true);
            _gl.Disable(EnableCap.Blend);
            _gl.Disable(EnableCap.CullFace);
            _gl.Disable(EnableCap.DepthTest);
            _gl.BindVertexArray(0);
        }

        void Collect(int mask, EVector3 camPos)
        {
            _opaque.Clear();
            _transparent.Clear();
            _mats.Clear();
            Renderer.CollectLive(_renderers);
            foreach (var r in _renderers)
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                if (r is not MeshRenderer && r is not SkinnedMeshRenderer) continue;
                var go = r.gameObject;
                if ((mask & (1 << go.layer)) == 0 || !go.activeInHierarchy) continue;
                if (go.isPrefabAsset) continue;
                Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0) continue;
                var mats = r.sharedMaterials;
                int subs = mesh.RenderSubmeshCount;
                for (int i = 0; i < mats.Length && i < Math.Max(subs, 1); i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (!_mats.TryGetValue(m, out var st)) _mats[m] = st = Classify(m);
                    var item = new Item { Renderer = r, Mesh = mesh, Submesh = Math.Min(i, subs - 1), Material = m, State = st };
                    if (st.Transparent)
                    {
                        item.Distance = (r.transform.position - camPos).sqrMagnitude;
                        _transparent.Add(item);
                    }
                    else _opaque.Add(item);
                }
            }
        }

        static MatState Classify(Material m)
        {
            var st = new MatState { FresPow = 4f, TexST = new Vector4(1, 1, 0, 0), Cull = 2, Queue = m.renderQueue };
            if (m.HasProperty(IdDark) && m.HasProperty(IdBright)) { st.Family = 2; st.DarkId = IdDark; st.BrightId = IdBright; if (m.HasProperty(IdFresPow)) st.FresPow = m.GetFloat(IdFresPow); }
            else if (m.HasProperty(IdDull) && m.HasProperty(IdBrightCrystal)) { st.Family = 2; st.DarkId = IdDull; st.BrightId = IdBrightCrystal; }
            else if (m.HasProperty(IdDullColor) && m.HasProperty(IdBright)) { st.Family = 2; st.DarkId = IdDullColor; st.BrightId = IdBright; }
            else if (m.HasProperty(IdColor1) && m.HasProperty(IdColor2)) { st.Family = 2; st.DarkId = IdColor1; st.BrightId = IdColor2; st.FresPow = 2f; }
            else
            {
                string sh = m.shader?.name ?? "";
                st.Family = sh.Contains("Lit") && !sh.Contains("Unlit") || sh == "Standard" || sh.StartsWith("Legacy Shaders/Diffuse") ? 1 : 0;
                st.DarkId = m.HasProperty(IdBaseColor) ? IdBaseColor : m.HasProperty(IdColor) ? IdColor : 0;
                st.BrightId = st.DarkId;
            }

            st.Dark = st.DarkId != 0 ? m.GetColor(st.DarkId) : Color.white;
            st.Bright = st.BrightId != 0 ? m.GetColor(st.BrightId) : Color.white;
            if (st.Family == 2 && st.DarkId == IdColor1 && m.HasProperty(IdColorMul))
            {
                float k = m.GetFloat(IdColorMul);
                if (k > 0f) { st.Dark = Mul(st.Dark, k); st.Bright = Mul(st.Bright, k); }
            }

            st.Tex = m.HasProperty(IdBaseMap) ? m.GetTexture(IdBaseMap) : m.GetTexture(IdMainTex);
            if (st.Tex != null)
            {
                var stv = m.GetTextureScaleOffset(m.HasProperty(IdBaseMap) ? "_BaseMap" : "_MainTex");
                st.TexST = stv;
            }
            if (m.HasProperty(IdEmission) && (m.IsKeywordEnabled("_EMISSION") || st.Family == 0))
            {
                var e = m.GetColor(IdEmission);
                st.Emission = new EVector3(ColorSpace.ToLinear(e.r), ColorSpace.ToLinear(e.g), ColorSpace.ToLinear(e.b));
            }

            bool surfaceTransparent = m.HasProperty(IdSurface) && m.GetFloat(IdSurface) >= 0.5f;
            st.Transparent = surfaceTransparent || m.renderQueue >= 2501;
            st.Src = st.Transparent ? BlendingFactor.SrcAlpha : BlendingFactor.One;
            st.Dst = st.Transparent ? BlendingFactor.OneMinusSrcAlpha : BlendingFactor.Zero;
            if (st.Transparent && m.HasProperty(IdSrc) && m.HasProperty(IdDst))
            {
                st.Src = Blend((int)m.GetFloat(IdSrc));
                st.Dst = Blend((int)m.GetFloat(IdDst));
            }
            st.ZWrite = m.HasProperty(IdZWrite) ? m.GetFloat(IdZWrite) >= 0.5f : !st.Transparent;
            if (m.HasProperty(IdCull)) st.Cull = (int)m.GetFloat(IdCull);
            st.Cutoff = m.HasProperty(IdAlphaClip) && m.GetFloat(IdAlphaClip) >= 0.5f && m.HasProperty(IdCutoff) ? m.GetFloat(IdCutoff) : -1f;
            if (!st.Transparent && st.Cutoff < 0f) { st.Dark.a = 1f; st.Bright.a = 1f; }
            return st;
        }

        static Color Mul(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        static BlendingFactor Blend(int unity) => unity switch
        {
            0 => BlendingFactor.Zero,
            1 => BlendingFactor.One,
            2 => BlendingFactor.DstColor,
            3 => BlendingFactor.SrcColor,
            4 => BlendingFactor.OneMinusDstColor,
            5 => BlendingFactor.SrcAlpha,
            6 => BlendingFactor.OneMinusSrcColor,
            7 => BlendingFactor.DstAlpha,
            8 => BlendingFactor.OneMinusDstAlpha,
            9 => BlendingFactor.SrcAlphaSaturate,
            10 => BlendingFactor.OneMinusSrcAlpha,
            _ => BlendingFactor.One,
        };

        unsafe void DrawBatch(List<Item> items)
        {
            if (items.Count == 0) return;
            var first = items[0];
            var entry = Upload(first.Mesh);
            if (entry == null || first.Submesh >= entry.SubmeshCount.Length || entry.SubmeshCount[first.Submesh] == 0) return;
            var st = first.State;

            int n = items.Count;
            if (_instanceData.Length < n * InstanceFloats) _instanceData = new float[Math.Max(n, _instanceData.Length / InstanceFloats * 2) * InstanceFloats];
            for (int i = 0; i < n; i++)
                WriteInstance(items[i], i * InstanceFloats);

            _gl.BindVertexArray(entry.Vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
            int bytes = n * InstanceFloats * sizeof(float);
            if (bytes > _instanceCapacity)
            {
                _instanceCapacity = Math.Max(bytes, _instanceCapacity * 2);
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)_instanceCapacity, null, BufferUsageARB.StreamDraw);
            }
            fixed (float* p = _instanceData)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)bytes, p);
            BindInstanceAttributes();

            _program.Set("uFamily", st.Family);
            _program.Set("uFresPow", st.FresPow);
            _program.Set("uCutoff", st.Cutoff);
            _program.Set("uTexST", st.TexST.x, st.TexST.y, st.TexST.z, st.TexST.w);
            _program.Set("uVertexColor", entry.HasColors ? 1 : 0);
            _gl.Uniform3(_program.Loc("uEmission"), st.Emission.x, st.Emission.y, st.Emission.z);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, st.Tex != null ? _textures.Get(st.Tex) : _textures.White);

            if (st.Cull == 0) _gl.Disable(EnableCap.CullFace);
            else
            {
                _gl.Enable(EnableCap.CullFace);
                _gl.CullFace(st.Cull == 1 ? TriangleFace.Front : TriangleFace.Back);
            }
            _gl.DepthMask(st.ZWrite);
            if (st.Transparent) _gl.BlendFunc(st.Src, st.Dst);

            _gl.DrawElementsInstancedBaseVertex(GlPrimitive.Triangles, (uint)entry.SubmeshCount[first.Submesh], DrawElementsType.UnsignedInt,
                (void*)(entry.SubmeshStart[first.Submesh] * sizeof(uint)), (uint)n, 0);
            DrawCalls++;
            Instances += n;
        }

        void WriteInstance(in Item it, int o)
        {
            var d = _instanceData;
            var m = it.Renderer.transform.localToWorldMatrix;
            // Column-major (GL): column c = (m0c, m1c, m2c, m3c).
            d[o + 0] = m.m00; d[o + 1] = m.m10; d[o + 2] = m.m20; d[o + 3] = m.m30;
            d[o + 4] = m.m01; d[o + 5] = m.m11; d[o + 6] = m.m21; d[o + 7] = m.m31;
            d[o + 8] = m.m02; d[o + 9] = m.m12; d[o + 10] = m.m22; d[o + 11] = m.m32;
            d[o + 12] = m.m03; d[o + 13] = m.m13; d[o + 14] = m.m23; d[o + 15] = m.m33;

            var st = it.State;
            Color dark = st.Dark, bright = st.Bright;
            float growStart = 0f, growRate = 0f;
            Vector4 frac = new(1, 1, 1, 0);
            if (it.Renderer.HasPropertyBlock())
            {
                var b = it.Renderer.PropertyBlockFor(it.Submesh);
                if (b != null)
                {
                    if (st.DarkId != 0 && b.HasColor(st.DarkId)) dark = b.GetColor(st.DarkId);
                    if (st.BrightId != 0 && b.HasColor(st.BrightId)) bright = b.GetColor(st.BrightId);
                    if (b.HasFloat(IdGrowRate)) { growRate = b.GetFloat(IdGrowRate); growStart = b.GetFloat(IdGrowStart); }
                    if (b.HasVector(IdGrowFrac)) frac = b.GetVector(IdGrowFrac);
                }
            }
            if (!st.Transparent && st.Cutoff < 0f) { dark.a = 1f; bright.a = 1f; }
            d[o + 16] = ColorSpace.ToLinear(dark.r); d[o + 17] = ColorSpace.ToLinear(dark.g); d[o + 18] = ColorSpace.ToLinear(dark.b); d[o + 19] = dark.a;
            d[o + 20] = ColorSpace.ToLinear(bright.r); d[o + 21] = ColorSpace.ToLinear(bright.g); d[o + 22] = ColorSpace.ToLinear(bright.b); d[o + 23] = bright.a;
            d[o + 24] = growStart; d[o + 25] = growRate; d[o + 26] = 0f; d[o + 27] = 0f;
            d[o + 28] = frac.x; d[o + 29] = frac.y; d[o + 30] = frac.z; d[o + 31] = 0f;
        }

        unsafe void BindInstanceAttributes()
        {
            uint stride = InstanceFloats * sizeof(float);
            for (uint i = 0; i < 8; i++)
            {
                _gl.EnableVertexAttribArray(4 + i);
                _gl.VertexAttribPointer(4 + i, 4, VertexAttribPointerType.Float, false, stride, (void*)(i * 4 * sizeof(float)));
                _gl.VertexAttribDivisor(4 + i, 1);
            }
        }

        unsafe MeshEntry Upload(Mesh mesh)
        {
            var e = _meshes.GetOrCreateValue(mesh);
            if (e.Frame == _frame) return e.Vao != 0 ? e : null;
            e.Frame = _frame;
            var verts = mesh.RenderVertices;
            var norms = mesh.RenderNormals;
            var uvs = mesh.RenderUv;
            var cols = mesh.RenderColors;
            int subs = mesh.RenderSubmeshCount;
            bool dirty = e.Vao == 0 || !ReferenceEquals(e.VertsRef, verts) || !ReferenceEquals(e.NormRef, norms)
                || !ReferenceEquals(e.UvRef, uvs) || !ReferenceEquals(e.ColRef, cols) || e.SubRefs.Length != subs;
            if (!dirty)
                for (int i = 0; i < subs; i++)
                    if (!ReferenceEquals(e.SubRefs[i], mesh.RenderSubmesh(i))) { dirty = true; break; }
            if (!dirty) return e;

            e.VertsRef = verts; e.NormRef = norms; e.UvRef = uvs; e.ColRef = cols;
            e.SubRefs = new object[subs];
            int n = verts.Length;
            if (n == 0) return null;
            bool hasN = norms.Length == n, hasUv = uvs.Length == n, hasC = cols.Length == n;
            e.HasColors = hasC;
            var data = new float[n * 12];
            for (int i = 0; i < n; i++)
            {
                int o = i * 12;
                data[o] = verts[i].x; data[o + 1] = verts[i].y; data[o + 2] = verts[i].z;
                if (hasN) { data[o + 3] = norms[i].x; data[o + 4] = norms[i].y; data[o + 5] = norms[i].z; }
                else data[o + 4] = 1f;
                if (hasUv) { data[o + 6] = uvs[i].x; data[o + 7] = uvs[i].y; }
                if (hasC) { data[o + 8] = cols[i].r; data[o + 9] = cols[i].g; data[o + 10] = cols[i].b; data[o + 11] = cols[i].a; }
                else { data[o + 8] = data[o + 9] = data[o + 10] = data[o + 11] = 1f; }
            }
            int total = 0;
            e.SubmeshStart = new int[subs];
            e.SubmeshCount = new int[subs];
            for (int i = 0; i < subs; i++)
            {
                var s = mesh.RenderSubmesh(i);
                e.SubRefs[i] = s;
                e.SubmeshStart[i] = total;
                e.SubmeshCount[i] = s.Length;
                total += s.Length;
            }
            var idx = new uint[total];
            for (int i = 0, k = 0; i < subs; i++)
            {
                var s = mesh.RenderSubmesh(i);
                for (int j = 0; j < s.Length; j++)
                {
                    int v = s[j];
                    idx[k++] = (uint)(v >= 0 && v < n ? v : 0);
                }
            }

            if (e.Vao == 0)
            {
                e.Vao = _gl.GenVertexArray();
                e.Vbo = _gl.GenBuffer();
                e.Ebo = _gl.GenBuffer();
            }
            _gl.BindVertexArray(e.Vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, e.Vbo);
            fixed (float* p = data) _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            uint stride = 12 * sizeof(float);
            _gl.EnableVertexAttribArray(0); _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.EnableVertexAttribArray(1); _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(2); _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
            _gl.EnableVertexAttribArray(3); _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, (void*)(8 * sizeof(float)));
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, e.Ebo);
            fixed (uint* p = idx) _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(idx.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);
            _gl.BindVertexArray(0);
            return e;
        }

        void SetLighting()
        {
            // The strongest enabled directional light is the sun; otherwise a soft key light.
            Light sun = RenderSettings.sun;
            if (sun == null || !sun.isActiveAndEnabled)
            {
                sun = null;
                foreach (var l in CosmicShore.Engine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && l.isActiveAndEnabled && (sun == null || l.intensity > sun.intensity)) sun = l;
            }
            EVector3 toLight = sun != null ? -sun.transform.forward : new EVector3(0.3f, 0.8f, -0.5f).normalized;
            var lc = sun != null ? sun.color : Color.white;
            float li = sun != null ? sun.intensity : 1f;
            _gl.Uniform3(_program.Loc("uLightDir"), toLight.x, toLight.y, toLight.z);
            _gl.Uniform3(_program.Loc("uLightColor"), ColorSpace.ToLinear(lc.r) * li, ColorSpace.ToLinear(lc.g) * li, ColorSpace.ToLinear(lc.b) * li);
            var a = RenderSettings.ambientSkyColor;
            float ai = RenderSettings.ambientMode == CosmicShore.Engine.Rendering.AmbientMode.Skybox ? 0.15f : 1f;
            _gl.Uniform3(_program.Loc("uAmbient"), ColorSpace.ToLinear(a.r) * ai + 0.02f, ColorSpace.ToLinear(a.g) * ai + 0.02f, ColorSpace.ToLinear(a.b) * ai + 0.03f);
        }

        void SetFog()
        {
            var c = RenderSettings.fogColor;
            _program.Set("uFogColor", ColorSpace.ToLinear(c.r), ColorSpace.ToLinear(c.g), ColorSpace.ToLinear(c.b), 1f);
            _program.Set("uFog", RenderSettings.fog ? (float)RenderSettings.fogMode : 0f, RenderSettings.fogDensity,
                RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
        }

        void SetVec3(string name, EVector3 v) => _gl.Uniform3(_program.Loc(name), v.x, v.y, v.z);

        /// <summary>Engine matrix (row/column fields) → System.Numerics, laid out so GL reads it column-major.</summary>
        public static System.Numerics.Matrix4x4 ToNumerics(EMatrix m) => new(
            m.m00, m.m10, m.m20, m.m30,
            m.m01, m.m11, m.m21, m.m31,
            m.m02, m.m12, m.m22, m.m32,
            m.m03, m.m13, m.m23, m.m33);

        public void Dispose()
        {
            _program.Dispose();
            _gl.DeleteBuffer(_instanceVbo);
        }
    }
}
