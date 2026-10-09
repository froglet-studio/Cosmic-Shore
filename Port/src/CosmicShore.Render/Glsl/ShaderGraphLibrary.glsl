// ShaderGraphLibrary.glsl - the project's Shader Graph Custom Function HLSL, ported to GLSL once.
//
// Every Custom Function node with Source = File calls <Name>_float(inputs..., out outputs...) in
// the order of the node's slots; the compiler (CosmicShore.Content/Shaders/ShaderGraphCompiler.cs)
// emits that call and the renderer prepends this file to every compiled graph, in both stages.
// Each block names the HLSL file it was ported from and that file's guid. When an HLSL file
// changes, port the change here: ShaderGraphCoverageTests fails when a graph calls a function
// this file does not define.
//
// Symbols from the renderer's template: sg_ObjectToWorld, sg_WorldToObject, sg_CamPos, sg_View,
// sg_Time, sg_ScreenParams; SG_FRAGMENT is defined in the fragment stage only. A global the HLSL
// reads (Shader.SetGlobal*) is a uniform here, declared unless the graph's own blackboard
// already declares it (the compiler defines SG_HAS<name> for each property it declares).
//
// ASCII only (the driver is handed a character count).

#define sg_saturate(x) clamp((x), 0.0, 1.0)

// ---------------------------------------------------------------------------------------------
// Assets/Unity Assests/TextMesh Pro/Shaders/SDFFunctions.hlsl (96de908384869cd409c75efa351d5edf)
// The TMP SDF graphs' face/outline/bevel math.
// ---------------------------------------------------------------------------------------------
#ifndef SG_HAS_Reflectivity
uniform float _Reflectivity;
#endif
#ifndef SG_HAS_SpecularColor
uniform vec4 _SpecularColor;
#endif
#ifndef SG_HAS_SpecularPower
uniform float _SpecularPower;
#endif
#ifndef SG_HAS_BevelType
uniform float _BevelType;
#endif
#ifndef SG_HAS_BevelOffset
uniform float _BevelOffset;
#endif
#ifndef SG_HAS_BevelWidth
uniform float _BevelWidth;
#endif
#ifndef SG_HAS_BevelRoundness
uniform float _BevelRoundness;
#endif
#ifndef SG_HAS_BevelClamp
uniform float _BevelClamp;
#endif
#ifndef SG_HAS_BevelAmount
uniform float _BevelAmount;
#endif
#ifndef SG_HAS_GradientScale
uniform float _GradientScale;
#endif
#ifndef SG_HAS_LightAngle
uniform float _LightAngle;
#endif
#ifndef SG_HAS_Diffuse
uniform float _Diffuse;
#endif
#ifndef SG_HAS_Ambient
uniform float _Ambient;
#endif

vec4 sgTmpBlendARGB(vec4 overlying, vec4 underlying)
{
  overlying.rgb *= overlying.a;
  underlying.rgb *= underlying.a;
  vec3 blended = overlying.rgb + ((1.0 - overlying.a) * underlying.rgb);
  float alpha = underlying.a + (1.0 - underlying.a) * overlying.a;
  return vec4(blended / alpha, alpha);
}

vec3 sgTmpSpecular(vec3 n, vec3 l)
{
  float spec = pow(max(0.0, dot(n, l)), _Reflectivity);
  return _SpecularColor.rgb * spec * _SpecularPower;
}

void GetSurfaceNormal_float(sampler2D atlas, float textureWidth, float textureHeight, vec2 uv, bool isFront, out vec3 nornmal)
{
  vec3 delta = vec3(1.0 / textureWidth, 1.0 / textureHeight, 0.0);
  vec4 h = vec4(
    textureLod(atlas, uv - delta.xz, 0.0).a,
    textureLod(atlas, uv + delta.xz, 0.0).a,
    textureLod(atlas, uv - delta.zy, 0.0).a,
    textureLod(atlas, uv + delta.zy, 0.0).a);
  bool raisedBevel = _BevelType > 0.5;
  h += _BevelOffset;
  float bevelWidth = max(0.01, _BevelWidth);
  h -= 0.5;
  h /= bevelWidth;
  h = sg_saturate(h + 0.5);
  if (raisedBevel) h = 1.0 - abs(h * 2.0 - 1.0);
  h = mix(h, sin(h * 3.141592 / 2.0), vec4(_BevelRoundness));
  h = min(h, 1.0 - vec4(_BevelClamp));
  h *= _BevelAmount * bevelWidth * _GradientScale * -2.0;
  vec3 va = normalize(vec3(-1.0, 0.0, h.y - h.x));
  vec3 vb = normalize(vec3(0.0, 1.0, h.w - h.z));
  vec3 f = vec3(1.0, 1.0, 1.0);
  if (isFront) f = vec3(1.0, 1.0, -1.0);
  nornmal = cross(va, vb) * f;
}

void EvaluateLight_float(vec4 faceColor, vec3 n, out vec4 color)
{
  n.z = abs(n.z);
  vec3 light = normalize(vec3(sin(_LightAngle), cos(_LightAngle), 1.0));
  vec3 col = max(faceColor.rgb, vec3(0.0)) + sgTmpSpecular(n, light) * faceColor.a;
  col *= 1.0 - (dot(n, light) * _Diffuse);
  col *= mix(_Ambient, 1.0, n.z * n.z);
  color = vec4(col, faceColor.a);
}

void GenerateUV_float(vec2 inUV, vec4 transform, vec2 animSpeed, out vec2 outUV)
{
  outUV = inUV * transform.xy + transform.zw + (animSpeed * sg_Time.x);
}

void ScreenSpaceRatio_float(vec2 UV, float TextureSize, bool Filter, out float SSR)
{
#ifdef SG_FRAGMENT
  if (Filter)
  {
    vec2 a = vec2(dFdx(UV.x), dFdy(UV.x));
    vec2 b = vec2(dFdx(UV.y), dFdy(UV.y));
    float s = mix(dot(a, a), dot(b, b), 0.5);
    SSR = inversesqrt(s) / TextureSize;
  }
  else
  {
    float s = inversesqrt(abs(dFdx(UV.x) * dFdy(UV.y) - dFdy(UV.x) * dFdx(UV.y)));
    SSR = s / TextureSize;
  }
#else
  SSR = 1.0; // derivatives exist in the fragment stage only
#endif
}

void ComputeSDF_float(float SSR, float SD, float SDR, float isoPerimeter, float softness, out float outAlpha)
{
  softness *= SSR * SDR;
  float d = (SD - 0.5) * SDR;
  outAlpha = sg_saturate((d * 2.0 * SSR + 0.5 + isoPerimeter * SDR * SSR + softness * 0.5) / (1.0 + softness));
}

void ComputeSDF44_float(float SSR, vec4 SD, float SDR, vec4 isoPerimeter, vec4 softness, bool outline, out vec4 outAlpha)
{
  softness *= SSR * SDR;
  vec4 d = (SD - 0.5) * SDR;
  if (outline) d.w = max(max(d.x, d.y), d.z);
  outAlpha = sg_saturate((d * 2.0 * SSR + 0.5 + isoPerimeter * SDR * SSR + softness * 0.5) / (1.0 + softness));
}

void Composite_float(vec4 overlying, vec4 underlying, out vec4 outColor)
{
  outColor = sgTmpBlendARGB(overlying, underlying);
}

void Layer1_float(float alpha, vec4 color0, out vec4 outColor)
{
  color0.a *= alpha;
  outColor = color0;
}

void Layer4_float(vec4 alpha, vec4 color0, vec4 color1, vec4 color2, vec4 color3, out vec4 outColor)
{
  color3.a *= alpha.w;
  color0.rgb *= color0.a; color1.rgb *= color1.a; color2.rgb *= color2.a; color3.rgb *= color3.a;
  outColor = mix(mix(mix(color3, color2, alpha.z), color1, alpha.y), color0, alpha.x);
  outColor.rgb /= outColor.a;
}

// ---------------------------------------------------------------------------------------------
// Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl (e3f9a1c27b8d4e05b6a4c9d1f0527a83)
// The prism graphs' clock-driven animation: every animation is a pure function of _PrismClock.
// ---------------------------------------------------------------------------------------------
void PrismGrowScale_float(float Clock, float StartTime, float Rate, vec3 StartFrac, out vec3 Scale)
{
  if (Rate <= 0.0) { Scale = vec3(1.0); return; }
  float t = max(Clock - StartTime, 0.0);
  Scale = max(vec3(1.0) - (vec3(1.0) - StartFrac) * exp(-Rate * t), vec3(0.0));
}

void PrismColorLerp_float(float Clock, float StartTime, float Duration,
    vec4 StartBright, vec4 StartDark, vec3 StartSpread,
    vec4 TargetBright, vec4 TargetDark, vec3 TargetSpread,
    out vec4 Bright, out vec4 Dark, out vec3 Spread)
{
  if (Duration <= 0.0) { Bright = TargetBright; Dark = TargetDark; Spread = TargetSpread; return; }
  float p = sg_saturate((Clock - StartTime) / Duration);
  float t = smoothstep(0.0, 1.0, p);
  Bright = mix(StartBright, TargetBright, t);
  Dark = mix(StartDark, TargetDark, t);
  Spread = mix(StartSpread, TargetSpread, t);
}

void PrismExplosionClock_float(float Clock, float StartTime, float Speed, float Duration,
    vec3 Velocity, float LegacyAmount, float LegacyOpacity,
    out float Amount, out float Opacity, out vec3 ObjectOffset)
{
  if (Duration <= 0.0) { Amount = LegacyAmount; Opacity = LegacyOpacity; ObjectOffset = vec3(0.0); return; }
  float t = max(Clock - StartTime, 0.0);
  Amount = Speed * t;
  Opacity = sg_saturate(1.0 - t / Duration);
  ObjectOffset = mat3(sg_WorldToObject) * (Velocity * t);
}

void PrismSuctionClock_float(float Clock, float StartTime, float Duration, float Direction,
    float GrowDelay, float LegacyState, out float State)
{
  if (Duration <= 0.0) { State = LegacyState; return; }
  float t = max(Clock - StartTime - GrowDelay, 0.0);
  float p = sg_saturate(t / Duration);
  State = Direction < 0.0 ? 1.0 - p : p;
}

void PrismDeathClock_float(float Clock, float StartTime, float Duration, float Direction,
    float LegacyState, out float State)
{
  if (Duration <= 0.0) { State = LegacyState; return; }
  float t = max(Clock - StartTime, 0.0);
  float p = sg_saturate(t / Duration);
  State = Direction < 0.0 ? 1.0 - p : p;
}

void PrismSuctionConverge_float(float State, vec3 WorldLocation, vec3 Position, out vec3 OutPosition)
{
  vec3 objectLocation = (sg_WorldToObject * vec4(WorldLocation, 1.0)).xyz;
  if (!(dot(objectLocation, objectLocation) < 1e12)) objectLocation = Position;
  OutPosition = mix(Position, objectLocation, sg_saturate(State));
}

void PrismFlightClock_float(float Clock, float StartTime, float Duration, vec3 Velocity, out vec3 ObjectOffset)
{
  if (Duration <= 0.0) { ObjectOffset = vec3(0.0); return; }
  float t = clamp(Clock - StartTime, 0.0, Duration);
  float covered = sin(t * 1.5707963 / Duration);
  vec3 worldOffset = Velocity * (0.63661977 * Duration) * (covered - 1.0);
  ObjectOffset = mat3(sg_WorldToObject) * worldOffset;
  if (!(dot(ObjectOffset, ObjectOffset) < 1e12)) ObjectOffset = vec3(0.0);
}

float sgPrismJiggleHash13(vec3 p)
{
  p = fract(p * vec3(0.1031, 0.1030, 0.0973));
  p += dot(p, p.yzx + 33.33);
  return fract((p.x + p.y) * p.z);
}

void sgPrismJiggleBasis(vec3 n, out vec3 t, out vec3 b)
{
  float s = n.z >= 0.0 ? 1.0 : -1.0;
  float a = -1.0 / (s + n.z);
  float c = n.x * n.y * a;
  t = vec3(1.0 + s * n.x * n.x * a, s * c, -s * n.x);
  b = vec3(c, s + n.y * n.y * a, -n.y);
}

vec3 sgPrismJiggleRotate(vec3 v, vec3 axis, float angle)
{
  float s = sin(angle), c = cos(angle);
  return v * c + cross(axis, v) * s + axis * (dot(axis, v) * (1.0 - c));
}

void PrismJiggleClock_float(float Clock, float StartTime, float Duration, vec3 Params,
    vec3 Position, vec3 Normal, out vec3 OutPosition, out vec3 OutNormal)
{
  OutPosition = Position;
  OutNormal = Normal;
  if (Duration <= 0.0) return;
  float t = Clock - StartTime;
  if (t <= 0.0 || t >= Duration) return;
  float nLenSq = dot(Normal, Normal);
  if (!(nLenSq > 1e-8)) return;
  vec3 n = Normal * inversesqrt(nLenSq);
  mat3 m = mat3(sg_ObjectToWorld);
  vec3 scale = vec3(length(m[0]), length(m[1]), length(m[2]));
  vec3 origin = sg_ObjectToWorld[3].xyz;
  if (!all(greaterThan(scale, vec3(1e-5)))) return;
  float u = t / Duration;
  float env = (1.0 - u) * exp(-2.5 * u);
  float seedA = sgPrismJiggleHash13(n * 17.0 + origin * 0.013 + StartTime);
  float seedB = sgPrismJiggleHash13(n * 29.0 - origin * 0.017 + StartTime * 1.7 + 11.0);
  vec3 tangent, bitangent;
  sgPrismJiggleBasis(n, tangent, bitangent);
  float phi = Params.y * t + seedA * 6.2831853;
  float theta = 1.5707963 * (0.5 - 0.5 * cos(Params.z * t + seedB * 6.2831853));
  vec3 axis = n * cos(theta) + (tangent * cos(phi) + bitangent * sin(phi)) * sin(theta);
  float angle = Params.x * env;
  OutPosition = sgPrismJiggleRotate(Position * scale, axis, angle) / scale;
  OutNormal = sgPrismJiggleRotate(Normal / scale, axis, angle) * scale;
}

void PrismFlightSqrDistance_float(float Clock, float StartTime, float Duration, vec3 Velocity,
    vec3 ObjectPosition, vec3 CameraPosition, out float SqrDistance)
{
  vec3 pos = ObjectPosition;
  if (Duration > 0.0)
  {
    float t = clamp(Clock - StartTime, 0.0, Duration);
    float covered = sin(t * 1.5707963 / Duration);
    pos += Velocity * (0.63661977 * Duration) * (covered - 1.0);
  }
  vec3 d = pos - CameraPosition;
  SqrDistance = dot(d, d);
}

void PrismShieldMorph_float(float Clock, float StartTime, float Duration, float Direction,
    float ShatterOffset, vec3 Position, vec3 Normal, vec3 FaceCentroid, out vec3 MorphedPosition)
{
  if (Duration <= 0.0) { MorphedPosition = Position; return; }
  float p = sg_saturate((Clock - StartTime) / Duration);
  float t = smoothstep(0.0, 1.0, p);
  float shatter = Direction < 0.0 ? 1.0 : 0.0;
  float faceScale = mix(t, 1.0 - t, shatter);
  float offset = shatter * t * ShatterOffset;
  MorphedPosition = FaceCentroid + faceScale * (Position - FaceCentroid) + offset * Normal;
}

// ---------------------------------------------------------------------------------------------
// Assets/_Graphics/Materials/Graphs/SpindleSway.hlsl (efddcf7a43d650551a2ceb7b095df22c)
// Assets/_Graphics/Materials/Graphs/PrismSway.hlsl (8d51a0c3f2e74b1e9a6c07d4b3f81520)
// ---------------------------------------------------------------------------------------------
void SpindleSway_float(vec3 PositionOS, float Clock, float Phase, float Amplitude, float Frequency, out vec3 Out)
{
  float t = Clock * Frequency + Phase;
  float span = PositionOS.z * Amplitude;
  vec3 p = PositionOS;
  p.x += span * sin(t);
  p.y += span * sin(t * 0.73 + Phase + 1.5707963) * 0.45;
  Out = p;
}

void PrismSway_float(vec3 PositionOS, float Clock, vec3 SpanX, vec3 SpanY, vec3 Axis, vec3 Timing, out vec3 Out)
{
  float freq = Timing.x;
  float phase = Timing.y;
  float t = Clock * freq + phase;
  float zl = Timing.z + dot(PositionOS, Axis);
  Out = PositionOS + SpanX * zl * sin(t) + SpanY * zl * sin(t * 0.73 + phase + 1.5707963) * 0.45;
}

// ---------------------------------------------------------------------------------------------
// Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl (5b3d90c14f7a4e6ea0d5c2183be97f41)
// ---------------------------------------------------------------------------------------------
float sgCrystalMorphEase(vec4 Target, float Clock, vec3 Morph)
{
  float t = sg_saturate((Clock - Morph.x) / Morph.y);
  float stagger = sg_saturate(Morph.z);
  float span = max(1e-4, 1.0 - stagger);
  float e = sg_saturate((t - sg_saturate(Target.w) * stagger) / span);
  return e * e * (3.0 - 2.0 * e);
}

void CrystalMorph_float(vec3 Position, vec4 Target, float Clock, vec3 Morph, out vec3 Out)
{
  if (Morph.y <= 0.0) { Out = Position; return; }
  Out = mix(Position, Target.xyz, sgCrystalMorphEase(Target, Clock, Morph));
}

void CrystalMorphNormal_float(vec3 Normal, vec4 Target, float Clock, vec3 Morph, out vec3 Out)
{
  if (Morph.y <= 0.0) { Out = Normal; return; }
  vec3 n = mix(Normal, Target.xyz, sgCrystalMorphEase(Target, Clock, Morph));
  float len = length(n);
  Out = len > 1e-5 ? n / len : Target.xyz;
}

// ---------------------------------------------------------------------------------------------
// Assets/_Graphics/Materials/Graphs/PrismCradle.hlsl (02815910e1a7418bb18c430341747719)
// The Urchin's drape: vertices inside a riding hull's reach slide along their radius onto it.
// ---------------------------------------------------------------------------------------------
uniform vec4 _PrismCradleCentre[4];
uniform vec4 _PrismCradleWeight[4];
#ifndef SG_HAS_PrismCradleParams
uniform vec4 _PrismCradleParams;
#endif

void sgPrismCradleFalloff(float s, float reach, float e, out float k, out float dk)
{
  if (s <= 0.0) { k = 1.0; dk = 0.0; return; }
  if (s >= reach) { k = 0.0; dk = 0.0; return; }
  float t = s / reach;
  float S = t * t * (3.0 - 2.0 * t);
  float dS = 6.0 * t * (1.0 - t);
  float u = 1.0 - S;
  k = pow(u, e);
  dk = -e * pow(u, e - 1.0) * dS / reach;
}

void PrismCradleDeform_float(vec3 Position, vec3 Normal, out vec3 OutPosition, out vec3 OutNormal)
{
  OutPosition = Position;
  OutNormal = Normal;
  int count = int(_PrismCradleParams.z);
  if (count <= 0) return;
  float reach = _PrismCradleParams.x;
  float expo = max(_PrismCradleParams.y, 1.0);
  if (!(reach > 0.0)) return;
  float nLenSq = dot(Normal, Normal);
  if (!(nLenSq > 1e-8)) return;
  vec3 nObj = Normal * inversesqrt(nLenSq);
  vec3 nW = nObj * mat3(sg_WorldToObject);
  float nwLenSq = dot(nW, nW);
  if (!(nwLenSq > 1e-12) || !(nwLenSq < 1e12)) return;
  nW *= inversesqrt(nwLenSq);
  vec3 pW = (sg_ObjectToWorld * vec4(Position, 1.0)).xyz;
  float bestA = 0.0, bestK = 0.0, bestDk = 0.0, bestW = 0.0, bestD = 0.0, bestS = 0.0;
  vec3 bestDir = nW, bestU = vec3(0.0);
  for (int i = 0; i < 4; i++)
  {
    if (i >= count) break;
    vec4 slot = _PrismCradleCentre[i];
    float w = sg_saturate(_PrismCradleWeight[i].x);
    if (!(w > 0.0) || !(slot.w > 0.0)) continue;
    vec3 rad = pW - slot.xyz;
    float d = length(rad);
    if (!(d > 1e-4)) continue;
    float s = d - slot.w;
    if (s >= reach) continue;
    float k, dk;
    sgPrismCradleFalloff(s, reach, expo, k, dk);
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
  vec3 outPos = (sg_WorldToObject * vec4(pNew, 1.0)).xyz;
  vec3 outNrm = nNew * mat3(sg_ObjectToWorld);
  float outNrmLenSq = dot(outNrm, outNrm);
  if (!(dot(outPos, outPos) < 1e12) || !(outNrmLenSq > 1e-12)) return;
  OutPosition = outPos;
  OutNormal = outNrm * inversesqrt(outNrmLenSq);
}

// ---------------------------------------------------------------------------------------------
// Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl (c7d41a9e5b8f4e3ab216d0f97c4e8a52)
// The LIT fundamental: the viewer's own aim, then up to eight peer lights.
// ---------------------------------------------------------------------------------------------
#ifndef SG_HAS_PrismSightBlockerColor
uniform vec4 _PrismSightBlockerColor;
#endif
uniform vec4 _PrismLitPeerApex[8];
uniform vec4 _PrismLitPeerAxis[8];
uniform vec4 _PrismLitPeerGape[8];
uniform vec4 _PrismLitPeerTint[8];
uniform vec4 _PrismLitPeerShape[8];
#ifndef SG_HAS_PrismLitPeerCount
uniform float _PrismLitPeerCount;
#endif

float sgPrismSightEdge(float d, float r) { return mix(0.35, 1.0, pow(sg_saturate(d / r), 2.0)); }

float sgPrismSightFill(vec3 P, vec3 apex, vec3 axis, vec3 gape, vec3 prm)
{
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
  return sgPrismSightEdge(d, core);
}

float sgPrismLitFill(vec3 P, vec3 o, vec3 axis, vec3 gape, vec3 prm, float shape)
{
  if (prm.x <= 0.0) return 0.0;
  if (shape >= 2.0)
  {
    if (prm.y <= 0.0) return 0.0;
    vec3 rel = P - o;
    float s = dot(rel, axis);
    float axial = prm.z > 0.0 ? abs(s) : s;
    if (axial < 0.0 || axial > prm.x) return 0.0;
    float d = length(rel - axis * s);
    if (d > prm.y) return 0.0;
    return sgPrismSightEdge(d, prm.y);
  }
  if (shape >= 1.0)
  {
    vec3 rel = P - o;
    float d2 = dot(rel, rel);
    if (d2 > prm.x * prm.x) return 0.0;
    return sgPrismSightEdge(sqrt(d2), prm.x);
  }
  return sgPrismSightFill(P, o, axis, gape, prm);
}

void PrismDestructionSight_float(vec3 PositionWS, vec3 Apex, vec3 Axis, vec3 Gape, vec3 Params,
    float Strength, vec3 BaseColor, float Domain, float SuperShielded, out vec3 Color)
{
  Color = BaseColor;
  int peerCount = min(int(_PrismLitPeerCount), 8);
  if ((Params.x <= 0.0 || Strength <= 0.0) && peerCount <= 0) return;
  vec3 samplePos = sg_ObjectToWorld[3].xyz; // PRISM_SIGHT_WHOLE_PRISM: the whole prism lights as one
  if (Params.x > 0.0 && Strength > 0.0)
  {
    float own = sgPrismSightFill(samplePos, Apex, Axis, Gape, Params) * Strength;
    if (own > 0.0)
    {
      if (SuperShielded > 0.5)
      {
        vec3 blocker = _PrismSightBlockerColor.w > 0.0 ? _PrismSightBlockerColor.xyz : vec3(1.0, 0.06, 0.05);
        Color = mix(BaseColor, blocker, 0.75 * Strength) + blocker * (Strength * 0.9);
        return;
      }
      Color = BaseColor + vec3(0.45, 0.70, 1.0) * (own * 0.7);
      return;
    }
  }
  vec3 weighted = vec3(0.0);
  float total = 0.0, peak = 0.0;
  for (int i = 0; i < 8; i++)
  {
    if (i >= peerCount) break;
    vec4 tag = _PrismLitPeerShape[i];
    if (tag.y > 0.0 && tag.y != Domain) continue;
    vec4 a = _PrismLitPeerApex[i], x = _PrismLitPeerAxis[i], g = _PrismLitPeerGape[i], t = _PrismLitPeerTint[i];
    float w = sgPrismLitFill(samplePos, a.xyz, x.xyz, g.xyz, vec3(a.w, x.w, g.w), tag.x) * t.a;
    if (w <= 0.0) continue;
    weighted += mix(t.rgb, vec3(1.0), 0.4) * w;
    total += w;
    peak = max(peak, w);
  }
  if (total <= 0.0) return;
  Color = BaseColor + (weighted / total) * (peak * 0.55);
}

// ---------------------------------------------------------------------------------------------
// Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl (bf8e2c1fa76142c89ba03b2e1ae46201)
// The camera->ship corridor that dissolves prism mass through the SHATTER screen-door (the
// shipped kernel), the exploding prism's erosion wipe, and the back-face alpha sharpening.
// ---------------------------------------------------------------------------------------------
#ifndef SG_HAS_PrismOcclusionNearRadius
uniform float _PrismOcclusionNearRadius;
#endif

vec2 sgOccHash2(vec2 cell) { vec3 p3 = fract(cell.xyx * vec3(0.1031, 0.1030, 0.0973)); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.xx + p3.yz) * p3.zy); }
vec3 sgOccHash3(vec3 p3) { p3 = fract(p3 * vec3(0.1031, 0.1030, 0.0973)); p3 += dot(p3, p3.yxz + 33.33); return fract((p3.xxy + p3.yxx) * p3.zyx); }
float sgOccHash1(vec3 p3) { p3 = fract(p3 * 0.1031); p3 += dot(p3, p3.zyx + 31.32); return fract((p3.x + p3.y) * p3.z); }
float sgOccSmoother(float t) { t = sg_saturate(t); return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }

float sgOccShatter(vec2 pixel, float time)
{
  const float cellPx = 16.26, wallPx = 20.0, morph = 0.3256;
  vec2 p = pixel / cellPx;
  vec2 base = floor(p);
  float phase = time * morph * 6.28318530718;
  float best = 8.0;
  vec2 owner = base;
  for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
  {
    vec2 cell = base + vec2(x, y);
    vec2 orbit = 0.5 + 0.5 * sin(6.28318530718 * sgOccHash2(cell) + phase);
    vec2 off = (cell + orbit) - p;
    float d = dot(off, off);
    if (d < best) { best = d; owner = cell; }
  }
  vec2 h = sgOccHash2(owner);
  float ang = 6.28318530718 * h.y;
  float ramp = dot(p - owner, vec2(cos(ang), sin(ang))) * (cellPx / wallPx);
  return fract(h.x + ramp + time * morph) * 0.998 + 0.001;
}

void sgPrismOcclusionFade(vec3 PositionWS, vec3 Target, vec3 Params, float BaseAlpha, float NoseClearance,
    out float Alpha, out float ClipThreshold)
{
  Alpha = BaseAlpha;
  ClipThreshold = 0.0;
  if (BaseAlpha <= 0.0) { Alpha = 0.0; ClipThreshold = 1.0; return; }
  float outerR = Params.x;
  if (outerR > 0.0)
  {
    vec3 axis = Target - sg_CamPos;
    vec3 rel = PositionWS - sg_CamPos;
    float axisLenSq = dot(axis, axis);
    if (axisLenSq > 1e-6)
    {
      float t = dot(rel, axis) / axisLenSq;
      float axisLen = sqrt(axisLenSq);
      float innerR = min(Params.y, outerR);
      float clearanceT = (outerR * NoseClearance) / axisLen;
      float bandT = (outerR - innerR) / axisLen;
      float shrink = min(1.0, 0.5 / max(clearanceT + bandT, 1e-4));
      clearanceT *= shrink;
      bandT *= shrink;
      float tSolid = sg_saturate(1.0 - clearanceT);
      if (t > 0.0 && t < tSolid)
      {
        float dAxis = length(rel - axis * t);
        float nearR = clamp(_PrismOcclusionNearRadius, 0.0, outerR);
        float outerAtT = mix(nearR, outerR, t);
        if (dAxis < outerAtT)
        {
          float innerAtT = outerAtT * (innerR / outerR);
          float clearRadial = 1.0 - sgOccSmoother((dAxis - innerAtT) / max(outerAtT - innerAtT, 1e-4));
          float band = clamp(bandT, 1e-4, 1.0);
          float clearAxial = 1.0 - sgOccSmoother((t - (tSolid - band)) / band);
          Alpha = BaseAlpha * mix(1.0, Params.z, clearRadial * clearAxial);
        }
      }
    }
  }
  if (Alpha >= 1.0) return;
#ifdef SG_FRAGMENT
  ClipThreshold = sgOccShatter(gl_FragCoord.xy, sg_Time.x);
#endif
}

void PrismOcclusionFade_float(vec3 PositionWS, vec3 Target, vec3 Params, float BaseAlpha, out float Alpha, out float ClipThreshold)
{
  sgPrismOcclusionFade(PositionWS, Target, Params, BaseAlpha, 1.0, Alpha, ClipThreshold);
}

void PrismOcclusionFadeDebris_float(vec3 PositionWS, vec3 Target, vec3 Params, float BaseAlpha, out float Alpha, out float ClipThreshold)
{
  sgPrismOcclusionFade(PositionWS, Target, Params, BaseAlpha, 0.0, Alpha, ClipThreshold);
}

void PrismErosionFade_float(vec3 UV, vec3 Velocity, float BaseOpacity, out float Survival)
{
  if (BaseOpacity >= 1.0) { Survival = 1.0; return; }
  if (BaseOpacity <= 0.0) { Survival = 0.0; return; }
  vec2 uv = UV.xy * 2.0 - 1.0;
  vec3 e = sgOccHash3(Velocity);
  vec3 h = sgOccHash3(e * 64.0 + 17.0);
  float ang = 6.28318530718 * h.x;
  vec2 dir = vec2(cos(ang), sin(ang));
  float w01 = dot(uv, dir) / (abs(dir.x) + abs(dir.y)) * 0.5 + 0.5;
  float c = dot(uv, vec2(-dir.y, dir.x)) * 2.5 + h.z * 64.0;
  float ci = floor(c);
  float cf = c - ci;
  cf = cf * cf * (3.0 - 2.0 * cf);
  float jag = mix(sgOccHash1(vec3(ci, h.y * 64.0, e.z * 64.0)), sgOccHash1(vec3(ci + 1.0, h.y * 64.0, e.z * 64.0)), cf);
  w01 = sg_saturate(w01 + (jag - 0.5) * 0.12);
  float threshold = (0.15 + smoothstep(-0.02, 1.02, w01) * 0.85) * 0.998 + 0.001;
  Survival = BaseOpacity >= threshold ? 1.0 : 0.0;
}

void PrismBackFaceFade_float(vec3 PositionWS, vec3 NormalWS, float BaseAlpha, out float Alpha)
{
  Alpha = BaseAlpha;
  if (BaseAlpha >= 1.0 || BaseAlpha <= 0.0) return;
  if (dot(NormalWS, sg_CamPos - PositionWS) < 0.0) Alpha = pow(BaseAlpha, 3.0);
}

// ---------------------------------------------------------------------------------------------
// Assets/_Graphics/Materials/Graphs/VesselVisionShading.hlsl (6862450db5b346df96c3355ca0543f93)
// A vessel re-shaded into a cel-banded silhouette in its domain colour by distance.
// ---------------------------------------------------------------------------------------------
#ifndef SG_HAS_VesselVisionBand
uniform vec4 _VesselVisionBand;
#endif
#ifndef SG_HAS_VesselVisionShape
uniform vec4 _VesselVisionShape;
#endif
#ifndef SG_HAS_VesselVisionRim
uniform vec4 _VesselVisionRim;
#endif
#ifndef SG_HAS_VesselVisionBreakup
uniform vec4 _VesselVisionBreakup;
#endif

float sgVvSmooth(float a, float b, float x) { float t = sg_saturate((x - a) / max(b - a, 1e-5)); return t * t * (3.0 - 2.0 * t); }
float sgVvHash1(vec3 p3) { p3 = fract(p3 * 0.1031); p3 += dot(p3, p3.zyx + 31.32); return fract((p3.x + p3.y) * p3.z); }

void VesselVisionShade_float(vec3 PositionWS, vec3 NormalWS, vec4 Tint, vec3 BaseColor, out vec3 Color)
{
  Color = BaseColor;
  if (Tint.a <= 0.0) return;
  float strength = sg_saturate(_VesselVisionShape.x);
  if (strength <= 0.0) return;
  vec3 originWS = sg_ObjectToWorld[3].xyz;
  float dist = distance(sg_CamPos, originWS);
  if (_VesselVisionBand.w <= 0.0) return;
  float band = sg_saturate(min(sgVvSmooth(_VesselVisionBand.x, _VesselVisionBand.y, dist), 1.0 - sgVvSmooth(_VesselVisionBand.z, _VesselVisionBand.w, dist)));
  float amount = band * strength;
  if (amount <= 0.0) return;
  vec3 V = normalize(sg_CamPos - PositionWS);
  vec3 N = normalize(NormalWS);
  float steps = max(_VesselVisionShape.y, 1.0);
  float ndv = sg_saturate(dot(N, V));
  float tone = mix(sg_saturate(_VesselVisionShape.z), 1.0, min(floor(ndv * steps), steps - 1.0) / max(steps - 1.0, 1.0));
  float rim01 = sg_saturate(sgVvSmooth(_VesselVisionRim.x, _VesselVisionRim.y, 1.0 - ndv));
  vec3 cel = Tint.rgb * (tone + rim01 * max(_VesselVisionRim.z, 0.0)) * max(_VesselVisionShape.w, 0.0);
  float breakup = 1.0;
  float cells = _VesselVisionBreakup.x, reach = _VesselVisionBreakup.y, bs = sg_saturate(_VesselVisionBreakup.z), endD = _VesselVisionBreakup.w;
  if (bs > 0.0 && reach > 0.0 && cells > 0.0)
  {
    float amt = bs * (1.0 - sgVvSmooth(endD * 0.4, max(endD, 1e-3), dist));
    vec3 offsetOS = mat3(sg_WorldToObject) * (PositionWS - originWS);
    if (amt > 0.0 && dot(offsetOS, offsetOS) >= 1e-8)
    {
      float coverage = sgVvSmooth(0.0, max(reach, 1e-3), 1.0 - ndv);
      vec3 cell = floor(normalize(offsetOS) * cells);
      float thr = sgVvHash1(cell + 0.5) * 0.92;
      breakup = mix(1.0, sgVvSmooth(thr - 0.03, thr + 0.03, coverage), amt);
    }
  }
  Color = mix(BaseColor, cel, amount * max(breakup, rim01));
}

// ---- PrismGravityWarp.hlsl (Docs/BLACK_HOLE.md section 5): the black hole's tidal stretch of a prism ----
// A line-for-line port of PrismGravityWarpDeform_float: the strongest hole's tide at the prism's centre,
// eased into the ceiling, applied as a volume-conserving stretch about the centre, with the normal carried
// through the inverse transpose. HLSL mul(v, M) is GLSL v * M; mul(M, v) is M * v. While the engine
// publishes no warp bank (_PrismGravityWarpParams.y = 0) every vertex passes through untouched.
#define PRISM_GRAVITY_WARP_SLOTS 4
uniform vec4 _PrismGravityWarpCentre[PRISM_GRAVITY_WARP_SLOTS];  // xyz world centre, w horizon radius
uniform vec4 _PrismGravityWarpWeight[PRISM_GRAVITY_WARP_SLOTS];  // x GM tau^2, y reach, z softening
uniform vec4 _PrismGravityWarpParams;                            // (ln max stretch, liveSlotCount, 0, 0)

float PrismGravityWarpWindow(float s, float reach)
{
  if (s >= reach) return 0.0;
  float u = sg_saturate(2.0 * s / reach - 1.0);
  return 1.0 - u * u * (3.0 - 2.0 * u);
}

float PrismGravityWarpCeiling(float eps, float ceiling)
{
  float x = min(eps / ceiling, 1e4);
  float x2 = x * x;
  return ceiling * x * inversesqrt(sqrt(1.0 + x2 * x2));
}

float PrismGravityWarpTide(float d, float rs, float k, float reach)
{
  float r = max(d, rs);
  return k / (r * r * r) * PrismGravityWarpWindow(d - rs, reach);
}

float PrismGravityWarpTideSoft(float d, float rs, float k, float reach, float softening)
{
  if (!(softening > 0.0)) return PrismGravityWarpTide(d, rs, k, reach);
  float q = d * d + softening * softening;
  return k / (q * sqrt(q)) * PrismGravityWarpWindow(max(d - rs, 0.0), reach);
}

void PrismGravityWarpDeform_float(vec3 Position, vec3 Normal, out vec3 OutPosition, out vec3 OutNormal)
{
  OutPosition = Position;
  OutNormal = Normal;
  int count = int(_PrismGravityWarpParams.y);
  if (count <= 0) return;
  float nLenSq = dot(Normal, Normal);
  if (!(nLenSq > 1e-8)) return;
  vec3 nObj = Normal * inversesqrt(nLenSq);
  vec3 nW = nObj * mat3(sg_WorldToObject);
  float nwLenSq = dot(nW, nW);
  if (!(nwLenSq > 1e-12) || !(nwLenSq < 1e12)) return;
  nW *= inversesqrt(nwLenSq);
  vec3 c = (sg_ObjectToWorld * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
  float bestTide = 0.0;
  vec3 bestDir = vec3(0.0, 0.0, 1.0);
  for (int i = 0; i < PRISM_GRAVITY_WARP_SLOTS; i++)
  {
    if (i >= count) break;
    vec4 slot = _PrismGravityWarpCentre[i];
    vec4 weight = _PrismGravityWarpWeight[i];
    float k = weight.x, reach = weight.y;
    if (!(abs(k) > 0.0) || !(slot.w > 0.0) || !(reach > 0.0)) continue;
    vec3 rad = c - slot.xyz;
    float d = length(rad);
    if (!(d > 1e-4)) continue;
    if (d - slot.w >= reach) continue;
    float tide = PrismGravityWarpTideSoft(d, slot.w, k, reach, weight.z);
    if (abs(tide) <= abs(bestTide)) continue;
    bestTide = tide;
    bestDir = rad / d;
  }
  if (!(abs(bestTide) > 0.0)) return;
  float ceiling = max(_PrismGravityWarpParams.x, 1e-3);
  float eps = sign(bestTide) * PrismGravityWarpCeiling(abs(bestTide), ceiling);
  float radial = exp(eps);
  float across = exp(-0.5 * eps);
  vec3 pW = (sg_ObjectToWorld * vec4(Position, 1.0)).xyz;
  vec3 q = pW - c;
  float qr = dot(q, bestDir);
  vec3 pNew = c + bestDir * (qr * radial) + (q - bestDir * qr) * across;
  float nr = dot(nW, bestDir);
  vec3 nNew = bestDir * (nr / radial) + (nW - bestDir * nr) / across;
  vec3 outPos = (sg_WorldToObject * vec4(pNew, 1.0)).xyz;
  vec3 outNrm = nNew * mat3(sg_ObjectToWorld);
  float outNrmLenSq = dot(outNrm, outNrm);
  if (!(dot(outPos, outPos) < 1e12) || !(outNrmLenSq > 1e-12)) return;
  OutPosition = outPos;
  OutNormal = outNrm * inversesqrt(outNrmLenSq);
}
