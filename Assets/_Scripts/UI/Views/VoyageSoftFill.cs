using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A fill with no hard edge, for the Voyage window's two screens (Rev 96-98, Docs/MENU_REDESIGN_B.md sections
    /// 181-184; Shombith: "the button in both the screens are too hard not soft hard soft").
    /// <list type="bullet">
    /// <item><see cref="Shape.Band"/>: solid in the middle, fading out to both ends (a button's fill, the band behind a
    /// group of choices, a hairline).</item>
    /// <item><see cref="Shape.Glow"/>: an ellipse, its colour at the centre fading to nothing at the rim (the light
    /// under a picked picture).</item>
    /// </list>
    /// Drawn with vertex alpha, so it needs no texture and takes the graphic's colour like any other.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageSoftFill : MaskableGraphic
    {
        public enum Shape
        {
            Band = 0,
            Glow = 1,
        }

        [Tooltip("Band: solid in the middle, fading to both ends. Glow: an ellipse fading to its rim.")]
        [SerializeField] Shape shape = Shape.Band;
        [Tooltip("How much of the band's width each soft end takes (0-0.5).")]
        [Range(0f, 0.5f)] [SerializeField] float soft = 0.2f;

        const int GlowSegments = 28;

        /// <summary>Set the shape and, for a band, how much of its width each soft end takes.</summary>
        public void Set(Shape newShape, float newSoft)
        {
            shape = newShape;
            soft = Mathf.Clamp(newSoft, 0f, 0.5f);
            SetVerticesDirty();
        }

        /// <summary>The band's opacity at <paramref name="t"/> (0 the left end, 1 the right) for soft ends
        /// <paramref name="soft"/> wide: 0 at the ends, 1 across the middle.</summary>
        public static float BandAlpha(float t, float soft)
        {
            if (soft <= 0f) return 1f;
            return Mathf.Clamp01(Mathf.Min(t, 1f - t) / soft);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;
            Color32 full = color, none = color;
            none.a = 0;
            if (shape == Shape.Glow)
            {
                vh.AddVert(r.center, full, Vector2.zero);
                for (int i = 0; i <= GlowSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / GlowSegments;
                    vh.AddVert(r.center + new Vector2(Mathf.Cos(a) * r.width * 0.5f, Mathf.Sin(a) * r.height * 0.5f), none, Vector2.zero);
                    if (i > 0) vh.AddTriangle(0, i, i + 1);
                }
                return;
            }
            // Four columns of two vertices: clear, full, full, clear.
            float s = Mathf.Clamp(soft, 0f, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                float t = i switch { 0 => 0f, 1 => s, 2 => 1f - s, _ => 1f };
                float x = Mathf.Lerp(r.xMin, r.xMax, t);
                var c = i == 0 || i == 3 ? none : full;
                vh.AddVert(new Vector3(x, r.yMin), c, Vector2.zero);
                vh.AddVert(new Vector3(x, r.yMax), c, Vector2.zero);
                if (i == 0) continue;
                int v = i * 2;
                vh.AddTriangle(v - 2, v - 1, v + 1);
                vh.AddTriangle(v - 2, v + 1, v);
            }
        }
    }
}
