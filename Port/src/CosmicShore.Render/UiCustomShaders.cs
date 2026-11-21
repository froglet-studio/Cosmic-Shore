using System.Collections.Generic;

namespace CosmicShore.Render
{
    /// <summary>
    /// Hand-written UI shaders a Graphic's material can name, translated to GLSL; every other
    /// Graphic draws with UI/Default. Each is the shader's fragment as a function of the mesh's
    /// uv0 (<c>vec4 uiShade(vec2 uv)</c>) plus the material properties it reads, bound per draw
    /// in the GPU's colour space. The project's UI names one: the HUD's team-volume gauge.
    /// </summary>
    public static class UiCustomShaders
    {
        public sealed class Def
        {
            public string Name;
            public string[] Floats, Colors;
            public string Glsl;
        }

        public static readonly Dictionary<string, Def> ByName = new()
        {
            ["Custom/VolumeDisplayShader"] = new Def
            {
                // VolumeDisplayShader.shader (Assets/_Graphics/Materials/Shaders/UI), translated: a ring in
                // the leading team's colour with inner and outer glow, inside it three 120-degree arcs
                // whose depth is each team's volume (VolumeUI sets _Radius1..3), with glowing edges
                // and dividers. The Image's sprite and colour are not read.
                Name = "Custom/VolumeDisplayShader",
                Floats = new[]
                {
                    "_Radius1", "_Radius2", "_Radius3", "_BorderThickness", "_GapThickness", "_DividerThickness",
                    "_OuterGlowIntensity", "_OuterGlowDistance", "_InnerGlowIntensity", "_InnerGlowDistance",
                    "_ArcGlowDistance", "_ArcGlowIntensity",
                },
                Colors = new[] { "_Color1", "_Color2", "_Color3" },
                Glsl = @"
uniform vec4 _Color1, _Color2, _Color3;
uniform float _Radius1, _Radius2, _Radius3, _BorderThickness, _GapThickness, _DividerThickness;
uniform float _OuterGlowIntensity, _OuterGlowDistance, _InnerGlowIntensity, _InnerGlowDistance, _ArcGlowDistance, _ArcGlowIntensity;
float vdGlow(float d, float maxD){ return max(0.0, 1.0 - d / maxD); }
float vdRemap(float v, float g){ return v * (1.0 - 2.0 * g) + 0.7 * g; }
vec4 uiShade(vec2 uv){
  float r1 = vdRemap(_Radius1, _ArcGlowDistance), r2 = vdRemap(_Radius2, _ArcGlowDistance), r3 = vdRemap(_Radius3, _ArcGlowDistance);
  float scaleFactor = 1.0 / (1.0 + _OuterGlowDistance);
  vec2 c = vec2(1.0 - uv.x, uv.y) * 2.0 - 1.0;
  float angle = atan(c.y, c.x);
  if (angle < 0.0) angle += 6.28318530718;
  float dist = length(c);
  vec4 dominant = r1 >= r2 && r1 >= r3 ? _Color1 : r2 >= r1 && r2 >= r3 ? _Color2 : _Color3;
  float borderStart = scaleFactor - _BorderThickness, borderEnd = scaleFactor;
  if (dist > borderStart && dist <= borderEnd) return dominant;
  if (dist > borderEnd && dist <= borderEnd + _OuterGlowDistance * scaleFactor)
    return dominant * _OuterGlowIntensity * vdGlow(dist - borderEnd, _OuterGlowDistance);
  if (dist > borderStart - _InnerGlowDistance * scaleFactor && dist <= borderStart)
    return dominant * _InnerGlowIntensity * vdGlow(borderStart - dist, _InnerGlowDistance);
  if (dist > borderStart - _GapThickness * scaleFactor && dist <= borderStart) return vec4(0.0);
  float maxDist = borderStart - _GapThickness * scaleFactor;
  float inner = maxDist - _GapThickness * scaleFactor;
  const float third = 2.09439510239, twoThirds = 4.18879020478;
  float toDivider, toInner;
  vec4 segment;
  if (angle < third && dist <= inner && dist > maxDist - r1) { toDivider = min(angle, third - angle); toInner = dist - (maxDist - r1); segment = _Color1; }
  else if (angle < twoThirds && angle > third && dist <= inner && dist > maxDist - r2) { toDivider = min(angle - third, twoThirds - angle); toInner = dist - (maxDist - r2); segment = _Color2; }
  else if (angle > twoThirds && dist <= inner && dist > maxDist - r3) { toDivider = min(angle - twoThirds, 6.28318530718 - angle); toInner = dist - (maxDist - r3); segment = _Color3; }
  else return vec4(0.0);
  toDivider *= dist;
  float strength = max(max(_ArcGlowIntensity * vdGlow(maxDist - dist, _ArcGlowDistance), vdGlow(toInner, _ArcGlowDistance)),
                       vdGlow(toDivider, _DividerThickness / 2.0));
  return mix(segment, vec4(0.0), strength);
}
",
            },
        };

        /// <summary>The fragment stage for <paramref name="def"/> over the uGUI vertex stage's outputs (RectMask2D clip kept).</summary>
        public static string Fragment(Def def) => "#version 330 core\n"
            + "in vec2 vUv; in vec4 vColor; in vec4 vClip; in vec2 vPos; in vec4 vSdf; in vec4 vOutline; flat in float vMode;\n"
            + "out vec4 frag;\n"
            + def.Glsl
            + "void main(){\n"
            + "  if (vPos.x < vClip.x || vPos.y < vClip.y || vPos.x > vClip.z || vPos.y > vClip.w) discard;\n"
            + "  frag = uiShade(vUv);\n"
            + "}\n";
    }
}
