using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Two authored colours across a graphic, along an angle: the Port's sea-lit look (Docs/MENU_REDESIGN_B.md section
    /// 91, the Rev 32 D board: "Sea-lit panels", Shombith 2026-10-27 "Go with D"). A message card and the message field
    /// are lit from their top-left corner, periwinkle into violet, the sea lines' own colours.
    ///
    /// <para>Writes vertex colours over the graphic's quads (a sliced Image keeps its slivered corners), multiplied by the
    /// graphic's own colour, so a CanvasGroup fade or a Button tint still applies. <see cref="KitGradient"/> is the PLAY
    /// button's one-colour lighten; this one takes both ends as data, so the look lives in the prefab, not the code.</para>
    ///
    /// <para>Both ends default to clear: a gradient nobody authored draws nothing rather than a colour nobody chose.</para>
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Two Tone Gradient")]
    public class KitTwoToneGradient : BaseMeshEffect
    {
        [Tooltip("The colour where the gradient starts (the lit corner).")]
        [SerializeField] private Color from;
        [Tooltip("The colour where it ends.")]
        [SerializeField] private Color to;
        [Tooltip("The direction, in degrees: 0 runs left to right, 90 bottom to top, -45 top-left to bottom-right.")]
        [SerializeField, Range(-180f, 180f)] private float angle = -45f;

        public Color From => from;
        public Color To => to;

        /// <summary>Repaints with new ends (a message card switching between yours and everyone else's look).</summary>
        public void SetColours(Color start, Color end)
        {
            from = start;
            to = end;
            if (graphic) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            Rect rect = graphic.rectTransform.rect;
            float rad = angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            // Project the rect's corners so t runs 0..1 exactly across the graphic, whatever the angle.
            float a = Vector2.Dot(new Vector2(rect.xMin, rect.yMin), dir), b = Vector2.Dot(new Vector2(rect.xMax, rect.yMin), dir);
            float c = Vector2.Dot(new Vector2(rect.xMin, rect.yMax), dir), d = Vector2.Dot(new Vector2(rect.xMax, rect.yMax), dir);
            float min = Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d)), max = Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));

            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = max > min ? Mathf.InverseLerp(min, max, Vector2.Dot(vertex.position, dir)) : 0f;
                vertex.color = (Color32)(Color.Lerp(from, to, t) * (Color)vertex.color);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
