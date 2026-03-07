using UnityEngine;

namespace CosmicShore.App.UI
{
    /// <summary>
    /// Constrains a RectTransform to a maximum aspect ratio, adding pillarboxing
    /// on ultra-wide displays. Attach to a full-screen UI panel that should not
    /// stretch beyond the given max aspect ratio.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class WidescreenLayoutAdapter : MonoBehaviour
    {
        [Tooltip("Maximum allowed aspect ratio (width / height). " +
                 "Content wider than this gets pillarboxed. Default 2.17 ≈ 19.5:9.")]
        [SerializeField] private float maxAspectRatio = 2.17f;

        private RectTransform _rt;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplyConstraint();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyConstraint();
        }

        private void ApplyConstraint()
        {
            if (_rt == null) return;

            float screenAspect = (float)Screen.width / Screen.height;
            if (screenAspect <= maxAspectRatio)
            {
                // Within allowed aspect ratio — fill the screen
                _rt.anchorMin = Vector2.zero;
                _rt.anchorMax = Vector2.one;
                _rt.offsetMin = Vector2.zero;
                _rt.offsetMax = Vector2.zero;
                return;
            }

            // Pillarbox: keep height at 100%, constrain width
            float targetWidth = maxAspectRatio / screenAspect;
            float inset = (1f - targetWidth) / 2f;

            _rt.anchorMin = new Vector2(inset, 0f);
            _rt.anchorMax = new Vector2(1f - inset, 1f);
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
