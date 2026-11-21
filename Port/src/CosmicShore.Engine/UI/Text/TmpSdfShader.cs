using System;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// The SDF material parameters a TMP "Distance Field" material carries, flattened for
    /// shading. Built from a material's saved properties (<see cref="FromMaterial"/>).
    /// </summary>
    public struct TmpSdfParams
    {
        public float gradientScale;
        public float scaleRatioA, scaleRatioB, scaleRatioC;
        public float sharpness;
        public float faceDilate;
        public float weightNormal, weightBold;
        public float outlineWidth, outlineSoftness;
        public Color faceColor, outlineColor, underlayColor;
        public float underlayOffsetX, underlayOffsetY, underlayDilate, underlaySoftness;
        public bool outlineOn, underlayOn, underlayInner;
        public float textureWidth, textureHeight;

        /// <summary>
        /// Reads a TMP material. <paramref name="atlasFont"/> (optional) supplies the atlas the
        /// quad samples — a fallback font's own gradient scale and texture size win over the
        /// primary material's, like TMP's fallback-material copy.
        /// </summary>
        public static TmpSdfParams FromMaterial(Material m, TMP_FontAsset atlasFont = null)
        {
            var p = new TmpSdfParams
            {
                gradientScale = 5f, scaleRatioA = 1f, scaleRatioB = 1f, scaleRatioC = 1f,
                faceColor = Color.white, outlineColor = Color.black, underlayColor = new Color(0, 0, 0, 0.5f),
                outlineOn = true,
                textureWidth = atlasFont?.atlasWidth ?? 512, textureHeight = atlasFont?.atlasHeight ?? 512,
            };
            if (m != null)
            {
                float F(string n, float d) => m.HasProperty(n) ? m.GetFloat(n) : d;
                p.gradientScale = F("_GradientScale", 5f);
                p.scaleRatioA = F("_ScaleRatioA", 1f);
                p.scaleRatioB = F("_ScaleRatioB", 1f);
                p.scaleRatioC = F("_ScaleRatioC", 1f);
                p.sharpness = F("_Sharpness", 0f);
                p.faceDilate = F("_FaceDilate", 0f);
                p.weightNormal = F("_WeightNormal", 0f);
                p.weightBold = F("_WeightBold", 0.5f);
                p.outlineWidth = F("_OutlineWidth", 0f);
                p.outlineSoftness = F("_OutlineSoftness", 0f);
                p.underlayOffsetX = F("_UnderlayOffsetX", 0f);
                p.underlayOffsetY = F("_UnderlayOffsetY", 0f);
                p.underlayDilate = F("_UnderlayDilate", 0f);
                p.underlaySoftness = F("_UnderlaySoftness", 0f);
                if (m.HasProperty("_FaceColor")) p.faceColor = m.GetColor("_FaceColor");
                if (m.HasProperty("_OutlineColor")) p.outlineColor = m.GetColor("_OutlineColor");
                if (m.HasProperty("_UnderlayColor")) p.underlayColor = m.GetColor("_UnderlayColor");
                bool mobile = m.shader != null && m.shader.name == TmpSdfShader.MobileDistanceFieldShaderName;
                p.outlineOn = !mobile || m.IsKeywordEnabled("OUTLINE_ON");
                p.underlayOn = m.IsKeywordEnabled("UNDERLAY_ON");
                p.underlayInner = m.IsKeywordEnabled("UNDERLAY_INNER");
            }
            if (atlasFont?.material != null && !ReferenceEquals(atlasFont.material, m) && atlasFont.material.HasProperty("_GradientScale"))
                p.gradientScale = atlasFont.material.GetFloat("_GradientScale");
            else if (m == null && atlasFont != null && atlasFont.atlasPadding > 0)
                p.gradientScale = atlasFont.atlasPadding + 1;
            return p;
        }
    }

    /// <summary>
    /// TextMeshPro "Distance Field" shading, reproduced from the shader's documented
    /// parameters and observable output (no TMP source used). Two implementations of ONE
    /// model: <see cref="VertexSource"/>/<see cref="FragmentSource"/> (GLSL 330 for the GL
    /// client) and <see cref="Shade"/> (C#, used by <see cref="TmpSoftwareRaster"/>).
    ///
    /// <para><b>The scale term.</b> The SDF value <c>d</c> (atlas alpha, 0.5 on the glyph
    /// outline) is turned into coverage with <c>alpha = saturate((d − bias) × scale)</c>, so
    /// <c>scale</c> is "screen pixels per unit of d". TMP derives it as
    /// <c>√2 × (screen px per atlas texel) × _GradientScale × (_Sharpness + 1)</c>: the √2
    /// comes from measuring the pixel's footprint as the length of its (x, y) diagonal. Screen
    /// px per atlas texel is <c>pixelsPerUnit × |vertex.scale|</c> (the vertex scale is the
    /// glyph's element scale = canvas units per texel). The GLSL derives the same quantity
    /// from screen-space derivatives of the atlas coordinate, which agrees exactly for
    /// unrotated UI text and stays correct under any transform.</para>
    ///
    /// <para><b>Weight.</b> <c>weight = ((bold ? _WeightBold : _WeightNormal) / 4 + _FaceDilate)
    /// × _ScaleRatioA × 0.5</c>; <c>bias = 0.5 − weight + 0.5 / scale</c>, which puts the
    /// antialiasing ramp centred on the (dilated) outline. Bold is signalled by a negative
    /// vertex scale, exactly as TMP packs it.</para>
    ///
    /// <para><b>Outline / underlay.</b> Outline width and softness are in SDF units × scale
    /// ratio A; the face is faded with the softness and the outline colour is blended in with
    /// weight <c>√min(1, outline)</c>. The underlay (UNDERLAY_ON) re-samples the atlas at an
    /// offset of <c>−offset × ratioC × gradientScale / textureSize</c> with its own
    /// scale/bias (softness widens the ramp, dilate grows it) and composites UNDER the face.
    /// Output is premultiplied (blend One, OneMinusSrcAlpha).</para>
    /// </summary>
    public static class TmpSdfShader
    {
        public const string DistanceFieldShaderName = "TextMeshPro/Distance Field";
        public const string MobileDistanceFieldShaderName = "TextMeshPro/Mobile/Distance Field";

        public static readonly float Sqrt2 = MathF.Sqrt(2f);

        /// <summary>TMP's scale term for a fragment (see class remarks).</summary>
        public static float Scale(in TmpSdfParams p, float screenPixelsPerTexel)
            => Sqrt2 * screenPixelsPerTexel * p.gradientScale * (p.sharpness + 1f);

        /// <summary>
        /// Shades one fragment. <paramref name="d"/> = atlas SDF sample at the glyph UV,
        /// <paramref name="dUnderlay"/> = sample at the underlay UV (ignored unless enabled),
        /// <paramref name="scale"/> from <see cref="Scale"/>, <paramref name="vertexColor"/> in the
        /// working colour space. Returns PREMULTIPLIED colour.
        /// </summary>
        public static Color Shade(in TmpSdfParams p, float d, float dUnderlay, float scale, bool bold, Color vertexColor,
            Color faceColor, Color outlineColor, Color underlayColor)
        {
            scale = MathF.Max(scale, 1e-4f);
            float weight = ((bold ? p.weightBold : p.weightNormal) / 4f + p.faceDilate) * p.scaleRatioA * 0.5f;
            float bias = 0.5f - weight + 0.5f / scale;
            float opacity = vertexColor.a;

            // face = vertex colour × _FaceColor; outline/underlay alpha carry the vertex alpha.
            var face = new Color(vertexColor.r * faceColor.r, vertexColor.g * faceColor.g, vertexColor.b * faceColor.b, opacity * faceColor.a);
            var outline = new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineColor.a * opacity);

            float sd = (bias - d) * scale;
            float outlineW = p.outlineOn ? p.outlineWidth * p.scaleRatioA * scale : 0f;
            float softness = p.outlineOn ? p.outlineSoftness * p.scaleRatioA * scale : 0f;

            float faceAlpha = 1f - Saturate((sd - outlineW * 0.5f + softness * 0.5f) / (1f + softness));
            float outlineAlpha = Saturate(sd + outlineW * 0.5f) * MathF.Sqrt(MathF.Min(1f, outlineW));

            var fp = new Color(face.r * face.a, face.g * face.a, face.b * face.a, face.a);
            var op = new Color(outline.r * outline.a, outline.g * outline.a, outline.b * outline.a, outline.a);
            var c = new Color(
                (fp.r + (op.r - fp.r) * outlineAlpha) * faceAlpha,
                (fp.g + (op.g - fp.g) * outlineAlpha) * faceAlpha,
                (fp.b + (op.b - fp.b) * outlineAlpha) * faceAlpha,
                (fp.a + (op.a - fp.a) * outlineAlpha) * faceAlpha);

            if (p.underlayOn || p.underlayInner)
            {
                float bScale = scale / (1f + p.underlaySoftness * p.scaleRatioC * scale);
                float bBias = (0.5f - weight) * bScale - 0.5f - p.underlayDilate * p.scaleRatioC * 0.5f * bScale;
                float ua = p.underlayInner
                    ? (1f - Saturate(dUnderlay * bScale - bBias)) * Saturate(d * scale - (bias * scale - 0.5f)) // inner shadow: inside the glyph
                    : Saturate(dUnderlay * bScale - bBias);
                float uaA = underlayColor.a * opacity;
                float k = ua * (1f - c.a);
                c = new Color(c.r + underlayColor.r * uaA * k, c.g + underlayColor.g * uaA * k, c.b + underlayColor.b * uaA * k, c.a + uaA * k);
            }
            return c;
        }

        /// <summary>UV offset of the underlay sample (add to the glyph UV).</summary>
        public static Vector2 UnderlayUvOffset(in TmpSdfParams p)
            => new(-(p.underlayOffsetX * p.scaleRatioC) * p.gradientScale / Math.Max(1f, p.textureWidth),
                   -(p.underlayOffsetY * p.scaleRatioC) * p.gradientScale / Math.Max(1f, p.textureHeight));

        static float Saturate(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>
        /// GLSL 330 vertex shader. Attributes: position (canvas units), atlas UV, vertex colour
        /// (RGBA8 normalized, already in the working colour space), TMP's signed element scale.
        /// </summary>
        public const string VertexSource = @"#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aUv;
layout(location = 2) in vec4 aColor;
layout(location = 3) in float aScale;      // TMP element scale; negative = bold

uniform mat4 uMvp;                          // canvas units -> clip space
uniform vec2 uUnderlayUvOffset;             // TmpSdfShader.UnderlayUvOffset

out vec2 vUv;
out vec2 vUnderlayUv;
out vec4 vColor;
flat out float vBold;

void main()
{
    gl_Position = uMvp * vec4(aPosition, 1.0);
    vUv = aUv;
    vUnderlayUv = aUv + uUnderlayUvOffset;
    vColor = aColor;
    vBold = aScale < 0.0 ? 1.0 : 0.0;
}
";

        /// <summary>
        /// GLSL 330 fragment shader: TMP "Distance Field" face + outline + underlay with the
        /// scale term from screen-space derivatives (see class remarks). Output premultiplied.
        /// </summary>
        public const string FragmentSource = @"#version 330 core
in vec2 vUv;
in vec2 vUnderlayUv;
in vec4 vColor;
flat in float vBold;

uniform sampler2D uAtlas;       // Alpha8/R8 SDF atlas, rows bottom-up
uniform vec2  uAtlasSize;       // texels
uniform float uGradientScale;
uniform float uScaleRatioA;
uniform float uScaleRatioC;
uniform float uSharpness;
uniform float uFaceDilate;
uniform float uWeightNormal;
uniform float uWeightBold;
uniform float uOutlineWidth;
uniform float uOutlineSoftness;
uniform vec4  uFaceColor;
uniform vec4  uOutlineColor;
uniform vec4  uUnderlayColor;
uniform float uUnderlayDilate;
uniform float uUnderlaySoftness;
uniform int   uOutlineOn;       // 1 unless a Mobile material lacks OUTLINE_ON
uniform int   uUnderlayOn;      // UNDERLAY_ON
uniform vec4  uClipRect;        // xy min, zw max in window pixels; w<=y disables
uniform int   uClipEnabled;

out vec4 fragColor;

float sampleSdf(vec2 uv) { return texture(uAtlas, uv).r; }

void main()
{
    // Screen pixels per atlas texel, from derivatives of the texel coordinate.
    vec2 t = vUv * uAtlasSize;
    vec2 dx = dFdx(t), dy = dFdy(t);
    float texelsPerPixel = sqrt(0.5 * (dot(dx, dx) + dot(dy, dy)));
    float pxPerTexel = 1.0 / max(texelsPerPixel, 1e-6);
    float scale = max(1.41421356 * pxPerTexel * uGradientScale * (uSharpness + 1.0), 1e-4);

    float weight = (mix(uWeightNormal, uWeightBold, vBold) / 4.0 + uFaceDilate) * uScaleRatioA * 0.5;
    float bias = 0.5 - weight + 0.5 / scale;
    float opacity = vColor.a;

    vec4 face = vec4(vColor.rgb * uFaceColor.rgb, opacity * uFaceColor.a);
    vec4 outline = vec4(uOutlineColor.rgb, uOutlineColor.a * opacity);

    float d = sampleSdf(vUv);
    float sd = (bias - d) * scale;
    float ow = uOutlineOn != 0 ? uOutlineWidth * uScaleRatioA * scale : 0.0;
    float soft = uOutlineOn != 0 ? uOutlineSoftness * uScaleRatioA * scale : 0.0;

    float faceAlpha = 1.0 - clamp((sd - ow * 0.5 + soft * 0.5) / (1.0 + soft), 0.0, 1.0);
    float outlineAlpha = clamp(sd + ow * 0.5, 0.0, 1.0) * sqrt(min(1.0, ow));

    face.rgb *= face.a;
    outline.rgb *= outline.a;
    vec4 c = mix(face, outline, outlineAlpha) * faceAlpha;

    if (uUnderlayOn != 0)
    {
        float bScale = scale / (1.0 + uUnderlaySoftness * uScaleRatioC * scale);
        float bBias = (0.5 - weight) * bScale - 0.5 - uUnderlayDilate * uScaleRatioC * 0.5 * bScale;
        float ua = clamp(sampleSdf(vUnderlayUv) * bScale - bBias, 0.0, 1.0);
        float uaA = uUnderlayColor.a * opacity;
        c += vec4(uUnderlayColor.rgb * uaA, uaA) * ua * (1.0 - c.a);
    }

    if (uClipEnabled != 0)
    {
        vec2 p = gl_FragCoord.xy;
        if (p.x < uClipRect.x || p.y < uClipRect.y || p.x >= uClipRect.z || p.y >= uClipRect.w) discard;
    }
    fragColor = c;   // premultiplied: glBlendFunc(GL_ONE, GL_ONE_MINUS_SRC_ALPHA)
}
";
    }
}
