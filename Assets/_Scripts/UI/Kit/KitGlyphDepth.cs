using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Gives a flat white glyph a baked-looking depth (Rev 144 G, picked 2026-11-24,
    /// Docs/MENU_REDESIGN_B.md section 235.2): a soft contact shadow, an extrusion stepping down and
    /// to the right through the slate surfaces, and a face lit from the top. Drawn as extra copies
    /// of the graphic's own quads, so the glyph stays the codex's one sprite
    /// (<see cref="ToyPortraitLibrary"/>): a redraw of the glyph re-skins its depth with nothing to
    /// re-bake, and the codex page keeps the flat drawing.
    ///
    /// <para>Every colour is a <see cref="UIThemeSO"/> token. Offsets are fractions of the rect, so
    /// the same component reads the same on a 140 px card glyph and a 96 px banner lens.</para>
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Glyph Depth")]
    public class KitGlyphDepth : BaseMeshEffect
    {
        [Header("Theme")]
        [Tooltip("The chrome theme the depth colours come from. Unwired falls back to the authored values.")]
        [SerializeField] UIThemeSO theme;

        [Tooltip("The extrusion's colour next to the face.")]
        [SerializeField] UIColorToken nearToken = UIColorToken.SurfaceLight;

        [Tooltip("The extrusion's colour at its far end.")]
        [SerializeField] UIColorToken farToken = UIColorToken.SurfaceDark;

        [Tooltip("The contact shadow's colour.")]
        [SerializeField] UIColorToken shadowToken = UIColorToken.SurfaceBlack;

        [Header("Shape")]
        [Tooltip("How many copies make the extrusion. More is smoother and costs one quad each.")]
        [Range(1, 24)]
        [SerializeField] int layers = 12;

        [Tooltip("The extrusion's full depth as a fraction of the rect's width (x right, y down).")]
        [SerializeField] Vector2 depth = new(0.026f, 0.034f);

        [Tooltip("The contact shadow's offset as a fraction of the rect's width (x right, y down).")]
        [SerializeField] Vector2 shadowOffset = new(0.04f, 0.055f);

        [Tooltip("The contact shadow's opacity, 0..1.")]
        [Range(0f, 1f)]
        [SerializeField] float shadowAlpha = 0.5f;

        [Tooltip("How much darker the face is at its foot than at its top, 0..1 (the light comes from above).")]
        [Range(0f, 1f)]
        [SerializeField] float faceFalloff = 0.18f;

        static readonly List<UIVertex> Source = new();
        static readonly List<UIVertex> Output = new();

        /// <summary>The extrusion layer count, for tests.</summary>
        public int Layers => layers;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            Source.Clear();
            Output.Clear();
            vh.GetUIVertexStream(Source);

            Rect rect = graphic.rectTransform.rect;
            float w = Mathf.Max(1f, rect.width);
            Color near = UITheme.Resolve(theme, nearToken);
            Color far = UITheme.Resolve(theme, farToken);
            Color shadow = UITheme.Resolve(theme, shadowToken);
            Color face = graphic.color;

            shadow.a *= shadowAlpha * face.a;
            Append(new Vector2(shadowOffset.x * w, -shadowOffset.y * w), shadow, 0f, rect);

            for (int i = layers; i >= 1; i--)
            {
                float t = (float)i / layers;
                Color c = Color.Lerp(near, far, t);
                c.a = face.a;
                Append(new Vector2(depth.x * w * t, -depth.y * w * t), c, 0f, rect);
            }

            Append(Vector2.zero, face, faceFalloff, rect);

            vh.Clear();
            vh.AddUIVertexTriangleStream(Output);
        }

        static void Append(Vector2 offset, Color color, float falloff, Rect rect)
        {
            for (int i = 0; i < Source.Count; i++)
            {
                var v = Source[i];
                v.position += (Vector3)offset;
                Color c = color;
                if (falloff > 0f && rect.height > 0f)
                {
                    float down = 1f - Mathf.InverseLerp(rect.yMin, rect.yMax, Source[i].position.y);
                    float k = 1f - falloff * down;
                    c.r *= k;
                    c.g *= k;
                    c.b *= k;
                }
                v.color = c;
                Output.Add(v);
            }
        }
    }
}
