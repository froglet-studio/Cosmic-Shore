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
        const int MaxBones = 128;

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
layout(location=12) in vec4 aBoneIdx;
layout(location=13) in vec4 aBoneW;
uniform mat4 uViewProj;
uniform int uSkinned;            // 1: linear-blend skinning, uBones[i] = bone.localToWorld * bindpose[i]
uniform mat4 uBones[128];
uniform vec3 uSkinOrigin;
uniform float uClock;
out vec3 vWorld;
out vec3 vNormal;
out vec2 vUv;
out vec4 vColor;
out vec3 vObj;
out vec3 vObjNormal;
flat out vec4 vDark;
flat out vec4 vBright;
flat out vec3 vOrigin;
void main(){
  mat4 M = mat4(iM0, iM1, iM2, iM3);
  vec3 p = aPos;
  // PrismGrowScale (PrismClockAnimation.hlsl): exponential approach from the stamped fraction.
  if (iGrow.y > 0.0) {
    float t = max(uClock - iGrow.x, 0.0);
    p *= max(vec3(1.0) - (vec3(1.0) - iGrowFrac.xyz) * exp(-iGrow.y * t), vec3(0.0));
  }
  vec4 w;
  vec3 nrm;
  if (uSkinned == 1) {
    mat4 S = uBones[int(aBoneIdx.x)] * aBoneW.x + uBones[int(aBoneIdx.y)] * aBoneW.y
           + uBones[int(aBoneIdx.z)] * aBoneW.z + uBones[int(aBoneIdx.w)] * aBoneW.w;
    w = S * vec4(p, 1.0);
    nrm = transpose(inverse(mat3(S))) * aNormal;
  } else {
    w = M * vec4(p, 1.0);
    nrm = transpose(inverse(mat3(M))) * aNormal;
  }
  vWorld = w.xyz;
  vNormal = nrm;
  vUv = aUv;
  vColor = aColor;
  vObj = p;
  vObjNormal = aNormal;
  vOrigin = uSkinned == 1 ? uSkinOrigin : iM3.xyz;
  vDark = iDark;
  vBright = iBright;
  gl_Position = uViewProj * w;
}";

        const string Frag = @"#version 330 core
in vec3 vWorld;
in vec3 vNormal;
in vec2 vUv;
in vec4 vColor;
in vec3 vObj;
in vec3 vObjNormal;
flat in vec4 vDark;
flat in vec4 vBright;
flat in vec3 vOrigin;
uniform int uFamily;          // 0 unlit, 1 lit, 2 fresnel pair, 3 snow, 4 cage, 5 voronoi cells, 6 crystal
uniform vec4 uParam;          // family-specific
uniform vec4 uColorC;         // family-specific extra colour
uniform float uAlpha;         // family-specific alpha
uniform float uTime;
uniform sampler2D uTex;
uniform vec4 uTexST;
uniform float uFresPow;
uniform float uMaxSqrDist;
uniform vec3 uEmission;
uniform float uCutoff;
uniform int uVertexColor;
uniform vec3 uCamPos;
uniform vec3 uLightDir;       // toward the light
uniform vec3 uLightColor;
uniform vec3 uAmbient;
uniform vec4 uFogColor;
uniform vec4 uFog;            // mode, density, start, end (mode 0 = off)
// ForcefieldCrackle (first-party ForcefieldCrackle.hlsl, translated): impacts from the controller's property block.
uniform vec4 uImpactPos[16];
uniform vec4 uImpactParams[16];
uniform int uImpactCount;
uniform vec3 uCamPosOS;
uniform vec4 uCrackleA, uCrackleB, uRimColor;
uniform vec4 uCrackleP0;      // arcDensity, arcSharpness, ringThickness, centerFill
uniform vec4 uCrackleP1;      // rippleSpeed, rimIntensity, rimPower, -
out vec4 frag;
float cHash1(float n){ return fract(sin(n) * 43758.5453123); }
float cNoise(float x){ float i = floor(x), f = fract(x); f = f * f * (3.0 - 2.0 * f); return mix(cHash1(i), cHash1(i + 1.0), f); }
float cFbm(float x, int oct){ float v = 0.0, a = 0.5, fr = 1.0; for (int o = 0; o < oct; o++){ v += a * (cNoise(x * fr) * 2.0 - 1.0); fr *= 2.17; a *= 0.5; } return v; }
vec4 crackle(vec3 posOS, vec3 nOS, vec3 viewOS){
  vec3 fragDir = normalize(posOS);
  float NdotV = clamp(dot(normalize(nOS), normalize(viewOS)), 0.0, 1.0);
  float fresnel = pow(1.0 - NdotV, uCrackleP1.z) * uCrackleP1.y;
  vec3 em = uRimColor.rgb * fresnel;
  if (uImpactCount <= 0) return vec4(em, fresnel);
  float total = 0.0; vec3 totalColor = vec3(0.0);
  for (int i = 0; i < 16; i++) {
    vec4 ip = uImpactPos[i], pa = uImpactParams[i];
    float maxLife = pa.z; if (maxLife <= 0.0) continue;
    float intensity = pa.x, angR = pa.y, elapsed = ip.w;
    float life = clamp(elapsed / maxLife, 0.0, 1.0);
    float timeFade = pow(1.0 - life, 1.5);
    vec3 idir = normalize(ip.xyz);
    float angle = acos(clamp(dot(fragDir, idir), -1.0, 1.0));
    vec3 tangent = normalize(cross(idir, vec3(0.123, 0.456, 0.789)));
    vec3 bitangent = cross(idir, tangent);
    float azimuth = atan(dot(fragDir, bitangent), dot(fragDir, tangent));
    float expanded = clamp(life * uCrackleP1.x, 0.0, 1.0);
    float waveAngle = angR * 3.14159 * expanded;
    float ringW = angR * uCrackleP0.z;
    float behind = waveAngle - angle;
    float band = smoothstep(-ringW * 0.1, 0.0, behind) * smoothstep(ringW, 0.0, behind);
    band *= step(angle, waveAngle + ringW * 0.2);
    float center = smoothstep(angR * 3.14159 * uCrackleP0.w, 0.0, angle) * (1.0 - life * life);
    float env = max(band, center);
    if (env < 0.001) continue;
    int arcCount = int(uCrackleP0.x);
    float arcC = 0.0, heat = 0.0, sh = uCrackleP0.y;
    for (int a = 0; a < 20; a++) {
      if (a >= arcCount) break;
      float baseA = (float(a) / float(arcCount)) * 6.28318 + cHash1(float(i) * 7.3 + 0.5) * 6.28318;
      float dA = azimuth - baseA; dA = dA - 6.28318 * floor(dA / 6.28318 + 0.5);
      float ni = angle * 15.0 + float(a) * 13.7 + float(i) * 5.3;
      float wob = cFbm(ni, 4) * 0.3 * (angle + 0.1);
      float sub = cFbm(ni * 2.3 + 100.0, 3) * 0.15 * angle;
      float ad = abs(dA - wob), ads = abs(dA - wob - sub);
      float line = exp(-ad * ad / (sh * sh));
      float subl = exp(-ads * ads / (sh * sh * 4.0)) * 0.4;
      float arc = max(line, subl) * smoothstep(0.0, 0.05, angle);
      arcC = max(arcC, arc); heat = max(heat, line);
    }
    float c = env * arcC * timeFade * intensity;
    vec3 ac = mix(uCrackleB.rgb, uCrackleA.rgb, heat * heat) * (1.0 + heat * 2.0);
    total += c; totalColor += ac * c;
  }
  total = clamp(total, 0.0, 1.0);
  em = total > 0.001 ? (totalColor / max(total, 0.001)) * total + uRimColor.rgb * fresnel : uRimColor.rgb * fresnel;
  return vec4(em, clamp(total + fresnel, 0.0, 1.0));
}
// Voronoi as Shader Graph's Voronoi node documents it (random cell offsets animated by AngleOffset).
vec2 voronoiRandom(vec2 uv, float offset){
  uv = fract(sin(vec2(dot(uv, vec2(15.27, 99.41)), dot(uv, vec2(47.63, 89.98)))) * 46839.32);
  return vec2(sin(uv.y * offset) * 0.5 + 0.5, cos(uv.x * offset) * 0.5 + 0.5);
}
float voronoi(vec2 uv, float angleOffset, float density){
  vec2 g = floor(uv * density), f = fract(uv * density);
  float best = 8.0;
  for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
    vec2 lattice = vec2(x, y);
    vec2 o = voronoiRandom(lattice + g, angleOffset);
    best = min(best, distance(lattice + o, f));
  }
  return best;
}
float fresnelNode(vec3 N, vec3 V, float p){ return pow(1.0 - clamp(dot(N, V), 0.0, 1.0), p); }
void main(){
  vec3 N = normalize(vNormal);
  if (!gl_FrontFacing) N = -N;
  vec3 V = normalize(uCamPos - vWorld);
  vec4 tex = texture(uTex, vUv * uTexST.xy + uTexST.zw);
  vec4 col;
  if (uFamily == 7) {
    vec3 nOS = gl_FrontFacing ? vObjNormal : -vObjNormal;
    col = crackle(vObj, nOS, uCamPosOS - vObj);
  } else if (uFamily == 2) {
    // FresnelPower4: back-facing normals keep a faint term (d+1)*0.2 instead of clamping.
    float d = dot(N, V);
    float x = d > 0.0 ? d : (d + 1.0) * 0.2;
    float f = pow(1.0 - x, uFresPow);
    vec4 bright = vBright;
    if (uMaxSqrDist > 0.0) {
      // DistanceSpreadAndColors: the rim sinks toward the base with camera distance.
      vec3 dc = vWorld - uCamPos;
      float n = dot(dc, dc) / uMaxSqrDist;
      bright = mix(vBright, vDark, n > 1.0 ? 0.9 : n * 0.9);
    }
    col = mix(vDark, bright, f) * tex;
  } else if (uFamily == 3) {
    // SnowGraph: colour + a gradient along the object's own axis, fixed opacity.
    col = vec4(clamp(vDark.rgb + dot(vObj, uParam.xyz), 0.0, 1.0), uAlpha);
  } else if (uFamily == 4) {
    // CageGraph: straight colour -> graph rim colour by Fresnel(1.91), material alpha.
    col = vec4(mix(vDark.rgb, uColorC.rgb, fresnelNode(N, V, uParam.x)), uAlpha);
  } else if (uFamily == 5) {
    // SpindleGraph: animated Voronoi cells, dense up close and thinning out to nothing at _Distance.
    float dist = length(uCamPos - vOrigin);
    float near = dist < uParam.y ? 1.0 - dist / uParam.y : 0.0;
    float v = voronoi(vUv + vec2(0.0, 0.5), sin(uParam.z + uTime), dist * uParam.x * near);
    col = vec4(vDark.rgb * (1.0 - v) + vBright.rgb * v, pow(clamp(v, 0.0, 1.0), (dist / max(uParam.y, 1e-3) + 0.1) * 10.0));
  } else if (uFamily == 6) {
    // CrystalGraph: overlay a white fresnel onto the dull colour, fade to bright + transparent with distance.
    float fr = fresnelNode(N, -V, 0.32);
    vec3 over = mix(vDark.rgb, mix(vec3(1.0), 2.0 * vDark.rgb, step(vDark.rgb, vec3(0.5))), fr);
    float t = clamp(length(uCamPos - vWorld) / 1000.0 - 0.2, 0.0, 1.0);
    col = vec4(mix(over, vBright.rgb, t), 1.0 - t);
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
            public bool HasSkin;
            public uint SkinVbo;
            public object SkinRef;
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
            public float MaxSqrDist;     // the prism graph's distance fade (0 = none)
            public float Alpha;          // graph alpha property (snow opacity, cage alpha)
            public Vector4 Param;        // family-specific parameters
            public Color ColorC;         // family-specific extra colour
            public float VesselMultiplier;
        }

        struct Item
        {
            public Renderer Renderer;
            public Mesh Mesh;
            public int Submesh;
            public Material Material;
            public MatState State;
            public float Distance;
            public bool Skinned;
            public bool WorldSpace;
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
        static readonly int IdSqrDistance = Shader.PropertyToID("_SqrDistance");
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
        EVector3 _camPos;
        readonly List<Item> _run = new(), _skinnedOpaque = new();
        sealed class TGroup { public readonly List<Item> Items = new(); public int Queue; public float Far; }
        readonly Dictionary<(object, int, object, int, int), TGroup> _tGroups = new();
        readonly List<TGroup> _tGroupPool = new(), _tOrder = new();
        readonly float[] _boneData = new float[MaxBones * 16];

        public void Render(Camera camera, int width, int height)
        {
            _frame++;
            DrawCalls = Instances = 0;
            var view = camera.worldToCameraMatrix;
            var proj = camera.projectionMatrix;
            var viewProj = proj * view;
            var camPos = camera.transform.position;
            _camPos = camPos;
            int mask = camera.cullingMask;

            Collect(mask, camPos);

            _program.Use();
            _program.Set("uViewProj", ToNumerics(viewProj));
            _program.Set("uClock", Shader.GetGlobalFloat(IdPrismClock) is var clk && clk > 0 ? clk : Time.time);
            _program.Set("uTime", Time.time);
            SetVec3("uCamPos", camPos);
            SetLighting();
            SetFog();
            _program.Set("uTex", 0);

            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Lequal);
            // Unity's front faces wind clockwise; worldToCameraMatrix's Z flip keeps that
            // true in GL clip space, so GL must be told which winding is front.
            _gl.FrontFace(FrontFaceDirection.CW);

            // Opaque: instanced batches, front-to-back by queue.
            _gl.Disable(EnableCap.Blend);
            foreach (var kv in _batches) { kv.Value.Clear(); _batchPool.Add(kv.Value); }
            _batches.Clear();
            foreach (var it in _opaque)
            {
                if (it.Skinned) { _skinnedOpaque.Add(it); continue; }
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
            foreach (var it in _skinnedOpaque) { _run.Clear(); _run.Add(it); DrawBatch(_run); }
            _skinnedOpaque.Clear();
            _run.Clear();

            // Transparent: back to front, one at a time.
            _gl.Enable(EnableCap.Blend);
            // Transparent: grouped by render state (queue, mesh, submesh, material), each group
            // back to front, groups ordered by queue then by their farthest member. A group draws
            // as ONE instanced call — the arena's 11k cactus spindles (eight phase materials,
            // interleaved in depth) would otherwise be tens of thousands of calls a frame. The
            // cost is cross-group order between overlapping translucent surfaces of different
            // materials; per-object effects (the crackle shield, skinned meshes) stay single.
            foreach (var g in _tGroups.Values) { g.Items.Clear(); _tGroupPool.Add(g); }
            _tGroups.Clear();
            _tOrder.Clear();
            int solo = 0;
            foreach (var it in _transparent)
            {
                bool single = it.State.Family == 7 || it.Skinned;
                var key = single ? ((object)it.Mesh, it.Submesh, (object)it.Material, it.State.Queue, ++solo)
                                 : ((object)it.Mesh, it.Submesh, (object)it.Material, it.State.Queue, 0);
                if (!_tGroups.TryGetValue(key, out var group))
                {
                    if (_tGroupPool.Count > 0) { group = _tGroupPool[^1]; _tGroupPool.RemoveAt(_tGroupPool.Count - 1); }
                    else group = new TGroup();
                    group.Queue = it.State.Queue; group.Far = 0f;
                    _tGroups[key] = group;
                    _tOrder.Add(group);
                }
                group.Items.Add(it);
                if (it.Distance > group.Far) group.Far = it.Distance;
            }
            _tOrder.Sort((a, b) => a.Queue != b.Queue ? a.Queue.CompareTo(b.Queue) : b.Far.CompareTo(a.Far));
            foreach (var g in _tOrder)
            {
                if (g.Items.Count > 1) g.Items.Sort((a, b) => b.Distance.CompareTo(a.Distance));
                DrawBatch(g.Items);
            }

            _gl.DepthMask(true);
            _gl.FrontFace(FrontFaceDirection.Ccw);
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
                if (r is TrailRenderer || r is LineRenderer)
                {
                    CollectRibbon(r, mask, camPos);
                    continue;
                }
                if (r is not MeshRenderer && r is not SkinnedMeshRenderer) continue;
                var go = r.gameObject;
                if ((mask & (1 << go.layer)) == 0 || !go.activeInHierarchy) continue;
                if (go.isPrefabAsset) continue;
                Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0) continue;
                if (r is SkinnedMeshRenderer morphing && mesh.blendShapeCount > 0) mesh = Morphed(morphing, mesh);
                var mats = r.sharedMaterials;
                int subs = mesh.RenderSubmeshCount;
                for (int i = 0; i < mats.Length && i < Math.Max(subs, 1); i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (!_mats.TryGetValue(m, out var st)) _mats[m] = st = Classify(m);
                    var item = new Item { Renderer = r, Mesh = mesh, Submesh = Math.Min(i, subs - 1), Material = m, State = st,
                        Skinned = r is SkinnedMeshRenderer sk && sk.bones is { Length: > 0 and <= MaxBones } && mesh.RenderBoneWeights.Length == mesh.vertexCount
                                  && mesh.RenderBindposes.Length >= sk.bones.Length };
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
            var st = new MatState { FresPow = 4f, TexST = new Vector4(1, 1, 0, 0), Cull = 2, Queue = m.renderQueue, Alpha = 1f };
            string graph = m.shader?.name ?? "";
            if (graph == "Shader Graphs/SnowGraph")
            {
                st.Family = 3; st.DarkId = st.BrightId = IdColor;
                st.Alpha = m.GetFloat("_Opacity");
                var v = m.HasProperty("_Vector3") ? m.GetVector("_Vector3") : new Vector4(0, 0, 0.65f, 0);
                st.Param = v;
            }
            else if (graph == "Shader Graphs/CageGraph")
            {
                st.Family = 4; st.DarkId = st.BrightId = Shader.PropertyToID("_Straight_Color");
                st.Alpha = m.GetFloat("_alpha");
                st.Param = new Vector4(1.91f, 0, 0, 0);
                st.ColorC = new Color(0.1086654f, 0.5329778f, 1.0504318f, 1f); // the graph's rim ColorNode
            }
            else if (graph == "Shader Graphs/SpindleGraph")
            {
                st.Family = 5; st.DarkId = IdDullColor; st.BrightId = IdBright;
                st.Param = new Vector4(m.GetFloat("_CellDensity"), m.GetFloat("_Distance"), m.GetFloat("_Phase"), 0);
            }
            else if (graph == "Shader Graphs/ForcefieldCrackle")
            {
                st.Family = 7; // hand-written: Blend One One, ZWrite Off, Cull Off (see the fix-up below)
            }
            else if (graph == "Shader Graphs/CrystalGraph")
            {
                st.Family = 6; st.DarkId = IdDull; st.BrightId = IdBrightCrystal;
            }
            else if (graph == "Shader Graphs/VesselGraph")
            {
                // Base = lerp(Color1, Color2, dot(N,N)) = Color2, times _ColorMultiplier.
                st.Family = 0; st.DarkId = st.BrightId = IdColor2;
                st.VesselMultiplier = m.HasProperty(IdColorMul) ? m.GetFloat(IdColorMul) : 1f;
            }
            else if (m.HasProperty(IdDark) && m.HasProperty(IdBright))
            {
                st.Family = 2; st.DarkId = IdDark; st.BrightId = IdBright;
                if (m.HasProperty(IdFresPow)) st.FresPow = m.GetFloat(IdFresPow);
                if (m.HasProperty(IdSqrDistance)) st.MaxSqrDist = m.GetFloat(IdSqrDistance);
            }
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
            if (st.VesselMultiplier > 0f) { st.Dark = Mul(st.Dark, st.VesselMultiplier); st.Bright = st.Dark; }

            st.Tex = m.HasProperty(IdBaseMap) ? m.GetTexture(IdBaseMap) : m.GetTexture(IdMainTex);
            if (st.Tex != null)
            {
                var stv = m.GetTextureScaleOffset(m.HasProperty(IdBaseMap) ? "_BaseMap" : "_MainTex");
                st.TexST = stv;
            }
            if (m.HasProperty(IdEmission) && (m.IsKeywordEnabled("_EMISSION") || st.Family == 0))
            {
                var e = m.GetColor(IdEmission);
                st.Emission = new EVector3(e.r, e.g, e.b);
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
            if (st.Family == 7)
            {
                st.Transparent = true; st.Src = BlendingFactor.One; st.Dst = BlendingFactor.One;
                st.ZWrite = false; st.Cull = 0; st.Cutoff = -1f;
            }
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

            if (st.Family == 7) SetCrackleUniforms(first);
            SetSkinUniforms(first, entry);
            _program.Set("uFamily", st.Family);
            _program.Set("uFresPow", st.FresPow);
            _program.Set("uMaxSqrDist", st.MaxSqrDist);
            _program.Set("uAlpha", st.Alpha);
            _program.Set("uParam", st.Param.x, st.Param.y, st.Param.z, st.Param.w);
            _program.Set("uColorC", st.ColorC.r, st.ColorC.g, st.ColorC.b, st.ColorC.a);
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

        static readonly int IdImpactPos = Shader.PropertyToID("_ImpactPositions"), IdImpactParams = Shader.PropertyToID("_ImpactParams"), IdImpactCount = Shader.PropertyToID("_ImpactCount");
        static readonly int IdCrackleA = Shader.PropertyToID("_CrackleColorA"), IdCrackleB = Shader.PropertyToID("_CrackleColorB"), IdRimColor = Shader.PropertyToID("_FresnelRimColor");
        static readonly int[] IdCrackleFloats =
        {
            Shader.PropertyToID("_ArcDensity"), Shader.PropertyToID("_ArcSharpness"), Shader.PropertyToID("_RingThickness"), Shader.PropertyToID("_CenterFillAmount"),
            Shader.PropertyToID("_RippleSpeed"), Shader.PropertyToID("_FresnelRimIntensity"), Shader.PropertyToID("_FresnelRimPower"),
        };
        readonly float[] _impactScratch = new float[64];

        /// <summary>The crackle's per-renderer state: the controller's property block, else the material.</summary>
        void SetCrackleUniforms(in Item it)
        {
            var m = it.Material;
            var b = it.Renderer.HasPropertyBlock() ? it.Renderer.PropertyBlockFor(it.Submesh) : null;
            float F(int id) => b != null && b.HasFloat(id) ? b.GetFloat(id) : m.HasProperty(id) ? m.GetFloat(id) : 0f;
            Color C(int id) => b != null && b.HasColor(id) ? b.GetColor(id) : m.HasProperty(id) ? m.GetColor(id) : Color.black;
            var f = IdCrackleFloats;
            _program.Set("uCrackleP0", F(f[0]), F(f[1]), F(f[2]), F(f[3]));
            _program.Set("uCrackleP1", F(f[4]), F(f[5]), F(f[6]), 0f);
            var a = C(IdCrackleA); _program.Set("uCrackleA", a.r, a.g, a.b, a.a);
            var bc = C(IdCrackleB); _program.Set("uCrackleB", bc.r, bc.g, bc.b, bc.a);
            var rc = C(IdRimColor); _program.Set("uRimColor", rc.r, rc.g, rc.b, rc.a);
            int count = (int)F(IdImpactCount);
            _program.Set("uImpactCount", count);
            UploadVec4Array("uImpactPos", b?.GetVectorArray(IdImpactPos));
            UploadVec4Array("uImpactParams", b?.GetVectorArray(IdImpactParams));
            var camOS = it.Renderer.transform.InverseTransformPoint(_camPos);
            _gl.Uniform3(_program.Loc("uCamPosOS"), camOS.x, camOS.y, camOS.z);
        }

        unsafe void UploadVec4Array(string name, Vector4[] values)
        {
            Array.Clear(_impactScratch);
            if (values != null)
                for (int i = 0; i < Math.Min(16, values.Length); i++)
                {
                    _impactScratch[i * 4] = values[i].x; _impactScratch[i * 4 + 1] = values[i].y;
                    _impactScratch[i * 4 + 2] = values[i].z; _impactScratch[i * 4 + 3] = values[i].w;
                }
            fixed (float* p = _impactScratch) _gl.Uniform4(_program.Loc(name), 16, p);
        }

        // ── Trails and lines: a camera-facing ribbon rebuilt each frame from the recorded points ──

        readonly ConditionalWeakTable<Renderer, Mesh> _ribbons = new();
        readonly List<EVector3> _ribbonPts = new();
        static Material s_defaultLine;

        /// <summary>Unity's Default-Line stand-in: unlit, alpha-blended, tinted by the vertex colour.</summary>
        static Material DefaultLineMaterial => s_defaultLine ??= new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")) { name = "Default-Line", renderQueue = 3000 };

        void CollectRibbon(Renderer r, int mask, EVector3 camPos)
        {
            var go = r.gameObject;
            if ((mask & (1 << go.layer)) == 0 || !go.activeInHierarchy || go.isPrefabAsset) return;
            _ribbonPts.Clear();
            float widthMul; AnimationCurve curve; Gradient gradient; bool loop = false;
            if (r is TrailRenderer trail)
            {
                // Newest point last; the ribbon runs from the emitter (u = 0) to the oldest point (u = 1).
                for (int i = trail.positionCount - 1; i >= 0; i--) _ribbonPts.Add(trail.GetPosition(i));
                if (trail.emitting && go.activeInHierarchy) _ribbonPts.Insert(0, r.transform.position);
                widthMul = trail.widthMultiplier; curve = trail.widthCurve; gradient = trail.colorGradient;
            }
            else
            {
                var line = (LineRenderer)r;
                for (int i = 0; i < line.positionCount; i++)
                    _ribbonPts.Add(line.useWorldSpace ? line.GetPosition(i) : r.transform.TransformPoint(line.GetPosition(i)));
                widthMul = line.widthMultiplier; curve = line.widthCurve; gradient = line.colorGradient; loop = line.loop;
            }
            if (loop && _ribbonPts.Count > 2) _ribbonPts.Add(_ribbonPts[0]);
            int n = _ribbonPts.Count;
            if (n < 2) return;

            float total = 0f;
            for (int i = 1; i < n; i++) total += (_ribbonPts[i] - _ribbonPts[i - 1]).magnitude;
            if (total <= 1e-5f) return;

            var verts = new EVector3[n * 2];
            var cols = new Color[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[(n - 1) * 6];
            float along = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) along += (_ribbonPts[i] - _ribbonPts[i - 1]).magnitude;
                float u = along / total;
                var p = _ribbonPts[i];
                var tangent = _ribbonPts[Math.Min(i + 1, n - 1)] - _ribbonPts[Math.Max(i - 1, 0)];
                var side = EVector3.Cross(tangent, camPos - p);
                float len = side.magnitude;
                side = len > 1e-6f ? side / len : EVector3.up;
                float halfWidth = 0.5f * widthMul * (curve != null ? curve.Evaluate(u) : 1f);
                verts[i * 2] = p - side * halfWidth;
                verts[i * 2 + 1] = p + side * halfWidth;
                var c = gradient != null ? gradient.Evaluate(u) : Color.white;
                cols[i * 2] = c; cols[i * 2 + 1] = c;
                uvs[i * 2] = new Vector2(u, 0f); uvs[i * 2 + 1] = new Vector2(u, 1f);
                if (i < n - 1)
                {
                    int o = i * 6, a = i * 2;
                    tris[o] = a; tris[o + 1] = a + 2; tris[o + 2] = a + 1;
                    tris[o + 3] = a + 1; tris[o + 4] = a + 2; tris[o + 5] = a + 3;
                }
            }
            var mesh = _ribbons.GetValue(r, _ => new Mesh { name = "ribbon" });
            mesh.Clear();
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.uv = uvs;
            mesh.triangles = tris;

            var mats = r.sharedMaterials;
            var m = mats is { Length: > 0 } && mats[0] != null ? mats[0] : DefaultLineMaterial;
            if (!_mats.TryGetValue(m, out var st))
            {
                _mats[m] = st = Classify(m);
                // A line/trail is a translucent strip whatever its material queue says; cull nothing.
                if (!st.Transparent) { st.Transparent = true; st.Src = BlendingFactor.SrcAlpha; st.Dst = BlendingFactor.OneMinusSrcAlpha; st.ZWrite = false; }
                st.Cull = 0;
                _mats[m] = st;
            }
            var item = new Item { Renderer = r, Mesh = mesh, Submesh = 0, Material = m, State = st, WorldSpace = true,
                                  Distance = (r.transform.position - camPos).sqrMagnitude };
            _transparent.Add(item);
        }

        sealed class MorphState { public Mesh Clone; public Mesh Source; public float[] Weights = Array.Empty<float>(); }
        readonly ConditionalWeakTable<SkinnedMeshRenderer, MorphState> _morphs = new();
        readonly ConditionalWeakTable<Mesh, Dictionary<(int, int), (EVector3[] V, EVector3[] N)>> _deltas = new();

        /// <summary>
        /// Blend shapes (the elemental hull morphs): the shared mesh's positions and normals plus each
        /// weighted shape's delta (weight 0..100 against the frame weights, interpolating between frames),
        /// baked into a per-renderer copy only when the weights change.
        /// </summary>
        Mesh Morphed(SkinnedMeshRenderer smr, Mesh mesh)
        {
            var weights = smr.RenderBlendShapeWeights;
            bool any = false;
            foreach (var kv in weights) if (kv.Value != 0f && kv.Key >= 0 && kv.Key < mesh.blendShapeCount) { any = true; break; }
            if (!any) return mesh;
            var state = _morphs.GetOrCreateValue(smr);
            int count = mesh.blendShapeCount;
            bool changed = !ReferenceEquals(state.Source, mesh) || state.Weights.Length != count;
            if (!changed)
                for (int i = 0; i < count; i++)
                    if (state.Weights[i] != smr.GetBlendShapeWeight(i)) { changed = true; break; }
            if (!changed && state.Clone != null) return state.Clone;

            if (state.Clone == null || !ReferenceEquals(state.Source, mesh))
            {
                state.Clone = new Mesh { name = mesh.name + " (morph)" };
                smr.BakeMesh(state.Clone); // a full copy of the shared mesh's buffers
                state.Source = mesh;
            }
            if (state.Weights.Length != count) state.Weights = new float[count];
            var baseV = mesh.RenderVertices;
            var baseN = mesh.RenderNormals;
            var v = (EVector3[])baseV.Clone();
            var nrm = baseN.Length == baseV.Length ? (EVector3[])baseN.Clone() : null;
            var cache = _deltas.GetOrCreateValue(mesh);
            for (int shape = 0; shape < count; shape++)
            {
                float w = smr.GetBlendShapeWeight(shape);
                state.Weights[shape] = w;
                if (w == 0f) continue;
                int frames = mesh.GetBlendShapeFrameCount(shape);
                if (frames == 0) continue;
                // Which frame pair brackets the weight (original: frames are ordered by weight).
                int hi = 0;
                while (hi < frames - 1 && mesh.GetBlendShapeFrameWeight(shape, hi) < w) hi++;
                float wHi = mesh.GetBlendShapeFrameWeight(shape, hi);
                float wLo = hi > 0 ? mesh.GetBlendShapeFrameWeight(shape, hi - 1) : 0f;
                float t = Math.Abs(wHi - wLo) > 1e-6f ? (w - wLo) / (wHi - wLo) : 1f;
                AddDelta(mesh, cache, shape, hi, t, v, nrm);
                if (hi > 0) AddDelta(mesh, cache, shape, hi - 1, 1f - t, v, nrm);
            }
            state.Clone.vertices = v;
            if (nrm != null) state.Clone.normals = nrm;
            return state.Clone;
        }

        static void AddDelta(Mesh mesh, Dictionary<(int, int), (EVector3[] V, EVector3[] N)> cache, int shape, int frame, float k,
            EVector3[] v, EVector3[] n)
        {
            if (!cache.TryGetValue((shape, frame), out var d))
            {
                var dv = new EVector3[mesh.vertexCount];
                var dn = new EVector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(shape, frame, dv, dn, null);
                cache[(shape, frame)] = d = (dv, dn);
            }
            for (int i = 0; i < v.Length && i < d.V.Length; i++) v[i] += d.V[i] * k;
            if (n != null)
                for (int i = 0; i < n.Length && i < d.N.Length; i++) n[i] += d.N[i] * k;
        }

        unsafe void SetSkinUniforms(in Item it, MeshEntry entry)
        {
            if (!it.Skinned || !entry.HasSkin || it.Renderer is not SkinnedMeshRenderer smr)
            {
                _program.Set("uSkinned", 0);
                return;
            }
            var bones = smr.bones;
            var bind = it.Mesh.RenderBindposes;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                var m = b != null ? b.localToWorldMatrix * bind[i] : smr.transform.localToWorldMatrix;
                int o = i * 16;
                _boneData[o + 0] = m.m00; _boneData[o + 1] = m.m10; _boneData[o + 2] = m.m20; _boneData[o + 3] = m.m30;
                _boneData[o + 4] = m.m01; _boneData[o + 5] = m.m11; _boneData[o + 6] = m.m21; _boneData[o + 7] = m.m31;
                _boneData[o + 8] = m.m02; _boneData[o + 9] = m.m12; _boneData[o + 10] = m.m22; _boneData[o + 11] = m.m32;
                _boneData[o + 12] = m.m03; _boneData[o + 13] = m.m13; _boneData[o + 14] = m.m23; _boneData[o + 15] = m.m33;
            }
            fixed (float* p = _boneData) _gl.UniformMatrix4(_program.Loc("uBones"), (uint)bones.Length, false, p);
            _program.Set("uSkinned", 1);
            var origin = smr.transform.position;
            _gl.Uniform3(_program.Loc("uSkinOrigin"), origin.x, origin.y, origin.z);
        }

        void WriteInstance(in Item it, int o)
        {
            var d = _instanceData;
            var m = it.WorldSpace ? CosmicShore.Engine.Matrix4x4.identity : it.Renderer.transform.localToWorldMatrix;
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
            // Material colours are the linear intensities the GPU receives (Docs/PALETTE.md §3,
            // measured on screen): no de-gamma step.
            d[o + 16] = dark.r; d[o + 17] = dark.g; d[o + 18] = dark.b; d[o + 19] = dark.a;
            d[o + 20] = bright.r; d[o + 21] = bright.g; d[o + 22] = bright.b; d[o + 23] = bright.a;
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
                || !ReferenceEquals(e.UvRef, uvs) || !ReferenceEquals(e.ColRef, cols) || e.SubRefs.Length != subs
                || !ReferenceEquals(e.SkinRef, mesh.RenderBoneWeights);
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
            var bw = mesh.RenderBoneWeights;
            e.HasSkin = bw != null && bw.Length == n;
            e.SkinRef = bw;
            if (e.HasSkin)
            {
                var skin = new float[n * 8];
                for (int i = 0; i < n; i++)
                {
                    int o = i * 8;
                    skin[o] = bw[i].boneIndex0; skin[o + 1] = bw[i].boneIndex1; skin[o + 2] = bw[i].boneIndex2; skin[o + 3] = bw[i].boneIndex3;
                    skin[o + 4] = bw[i].weight0; skin[o + 5] = bw[i].weight1; skin[o + 6] = bw[i].weight2; skin[o + 7] = bw[i].weight3;
                }
                if (e.SkinVbo == 0) e.SkinVbo = _gl.GenBuffer();
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, e.SkinVbo);
                fixed (float* sp = skin) _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(skin.Length * sizeof(float)), sp, BufferUsageARB.StaticDraw);
                _gl.EnableVertexAttribArray(12); _gl.VertexAttribPointer(12, 4, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)0);
                _gl.EnableVertexAttribArray(13); _gl.VertexAttribPointer(13, 4, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(4 * sizeof(float)));
            }
            else
            {
                _gl.DisableVertexAttribArray(12);
                _gl.DisableVertexAttribArray(13);
            }
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
