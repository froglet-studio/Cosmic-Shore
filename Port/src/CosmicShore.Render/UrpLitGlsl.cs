namespace CosmicShore.Render
{
    /// <summary>
    /// URP's Lit lighting (UniversalFragmentPBR, metallic workflow), shared by the scene program's
    /// Lit family and every compiled graph with a Lit target: the ambient probe and the default
    /// reflection through the environment BRDF, then the main light's diffuse and GGX specular.
    /// <see cref="EnvironmentLighting"/> fills the probe and the cubemap; the constants are URP's
    /// documented ones (dielectric F0 0.04, roughness floors, the roughness-to-mip curve).
    /// Additional (point and spot) lights and shadows are not drawn.
    /// </summary>
    static class UrpLitGlsl
    {
        public const string Source = @"
uniform vec3 uSH[9];              // ambient probe: coefficients of (1, y, z, x, xy, yz, 3z^2-1, xz, x^2-y^2)
uniform samplerCube uEnvCube;     // the default reflection (EnvironmentLighting.CubeUnit)
uniform vec4 uEnvParams;          // x reflection intensity, y 1 = a reflection cubemap is bound
vec3 urpSH(vec3 n){
  return max(uSH[0] + uSH[1] * n.y + uSH[2] * n.z + uSH[3] * n.x
           + uSH[4] * (n.x * n.y) + uSH[5] * (n.y * n.z) + uSH[6] * (3.0 * n.z * n.z - 1.0)
           + uSH[7] * (n.x * n.z) + uSH[8] * (n.x * n.x - n.y * n.y), vec3(0.0));
}
vec3 urpGlossy(vec3 r, float perceptualRoughness, bool reflections){
  if (!reflections) return uSH[0] * uEnvParams.x;          // _GlossyEnvironmentColor
  if (uEnvParams.y < 0.5) return urpSH(r) * uEnvParams.x;  // no cubemap: the probe stands in
  float mip = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness) * 6.0;
  return textureLod(uEnvCube, r, mip).rgb * uEnvParams.x;
}
vec3 urpLit(vec3 albedo, float metallic, float smoothness, float occlusion, vec3 N, vec3 V, vec3 L, vec3 lightColor, bool specularHighlights, bool reflections){
  float oneMinusReflectivity = 0.96 * (1.0 - metallic);
  float reflectivity = 1.0 - oneMinusReflectivity;
  vec3 diffuse = albedo * oneMinusReflectivity;
  vec3 specular = mix(vec3(0.04), albedo, metallic);
  float perceptualRoughness = 1.0 - smoothness;
  float roughness = max(perceptualRoughness * perceptualRoughness, 0.0078125);
  float roughness2 = max(roughness * roughness, 6.103515625e-5);
  float NoV = clamp(dot(N, V), 0.0, 1.0);
  float fresnel = (1.0 - NoV) * (1.0 - NoV); fresnel *= fresnel;
  float grazing = clamp(smoothness + reflectivity, 0.0, 1.0);
  vec3 color = urpSH(N) * diffuse
             + urpGlossy(reflect(-V, N), perceptualRoughness, reflections) * (mix(specular, vec3(grazing), fresnel) / (roughness2 + 1.0));
  color *= occlusion;
  float NdotL = clamp(dot(N, L), 0.0, 1.0);
  vec3 brdf = diffuse;
  if (specularHighlights) {
    vec3 h = L + V;
    vec3 H = dot(h, h) > 1e-12 ? normalize(h) : N;
    float NoH = clamp(dot(N, H), 0.0, 1.0), LoH = clamp(dot(L, H), 0.0, 1.0);
    float d = NoH * NoH * (roughness2 - 1.0) + 1.00001;
    brdf += specular * (roughness2 / ((d * d) * max(0.1, LoH * LoH) * (roughness * 4.0 + 2.0)));
  }
  return color + brdf * lightColor * NdotL;
}
";
    }
}
