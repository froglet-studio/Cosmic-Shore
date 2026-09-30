using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CosmicShore.Engine;
using CosmicShore.Engine.Rendering;
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
layout(location=14) in vec4 aTangent;
layout(location=15) in vec4 aUv1;       // TEXCOORD1: the shield meshes' per-face centroid
uniform mat4 uViewProj;
uniform samplerBuffer uExt;      // per-instance extended clock block (15 vec4), see SceneRenderer.ExtLayout
uniform int uPrismGraph;         // 0 none, 1 BlockGraph, 2 ExplodingBlockGraph
uniform vec2 uExplosive;         // _ExplosiveRotation, _ExplosiveSpead
uniform float uMaxSqrDist;
uniform vec3 uCamPos;
uniform int uSkinned;            // 1: linear-blend skinning, uBones[i] = bone.localToWorld * bindpose[i]
uniform mat4 uBones[128];
uniform vec3 uSkinOrigin;
uniform float uClock;
uniform vec4 uCradleCentre[4];   // _PrismCradleCentre: xyz hull centre, w hull radius
uniform vec4 uCradleWeight[4];   // _PrismCradleWeight: x strength
uniform vec4 uCradleParams;      // _PrismCradleParams: (drape reach, exponent, live slots, -)
uniform vec4 uSliceP0, uSliceP1, uSliceP2, uSliceP3; // PrismSlice material constants (see SetSliceUniforms)
uniform int uFamily;
// PrismCradle.hlsl, translated: the last vertex node of both prism graphs. Every vertex inside
// the drape reach of a riding hull slides along its own radius toward that hull's surface, with
// the analytic normal of that radial map. Applied in world space (equivalent, since it is last).
void cradleFalloff(float s, float reach, float e, out float k, out float dk){
  if (s <= 0.0) { k = 1.0; dk = 0.0; return; }
  if (s >= reach) { k = 0.0; dk = 0.0; return; }
  float t = s / reach;
  float S = t * t * (3.0 - 2.0 * t);
  float dS = 6.0 * t * (1.0 - t);
  float u = 1.0 - S;
  k = pow(u, e);
  dk = -e * pow(u, e - 1.0) * dS / reach;
}
void cradle(inout vec3 pW, inout vec3 nrm){
  int count = int(uCradleParams.z);
  float reach = uCradleParams.x;
  if (count <= 0 || !(reach > 0.0)) return;
  float expo = max(uCradleParams.y, 1.0);
  float nl = dot(nrm, nrm);
  if (!(nl > 1e-12) || !(nl < 1e12)) return;
  vec3 nW = nrm * inversesqrt(nl);
  float bestA = 0.0, bestK = 0.0, bestDk = 0.0, bestW = 0.0, bestD = 0.0, bestS = 0.0;
  vec3 bestDir = nW, bestU = vec3(0.0);
  for (int i = 0; i < 4; i++) {
    if (i >= count) break;
    vec4 slot = uCradleCentre[i];
    float w = clamp(uCradleWeight[i].x, 0.0, 1.0);
    if (!(w > 0.0) || !(slot.w > 0.0)) continue;
    vec3 rad = pW - slot.xyz;
    float d = length(rad);
    if (!(d > 1e-4)) continue;
    float s = d - slot.w;
    if (s >= reach) continue;
    float k, dk;
    cradleFalloff(s, reach, expo, k, dk);
    float a = k * w;
    if (a <= bestA) continue;
    bestA = a; bestK = k; bestDk = dk; bestW = w; bestD = d; bestS = s; bestDir = rad / d; bestU = slot.xyz;
  }
  if (!(bestA > 0.0)) return;
  float f = bestD - bestS * bestK * bestW;
  vec3 pNew = bestU + bestDir * f;
  float ra = max(1.0 - bestW * (bestK + bestS * bestDk), 1e-3);
  float rb = max(f / bestD, 1e-3);
  float nr = dot(nW, bestDir);
  vec3 nNew = bestDir * (nr / ra) + (nW - bestDir * nr) / rb;
  if (!(dot(nNew, nNew) > 1e-12)) nNew = bestDir * (nr >= 0.0 ? 1.0 : -1.0);
  if (!(dot(pNew, pNew) < 1e12)) return;
  pW = pNew;
  nrm = normalize(nNew);
}
out vec3 vWorld;
out vec3 vNormal;
out vec2 vUv;
out vec4 vColor;
out vec3 vObj;
out vec3 vObjNormal;
flat out vec4 vDark;
flat out vec4 vBright;
flat out vec3 vOrigin;
flat out float vOpacity;
flat out vec3 vVelocity;
flat out vec2 vLit;
out vec3 vCutN;                  // slice: the cut face's normal, carried with the half
out vec4 vCut;                   // slice: (rest depth, far flag, depth fraction, -)
out vec3 vNoise;                 // slice: dissolve noise position
flat out vec2 vPhase;            // slice: (dissolve progress, heat)              // (_PrismSuperShielded, _PrismLitDomain) for the destruction sight
// -- The prism graphs' vertex chain (BlockGraph / ExplodingBlockGraph), translated from the
// project's own PrismClockAnimation.hlsl, PrismSway.hlsl and the Prism Sub Graph / Distance
// Spread And Colors / Spread Sub Graph / Tangent Slider / Rotate Faces Along Axis subgraphs.
vec4 X(int k){ return texelFetch(uExt, gl_InstanceID * 15 + k); }
vec3 rotAxis(vec3 v, vec3 axis, float ang){
  float l = length(axis);
  if (!(l > 1e-8)) return v;               // Rotate About Axis normalizes; a zero axis has no rotation
  axis /= l;
  float s = sin(ang), c = cos(ang);
  return v * c + cross(axis, v) * s + axis * (dot(axis, v) * (1.0 - c));
}
float jHash(vec3 p){ p = fract(p * vec3(0.1031, 0.1030, 0.0973)); p += dot(p, p.yzx + 33.33); return fract((p.x + p.y) * p.z); }
void growScale(inout vec3 p){
  if (iGrow.y > 0.0) {
    float t = max(uClock - iGrow.x, 0.0);
    p *= max(vec3(1.0) - (vec3(1.0) - iGrowFrac.xyz) * exp(-iGrow.y * t), vec3(0.0));
  }
}
// PrismSlice.shader/.hlsl, translated: one half of a prism the Rhino's blade cut. Its far skin
// is centrally projected onto the cut plane, then the half hinges open about the cut, separates
// along the cut normal and drifts, all off one clock stamp.
vec3 sliceRotate(vec3 v, vec3 a, float ang){ float sn = sin(ang), cs = cos(ang); return v * cs + cross(a, v) * sn + a * (dot(a, v) * (1.0 - cs)); }
void sliceVertex(mat4 M){
  vec4 timing = X(0), plane = X(1), centre = X(2), pivot = X(3), axis = X(4), drift = X(5);
  float age = max(uClock - timing.x, 0.0);
  vec3 pos = aPos;
  float dp = dot(plane.xyz, aPos) - plane.w;
  float restDepth = -dp, farFlag = 0.0;
  if (dp > 0.0) {
    float dc = dot(plane.xyz, centre.xyz) - plane.w;
    float denom = dp - dc;
    float lambda = denom > 1e-6 ? clamp(-dc / denom, 0.0, 1.0) : 1.0;
    pos = centre.xyz + (aPos - centre.xyz) * lambda;
    farFlag = 1.0;
  }
  vec3 restWS = (M * vec4(pos, 1.0)).xyz;
  mat3 NM = transpose(inverse(mat3(M)));
  vec3 skinWS = normalize(NM * aNormal);
  vec3 cutWS = dot(plane.xyz, plane.xyz) > 1e-12 ? normalize(NM * plane.xyz) : skinWS;
  float u = age;
  float separate = 1.0 - exp(-u / max(uSliceP0.x, 1e-4));
  float open = 1.0 - exp(-u / max(uSliceP0.y, 1e-4));
  float td = max(uSliceP0.z, 1e-4);
  float driftAmt = td * (1.0 - exp(-u / td));
  float ang = axis.w * open;
  vec3 posWS = pivot.xyz + sliceRotate(restWS - pivot.xyz, axis.xyz, ang) - cutWS * (pivot.w * separate) + drift.xyz * driftAmt;
  gl_Position = uViewProj * vec4(posWS, 1.0);
  vWorld = posWS;
  vNormal = sliceRotate(skinWS, axis.xyz, ang);
  vCutN = sliceRotate(cutWS, axis.xyz, ang);
  float cutDepth = farFlag > 0.5 ? 0.0 : max(restDepth, 0.0);
  vCut = vec4(restDepth, farFlag, cutDepth / max(centre.w, 1e-4), 0.0);
  vNoise = (mat3(M) * pos) / max(uSliceP2.w, 1e-3) + vec3(17.13, 31.71, 7.37) * timing.z;
  float progress = 0.0;
  if (timing.y > 0.0) {
    float a = clamp(uSliceP1.x, 0.0, 1.0) * timing.y;
    float b = (1.0 - clamp(uSliceP1.y, 0.0, 1.0)) * timing.y;
    progress = clamp((age - a) / max(b - a, 1e-4), 0.0, 1.0) * 1.02;
  }
  float heat = 1.0 - smoothstep(0.0, max(uSliceP2.y, 1e-3), age);
  vPhase = vec2(progress, heat);
  vDark = iDark; vBright = iBright;
  vUv = aUv; vColor = aColor; vObj = pos; vObjNormal = aNormal; vOrigin = iM3.xyz;
  vOpacity = 1.0; vVelocity = vec3(0.0); vLit = vec2(0.0);
}
void main(){
  mat4 M = mat4(iM0, iM1, iM2, iM3);
  if (uFamily == 8) { sliceVertex(M); return; }
  vec3 p = aPos;
  vec3 nO = aNormal;
  vec4 dark = iDark, bright = iBright;
  float opacity = 1.0;
  vec3 vel = vec3(0.0);
  if (uPrismGraph != 0 && uSkinned == 0) {
    vec3 T = aTangent.xyz;
    vec3 scale = vec3(length(M[0].xyz), length(M[1].xyz), length(M[2].xyz));
    mat3 invM = inverse(mat3(M));
    vec4 e0 = X(0), e3 = X(3), e4 = X(4), e5 = X(5), e6 = X(6), e7 = X(7), e8 = X(8), e9 = X(9);
    vec4 e10 = X(10), e11 = X(11), e12 = X(12), e13 = X(13), e14 = X(14);
    // PrismColorLerp: stamped start colours/spread toward the targets, smoothstep eased.
    vec3 spread = e4.xyz;
    if (e0.y > 0.0) {
      float t = smoothstep(0.0, 1.0, clamp((uClock - e0.x) / e0.y, 0.0, 1.0));
      bright = mix(X(1), bright, t);
      dark = mix(X(2), dark, t);
      spread = mix(e3.xyz, spread, t);
    }
    // PrismExplosionClock: amount, opacity and the world-space flight carried into object space.
    vec3 offsetOS = vec3(0.0);
    float amount = e4.w;
    opacity = e5.w;
    vel = e5.xyz;
    if (e3.w > 0.0) {
      float t = max(uClock - e0.z, 0.0);
      amount = e0.w * t;
      opacity = clamp(1.0 - t / e3.w, 0.0, 1.0);
      offsetOS += invM * (vel * t);
    }
    // PrismFlightClock: walked in from the muzzle under the bullets' cosine easing.
    vec3 flightWorld = vec3(0.0);
    if (e7.w > 0.0) {
      float t = clamp(uClock - e6.w, 0.0, e7.w);
      float cov = sin(t * 1.5707963 / e7.w);
      flightWorld = e6.xyz * (0.63661977 * e7.w) * (cov - 1.0);
      vec3 fo = invM * flightWorld;
      if (dot(fo, fo) < 1e12) offsetOS += fo;
    }
    // Prism Sub Graph: Distance Spread And Colors -> Spread Sub Graph -> Tangent Slider.
    float sqrDist;
    if (uPrismGraph == 2) { spread += vec3(amount * uExplosive.y); sqrDist = 1000.0; }
    else { vec3 d = (M[3].xyz + flightWorld) - uCamPos; sqrDist = dot(d, d); }
    bool over = sqrDist > uMaxSqrDist;
    vec3 k = over ? spread * vec3(50.0, 35.0, 20.0) : mix(vec3(-7.0), spread * vec3(50.0, 35.0, 20.0), sqrDist / uMaxSqrDist);
    vec3 sv = max(k, spread) / scale;
    vec3 reduced = sv - spread / scale;
    p = p + sv * nO + 0.5 * reduced * T;
    // PrismShieldMorph: each face scales about its own centroid (TEXCOORD1).
    if (e9.y > 0.0) {
      float t = smoothstep(0.0, 1.0, clamp((uClock - e9.x) / e9.y, 0.0, 1.0));
      float sh = e9.z < 0.0 ? 1.0 : 0.0;
      p = aUv1.xyz + mix(t, 1.0 - t, sh) * (p - aUv1.xyz) + sh * t * e9.w * nO;
    }
    // PrismSway: first-order bend about the limb, a function of limb height alone.
    {
      float tt = uClock * e14.x + e14.y;
      float zl = e14.z + dot(p, e13.xyz);
      p += e11.xyz * zl * sin(tt) + e12.xyz * zl * sin(tt * 0.73 + e14.y + 1.5707963) * 0.45;
    }
    // Rotate Faces Along Axis (ExplodingBlockGraph): each face spins away about its tangent and
    // about cross(velocity, normal), in the locally isotropic frame.
    if (uPrismGraph == 2) {
      vec3 N = nO;
      vec3 Pn = dot(p, N) * N;
      vec3 sMinus = vec3(-0.5) - reduced;
      vec3 sPlus = vec3(0.5) + reduced + vec3(amount * uExplosive.y);
      vec3 A = (p - Pn) + 0.5 * sMinus * T;
      vec3 c = (aUv1.xyz - Pn + 0.5 * sMinus * T) * e12.w;
      float ang = amount * uExplosive.x;
      vec3 ax2 = cross(vel, N);
      vec3 q = rotAxis(rotAxis(scale * (A - c), T, ang), ax2, ang);
      p = q / scale + Pn + 0.5 * sPlus * T + c;
      nO = rotAxis(rotAxis(N, T, ang), ax2, ang);
    }
    // PrismJiggleClock: the super-shield deflection wobble.
    if (e11.w > 0.0) {
      float t = uClock - e10.w;
      float nl = dot(nO, nO);
      if (t > 0.0 && t < e11.w && nl > 1e-8 && all(greaterThan(scale, vec3(1e-5)))) {
        vec3 n = nO * inversesqrt(nl);
        float u = t / e11.w;
        float env = (1.0 - u) * exp(-2.5 * u);
        vec3 origin = M[3].xyz;
        float sa = jHash(n * 17.0 + origin * 0.013 + e10.w);
        float sb = jHash(n * 29.0 - origin * 0.017 + e10.w * 1.7 + 11.0);
        float sg = n.z >= 0.0 ? 1.0 : -1.0;
        float a = -1.0 / (sg + n.z);
        float cc = n.x * n.y * a;
        vec3 tg = vec3(1.0 + sg * n.x * n.x * a, sg * cc, -sg * n.x);
        vec3 bt = vec3(cc, sg + n.y * n.y * a, -n.y);
        float phi = e10.y * t + sa * 6.2831853;
        float theta = 1.5707963 * (0.5 - 0.5 * cos(e10.z * t + sb * 6.2831853));
        vec3 axis = n * cos(theta) + (tg * cos(phi) + bt * sin(phi)) * sin(theta);
        float ang = e10.x * env;
        p = rotAxis(p * scale, axis, ang) / scale;
        nO = rotAxis(nO / scale, axis, ang) * scale;
      }
    }
    growScale(p);
    p += offsetOS;
    // PrismSuctionClock + PrismSuctionConverge: the whole prism lerps toward a world point.
    if (e8.y > 0.0) {
      float t = max(uClock - e8.x - e8.w, 0.0);
      float pr = clamp(t / e8.y, 0.0, 1.0);
      float state = e8.z < 0.0 ? 1.0 - pr : pr;
      vec3 loc = (inverse(M) * vec4(e7.xyz, 1.0)).xyz;
      if (!(dot(loc, loc) < 1e12)) loc = p;
      p = mix(p, loc, clamp(state, 0.0, 1.0));
    }
  } else {
    growScale(p);
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
    nrm = transpose(inverse(mat3(M))) * nO;
    if (uPrismGraph != 0) { vec3 cw = w.xyz; cradle(cw, nrm); w.xyz = cw; }
  }
  vWorld = w.xyz;
  vNormal = nrm;
  vUv = aUv;
  vColor = aColor;
  vObj = p;
  vObjNormal = nO;
  vOrigin = uSkinned == 1 ? uSkinOrigin : iM3.xyz;
  vDark = dark;
  vBright = bright;
  vOpacity = opacity;
  vVelocity = vel;
  vLit = uPrismGraph != 0 ? vec2(X(13).w, X(14).w) : vec2(0.0);
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
flat in float vOpacity;
flat in vec3 vVelocity;
flat in vec2 vLit;
in vec3 vCutN;
in vec4 vCut;
in vec3 vNoise;
flat in vec2 vPhase;
uniform vec4 uSliceP0, uSliceP1, uSliceP2, uSliceP3;
uniform vec4 uSliceHot;       // _CutHotColor
uniform int uPrismGraph;
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
uniform vec3 uOccTarget;       // _PrismOcclusionTarget: the local pilot's vessel
uniform vec3 uOccParams;       // _PrismOcclusionParams: (outer radius, inner radius, core alpha); x <= 0 = off
uniform float uOccNear;        // _PrismOcclusionNearRadius: the frustum's radius at the lens
uniform vec3 uSightApex, uSightAxis, uSightGape, uSightParams; // the viewer's OWN aim (_PrismSight*)
uniform float uSightStrength;
uniform vec4 uSightBlocker;    // _PrismSightBlockerColor (w > 0 = published)
uniform vec4 uLitApex[8], uLitAxis[8], uLitGape[8], uLitTint[8], uLitShape[8]; // _PrismLitPeer* bank
uniform int uLitCount;
uniform int uVesselVision;     // 1: VesselGraph batch (vBright carries _VesselVisionTint)
uniform vec4 uVisionBand, uVisionShape, uVisionRim, uVisionBreakup; // _VesselVision* globals
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
// PrismErosionFade (PrismOcclusionCorridor.hlsl): the exploding prism's hard-edged wipe across each face.
vec3 oHash3(vec3 p3){ p3 = fract(p3 * vec3(0.1031, 0.1030, 0.0973)); p3 += dot(p3, p3.yxz + 33.33); return fract((p3.xxy + p3.yxx) * p3.zyx); }
float oHash1(vec3 p3){ p3 = fract(p3 * 0.1031); p3 += dot(p3, p3.zyx + 31.32); return fract((p3.x + p3.y) * p3.z); }
float erosionSurvival(vec2 UV, vec3 vel, float op){
  if (op >= 1.0) return 1.0;
  if (op <= 0.0) return 0.0;
  vec2 uv = UV * 2.0 - 1.0;
  vec3 e = oHash3(vel);
  vec3 h = oHash3(e * 64.0 + 17.0);
  float ang = 6.28318530718 * h.x;
  vec2 dir = vec2(cos(ang), sin(ang));
  float w01 = dot(uv, dir) / (abs(dir.x) + abs(dir.y)) * 0.5 + 0.5;
  float c = dot(uv, vec2(-dir.y, dir.x)) * 2.5 + h.z * 64.0;
  float ci = floor(c);
  float cf = c - ci;
  cf = cf * cf * (3.0 - 2.0 * cf);
  float jag = mix(oHash1(vec3(ci, h.y * 64.0, e.z * 64.0)), oHash1(vec3(ci + 1.0, h.y * 64.0, e.z * 64.0)), cf);
  w01 = clamp(w01 + (jag - 0.5) * 0.12, 0.0, 1.0);
  float thr = (0.15 + smoothstep(-0.02, 1.02, w01) * 0.85) * 0.998 + 0.001;
  return op >= thr ? 1.0 : 0.0;
}
// VesselVisionShading.hlsl, translated: a vessel is re-shaded into a flat cel-banded silhouette
// in its domain colour as a function of its distance from this camera (both edges graded), its
// interior broken up by object-space angular cells that close with distance, the rim exempt.
float vvSmooth(float a, float b, float x){ float t = clamp((x - a) / max(b - a, 1e-5), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
float vvHash1(vec3 p3){ p3 = fract(p3 * 0.1031); p3 += dot(p3, p3.zyx + 31.32); return fract((p3.x + p3.y) * p3.z); }
vec3 vesselVision(vec3 base, vec3 P, vec3 Nraw, vec4 tint, vec3 origin, vec3 offsetOS){
  if (tint.a <= 0.0) return base;
  float strength = clamp(uVisionShape.x, 0.0, 1.0);
  if (strength <= 0.0 || uVisionBand.w <= 0.0) return base;
  float dist = distance(uCamPos, origin);
  float band = clamp(min(vvSmooth(uVisionBand.x, uVisionBand.y, dist), 1.0 - vvSmooth(uVisionBand.z, uVisionBand.w, dist)), 0.0, 1.0);
  float amount = band * strength;
  if (amount <= 0.0) return base;
  vec3 V = normalize(uCamPos - P);
  vec3 N = normalize(Nraw);
  float steps = max(uVisionShape.y, 1.0);
  float ndv = clamp(dot(N, V), 0.0, 1.0);
  float tone = mix(clamp(uVisionShape.z, 0.0, 1.0), 1.0, min(floor(ndv * steps), steps - 1.0) / max(steps - 1.0, 1.0));
  float rim = clamp(vvSmooth(uVisionRim.x, uVisionRim.y, 1.0 - ndv), 0.0, 1.0);
  vec3 cel = tint.rgb * (tone + rim * max(uVisionRim.z, 0.0)) * max(uVisionShape.w, 0.0);
  float breakup = 1.0;
  float cells = uVisionBreakup.x, reach = uVisionBreakup.y, bs = clamp(uVisionBreakup.z, 0.0, 1.0), endD = uVisionBreakup.w;
  if (bs > 0.0 && reach > 0.0 && cells > 0.0 && dot(offsetOS, offsetOS) >= 1e-8) {
    float amt = bs * (1.0 - vvSmooth(endD * 0.4, max(endD, 1e-3), dist));
    if (amt > 0.0) {
      float coverage = vvSmooth(0.0, max(reach, 1e-3), 1.0 - ndv);
      vec3 cell = floor(normalize(offsetOS) * cells);
      float thr = vvHash1(cell + 0.5) * 0.92;
      breakup = mix(1.0, vvSmooth(thr - 0.03, thr + 0.03, coverage), amt);
    }
  }
  return mix(base, cel, amount * max(breakup, rim));
}
// PrismDestructionSight.hlsl, translated: the LIT fundamental. The viewer's own aim lights
// whole prisms (sampled at the prism's origin) in a pale cool cast, or flags a super-shield in
// the danger colour; otherwise up to eight peer lights (cone / sphere / cylinder) blend by
// weight-averaged hue at the brightness of the strongest, desaturated toward white.
float sightEdge(float d, float r){ return mix(0.35, 1.0, pow(clamp(d / r, 0.0, 1.0), 2.0)); }
float sightFillCone(vec3 P, vec3 apex, vec3 axis, vec3 gape, vec3 prm){
  if (prm.x <= 0.0) return 0.0;
  vec3 rel = P - apex;
  float s = dot(rel, axis);
  if (s <= 0.0 || s > prm.x) return 0.0;
  float core = prm.y * s;
  if (core <= 0.0) return 0.0;
  vec3 radial = rel - axis * s;
  float halfLen = prm.z * s;
  float along = dot(radial, gape);
  float d = length(radial - gape * clamp(along, -halfLen, halfLen));
  if (d > core) return 0.0;
  return sightEdge(d, core);
}
float litFill(vec3 P, vec3 o, vec3 axis, vec3 gape, vec3 prm, float shape){
  if (prm.x <= 0.0) return 0.0;
  if (shape >= 2.0) {
    if (prm.y <= 0.0) return 0.0;
    vec3 rel = P - o;
    float s = dot(rel, axis);
    float axial = prm.z > 0.0 ? abs(s) : s;
    if (axial < 0.0 || axial > prm.x) return 0.0;
    float d = length(rel - axis * s);
    if (d > prm.y) return 0.0;
    return sightEdge(d, prm.y);
  }
  if (shape >= 1.0) {
    vec3 rel = P - o;
    float d2 = dot(rel, rel);
    if (d2 > prm.x * prm.x) return 0.0;
    return sightEdge(sqrt(d2), prm.x);
  }
  return sightFillCone(P, o, axis, gape, prm);
}
vec3 destructionSight(vec3 base, vec3 P, float domain, float superShielded){
  if (uSightParams.x > 0.0 && uSightStrength > 0.0) {
    float own = sightFillCone(P, uSightApex, uSightAxis, uSightGape, uSightParams) * uSightStrength;
    if (own > 0.0) {
      if (superShielded > 0.5) {
        vec3 blocker = uSightBlocker.w > 0.0 ? uSightBlocker.xyz : vec3(1.0, 0.06, 0.05);
        return mix(base, blocker, 0.75 * uSightStrength) + blocker * (uSightStrength * 0.9);
      }
      return base + vec3(0.45, 0.70, 1.0) * (own * 0.7);
    }
  }
  vec3 weighted = vec3(0.0);
  float total = 0.0, peak = 0.0;
  for (int i = 0; i < 8; i++) {
    if (i >= uLitCount) break;
    vec4 tag = uLitShape[i];
    if (tag.y > 0.0 && tag.y != domain) continue;
    vec4 a = uLitApex[i], x = uLitAxis[i], g = uLitGape[i], t = uLitTint[i];
    float w = litFill(P, a.xyz, x.xyz, g.xyz, vec3(a.w, x.w, g.w), tag.x) * t.a;
    if (w <= 0.0) continue;
    weighted += mix(t.rgb, vec3(1.0), 0.4) * w;
    total += w;
    peak = max(peak, w);
  }
  if (total <= 0.0) return base;
  return base + (weighted / total) * (peak * 0.55);
}
// PrismOcclusionCorridor.hlsl, translated: the camera->ship frustum inside which prism mass
// dissolves through the SHATTER screen-door (a cracked lattice of Voronoi walls in pixels),
// and PrismBackFaceFade, which sharpens alpha on away-facing surfaces. Kernel 4 is the
// shipped one; the constants are the file's.
vec2 oHash2(vec2 cell){ vec3 p3 = fract(cell.xyx * vec3(0.1031, 0.1030, 0.0973)); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.xx + p3.yz) * p3.zy); }
float occSmoother(float t){ t = clamp(t, 0.0, 1.0); return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }
float occShatter(vec2 pixel, float time){
  const float cellPx = 16.26, wallPx = 20.0, morph = 0.3256;
  vec2 p = pixel / cellPx;
  vec2 base = floor(p);
  float phase = time * morph * 6.28318530718;
  float best = 8.0;
  vec2 owner = base;
  for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
    vec2 cell = base + vec2(x, y);
    vec2 orbit = 0.5 + 0.5 * sin(6.28318530718 * oHash2(cell) + phase);
    vec2 off = (cell + orbit) - p;
    float d = dot(off, off);
    if (d < best) { best = d; owner = cell; }
  }
  vec2 h = oHash2(owner);
  float ang = 6.28318530718 * h.y;
  float ramp = dot(p - owner, vec2(cos(ang), sin(ang))) * (cellPx / wallPx);
  return fract(h.x + ramp + time * morph) * 0.998 + 0.001;
}
// Returns (alpha, clip threshold).
vec2 occlusionFade(vec3 P, float baseAlpha, float noseClearance){
  if (baseAlpha <= 0.0) return vec2(0.0, 1.0);
  float alpha = baseAlpha;
  float outerR = uOccParams.x;
  if (outerR > 0.0) {
    vec3 axis = uOccTarget - uCamPos;
    vec3 rel = P - uCamPos;
    float axisLenSq = dot(axis, axis);
    if (axisLenSq > 1e-6) {
      float t = dot(rel, axis) / axisLenSq;
      float axisLen = sqrt(axisLenSq);
      float innerR = min(uOccParams.y, outerR);
      float clearanceT = (outerR * noseClearance) / axisLen;
      float bandT = (outerR - innerR) / axisLen;
      float shrink = min(1.0, 0.5 / max(clearanceT + bandT, 1e-4));
      clearanceT *= shrink;
      bandT *= shrink;
      float tSolid = clamp(1.0 - clearanceT, 0.0, 1.0);
      if (t > 0.0 && t < tSolid) {
        float dAxis = length(rel - axis * t);
        float nearR = clamp(uOccNear, 0.0, outerR);
        float outerAtT = mix(nearR, outerR, t);
        if (dAxis < outerAtT) {
          float innerAtT = outerAtT * (innerR / outerR);
          float clearRadial = 1.0 - occSmoother((dAxis - innerAtT) / max(outerAtT - innerAtT, 1e-4));
          float band = clamp(bandT, 1e-4, 1.0);
          float clearAxial = 1.0 - occSmoother((t - (tSolid - band)) / band);
          alpha = baseAlpha * mix(1.0, uOccParams.z, clearRadial * clearAxial);
        }
      }
    }
  }
  if (alpha >= 1.0) return vec2(alpha, 0.0);
  return vec2(alpha, occShatter(gl_FragCoord.xy, uTime));
}
float sliceHash(vec3 p){ p = fract(p * 0.3183099 + vec3(0.71, 0.113, 0.419)); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float sliceNoise(vec3 x){
  vec3 i = floor(x), f = x - i, w = f * f * (3.0 - 2.0 * f);
  float n000 = sliceHash(i), n100 = sliceHash(i + vec3(1,0,0)), n010 = sliceHash(i + vec3(0,1,0)), n110 = sliceHash(i + vec3(1,1,0));
  float n001 = sliceHash(i + vec3(0,0,1)), n101 = sliceHash(i + vec3(1,0,1)), n011 = sliceHash(i + vec3(0,1,1)), n111 = sliceHash(i + vec3(1,1,1));
  return mix(mix(mix(n000, n100, w.x), mix(n010, n110, w.x), w.y), mix(mix(n001, n101, w.x), mix(n011, n111, w.x), w.y), w.z);
}
vec4 sliceColor(){
  float progress = vPhase.x, heat = vPhase.y;
  float value = 1.0;
  if (progress > 0.0) value = mix(clamp(vCut.z, 0.0, 1.0), clamp(sliceNoise(vNoise), 0.0, 1.0), clamp(uSliceP3.x, 0.0, 1.0));
  if ((vCut.x < 0.0 && vCut.y < 0.999) || (progress > 0.0 && value < progress)) discard;
  vec2 occ = occlusionFade(vWorld, 1.0, 0.0);
  if (occ.x < occ.y) discard;
  vec3 bright = vBright.rgb;
  vec3 V = normalize(uCamPos - vWorld);
  vec3 flesh = mix(bright * uSliceP1.w, uSliceHot.rgb, heat);
  float fleshShade = mix(1.0, abs(dot(normalize(vCutN), V)), uSliceP2.x);
  vec3 color;
  if (!gl_FrontFacing || vCut.y >= 0.999) color = flesh * fleshShade;
  else {
    float fres = pow(1.0 - clamp(dot(normalize(vNormal), V), 0.0, 1.0), uSliceP1.z);
    color = mix(vDark.rgb, bright, fres);
    float seam = exp(-max(vCut.x, 0.0) / max(uSliceP2.z, 1e-3)) * heat;
    color = mix(color, uSliceHot.rgb, clamp(seam, 0.0, 1.0));
  }
  if (progress > 0.0) {
    float ember = 1.0 - clamp((value - progress) / max(uSliceP3.y, 1e-4), 0.0, 1.0);
    color = mix(color, mix(bright * uSliceP3.z, uSliceHot.rgb, uSliceP3.w), ember);
  }
  return vec4(color, 1.0);
}
void main(){
  if (uPrismGraph != 0) {
    // BlockGraph: corridor(material alpha). ExplodingBlockGraph: the erosion wipe feeds the
    // corridor (debris dissolves flush to the ship: no nose clearance, it has no collider).
    float baseA = uPrismGraph == 2 ? erosionSurvival(vUv, vVelocity, vOpacity) : vDark.a;
    vec2 occ = occlusionFade(vWorld, baseA, uPrismGraph == 2 ? 0.0 : 1.0);
    float a = occ.x;
    if (a < 1.0 && a > 0.0 && dot(normalize(vNormal), uCamPos - vWorld) < 0.0) a = pow(a, 3.0);
    if (a < occ.y || a <= 0.0) discard;
  }
  vec3 N = normalize(vNormal);
  if (!gl_FrontFacing) N = -N;
  vec3 V = normalize(uCamPos - vWorld);
  vec4 tex = texture(uTex, vUv * uTexST.xy + uTexST.zw);
  vec4 col;
  if (uFamily == 8) {
    col = sliceColor();
  } else if (uFamily == 7) {
    vec3 nOS = gl_FrontFacing ? vObjNormal : -vObjNormal;
    col = crackle(vObj, nOS, uCamPosOS - vObj);
  } else if (uFamily == 2) {
    // FresnelPower4: back-facing normals keep a faint term (d+1)*0.2 instead of clamping.
    float d = dot(N, V);
    float x = d > 0.0 ? d : (d + 1.0) * 0.2;
    float f = pow(1.0 - x, uFresPow);
    vec4 bright = vBright;
    if (uMaxSqrDist > 0.0) {
      // DistanceSpreadAndColors: the rim sinks toward the base with camera distance
      // (ExplodingBlockGraph wires a constant 1000 in place of the camera distance).
      vec3 dc = vWorld - uCamPos;
      float n = (uPrismGraph == 2 ? 1000.0 : dot(dc, dc)) / uMaxSqrDist;
      bright = mix(vBright, vDark, n > 1.0 ? 0.9 : n * 0.9);
    }
    col = mix(vDark, bright, f) * tex;
    if (uPrismGraph != 0) col.rgb = destructionSight(col.rgb, vOrigin, vLit.y, vLit.x);
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
  if (uVesselVision == 1) col.rgb = vesselVision(col.rgb, vWorld, vNormal, vBright, vOrigin, vObj);
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
            public object VertsRef, NormRef, UvRef, ColRef, TanRef, Uv1Ref;
            public object[] SubRefs = Array.Empty<object>();
            public bool HasColors;
            public bool HasSkin;
            public uint SkinVbo;
            public object SkinRef;
            public int Frame;
        }

        sealed class MatState
        {
            public int Revision;      // Material.Revision this was derived from; a runtime edit re-derives
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
            public bool VesselVision;       // VesselGraph: the vision band re-shades it (tint rides the bright slot)
            public int PrismGraph;          // 0 none, 1 BlockGraph, 2 ExplodingBlockGraph
            public float ExplosiveRotation, ExplosiveSpread;
            public float[] Ext;             // the material's value for every extended clock property
            public ExtProp[] Layout;        // which properties the block carries (prism clock, or the slice stamps)
            public Vector4 SliceP0, SliceP1, SliceP2, SliceP3; // PrismSlice material constants
            public Color SliceHot;
        }

        // The clock / animation properties the prism graphs read beyond the vertex attributes,
        // packed into a per-instance texture buffer (15 vec4). Each value is the entity's
        // override when it has one, else the material's (Entities Graphics semantics).
        const int ExtVec4 = 15, ExtFloats = ExtVec4 * 4;
        sealed class ExtProp
        {
            public string Name; public int Id, Slot, Offset, Count; public bool IsColor;
            public ExtProp(string name, int offset, int count, bool color = false)
            { Name = name; Id = Shader.PropertyToID(name); Slot = EntityDrawList.Slot(name); Offset = offset; Count = count; IsColor = color; }
        }
        static readonly ExtProp[] ExtLayout =
        {
            new("_ColorStartTime", 0, 1), new("_ColorDuration", 1, 1), new("_ExplodeStartTime", 2, 1), new("_ExplodeSpeed", 3, 1),
            new("_StartBrightColor", 4, 4, true),
            new("_StartDarkColor", 8, 4, true),
            new("_StartSpread", 12, 3), new("_ExplodeDuration", 15, 1),
            new("_Spread", 16, 3), new("_ExplosionAmount", 19, 1),
            new("_Velocity", 20, 3), new("_Opacity", 23, 1),
            new("_FlightVelocity", 24, 3), new("_FlightStartTime", 27, 1),
            new("_Location", 28, 3), new("_FlightDuration", 31, 1),
            new("_SuctionStartTime", 32, 1), new("_SuctionDuration", 33, 1), new("_SuctionDirection", 34, 1), new("_SuctionGrowDelay", 35, 1),
            new("_ShieldMorphStartTime", 36, 1), new("_ShieldMorphDuration", 37, 1), new("_ShieldMorphDirection", 38, 1), new("_ShieldMorphOffset", 39, 1),
            new("_JiggleParams", 40, 3), new("_JiggleStartTime", 43, 1),
            new("_SwaySpanX", 44, 3), new("_JiggleDuration", 47, 1),
            new("_SwaySpanY", 48, 3), new("_FacePivotFromCentroid", 51, 1),
            new("_SwayAxis", 52, 3), new("_PrismSuperShielded", 55, 1),
            new("_SwayTiming", 56, 3), new("_PrismLitDomain", 59, 1),
        };
        // CosmicShore/PrismSlice's per-instance stamps, in the same 15-vec4 block.
        static readonly ExtProp[] SliceLayout =
        {
            new("_SliceTiming", 0, 4), new("_SlicePlane", 4, 4), new("_SliceCentre", 8, 4),
            new("_SlicePivot", 12, 4), new("_SliceAxis", 16, 4), new("_SliceDrift", 20, 3),
        };
        static readonly int SlotDark = EntityDrawList.Slot("_DarkColor"), SlotBright = EntityDrawList.Slot("_BrightColor");
        static readonly int SlotGrowStart = EntityDrawList.Slot("_GrowStartTime"), SlotGrowRate = EntityDrawList.Slot("_GrowRate"), SlotGrowFrac = EntityDrawList.Slot("_GrowStartFrac");
        static Vector4 ToV4(Color c) => new(c.r, c.g, c.b, c.a);
        static float Comp(in Vector4 v, int k) => k switch { 0 => v.x, 1 => v.y, 2 => v.z, _ => v.w };

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
            public int Entity; // index into _entities + 1 (0 = a Renderer item)
        }

        readonly GL _gl;
        readonly TextureCache _textures;
        readonly GlProgram _program;
        readonly uint _instanceVbo;
        readonly ConditionalWeakTable<Mesh, MeshEntry> _meshes = new();
        readonly List<Renderer> _renderers = new();
        readonly List<Item> _opaque = new(), _transparent = new();
        readonly Dictionary<Material, MatState> _mats = new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<Material, MatState> _ribbonMats = new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<(Mesh, int, Material), List<Item>> _batches = new();
        readonly List<List<Item>> _batchPool = new();
        float[] _instanceData = new float[InstanceFloats * 256];
        int _instanceCapacity;
        float[] _extData = new float[ExtFloats * 256];
        uint _extBuffer, _extTex;
        int _extCapacity;

        /// <summary>Uploads this batch's extended blocks to the texture buffer the vertex stage fetches by instance id.</summary>
        unsafe void UploadExt(int n)
        {
            if (_extBuffer == 0) { _extBuffer = _gl.GenBuffer(); _extTex = _gl.GenTexture(); }
            _gl.BindBuffer(BufferTargetARB.TextureBuffer, _extBuffer);
            int bytes = n * ExtFloats * sizeof(float);
            if (bytes > _extCapacity)
            {
                _extCapacity = Math.Max(bytes, _extCapacity * 2);
                _gl.BufferData(BufferTargetARB.TextureBuffer, (nuint)_extCapacity, null, BufferUsageARB.StreamDraw);
            }
            fixed (float* p = _extData) _gl.BufferSubData(BufferTargetARB.TextureBuffer, 0, (nuint)bytes, p);
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.TextureBuffer, _extTex);
            _gl.TexBuffer(TextureTarget.TextureBuffer, SizedInternalFormat.Rgba32f, _extBuffer);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindBuffer(BufferTargetARB.TextureBuffer, 0);
        }
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
        static readonly int IdSightApex = Shader.PropertyToID("_PrismSightApex"), IdSightAxis = Shader.PropertyToID("_PrismSightAxis"),
            IdSightGape = Shader.PropertyToID("_PrismSightGape"), IdSightParams = Shader.PropertyToID("_PrismSightParams"),
            IdSightStrength = Shader.PropertyToID("_PrismSightStrength"), IdSightBlocker = Shader.PropertyToID("_PrismSightBlockerColor");
        static readonly int IdLitApex = Shader.PropertyToID("_PrismLitPeerApex"), IdLitAxis = Shader.PropertyToID("_PrismLitPeerAxis"),
            IdLitGape = Shader.PropertyToID("_PrismLitPeerGape"), IdLitTint = Shader.PropertyToID("_PrismLitPeerTint"),
            IdLitShape = Shader.PropertyToID("_PrismLitPeerShape"), IdLitCount = Shader.PropertyToID("_PrismLitPeerCount");
        readonly float[] _litScratch = new float[8 * 4];

        /// <summary>The Lit fundamental's globals: the viewer's own aim plus the eight-slot peer bank.</summary>
        void SetSightUniforms()
        {
            SetGlobalVec3("uSightApex", IdSightApex);
            SetGlobalVec3("uSightAxis", IdSightAxis);
            SetGlobalVec3("uSightGape", IdSightGape);
            SetGlobalVec3("uSightParams", IdSightParams);
            _program.Set("uSightStrength", Shader.GetGlobalFloat(IdSightStrength));
            var b = Shader.GetGlobalVector(IdSightBlocker);
            _program.Set("uSightBlocker", b.x, b.y, b.z, b.w);
            int count = Math.Clamp((int)Shader.GetGlobalFloat(IdLitCount), 0, 8);
            _program.Set("uLitCount", count);
            if (count == 0) return;
            SetGlobalVec4Array("uLitApex", IdLitApex);
            SetGlobalVec4Array("uLitAxis", IdLitAxis);
            SetGlobalVec4Array("uLitGape", IdLitGape);
            SetGlobalVec4Array("uLitTint", IdLitTint);
            SetGlobalVec4Array("uLitShape", IdLitShape);
        }

        void SetGlobalVec3(string uniform, int id)
        {
            var v = Shader.GetGlobalVector(id);
            _program.Set(uniform, v.x, v.y, v.z);
        }

        void SetGlobalVec4Array(string uniform, int id, int slots = 8)
        {
            Array.Clear(_litScratch);
            var arr = Shader.GetGlobalVectorArray(id);
            if (arr != null)
                for (int i = 0; i < arr.Length && i < slots; i++)
                { _litScratch[i * 4] = arr[i].x; _litScratch[i * 4 + 1] = arr[i].y; _litScratch[i * 4 + 2] = arr[i].z; _litScratch[i * 4 + 3] = arr[i].w; }
            _program.Set4v(uniform, _litScratch, slots);
        }

        static readonly (string, int)[] s_visionGlobals =
        {
            ("uVisionBand", Shader.PropertyToID("_VesselVisionBand")), ("uVisionShape", Shader.PropertyToID("_VesselVisionShape")),
            ("uVisionRim", Shader.PropertyToID("_VesselVisionRim")), ("uVisionBreakup", Shader.PropertyToID("_VesselVisionBreakup")),
        };
        static readonly int IdCradleCentre = Shader.PropertyToID("_PrismCradleCentre"), IdCradleWeight = Shader.PropertyToID("_PrismCradleWeight"),
            IdCradleParams = Shader.PropertyToID("_PrismCradleParams");
        static readonly int IdVisionTint = Shader.PropertyToID("_VesselVisionTint");
        static readonly int IdOccTarget = Shader.PropertyToID("_PrismOcclusionTarget"), IdOccParams = Shader.PropertyToID("_PrismOcclusionParams"), IdOccNear = Shader.PropertyToID("_PrismOcclusionNearRadius");

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

        /// <summary>
        /// Draw one camera. The whole draw is a read-only transform pass (nothing here writes a
        /// transform), so each world pose is validated once for the collect AND the instance
        /// writes that follow it, instead of re-walking its parent chain per read.
        /// </summary>
        public void Render(Camera camera, int width, int height)
        {
            CosmicShore.Engine.Transform.BeginReadOnlyPass();
            try { RenderCamera(camera, width, height); }
            finally { CosmicShore.Engine.Transform.EndReadOnlyPass(); }
        }

        void RenderCamera(Camera camera, int width, int height)
        {
            _frame++;
            DrawCalls = Instances = 0;
            var view = camera.worldToCameraMatrix;
            var proj = camera.projectionMatrix;
            var viewProj = proj * view;
            var camPos = camera.transform.position;
            _camPos = camPos;
            int mask = camera.cullingMask;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            SetFrustum(viewProj);
            _cullClock = Shader.GetGlobalFloat(IdPrismClock) is var cc && cc > 0 ? cc : Time.time;
            Collect(mask, camPos);
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            _writeTicks = 0; _instTicks = 0; _extTicks = 0;

            _program.Use();
            _program.Set("uViewProj", ToNumerics(viewProj));
            _program.Set("uClock", Shader.GetGlobalFloat(IdPrismClock) is var clk && clk > 0 ? clk : Time.time);
            _program.Set("uTime", Time.time);
            SetVec3("uCamPos", camPos);
            SetLighting();
            SetFog();
            _program.Set("uTex", 0);
            _program.Set("uExt", 1);
            var occTarget = Shader.GetGlobalVector(IdOccTarget);
            var occParams = Shader.GetGlobalVector(IdOccParams);
            _program.Set("uOccTarget", occTarget.x, occTarget.y, occTarget.z);
            _program.Set("uOccParams", occParams.x, occParams.y, occParams.z);
            _program.Set("uOccNear", Shader.GetGlobalFloat(IdOccNear));
            SetSightUniforms();
            SetGlobalVec4Array("uCradleCentre", IdCradleCentre, 4);
            SetGlobalVec4Array("uCradleWeight", IdCradleWeight, 4);
            var cp = Shader.GetGlobalVector(IdCradleParams);
            _program.Set("uCradleParams", cp.x, cp.y, cp.z, cp.w);
            foreach (var (u, id) in s_visionGlobals)
            {
                var v = Shader.GetGlobalVector(id);
                _program.Set(u, v.x, v.y, v.z, v.w);
            }

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
            if (s_timing)
            {
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                _gl.Finish();
                long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
                double ms(long a) => a * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (_frame % 30 == 0)
                    Console.WriteLine($"[render] collect {ms(t1 - t0):F1} ms (renderers {ms(t1 - t0 - _entityTicks):F1}, ECS hand-over {ms(_entityHandTicks):F1} of {_entities.Count}, entity cull {ms(_entityTicks - _entityHandTicks):F1}), instance writes+uploads {ms(_writeTicks):F1} ms (instances {ms(_instTicks):F1}, clock blocks {ms(_extTicks):F1}), submit {ms(t2 - t1 - _writeTicks):F1} ms, GPU wait {ms(t3 - t2):F1} ms — {Instances} instances, {DrawCalls} draws, {_opaque.Count + _transparent.Count} collected"
                        + $" [renderers: {_cLive} live, {_cShown} tested; walk {ms(_cLoop):F1}]");
            }
        }

        static readonly bool s_timing = Environment.GetEnvironmentVariable("COSMIC_SHORE_RENDER_TIMING") == "1";
        long _writeTicks, _instTicks, _extTicks;

        void Collect(int mask, EVector3 camPos)
        {
            _opaque.Clear();
            _transparent.Clear();
            // The cache re-derives an edited material on its own (Material.Revision); a periodic
            // purge only lets go of materials nothing draws with any more.
            if (_frame % 600 == 0) { _mats.Clear(); _ribbonMats.Clear(); _spheres.Clear(); }
            _cShown = 0;
            long tm0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (s_slowCollect) CollectAllRenderers(mask, camPos);
            else CollectSlotted(mask, camPos);
            if (s_census && _frame % 30 == 0) { Renderer.CollectLive(_renderers); RendererCensus(); }
            long te = System.Diagnostics.Stopwatch.GetTimestamp();
            _cLoop = te - tm0;
            if (s_verifyCull && _frame % 30 == 0) VerifySlotted(mask, camPos);
            CollectEntities(mask, camPos);
            _entityTicks = System.Diagnostics.Stopwatch.GetTimestamp() - te;
        }

        /// <summary>The per-frame walk of every live renderer (COSMIC_SHORE_SLOW_COLLECT=1, and the verify reference).</summary>
        void CollectAllRenderers(int mask, EVector3 camPos)
        {
            Renderer.CollectLive(_renderers);
            _cLive = _renderers.Count;
            foreach (var r in _renderers) EmitRenderer(r, mask, camPos, knownVisible: false);
        }

        /// <summary>
        /// One renderer's draw items: the full per-renderer test (enabled, layer, activity, mesh,
        /// frustum, materials). <paramref name="knownVisible"/>: the caller already proved its
        /// cached bounds are in the frustum, so the sphere test is skipped.
        /// </summary>
        void EmitRenderer(Renderer r, int mask, EVector3 camPos, bool knownVisible)
        {
            if (!r.enabled || r.forceRenderingOff) return;
            if (r is TrailRenderer || r is LineRenderer)
            {
                CollectRibbon(r, mask, camPos);
                return;
            }
            if (r is not MeshRenderer && r is not SkinnedMeshRenderer) return;
            var go = r.gameObject;
            if (go is null || (mask & (1 << go.layer)) == 0 || !go.activeInHierarchy) return;
            if (go.isPrefabAsset) return;
            _cShown++;
            Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0) return;
            if (r is SkinnedMeshRenderer morphing && mesh.blendShapeCount > 0) mesh = Morphed(morphing, mesh);
            bool visible = knownVisible || r is SkinnedMeshRenderer || InFrustum(mesh, r.transform.localToWorldMatrix);
            var mats = r.sharedMaterials;
            int subs = mesh.RenderSubmeshCount;
            for (int i = 0; i < mats.Length && i < Math.Max(subs, 1); i++)
            {
                var m = mats[i];
                if (m == null) continue;
                var st = StateFor(m);
                // Frustum culling (Unity culls every renderer against the camera first).
                // Vertex-animated prism families move geometry off their transform: never culled.
                if (!visible && st.PrismGraph == 0 && st.Family != 8) continue;
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

        // ── The slot table ──
        // Every live renderer holds a slot. A CULLABLE slot (a mesh renderer whose materials
        // all draw where its transform puts them) caches its world bounding sphere; each frame
        // only the slots whose sphere is in the frustum touch their renderer at all. Everything
        // else (skinned, ribbons, UI-parented, vertex-animated families) is an ALWAYS slot that
        // takes the full per-renderer test every frame. A slot is rebuilt only when the engine
        // reports its renderer changed (Renderer.TrackChanges: any transform write in its
        // ancestry, a reparent, a material or mesh assignment); a cached sphere also re-derives
        // when its mesh's vertex buffer is replaced. Enabled, layer and activity are not cached:
        // they are tested when a slot is visible, so they can never go stale. The whole table is
        // rebuilt from the live list every 600 frames, which is also what reclaims slots of
        // renderers destroyed while off screen. COSMIC_SHORE_VERIFY_CULL=1 compares the result
        // with the full walk every 30 frames.
        const byte SlotFree = 0, SlotIgnore = 1, SlotCull = 2, SlotAlways = 3;
        struct SlotBounds { public float X, Y, Z, R; }
        Renderer[] _sRenderer = new Renderer[1024];
        byte[] _sKind = new byte[1024];
        SlotBounds[] _sBounds = new SlotBounds[1024];
        Mesh[] _sMesh = new Mesh[1024];
        Vector3[][] _sVerts = new Vector3[1024][];
        int _slotCount;
        readonly Stack<int> _freeSlots = new();
        readonly List<Renderer> _dirtyRenderers = new();

        static readonly bool s_slowCollect = Environment.GetEnvironmentVariable("COSMIC_SHORE_SLOW_COLLECT") == "1";
        static readonly bool s_verifyCull = Environment.GetEnvironmentVariable("COSMIC_SHORE_VERIFY_CULL") == "1";

        void CollectSlotted(int mask, EVector3 camPos)
        {
            if (!Renderer.TrackChanges || _frame % 600 == 0) RebuildSlots();
            Renderer.DrainDirty(_dirtyRenderers);
            foreach (var r in _dirtyRenderers) UpdateSlot(r);
            _cLive = _slotCount - _freeSlots.Count;

            for (int i = 0; i < _slotCount; i++)
            {
                byte kind = _sKind[i];
                if (kind == SlotCull)
                {
                    if (!s_noCull)
                    {
                        if (!ReferenceEquals(_sMesh[i].RenderVertices, _sVerts[i])) RefreshBounds(i);
                        var b = _sBounds[i];
                        bool outside = false;
                        for (int p = 0; p < 6; p++)
                        {
                            var pl = _planes[p];
                            if (pl.X * b.X + pl.Y * b.Y + pl.Z * b.Z + pl.W < -b.R) { outside = true; break; }
                        }
                        if (outside) continue;
                    }
                    var r = _sRenderer[i];
                    if (r.IsDestroyed) { FreeSlot(i); continue; }
                    EmitRenderer(r, mask, camPos, knownVisible: true);
                }
                else if (kind == SlotAlways)
                {
                    var r = _sRenderer[i];
                    if (r.IsDestroyed) { FreeSlot(i); continue; }
                    EmitRenderer(r, mask, camPos, knownVisible: false);
                }
            }
        }

        void RebuildSlots()
        {
            Array.Clear(_sRenderer, 0, _slotCount);
            Array.Clear(_sMesh, 0, _slotCount);
            Array.Clear(_sVerts, 0, _slotCount);
            Array.Clear(_sKind, 0, _slotCount);
            _slotCount = 0;
            _freeSlots.Clear();
            Renderer.TrackChanges = true;
            Renderer.CollectLive(_renderers);
            foreach (var r in _renderers) UpdateSlot(r);
        }

        void FreeSlot(int i)
        {
            if (_sKind[i] == SlotFree) return;
            if (_sRenderer[i] is { } r && r.PortRenderSlot == i) r.PortRenderSlot = -1;
            _sRenderer[i] = null; _sMesh[i] = null; _sVerts[i] = null; _sKind[i] = SlotFree;
            _freeSlots.Push(i);
        }

        void UpdateSlot(Renderer r)
        {
            int i = r.PortRenderSlot;
            bool owned = i >= 0 && i < _slotCount && ReferenceEquals(_sRenderer[i], r);
            if (r.IsDestroyed || r.gameObject is null || r.gameObject.IsDestroyed)
            {
                if (owned) FreeSlot(i);
                return;
            }
            if (!owned)
            {
                if (_freeSlots.Count > 0) i = _freeSlots.Pop();
                else
                {
                    if (_slotCount == _sRenderer.Length)
                    {
                        int n = _sRenderer.Length * 2;
                        Array.Resize(ref _sRenderer, n); Array.Resize(ref _sKind, n); Array.Resize(ref _sBounds, n);
                        Array.Resize(ref _sMesh, n); Array.Resize(ref _sVerts, n);
                    }
                    i = _slotCount++;
                }
                _sRenderer[i] = r;
                r.PortRenderSlot = i;
            }
            _sKind[i] = ClassifySlot(r, out var mesh);
            _sMesh[i] = mesh;
            if (_sKind[i] == SlotCull) RefreshBounds(i);
        }

        byte ClassifySlot(Renderer r, out Mesh mesh)
        {
            mesh = null;
            if (r is TrailRenderer || r is LineRenderer || r is SkinnedMeshRenderer) return SlotAlways;
            if (r is not MeshRenderer) return SlotIgnore;
            mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) return SlotIgnore; // assigning a mesh marks the renderer dirty
            for (var t = r.transform; t is not null; t = t.parent)
                if (t is RectTransform) return SlotAlways; // anchor-derived poses report no writes
            foreach (var m in r.sharedMaterials)
                if (m != null) { var st = StateFor(m); if (st.PrismGraph != 0 || st.Family == 8) return SlotAlways; }
            return SlotCull;
        }

        void RefreshBounds(int i)
        {
            var mesh = _sMesh[i];
            var m = _sRenderer[i].transform.localToWorldMatrix;
            var sph = SphereOf(mesh);
            var c = m.MultiplyPoint3x4(sph.Centre);
            float sx = new EVector3(m.m00, m.m10, m.m20).magnitude, sy = new EVector3(m.m01, m.m11, m.m21).magnitude, sz = new EVector3(m.m02, m.m12, m.m22).magnitude;
            // Same margin as InFrustum: sway, jiggle, cradle and grow all stay near the rest shape.
            _sBounds[i] = new SlotBounds { X = c.x, Y = c.y, Z = c.z, R = sph.Radius * MathF.Max(sx, MathF.Max(sy, sz)) * 1.25f + 1f };
            _sVerts[i] = mesh.RenderVertices;
        }

        readonly List<Item> _verifyOpaque = new(), _verifyTransparent = new();

        /// <summary>COSMIC_SHORE_VERIFY_CULL: rerun the full walk and report any draw item one path has and the other lacks.</summary>
        void VerifySlotted(int mask, EVector3 camPos)
        {
            _verifyOpaque.Clear(); _verifyOpaque.AddRange(_opaque); _verifyOpaque.AddRange(_transparent);
            int ribbonO = _opaque.Count, ribbonT = _transparent.Count;
            _opaque.Clear(); _transparent.Clear();
            CollectAllRenderers(mask, camPos);
            var reference = new HashSet<(Renderer, int, Material)>();
            foreach (var it in _opaque) reference.Add((it.Renderer, it.Submesh, it.Material));
            foreach (var it in _transparent) reference.Add((it.Renderer, it.Submesh, it.Material));
            var fast = new HashSet<(Renderer, int, Material)>();
            foreach (var it in _verifyOpaque) fast.Add((it.Renderer, it.Submesh, it.Material));
            int missing = 0, extra = 0; string sample = null;
            foreach (var k in reference) if (!fast.Contains(k)) { missing++; sample ??= "missing " + k.Item1.name; }
            foreach (var k in fast) if (!reference.Contains(k)) { extra++; sample ??= "extra " + k.Item1.name; }
            Console.WriteLine($"[verify-cull] frame {_frame}: {fast.Count} slotted vs {reference.Count} walked, {missing} missing, {extra} extra{(sample != null ? " (" + sample + ")" : "")}");
            // Draw what the slotted path chose, so verification never changes the picture.
            _opaque.Clear(); _transparent.Clear();
            foreach (var it in _verifyOpaque) (it.State.Transparent ? _transparent : _opaque).Add(it);
        }

        long _entityTicks, _entityHandTicks;

        int _cLive, _cEnabled, _cShown;
        static readonly bool s_census = Environment.GetEnvironmentVariable("CS_PORT_TRACE_RENDERERS") != null;

        /// <summary>Diagnostics (CS_PORT_TRACE_RENDERERS): the enabled renderers by object name and material, largest groups first.</summary>
        void RendererCensus()
        {
            var groups = new Dictionary<string, int>();
            foreach (var r in _renderers)
            {
                if (!r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
                string n = r.gameObject.name;
                int cut = n.IndexOf(" (", StringComparison.Ordinal); if (cut > 0) n = n[..cut];
                var m = r.sharedMaterials; string key = $"{r.GetType().Name} {n} [{(m.Length > 0 && m[0] != null ? m[0].name : "-")}]";
                groups[key] = groups.TryGetValue(key, out int c) ? c + 1 : 1;
            }
            var top = new List<KeyValuePair<string, int>>(groups);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            Console.WriteLine("[renderers] " + string.Join(", ", top.GetRange(0, Math.Min(8, top.Count)).ConvertAll(kv => $"{kv.Key}={kv.Value}")));
        }
        long _cMesh, _cCull, _cMat, _cLoop;

        // ── Entities Graphics: every visible entity the ECS emulation hands over ──

        readonly EntityDrawList _entities = new();

        // ── Frustum culling ──

        readonly System.Numerics.Vector4[] _planes = new System.Numerics.Vector4[6];
        float _cullClock;
        static readonly int SlotFlightStart = EntityDrawList.Slot("_FlightStartTime"), SlotFlightDuration = EntityDrawList.Slot("_FlightDuration");
        static readonly int SlotSuctionStart = EntityDrawList.Slot("_SuctionStartTime"), SlotSuctionDuration = EntityDrawList.Slot("_SuctionDuration");
        sealed class MeshSphere { public Vector3[] Source; public EVector3 Centre; public float Radius; }
        // A plain reference-keyed dictionary: this is read for every drawn renderer every frame,
        // and a ConditionalWeakTable lookup costs several times as much. Cleared with the
        // material caches (below), so meshes nothing draws any more are let go.
        readonly Dictionary<Mesh, MeshSphere> _spheres = new(ReferenceEqualityComparer.Instance);

        /// <summary>The six clip planes of <paramref name="vp"/> (Gribb–Hartmann), normalised; inside = dot >= 0.</summary>
        void SetFrustum(CosmicShore.Engine.Matrix4x4 vp)
        {
            var r0 = new System.Numerics.Vector4(vp.m00, vp.m01, vp.m02, vp.m03);
            var r1 = new System.Numerics.Vector4(vp.m10, vp.m11, vp.m12, vp.m13);
            var r2 = new System.Numerics.Vector4(vp.m20, vp.m21, vp.m22, vp.m23);
            var r3 = new System.Numerics.Vector4(vp.m30, vp.m31, vp.m32, vp.m33);
            _planes[0] = r3 + r0; _planes[1] = r3 - r0; _planes[2] = r3 + r1;
            _planes[3] = r3 - r1; _planes[4] = r3 + r2; _planes[5] = r3 - r2;
            for (int i = 0; i < 6; i++)
            {
                var p = _planes[i];
                float len = MathF.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
                _planes[i] = len > 1e-12f ? p / len : p;
            }
        }

        /// <summary>A bounding sphere of the mesh's own vertices (object space), cached per vertex buffer.</summary>
        MeshSphere SphereOf(Mesh mesh)
        {
            var verts = mesh.RenderVertices;
            if (!_spheres.TryGetValue(mesh, out var s)) _spheres[mesh] = s = new MeshSphere();
            if (ReferenceEquals(s.Source, verts)) return s;
            s.Source = verts;
            if (verts.Length == 0) { s.Centre = default; s.Radius = 0f; return s; }
            var min = verts[0]; var max = verts[0];
            foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            var c = (min + max) * 0.5f;
            float r2 = 0f;
            foreach (var v in verts) r2 = MathF.Max(r2, (v - c).sqrMagnitude);
            s.Centre = new EVector3(c.x, c.y, c.z);
            s.Radius = MathF.Sqrt(r2);
            return s;
        }

        static readonly bool s_noCull = Environment.GetEnvironmentVariable("COSMIC_SHORE_NO_CULL") == "1";

        bool InFrustum(Mesh mesh, in CosmicShore.Engine.Matrix4x4 m)
        {
            if (s_noCull) return true;
            var sph = SphereOf(mesh);
            var c = m.MultiplyPoint3x4(sph.Centre);
            float sx = new EVector3(m.m00, m.m10, m.m20).magnitude, sy = new EVector3(m.m01, m.m11, m.m21).magnitude, sz = new EVector3(m.m02, m.m12, m.m22).magnitude;
            // Margin: sway, jiggle, cradle and grow all stay near the rest shape.
            float r = sph.Radius * MathF.Max(sx, MathF.Max(sy, sz)) * 1.25f + 1f;
            for (int i = 0; i < 6; i++)
            {
                var p = _planes[i];
                if (p.X * c.x + p.Y * c.y + p.Z * c.z + p.W < -r) return false;
            }
            return true;
        }

        bool EntityVisible(int i, Mesh mesh, in MatState st)
        {
            if (st.PrismGraph == 2 || st.Family == 8) return true;     // debris / slice halves fly off their transform
            if (_entities.TryGet(i, SlotFlightDuration, out var fd) && fd.x > 0f
                && _entities.TryGet(i, SlotFlightStart, out var fs) && _cullClock < fs.x + fd.x) return true;
            if (_entities.TryGet(i, SlotSuctionDuration, out var sd) && sd.x > 0f
                && _entities.TryGet(i, SlotSuctionStart, out var ss) && _cullClock >= ss.x) return true;
            return InFrustum(mesh, _entities.Matrices[i]);
        }

        void CollectEntities(int mask, EVector3 camPos)
        {
            _entities.Clear();
            long th = System.Diagnostics.Stopwatch.GetTimestamp();
            EntityDraws.Collect?.Invoke(_entities);
            _entityHandTicks = System.Diagnostics.Stopwatch.GetTimestamp() - th;
            for (int i = 0; i < _entities.Count; i++)
            {
                if ((mask & (1 << _entities.Layers[i])) == 0) continue;
                var mesh = _entities.Meshes[i];
                var m = _entities.Materials[i];
                if (mesh == null || mesh.vertexCount == 0 || m == null) continue;
                var st = StateFor(m);
                if (!EntityVisible(i, mesh, st)) continue;
                int subs = mesh.RenderSubmeshCount;
                var item = new Item { Mesh = mesh, Submesh = Math.Clamp(_entities.Submeshes[i], 0, Math.Max(subs - 1, 0)), Material = m, State = st, Entity = i + 1 };
                if (st.Transparent)
                {
                    var mm = _entities.Matrices[i];
                    item.Distance = (new EVector3(mm.m03, mm.m13, mm.m23) - camPos).sqrMagnitude;
                    _transparent.Add(item);
                }
                else _opaque.Add(item);
            }
        }

        /// <summary>The cached state for a material, re-derived whenever the material was edited at runtime.</summary>
        MatState StateFor(Material m)
        {
            if (!_mats.TryGetValue(m, out var st) || st.Revision != m.Revision) _mats[m] = st = Classify(m);
            return st;
        }

        static MatState Classify(Material m)
        {
            var st = new MatState { Revision = m.Revision, FresPow = 4f, TexST = new Vector4(1, 1, 0, 0), Cull = 2, Queue = m.renderQueue, Alpha = 1f };
            string graph = m.shader?.name ?? "";
            if (graph == "Shader Graphs/SnowGraph")
            {
                st.Family = 3; st.DarkId = st.BrightId = IdColor;
                st.Alpha = m.GetFloat("_Opacity");
                var v = m.HasStoredProperty("_Vector3") ? m.GetVector("_Vector3") : new Vector4(0, 0, 0.65f, 0);
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
            else if (graph == "CosmicShore/PrismSlice")
            {
                st.Family = 8; st.DarkId = IdDark; st.BrightId = IdBright;
                var mt = m.GetVector("_SliceMotionTimes");
                var dw = m.GetVector("_SliceDissolveWindow");
                st.SliceP0 = new Vector4(mt.x, mt.y, mt.z, 0f);
                st.SliceP1 = new Vector4(dw.x, dw.y, m.GetFloat("_FresnelPower"), m.GetFloat("_CutGlow"));
                st.SliceP2 = new Vector4(m.GetFloat("_CutShade"), m.GetFloat("_SeamCool"), m.GetFloat("_RimWidth"), m.GetFloat("_DissolveNoiseScale"));
                st.SliceP3 = new Vector4(m.GetFloat("_DissolveNoise"), m.GetFloat("_EmberBand"), m.GetFloat("_EmberGlow"), m.GetFloat("_EmberWhite"));
                st.SliceHot = m.GetColor("_CutHotColor");
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
                st.VesselMultiplier = m.HasStoredProperty(IdColorMul) ? m.GetFloat(IdColorMul) : 1f;
                st.VesselVision = true;
            }
            else if (m.HasStoredProperty(IdDark) && m.HasStoredProperty(IdBright))
            {
                st.Family = 2; st.DarkId = IdDark; st.BrightId = IdBright;
                if (m.HasStoredProperty(IdFresPow)) st.FresPow = m.GetFloat(IdFresPow);
                if (m.HasStoredProperty(IdSqrDistance)) st.MaxSqrDist = m.GetFloat(IdSqrDistance);
            }
            else if (m.HasStoredProperty(IdDull) && m.HasStoredProperty(IdBrightCrystal)) { st.Family = 2; st.DarkId = IdDull; st.BrightId = IdBrightCrystal; }
            else if (m.HasStoredProperty(IdDullColor) && m.HasStoredProperty(IdBright)) { st.Family = 2; st.DarkId = IdDullColor; st.BrightId = IdBright; }
            else if (m.HasStoredProperty(IdColor1) && m.HasStoredProperty(IdColor2)) { st.Family = 2; st.DarkId = IdColor1; st.BrightId = IdColor2; st.FresPow = 2f; }
            else
            {
                string sh = m.shader?.name ?? "";
                st.Family = sh.Contains("Lit") && !sh.Contains("Unlit") || sh == "Standard" || sh.StartsWith("Legacy Shaders/Diffuse") ? 1 : 0;
                st.DarkId = m.HasStoredProperty(IdBaseColor) ? IdBaseColor : m.HasStoredProperty(IdColor) ? IdColor : 0;
                st.BrightId = st.DarkId;
            }

            st.Dark = st.DarkId != 0 ? m.GetColor(st.DarkId) : Color.white;
            st.Bright = st.BrightId != 0 ? m.GetColor(st.BrightId) : Color.white;
            if (st.VesselMultiplier > 0f) { st.Dark = Mul(st.Dark, st.VesselMultiplier); st.Bright = st.Dark; }

            st.Tex = m.HasStoredProperty(IdBaseMap) ? m.GetTexture(IdBaseMap) : m.GetTexture(IdMainTex);
            if (st.Tex != null)
            {
                var stv = m.GetTextureScaleOffset(m.HasStoredProperty(IdBaseMap) ? "_BaseMap" : "_MainTex");
                st.TexST = stv;
            }
            if (m.HasStoredProperty(IdEmission) && (m.IsKeywordEnabled("_EMISSION") || st.Family == 0))
            {
                var e = m.GetColor(IdEmission);
                st.Emission = new EVector3(e.r, e.g, e.b);
            }

            bool surfaceTransparent = m.HasStoredProperty(IdSurface) && m.GetFloat(IdSurface) >= 0.5f;
            st.Transparent = surfaceTransparent || m.renderQueue >= 2501;
            st.Src = st.Transparent ? BlendingFactor.SrcAlpha : BlendingFactor.One;
            st.Dst = st.Transparent ? BlendingFactor.OneMinusSrcAlpha : BlendingFactor.Zero;
            if (st.Transparent && m.HasStoredProperty(IdSrc) && m.HasStoredProperty(IdDst))
            {
                st.Src = Blend((int)m.GetFloat(IdSrc));
                st.Dst = Blend((int)m.GetFloat(IdDst));
            }
            st.ZWrite = m.HasStoredProperty(IdZWrite) ? m.GetFloat(IdZWrite) >= 0.5f : !st.Transparent;
            if (m.HasStoredProperty(IdCull)) st.Cull = (int)m.GetFloat(IdCull);
            st.Cutoff = m.HasStoredProperty(IdAlphaClip) && m.GetFloat(IdAlphaClip) >= 0.5f && m.HasStoredProperty(IdCutoff) ? m.GetFloat(IdCutoff) : -1f;
            if (!st.Transparent && st.Cutoff < 0f) { st.Dark.a = 1f; st.Bright.a = 1f; }
            if (st.Family == 8)
            {
                // Hand-written: Queue AlphaTest, Cull Off, ZWrite On; it clips, it does not blend.
                st.Transparent = false; st.Src = BlendingFactor.One; st.Dst = BlendingFactor.Zero;
                st.ZWrite = true; st.Cull = 0; st.Cutoff = -1f;
            }
            if (st.Family == 7)
            {
                st.Transparent = true; st.Src = BlendingFactor.One; st.Dst = BlendingFactor.One;
                st.ZWrite = false; st.Cull = 0; st.Cutoff = -1f;
            }
            st.PrismGraph = graph == "Shader Graphs/BlockGraph" ? 1 : graph == "Shader Graphs/ExplodingBlockGraph" ? 2 : 0;
            if (st.PrismGraph != 0)
            {
                st.ExplosiveRotation = m.GetFloat("_ExplosiveRotation");
                st.ExplosiveSpread = m.GetFloat("_ExplosiveSpead"); // [sic] the graph's reference name
            }
            st.Ext = new float[ExtFloats];
            st.Layout = st.Family == 8 ? SliceLayout : ExtLayout;
            foreach (var p in st.Layout)
            {
                var v = p.Count == 1 ? new Vector4(m.GetFloat(p.Id), 0, 0, 0) : p.IsColor ? ToV4(m.GetColor(p.Id)) : m.GetVector(p.Id);
                for (int k = 0; k < p.Count; k++) st.Ext[p.Offset + k] = Comp(v, k);
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
            long tw = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_instanceData.Length < n * InstanceFloats) _instanceData = new float[Math.Max(n, _instanceData.Length / InstanceFloats * 2) * InstanceFloats];
            for (int i = 0; i < n; i++)
                WriteInstance(items[i], i * InstanceFloats);
            long tx = System.Diagnostics.Stopwatch.GetTimestamp();
            _instTicks += tx - tw;
            if (_extData.Length < n * ExtFloats) _extData = new float[Math.Max(n, _extData.Length / ExtFloats * 2) * ExtFloats];
            for (int i = 0; i < n; i++)
                WriteExt(items[i], i * ExtFloats);
            _extTicks += System.Diagnostics.Stopwatch.GetTimestamp() - tx;
            UploadExt(n);

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
            _writeTicks += System.Diagnostics.Stopwatch.GetTimestamp() - tw;

            if (st.Family == 7 && first.Renderer != null) SetCrackleUniforms(first);
            if (st.Family == 8)
            {
                _program.Set("uSliceP0", st.SliceP0.x, st.SliceP0.y, st.SliceP0.z, st.SliceP0.w);
                _program.Set("uSliceP1", st.SliceP1.x, st.SliceP1.y, st.SliceP1.z, st.SliceP1.w);
                _program.Set("uSliceP2", st.SliceP2.x, st.SliceP2.y, st.SliceP2.z, st.SliceP2.w);
                _program.Set("uSliceP3", st.SliceP3.x, st.SliceP3.y, st.SliceP3.z, st.SliceP3.w);
                _program.Set("uSliceHot", st.SliceHot.r, st.SliceHot.g, st.SliceHot.b, st.SliceHot.a);
            }
            SetSkinUniforms(first, entry);
            _program.Set("uFamily", st.Family);
            _program.Set("uPrismGraph", st.PrismGraph);
            _program.Set("uVesselVision", st.VesselVision ? 1 : 0);
            _program.Set("uExplosive", st.ExplosiveRotation, st.ExplosiveSpread);
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
            float F(int id) => b != null && b.HasFloat(id) ? b.GetFloat(id) : m.HasStoredProperty(id) ? m.GetFloat(id) : 0f;
            Color C(int id) => b != null && b.HasColor(id) ? b.GetColor(id) : m.HasStoredProperty(id) ? m.GetColor(id) : Color.black;
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
        /// <summary>Unity's Default-Line: what a Line/TrailRenderer with no material draws with.</summary>
        static Material DefaultLineMaterial => BuiltinMaterials.DefaultLine;

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
            if (!_ribbonMats.TryGetValue(m, out var st) || st.Revision != m.Revision)
            {
                st = Classify(m);
                // A line/trail is a translucent strip whatever its material queue says; cull nothing.
                if (!st.Transparent) { st.Transparent = true; st.Src = BlendingFactor.SrcAlpha; st.Dst = BlendingFactor.OneMinusSrcAlpha; st.ZWrite = false; }
                st.Cull = 0;
                _ribbonMats[m] = st;
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
            int ent = it.Entity - 1;
            var m = ent >= 0 ? _entities.Matrices[ent]
                  : it.WorldSpace ? CosmicShore.Engine.Matrix4x4.identity : it.Renderer.transform.localToWorldMatrix;
            // Column-major (GL): column c = (m0c, m1c, m2c, m3c).
            d[o + 0] = m.m00; d[o + 1] = m.m10; d[o + 2] = m.m20; d[o + 3] = m.m30;
            d[o + 4] = m.m01; d[o + 5] = m.m11; d[o + 6] = m.m21; d[o + 7] = m.m31;
            d[o + 8] = m.m02; d[o + 9] = m.m12; d[o + 10] = m.m22; d[o + 11] = m.m32;
            d[o + 12] = m.m03; d[o + 13] = m.m13; d[o + 14] = m.m23; d[o + 15] = m.m33;

            var st = it.State;
            Color dark = st.Dark, bright = st.Bright;
            float growStart = 0f, growRate = 0f;
            Vector4 frac = new(1, 1, 1, 0);
            if (ent >= 0)
            {
                // Entities Graphics: [MaterialProperty] overrides, uploaded verbatim.
                if (st.DarkId != 0 && _entities.TryGet(ent, SlotDark, out var dv)) dark = new Color(dv.x, dv.y, dv.z, dv.w);
                if (st.BrightId != 0 && _entities.TryGet(ent, SlotBright, out var bv)) bright = new Color(bv.x, bv.y, bv.z, bv.w);
                if (_entities.TryGet(ent, SlotGrowRate, out var gr)) growRate = gr.x;
                if (_entities.TryGet(ent, SlotGrowStart, out var gs)) growStart = gs.x;
                if (_entities.TryGet(ent, SlotGrowFrac, out var gf)) frac = gf;
            }
            else if (it.Renderer.HasPropertyBlock())
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
            if (st.VesselVision)
            {
                // _VesselVisionTint (a per-material-index property block, VesselVisionShading.Stamp);
                // alpha 0 = nobody stamped this object, so the law leaves it alone.
                var tb = ent < 0 && it.Renderer.HasPropertyBlock() ? it.Renderer.PropertyBlockFor(it.Submesh) : null;
                bright = tb != null && tb.HasColor(IdVisionTint) ? tb.GetColor(IdVisionTint) : new Color(0, 0, 0, 0);
            }
            d[o + 16] = dark.r; d[o + 17] = dark.g; d[o + 18] = dark.b; d[o + 19] = dark.a;
            d[o + 20] = bright.r; d[o + 21] = bright.g; d[o + 22] = bright.b; d[o + 23] = bright.a;
            d[o + 24] = growStart; d[o + 25] = growRate; d[o + 26] = 0f; d[o + 27] = 0f;
            d[o + 28] = frac.x; d[o + 29] = frac.y; d[o + 30] = frac.z; d[o + 31] = 0f;
        }

        /// <summary>The instance's extended clock block: the material's values, patched by the entity's overrides.</summary>
        void WriteExt(in Item it, int o)
        {
            var e = _extData;
            var defaults = it.State.Ext;
            Array.Copy(defaults, 0, e, o, ExtFloats);
            int ent = it.Entity - 1;
            if (ent < 0 || _entities.Mask[ent] == 0) return;
            foreach (var p in it.State.Layout ?? ExtLayout)
                if (_entities.TryGet(ent, p.Slot, out var v))
                    for (int k = 0; k < p.Count; k++) e[o + p.Offset + k] = Comp(v, k);
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
            var tans = mesh.RenderTangents;
            var uv1 = mesh.RenderUv1Wide;
            int subs = mesh.RenderSubmeshCount;
            bool dirty = e.Vao == 0 || !ReferenceEquals(e.VertsRef, verts) || !ReferenceEquals(e.NormRef, norms) || !ReferenceEquals(e.TanRef, tans) || !ReferenceEquals(e.Uv1Ref, uv1)
                || !ReferenceEquals(e.UvRef, uvs) || !ReferenceEquals(e.ColRef, cols) || e.SubRefs.Length != subs
                || !ReferenceEquals(e.SkinRef, mesh.RenderBoneWeights);
            if (!dirty)
                for (int i = 0; i < subs; i++)
                    if (!ReferenceEquals(e.SubRefs[i], mesh.RenderSubmesh(i))) { dirty = true; break; }
            if (!dirty) return e;

            e.VertsRef = verts; e.NormRef = norms; e.UvRef = uvs; e.ColRef = cols; e.TanRef = tans; e.Uv1Ref = uv1;
            e.SubRefs = new object[subs];
            int n = verts.Length;
            if (n == 0) return null;
            bool hasN = norms.Length == n, hasUv = uvs.Length == n, hasC = cols.Length == n, hasT = tans.Length == n;
            e.HasColors = hasC;
            bool hasU1 = uv1 != null && uv1.Length == n;
            const int VF = 20;
            var data = new float[n * VF];
            for (int i = 0; i < n; i++)
            {
                int o = i * VF;
                if (hasT) { data[o + 12] = tans[i].x; data[o + 13] = tans[i].y; data[o + 14] = tans[i].z; data[o + 15] = tans[i].w; }
                else { data[o + 12] = 1f; data[o + 15] = 1f; }
                if (hasU1) { data[o + 16] = uv1[i].x; data[o + 17] = uv1[i].y; data[o + 18] = uv1[i].z; data[o + 19] = uv1[i].w; }
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
            uint stride = VF * sizeof(float);
            _gl.EnableVertexAttribArray(0); _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.EnableVertexAttribArray(1); _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(2); _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
            _gl.EnableVertexAttribArray(3); _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, (void*)(8 * sizeof(float)));
            _gl.EnableVertexAttribArray(14); _gl.VertexAttribPointer(14, 4, VertexAttribPointerType.Float, false, stride, (void*)(12 * sizeof(float)));
            _gl.EnableVertexAttribArray(15); _gl.VertexAttribPointer(15, 4, VertexAttribPointerType.Float, false, stride, (void*)(16 * sizeof(float)));
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

        readonly List<Light> _lights = new();

        void SetLighting()
        {
            // The strongest enabled directional light is the sun; otherwise a soft key light.
            Light sun = RenderSettings.sun;
            if (sun == null || !sun.isActiveAndEnabled)
            {
                sun = null;
                LiveComponents<Light>.CollectActive(_lights);
                foreach (var l in _lights)
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
