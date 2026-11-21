// Hand translation of Assets/_Graphics/Materials/Graphs/ForcefieldCrackleCapsule.shader
// (Shader Graphs/ForcefieldCrackleCapsule) and its ForcefieldCrackleCapsule_float in
// ForcefieldCrackle.hlsl: a capsule shield's fresnel rim plus electrical arcs spreading from
// each impact the controller writes into the renderer's block (_ImpactPositions / _ImpactParams).
uniform vec4 _ImpactPositions[16];
uniform vec4 _ImpactParams[16];
float ffHash1(float n) { return fract(sin(n) * 43758.5453123); }
float ffValueNoise1D(float x) {
  float i = floor(x);
  float f = fract(x);
  f = f * f * (3.0 - 2.0 * f);
  return mix(ffHash1(i), ffHash1(i + 1.0), f);
}
float ffFBM1D(float x, int octaves) {
  float value = 0.0, amplitude = 0.5, frequency = 1.0;
  for (int o = 0; o < octaves; o++) {
    value += amplitude * (ffValueNoise1D(x * frequency) * 2.0 - 1.0);
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
  vec3 objectPosition = sg_PosOS;
  vec3 objectNormal = sg_NrmOS * (sg_FrontFace ? 1.0 : -1.0);
  vec3 cameraPosOS = (sg_WorldToObject * vec4(sg_CamPos, 1.0)).xyz;
  vec3 viewDirOS = cameraPosOS - objectPosition;
  vec3 scaleOS = vec3(length(sg_ObjectToWorld[0].xyz), length(sg_ObjectToWorld[1].xyz), length(sg_ObjectToWorld[2].xyz));

  vec3 N = normalize(objectNormal);
  vec3 V = normalize(viewDirOS);
  float NdotV = clamp(dot(N, V), 0.0, 1.0);
  float fresnel = pow(1.0 - NdotV, _FresnelRimPower) * _FresnelRimIntensity;
  float alpha = fresnel;
  vec3 emission = _FresnelRimColor.rgb * fresnel;

  if (_ImpactCount > 0.5) {
    vec3 fragSC = objectPosition * scaleOS;
    float totalContribution = 0.0;
    vec3 totalColor = vec3(0.0);
    for (int i = 0; i < 16; i++) {
      vec4 impactPos = _ImpactPositions[i];
      vec4 impactParam = _ImpactParams[i];
      float maxLifetime = impactParam.z;
      if (maxLifetime <= 0.0) continue;
      float intensity = impactParam.x;
      float reach = max(impactParam.y, 0.01);
      float elapsed = impactPos.w;
      float lifeRatio = clamp(elapsed / maxLifetime, 0.0, 1.0);
      float timeFade = pow(1.0 - lifeRatio, 1.5);
      vec3 impSC = impactPos.xyz * scaleOS;
      vec3 d = fragSC - impSC;
      float nd = length(d) / reach;
      float front = clamp(lifeRatio * _RippleSpeed, 0.0, 1.0);
      float ringW = _RingThickness * 0.3183;
      float distBehindFront = front - nd;
      float waveBand = smoothstep(-ringW * 0.1, 0.0, distBehindFront) * smoothstep(ringW, 0.0, distBehindFront);
      waveBand *= step(nd, front + ringW * 0.2);
      float centerGlow = smoothstep(_CenterFillAmount, 0.0, nd) * (1.0 - lifeRatio * lifeRatio);
      float spatialEnvelope = max(waveBand, centerGlow);
      if (spatialEnvelope < 0.001) continue;
      float segHalf = 0.5 * scaleOS.y;
      vec3 axisPoint = vec3(0.0, clamp(impSC.y, -segHalf, segHalf), 0.0);
      vec3 outward = normalize(impSC - axisPoint + vec3(1e-4, 0.0, 0.0));
      vec3 tangent = normalize(cross(outward, vec3(0.123, 0.456, 0.789)));
      vec3 bitangent = cross(outward, tangent);
      float azimuth = atan(dot(d, bitangent), dot(d, tangent));
      float ndAngle = nd * 3.14159;
      int arcCount = int(_ArcDensity);
      float arcContrib = 0.0, arcHeat = 0.0;
      for (int a = 0; a < 20; a++) {
        if (a >= arcCount) break;
        float baseAngle = (float(a) / float(arcCount)) * 6.28318 + ffHash1(float(i) * 7.3 + 0.5) * 6.28318;
        float dAzimuth = azimuth - baseAngle;
        dAzimuth = dAzimuth - 6.28318 * floor(dAzimuth / 6.28318 + 0.5);
        float noiseInput = ndAngle * 15.0 + float(a) * 13.7 + float(i) * 5.3;
        float wobble = ffFBM1D(noiseInput, 4) * 0.3 * (ndAngle + 0.1);
        float subBranch = ffFBM1D(noiseInput * 2.3 + 100.0, 3) * 0.15 * ndAngle;
        float arcDist = abs(dAzimuth - wobble);
        float arcDistSub = abs(dAzimuth - wobble - subBranch);
        float arcLine = exp(-arcDist * arcDist / (_ArcSharpness * _ArcSharpness));
        float subLine = exp(-arcDistSub * arcDistSub / (_ArcSharpness * _ArcSharpness * 4.0)) * 0.4;
        float thisArc = max(arcLine, subLine) * smoothstep(0.0, 0.016, nd);
        arcContrib = max(arcContrib, thisArc);
        arcHeat = max(arcHeat, arcLine);
      }
      float contribution = spatialEnvelope * arcContrib * timeFade * intensity;
      vec3 arcColor = mix(_CrackleColorB.rgb, _CrackleColorA.rgb, arcHeat * arcHeat) * (1.0 + arcHeat * 2.0);
      totalContribution += contribution;
      totalColor += arcColor * contribution;
    }
    totalContribution = clamp(totalContribution, 0.0, 1.0);
    alpha = clamp(totalContribution + fresnel, 0.0, 1.0);
    emission = totalContribution > 0.001
      ? (totalColor / max(totalContribution, 0.001)) * totalContribution + _FresnelRimColor.rgb * fresnel
      : _FresnelRimColor.rgb * fresnel;
  }
  s.BaseColor = emission;
  s.Alpha = alpha;
  return s;
}
