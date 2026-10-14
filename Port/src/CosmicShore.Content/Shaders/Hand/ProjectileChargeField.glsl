// Hand translation of Assets/_Graphics/Materials/Graphs/ProjectileChargeField.shader
// (Shader Graphs/ProjectileChargeField) and ProjectileChargeField.hlsl: the Sparrow round's
// charge shell - a fresnel rim plus one to four arcs striking across the sphere, brighter and
// faster as the round grows. _RoundSeed is per renderer (its property block).
const float PCF_TAU = 6.28318530718;
float pcfHash1(float n) { return fract(sin(n) * 43758.5453123); }
float pcfValueNoise1D(float x) {
  float i = floor(x);
  float f = fract(x);
  f = f * f * (3.0 - 2.0 * f);
  return mix(pcfHash1(i), pcfHash1(i + 1.0), f);
}
float pcfFBM1D(float x, int octaves) {
  float value = 0.0, amplitude = 0.5, frequency = 1.0;
  for (int o = 0; o < octaves; o++) {
    value += amplitude * (pcfValueNoise1D(x * frequency) * 2.0 - 1.0);
    frequency *= 2.17;
    amplitude *= 0.5;
  }
  return value;
}
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) { p = sg_PosOS; n = sg_NrmOS; t = sg_TanOS.xyz; }
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  vec3 cameraPosOS = (sg_WorldToObject * vec4(sg_CamPos, 1.0)).xyz;
  vec3 viewDirOS = cameraPosOS - sg_PosOS;
  float seed = _RoundSeed;
  // ProjectileChargeFieldPhase_float: column 0's length is the shell's diameter.
  float worldRadius = 0.5 * length(sg_ObjectToWorld[0].xyz);
  float phase = sg_Time.x * _PhaseSpeed + worldRadius * _PhaseByRadius + seed * _PhaseBySeed;
  float charge = clamp(worldRadius / max(_ChargeReferenceRadius, 1e-3), 0.0, 1.0);

  vec3 fragDir = normalize(sg_PosOS);
  float gain = mix(_ChargeFloor, 1.0, charge);
  vec3 N = normalize(sg_NrmOS);
  vec3 V = normalize(viewDirOS);
  float NdotV = clamp(dot(N, V), 0.0, 1.0);
  float fresnel = pow(1.0 - NdotV, _FresnelRimPower) * _FresnelRimIntensity * gain;
  int arcCount = int(_ArcCount);
  float totalContribution = 0.0;
  vec3 totalColor = vec3(0.0);
  for (int i = 0; i < 4; i++) {
    if (i >= arcCount) break;
    float offset = pcfHash1(float(i) * 7.3 + 0.5) * 11.0;
    float cycle = phase * _CrackleRate + offset;
    float life = fract(cycle);
    float idx = floor(cycle);
    float strike01 = clamp(life / max(_StrikeTime, 1e-3), 0.0, 1.0);
    float env = smoothstep(0.0, 0.07, life) * pow(smoothstep(1.0, min(_HoldTime, 0.98), life), _FadeShape);
    if (env < 0.002) continue;
    float h1 = pcfHash1(idx * 3.7 + float(i) * 11.3);
    float h2 = pcfHash1(idx * 5.1 + float(i) * 17.9);
    float h3 = pcfHash1(idx * 9.3 + float(i) * 23.1);
    vec3 va = normalize(cameraPosOS);
    vec3 refv = abs(va.z) < 0.9 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 e1 = normalize(cross(va, refv));
    vec3 e2 = cross(va, e1);
    float ang = h2 * PCF_TAU + seed * _SeedSpin;
    float tilt = _ArcTiltRange * sin(h1 * PCF_TAU + seed * _SeedTilt);
    vec3 pole = normalize(cos(tilt) * (cos(ang) * e1 + sin(ang) * e2) + sin(tilt) * va);
    vec3 u = normalize(cross(va, pole));
    vec3 v = cross(pole, u);
    float height = dot(fragDir, pole);
    float theta = atan(dot(fragDir, v), dot(fragDir, u));
    float wob = pcfFBM1D(theta * _ArcWanderScale + idx * 4.7 + float(i) * 3.1 + seed * _SeedWobble, 3) * _ArcWander;
    float d = height - wob;
    float sharp = max(_ArcSharpness, 1e-3);
    float stroke = exp(-(d * d) / (sharp * sharp));
    if (stroke < 0.002) continue;
    float start = (h3 * 2.0 - 1.0) * _ArcStartSpread;
    float dTheta = theta - start;
    dTheta = dTheta - PCF_TAU * floor(dTheta / PCF_TAU + 0.5);
    float away = abs(dTheta);
    float halfSpan = _ArcSpan * 0.5;
    float reach = halfSpan * strike01;
    float soften = max(halfSpan * 0.35, 0.08);
    float along = 1.0 - smoothstep(reach - soften, reach, away);
    float tipWidth = max(soften * 0.5, 0.04);
    float tipDist = away - reach;
    float tip = exp(-(tipDist * tipDist) / (tipWidth * tipWidth)) * (1.0 - strike01) * _TipGlow;
    float body = stroke * (along + tip);
    float contribution = body * env * gain * _ArcIntensity;
    if (contribution < 0.001) continue;
    float heat = clamp(body, 0.0, 1.0);
    float core = smoothstep(_CoreThreshold, 1.0, heat);
    vec3 arcColor = mix(_CrackleColorB.rgb, _CrackleColorA.rgb, core) * (1.0 + heat * 2.0);
    totalContribution += contribution;
    totalColor += arcColor * contribution;
  }
  totalContribution = clamp(totalContribution, 0.0, 1.0);
  s.Alpha = clamp(totalContribution + fresnel, 0.0, 1.0);
  s.BaseColor = totalContribution > 0.001
    ? (totalColor / max(totalContribution, 0.001)) * totalContribution + _FresnelRimColor.rgb * fresnel
    : _FresnelRimColor.rgb * fresnel;
  return s;
}
