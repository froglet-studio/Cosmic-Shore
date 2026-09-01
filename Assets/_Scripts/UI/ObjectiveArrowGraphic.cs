using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Procedural chevron-arrow Graphic for off-screen objective indicators.
    /// Renders three stacked concave hexagons - soft glow halo, mid stroke,
    /// bright inner core - with no sprite/texture/font dependency.
    ///
    /// Shape is a right-pointing chevron with a notched (V-cut) back so the
    /// silhouette reads as an arrow rather than a generic shape. Points right
    /// at rotation 0; rotate the host RectTransform to aim it. Pulse
    /// animation modulates scale + glow alpha each frame.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Cosmic Shore/Objective Arrow")]
    public class ObjectiveArrowGraphic : MaskableGraphic
    {
        // Lime-green palette - same hue (~85°), varying lightness + saturation.
        // Glow: low V, low S → muted dark lime. Outer: high V + high S → vivid
        // lime stroke. Inner: peak V + low S → near-white lime highlight.

        [Header("Colors (lime-green, varied L+S)")]
        [Tooltip("Faint halo behind the arrow.")]
        [SerializeField] Color glowColor = new Color(0.31f, 0.50f, 0.18f, 0.32f);

        [Tooltip("Mid stroke - the main visible silhouette.")]
        [SerializeField] Color outerColor = new Color(0.55f, 0.92f, 0.10f, 0.95f);

        [Tooltip("Bright inner core, sits on top.")]
        [SerializeField] Color innerColor = new Color(0.86f, 1.00f, 0.62f, 1.00f);

        [Header("Shape")]
        [Tooltip("How far the shoulder sits ahead of the rect centre (fraction of half-width).")]
        [Range(-0.4f, 0.6f)]
        [SerializeField] float shoulderX = 0.20f;

        [Tooltip("Shoulder height (fraction of half-height).")]
        [Range(0.4f, 1f)]
        [SerializeField] float shoulderY = 0.85f;

        [Tooltip("How far back the wingtips extend (fraction of half-width, negative = behind centre).")]
        [Range(-1f, -0.3f)]
        [SerializeField] float tailX = -0.85f;

        [Tooltip("Wingtip height (fraction of half-height).")]
        [Range(0.2f, 0.7f)]
        [SerializeField] float tailY = 0.45f;

        [Tooltip("How far the back-notch apex sits in front of the wingtips (fraction of half-width).")]
        [Range(-0.6f, 0.3f)]
        [SerializeField] float notchX = -0.40f;

        [Tooltip("Inner-core scale relative to the outer stroke.")]
        [Range(0.3f, 0.95f)]
        [SerializeField] float innerScale = 0.58f;

        [Tooltip("Glow halo scale relative to the outer stroke.")]
        [Range(1f, 1.6f)]
        [SerializeField] float glowScale = 1.22f;

        [Header("Pulse Animation")]
        [SerializeField] bool pulse = true;
        [Range(0.5f, 6f)]
        [SerializeField] float pulseSpeed = 2.6f;
        [Range(0f, 0.3f)]
        [SerializeField] float pulseScaleAmplitude = 0.08f;
        [Range(0f, 0.5f)]
        [SerializeField] float pulseGlowAmplitude = 0.22f;

        float _currentScale = 1f;
        float _currentRendererAlpha = 1f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;
            float halfW = r.width * 0.5f;
            float halfH = r.height * 0.5f;
            float cx = r.center.x;
            float cy = r.center.y;

            // Back-to-front: glow halo, outer stroke, bright core.
            // Mesh is static - pulse animation modulates canvasRenderer alpha
            // and rectTransform scale at runtime, neither of which rebuilds
            // this mesh.
            AddChevron(vh, cx, cy, halfW * glowScale, halfH * glowScale, glowColor);
            AddChevron(vh, cx, cy, halfW, halfH, outerColor);
            AddChevron(vh, cx, cy, halfW * innerScale, halfH * innerScale, innerColor);
        }

        /// <summary>
        /// Appends a six-vertex chevron (right-pointing arrow with notched back)
        /// to the mesh. The polygon is concave at the notch, so we split it
        /// into top + bottom convex quads and triangulate each as a fan.
        /// </summary>
        void AddChevron(VertexHelper vh, float cx, float cy, float w, float h, Color c)
        {
            int b = vh.currentVertCount;

            // Six perimeter vertices, CCW from tip.
            vh.AddVert(MakeVert(new Vector2(cx + w,                cy),                c)); // 0 tip
            vh.AddVert(MakeVert(new Vector2(cx + w * shoulderX,    cy + h * shoulderY), c)); // 1 top-shoulder
            vh.AddVert(MakeVert(new Vector2(cx + w * tailX,        cy + h * tailY),     c)); // 2 top-wingtip
            vh.AddVert(MakeVert(new Vector2(cx + w * notchX,       cy),                c)); // 3 notch apex
            vh.AddVert(MakeVert(new Vector2(cx + w * tailX,        cy - h * tailY),     c)); // 4 bottom-wingtip
            vh.AddVert(MakeVert(new Vector2(cx + w * shoulderX,    cy - h * shoulderY), c)); // 5 bottom-shoulder

            // Top half (convex quad 0-1-2-3, fan from tip).
            vh.AddTriangle(b + 0, b + 1, b + 2);
            vh.AddTriangle(b + 0, b + 2, b + 3);
            // Bottom half (convex quad 0-3-4-5, fan from tip).
            vh.AddTriangle(b + 0, b + 3, b + 4);
            vh.AddTriangle(b + 0, b + 4, b + 5);
        }

        static UIVertex MakeVert(Vector2 pos, Color c)
        {
            return new UIVertex { position = pos, color = c, uv0 = Vector2.zero };
        }

        void Update()
        {
            if (!pulse)
            {
                if (_currentScale != 1f)
                {
                    _currentScale = 1f;
                    rectTransform.localScale = Vector3.one;
                }
                if (_currentRendererAlpha != 1f)
                {
                    _currentRendererAlpha = 1f;
                    canvasRenderer.SetAlpha(1f);
                }
                return;
            }

            float t = Mathf.Sin(Time.unscaledTime * pulseSpeed);

            float scale = 1f + t * pulseScaleAmplitude;
            if (scale != _currentScale)
            {
                _currentScale = scale;
                rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            // Pulse the whole graphic's canvasRenderer alpha - does NOT
            // rebuild the mesh. The previous implementation mutated the glow
            // chevron's vertex colors via SetVerticesDirty every frame, which
            // forced OnPopulateMesh + a parent Canvas batch rebuild every
            // frame - extremely expensive when this lives on a shared HUD
            // canvas alongside many other Graphics.
            float rendererAlpha = Mathf.Clamp01(1f + t * pulseGlowAmplitude);
            if (rendererAlpha != _currentRendererAlpha)
            {
                _currentRendererAlpha = rendererAlpha;
                canvasRenderer.SetAlpha(rendererAlpha);
            }
        }
    }
}
