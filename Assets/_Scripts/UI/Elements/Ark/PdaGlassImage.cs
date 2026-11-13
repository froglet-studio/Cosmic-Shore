using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A picture laid on a flat surface the Ark's backdrop shows in perspective: the crew PDA's glass lying on the hangar's
    /// workbench (Rev 99 C, Docs/MENU_REDESIGN_B.md section 191). The four corners are where the glass is in the backdrop,
    /// as fractions of this rectangle (which covers the picture exactly, as every Ark page does); the sprite is cut to the
    /// glass's own proportion and drawn through the homography those corners make, on a grid fine enough that the
    /// picture does not bend along the quad's diagonal.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PdaGlassImage : MaskableGraphic
    {
        [Tooltip("What the glass shows. Empty draws nothing.")]
        [SerializeField] Sprite sprite;
        [Tooltip("The glass's corners in the backdrop, top-left, top-right, bottom-right, bottom-left, as fractions of " +
                 "this rectangle measured from its top-left (the render's own pixel corners over its size).")]
        [SerializeField] Vector2[] corners = { new(0.4f, 0.75f), new(0.6f, 0.75f), new(0.62f, 0.9f), new(0.38f, 0.9f) };
        [Tooltip("The glass's width over its height, flat (the picture is cut to it).")]
        [SerializeField, Min(0.1f)] float glassAspect = 1.38f;
        [Tooltip("Where the cut sits in the sprite's height: 0 its top, 0.5 its middle (a close-up's face is high).")]
        [SerializeField, Range(0f, 1f)] float cutFromTop = 0.3f;
        [Tooltip("Cells along each side of the grid the picture is drawn on.")]
        [SerializeField, Range(1, 16)] int grid = 8;

        public override Texture mainTexture => sprite ? sprite.texture : s_WhiteTexture;

        /// <summary>What the glass shows; null shows nothing.</summary>
        public Sprite Sprite
        {
            get => sprite;
            set
            {
                if (sprite == value) return;
                sprite = value;
                SetAllDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!sprite || corners is not { Length: 4 }) return;

            Rect r = rectTransform.rect;
            // The corners in local space (y up), from fractions measured from the top-left.
            var p = new Vector2[4];
            for (int i = 0; i < 4; i++)
                p[i] = new Vector2(r.xMin + corners[i].x * r.width, r.yMax - corners[i].y * r.height);

            Rect uv = CoverUv();
            int n = Mathf.Max(1, grid);
            var c = (Color32)color;
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n, v = (float)j / n;   // v down the glass, from its top edge
                vh.AddVert(Project(p, u, v), c, new Vector2(Mathf.Lerp(uv.xMin, uv.xMax, u), Mathf.Lerp(uv.yMax, uv.yMin, v)));
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, d = a + n + 1, e = d + 1;
                vh.AddTriangle(a, b, e);
                vh.AddTriangle(a, e, d);
            }
        }

        // The part of the sprite (in texture UVs) that fills the glass's proportion, centred across, `cutFromTop` down.
        Rect CoverUv()
        {
            Rect t = sprite.textureRect;
            float tw = sprite.texture.width, th = sprite.texture.height;
            float w = t.width, h = t.height;
            if (w / h > glassAspect)
            {
                float cw = h * glassAspect;
                t.x += (w - cw) * 0.5f;
                t.width = cw;
            }
            else
            {
                float ch = w / glassAspect;
                t.y += (h - ch) * (1f - cutFromTop);   // texture y is up: the cut's top is cutFromTop of the spare from the top
                t.height = ch;
            }
            return new Rect(t.x / tw, t.y / th, t.width / tw, t.height / th);
        }

        // The point (u across, v down) of the unit square under the homography that takes its corners to p (TL TR BR BL).
        static Vector2 Project(Vector2[] p, float u, float v)
        {
            // The square-to-quad map (Heckbert): x = (a u + b v + c) / (g u + h v + 1), the same for y.
            Vector2 p0 = p[0], p1 = p[1], p2 = p[3], p3 = p[2];   // (0,0) (1,0) (0,1) (1,1)
            Vector2 s = p0 - p1 + p3 - p2;
            float g = 0f, hh = 0f;
            Vector2 d1 = p1 - p3, d2 = p2 - p3;
            float den = d1.x * d2.y - d2.x * d1.y;
            if (Mathf.Abs(den) > 1e-6f && s.sqrMagnitude > 1e-8f)
            {
                g = (s.x * d2.y - d2.x * s.y) / den;
                hh = (d1.x * s.y - s.x * d1.y) / den;
            }
            Vector2 a = p1 - p0 + g * p1, b = p2 - p0 + hh * p2;
            float w = g * u + hh * v + 1f;
            return (a * u + b * v + p0) / w;
        }
    }
}
