using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Procedural arrow Graphic for off-screen objective indicators. Renders
    /// three stacked triangles — soft glow halo, mid stroke, bright inner core
    /// — with no sprite/texture/font dependency.
    ///
    /// Points right at rotation 0; rotate the host RectTransform to aim it.
    /// Pulse animation modulates scale + glow alpha each frame.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Cosmic Shore/Objective Arrow")]
    public class ObjectiveArrowGraphic : MaskableGraphic
    {
        [Header("Colors")]
        [Tooltip("Faint halo behind the arrow.")]
        [SerializeField] Color glowColor = new Color(1f, 0.42f, 0.08f, 0.35f);
        [Tooltip("Mid stroke — the main visible silhouette.")]
        [SerializeField] Color outerColor = new Color(1f, 0.6f, 0.14f, 0.95f);
        [Tooltip("Bright inner core, sits on top.")]
        [SerializeField] Color innerColor = new Color(1f, 0.97f, 0.55f, 1f);

        [Header("Shape")]
        [Tooltip("Half-height at the back of the arrow as a fraction of rect height. Lower = more pointed.")]
        [Range(0.4f, 1f)]
        [SerializeField] float backHeightFraction = 0.72f;

        [Tooltip("Inner-core size as a fraction of the outer stroke.")]
        [Range(0.3f, 0.95f)]
        [SerializeField] float innerScale = 0.55f;

        [Tooltip("Glow halo size relative to the outer stroke.")]
        [Range(1f, 1.6f)]
        [SerializeField] float glowScale = 1.28f;

        [Header("Pulse Animation")]
        [SerializeField] bool pulse = true;
        [Range(0.5f, 6f)]
        [SerializeField] float pulseSpeed = 2.6f;
        [Range(0f, 0.3f)]
        [SerializeField] float pulseScaleAmplitude = 0.08f;
        [Range(0f, 0.5f)]
        [SerializeField] float pulseGlowAmplitude = 0.22f;

        float _glowAlphaPhase;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;
            float halfW = r.width * 0.5f;
            float halfH = r.height * 0.5f;
            float cx = r.center.x;
            float cy = r.center.y;

            // Draw back-to-front so the bright core sits on top.
            AddArrow(vh, cx, cy,
                     halfW * glowScale,
                     halfH * backHeightFraction * glowScale,
                     OffsetAlpha(glowColor, _glowAlphaPhase));

            AddArrow(vh, cx, cy,
                     halfW,
                     halfH * backHeightFraction,
                     outerColor);

            AddArrow(vh, cx, cy,
                     halfW * innerScale,
                     halfH * backHeightFraction * innerScale,
                     innerColor);
        }

        /// <summary>Appends a right-pointing isosceles triangle to the vertex helper.</summary>
        static void AddArrow(VertexHelper vh, float cx, float cy, float w, float h, Color c)
        {
            int baseIdx = vh.currentVertCount;
            vh.AddVert(MakeVert(new Vector2(cx + w, cy), c));      // tip (right)
            vh.AddVert(MakeVert(new Vector2(cx - w, cy + h), c));  // top-back
            vh.AddVert(MakeVert(new Vector2(cx - w, cy - h), c));  // bottom-back
            vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
        }

        static UIVertex MakeVert(Vector2 pos, Color c)
        {
            return new UIVertex { position = pos, color = c, uv0 = Vector2.zero };
        }

        static Color OffsetAlpha(Color baseColor, float extraAlpha)
        {
            baseColor.a = Mathf.Clamp01(baseColor.a + extraAlpha);
            return baseColor;
        }

        void Update()
        {
            if (!pulse)
            {
                if (_glowAlphaPhase != 0f)
                {
                    _glowAlphaPhase = 0f;
                    rectTransform.localScale = Vector3.one;
                    SetVerticesDirty();
                }
                return;
            }

            float t = Mathf.Sin(Time.unscaledTime * pulseSpeed);

            float scale = 1f + t * pulseScaleAmplitude;
            rectTransform.localScale = new Vector3(scale, scale, 1f);

            // Glow alpha pulses with the same phase as scale so the halo
            // breathes outward in sync.
            float glowPhase = t * pulseGlowAmplitude;
            if (!Mathf.Approximately(glowPhase, _glowAlphaPhase))
            {
                _glowAlphaPhase = glowPhase;
                SetVerticesDirty();
            }
        }
    }
}
