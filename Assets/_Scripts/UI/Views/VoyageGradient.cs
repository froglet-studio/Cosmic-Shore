using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A square fill shading from <see cref="Top"/> through <see cref="Middle"/> (at <see cref="MiddleAt"/> of the way
    /// down) to <see cref="Bottom"/>: the Voyage pages' glass (Frogma Rev 108-111, Docs/MENU_REDESIGN_B.md section 205: the
    /// outline's blue at the top fading into the deep popup ground by half way) and the page's own ground behind the
    /// board. Drawn with vertex colours, no texture. Each stop's colour is multiplied by <see cref="Graphic.color"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageGradient : MaskableGraphic
    {
        public Color Top = Color.white, Middle = Color.white, Bottom = Color.white;
        [Range(0.01f, 0.99f)] public float MiddleAt = 0.55f;

        /// <summary>Set the three stops at once.</summary>
        public void Set(Color top, Color middle, Color bottom, float middleAt = 0.55f)
        {
            Top = top;
            Middle = middle;
            Bottom = bottom;
            MiddleAt = Mathf.Clamp(middleAt, 0.01f, 0.99f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;
            float yMid = Mathf.Lerp(r.yMax, r.yMin, MiddleAt);
            Color tint = color;
            AddRow(vh, r, r.yMax, Top * tint);
            AddRow(vh, r, yMid, Middle * tint);
            AddRow(vh, r, r.yMin, Bottom * tint);
            vh.AddTriangle(0, 1, 3);
            vh.AddTriangle(0, 3, 2);
            vh.AddTriangle(2, 3, 5);
            vh.AddTriangle(2, 5, 4);
        }

        static void AddRow(VertexHelper vh, Rect r, float y, Color c)
        {
            vh.AddVert(new Vector3(r.xMin, y), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, y), c, Vector2.zero);
        }
    }
}
