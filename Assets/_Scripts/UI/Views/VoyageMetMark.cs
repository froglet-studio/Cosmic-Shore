using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// How well a vessel meets an asked seat (Frogma Rev 146 A, Docs/MENU_REDESIGN_B.md section 236): one thin ring in
    /// <see cref="Graphic.color"/> with a dot inside it, whole when the seat is met fully (the vessel's primary), its left
    /// half when met by half (a second or third archetype), none when it is not met. Fills its rect; never takes a press.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageMetMark : MaskableGraphic
    {
        const int Segments = 28;
        const float Line = 1.5f, Dot = 0.55f;

        float _meets;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>Show a seat met by <paramref name="meets"/> (0, 0.5 or 1) in <paramref name="ink"/>.</summary>
        public void Set(float meets, Color ink)
        {
            _meets = meets;
            color = ink;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width < 1f || r.height < 1f) return;
            var c = r.center;
            float outer = Mathf.Min(r.width, r.height) * 0.5f, inner = outer - Line;
            // The ring.
            for (int i = 0; i <= Segments; i++)
            {
                var d = Dir(i * 360f / Segments);
                vh.AddVert(c + d * outer, color, Vector2.zero);
                vh.AddVert(c + d * inner, color, Vector2.zero);
                if (i == 0) continue;
                int v = i * 2;
                vh.AddTriangle(v - 2, v - 1, v + 1);
                vh.AddTriangle(v - 2, v + 1, v);
            }
            if (_meets <= 0f) return;
            // The dot: whole when met fully, the left half (90 to 270 degrees) when met by half.
            float from = _meets >= 1f ? 0f : 90f, sweep = _meets >= 1f ? 360f : 180f, radius = outer * Dot;
            int start = vh.currentVertCount;
            vh.AddVert(c, color, Vector2.zero);
            int n = Segments / (_meets >= 1f ? 1 : 2);
            for (int i = 0; i <= n; i++)
            {
                vh.AddVert(c + Dir(from + sweep * i / n) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        static Vector2 Dir(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }
}
