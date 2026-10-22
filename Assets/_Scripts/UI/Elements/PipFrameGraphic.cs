using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The frame <see cref="PipViewFrame"/> draws round a picture-in-picture camera: its rect is the PICTURE, and the frame
    /// grows outward from it - a Light hairline hugging the picture along a slivered outline, then the Neutral (Dark)
    /// chrome band out to a plain rectangle. At the two cut corners the band widens to fill the corner, so the picture
    /// reads slivered and the frame's outside edge stays clean (<c>Docs/STYLE_FOUNDATION.md</c> §5). One mesh, rebuilt
    /// only when the size or style changes.
    /// </summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PipFrameGraphic : MaskableGraphic
    {
        Color _edge, _chrome;
        float _edgeWidth, _chromeWidth, _sliver;
        bool _flip;

        static readonly Vector2[] s_inner = new Vector2[6], s_mid = new Vector2[6], s_outer = new Vector2[6];
        // how much longer a 45 degree cut's legs get, measured from the grown rect's corner, per unit the outline grows: the
        // cut line moves out by d along its normal, so each leg gains d (2 - sqrt 2) and the band keeps one width all round
        static readonly float Grow = 2f - Mathf.Sqrt(2f);

        /// <summary>Set the frame's look (in canvas pixels); rebuilds the mesh only when something changed.</summary>
        public void SetStyle(Color edge, float edgeWidth, Color chrome, float chromeWidth, float sliver, bool flip)
        {
            if (edge == _edge && chrome == _chrome && Mathf.Approximately(edgeWidth, _edgeWidth) &&
                Mathf.Approximately(chromeWidth, _chromeWidth) && Mathf.Approximately(sliver, _sliver) && flip == _flip) return;
            _edge = edge; _chrome = chrome; _edgeWidth = edgeWidth; _chromeWidth = chromeWidth; _sliver = sliver; _flip = flip;
            SetVerticesDirty();
        }

        /// <summary>The outer reach of the frame beyond the picture, in canvas pixels.</summary>
        public float Reach => _edgeWidth + _chromeWidth;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float s = Mathf.Min(_sliver, 0.5f * Mathf.Min(r.width, r.height));
            if (r.width <= 0f || r.height <= 0f) return;

            Outline(r, s, 0f, s_inner);
            Outline(r, s, _edgeWidth, s_mid);
            Outline(r, -1f, _edgeWidth + _chromeWidth, s_outer);   // square: the band fills the cut corners

            Ring(vh, s_mid, s_outer, _chrome);
            Ring(vh, s_inner, s_mid, _edge);
        }

        // The picture's outline offset outward by d: a rect with two opposite corners cut at 45 degrees (s < 0: not cut, the
        // cut corners' two points coincide). Corner order is anticlockwise from the bottom-left, each cut corner
        // contributing two points, the others one (six in all), so any two outlines pair up into a ring.
        void Outline(Rect r, float s, float d, Vector2[] o)
        {
            float x0 = r.xMin - d, x1 = r.xMax + d, y0 = r.yMin - d, y1 = r.yMax + d, c = s < 0f ? 0f : s + d * Grow;
            if (!_flip)
            {
                // cut top-left and bottom-right
                o[0] = new Vector2(x0, y0);
                o[1] = new Vector2(x1 - c, y0);
                o[2] = new Vector2(x1, y0 + c);
                o[3] = new Vector2(x1, y1);
                o[4] = new Vector2(x0 + c, y1);
                o[5] = new Vector2(x0, y1 - c);
            }
            else
            {
                // cut top-right and bottom-left
                o[0] = new Vector2(x0 + c, y0);
                o[1] = new Vector2(x1, y0);
                o[2] = new Vector2(x1, y1 - c);
                o[3] = new Vector2(x1 - c, y1);
                o[4] = new Vector2(x0, y1);
                o[5] = new Vector2(x0, y0 + c);
            }
        }

        static void Ring(VertexHelper vh, Vector2[] inner, Vector2[] outer, Color color)
        {
            int v = vh.currentVertCount;
            for (int i = 0; i < 6; i++)
            {
                vh.AddVert(inner[i], color, Vector4.zero);
                vh.AddVert(outer[i], color, Vector4.zero);
            }
            for (int i = 0; i < 6; i++)
            {
                int a = v + 2 * i, b = v + 2 * ((i + 1) % 6);
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }
    }
}
