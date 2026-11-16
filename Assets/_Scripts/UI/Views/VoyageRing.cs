using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A ring or a diamond for the Voyages table's pins (Frogma Rev 111 Full, Docs/MENU_REDESIGN_B.md section 205): a line
    /// <see cref="Width"/> wide in <see cref="Graphic.color"/> round a <see cref="Fill"/>, filling the rect. The ring marks
    /// a voyage's place; the diamond marks home (the Ark). Never takes a press.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageRing : MaskableGraphic
    {
        public bool Diamond;
        public float Width = 2f;
        public Color Fill = new(0f, 0f, 0f, 0f);

        const int Segments = 32;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Set(Color line, Color fill)
        {
            color = line;
            Fill = fill;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width < 1f || r.height < 1f) return;
            int n = Diamond ? 4 : Segments;
            var c = r.center;
            float rx = r.width * 0.5f, ry = r.height * 0.5f, w = Mathf.Min(Width, Mathf.Min(rx, ry));
            Color line = color;
            Color fill = Fill * new Color(1f, 1f, 1f, color.a);
            // The fill: a fan inside the line.
            vh.AddVert(c, fill, Vector2.zero);
            for (int i = 0; i <= n; i++)
            {
                var d = Dir(i, n);
                vh.AddVert(c + new Vector2(d.x * (rx - w), d.y * (ry - w)), fill, Vector2.zero);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }
            // The line.
            int start = vh.currentVertCount;
            for (int i = 0; i <= n; i++)
            {
                var d = Dir(i, n);
                vh.AddVert(c + new Vector2(d.x * rx, d.y * ry), line, Vector2.zero);
                vh.AddVert(c + new Vector2(d.x * (rx - w), d.y * (ry - w)), line, Vector2.zero);
                if (i == 0) continue;
                int v = start + i * 2;
                vh.AddTriangle(v - 2, v - 1, v + 1);
                vh.AddTriangle(v - 2, v + 1, v);
            }
        }

        static Vector2 Dir(int i, int n)
        {
            float a = i * Mathf.PI * 2f / n + Mathf.PI * 0.5f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }
}
