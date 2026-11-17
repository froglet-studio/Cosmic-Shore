using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A soft glow along a card's four edges (the Ark page's seat cards; Shombith, 2026-11-16: "glowing borders at the edges,
    /// blue coloured"): <see cref="Graphic.color"/> at the edge, fading to clear <see cref="Outside"/> units out and
    /// <see cref="Inside"/> units in, with rounded-off corners. No fill; never takes a press.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageEdgeGlow : MaskableGraphic
    {
        /// <summary>How far the glow reaches outside the card's edge.</summary>
        public float Outside = 14f;
        /// <summary>How far the glow reaches inside the card's edge.</summary>
        public float Inside = 6f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width < 2f || r.height < 2f) return;
            var lit = (Color32)color;
            var clear = new Color32(lit.r, lit.g, lit.b, 0);
            if (Outside > 0f) Band(vh, r, Outside, lit, clear);
            if (Inside > 0f) Band(vh, r, -Mathf.Min(Inside, Mathf.Min(r.width, r.height) * 0.5f), lit, clear);
        }

        // A band round the rect's edge: lit on the edge, clear <paramref name="reach"/> away (out when positive, in when
        // negative). Each side is a quad; each corner a fan of quads, so an outside glow turns its corners round.
        static void Band(VertexHelper vh, Rect r, float reach, Color32 lit, Color32 clear)
        {
            Vector2 a = new(r.xMin, r.yMin), b = new(r.xMin, r.yMax), c = new(r.xMax, r.yMax), d = new(r.xMax, r.yMin);
            Side(vh, a, b, new Vector2(-reach, 0f), lit, clear);
            Side(vh, b, c, new Vector2(0f, reach), lit, clear);
            Side(vh, c, d, new Vector2(reach, 0f), lit, clear);
            Side(vh, d, a, new Vector2(0f, -reach), lit, clear);
            if (reach <= 0f) return;
            Corner(vh, a, 180f, reach, lit, clear);
            Corner(vh, b, 90f, reach, lit, clear);
            Corner(vh, c, 0f, reach, lit, clear);
            Corner(vh, d, 270f, reach, lit, clear);
        }

        static void Side(VertexHelper vh, Vector2 from, Vector2 to, Vector2 off, Color32 lit, Color32 clear)
        {
            int i = vh.currentVertCount;
            vh.AddVert(from, lit, Vector2.zero);
            vh.AddVert(to, lit, Vector2.zero);
            vh.AddVert(to + off, clear, Vector2.zero);
            vh.AddVert(from + off, clear, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        // A quarter circle about corner <paramref name="at"/>, from <paramref name="startDeg"/> round 90 degrees.
        static void Corner(VertexHelper vh, Vector2 at, float startDeg, float reach, Color32 lit, Color32 clear)
        {
            const int Steps = 6;
            int centre = vh.currentVertCount;
            vh.AddVert(at, lit, Vector2.zero);
            for (int k = 0; k <= Steps; k++)
            {
                float a = (startDeg + 90f * k / Steps) * Mathf.Deg2Rad;
                vh.AddVert(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach, clear, Vector2.zero);
            }
            for (int k = 0; k < Steps; k++) vh.AddTriangle(centre, centre + 1 + k, centre + 2 + k);
        }
    }
}
