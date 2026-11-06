using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drives <see cref="CanvasScaler.matchWidthOrHeight"/> from the live screen aspect ratio so a
    /// Scale-With-Screen-Size canvas renders correctly on any monitor (16:9, 16:10, 21:9, 32:9,
    /// 4:3, portrait). Place next to the <see cref="CanvasScaler"/> on the root canvas.
    ///
    /// At and above the reference aspect (16:9) the scaler matches HEIGHT (match = 1) so ultrawide
    /// displays gain horizontal play space instead of enlarging the HUD. Below it the scaler blends
    /// linearly toward matching WIDTH (match = 0) across <see cref="blendRange"/> so narrower/taller
    /// displays never crop UI horizontally. At exactly 16:9 (with a 1920x1080 reference resolution)
    /// both match values produce the identical scale factor, so the blend is seam-free.
    ///
    /// Optional HUD safe zone: assign a child RectTransform holding HUD content and it is pinned to
    /// a centered region capped at <see cref="safeZoneMaxAspect"/> on ultrawide displays. The
    /// constraint is pure anchor math (anchors are normalized against the canvas rect, which always
    /// carries the screen's aspect), so it is exact without waiting for a canvas layout pass.
    /// Leave null to disable — off by default. Sibling concept to <see cref="WidescreenLayoutAdapter"/>,
    /// which pillarboxes a single full-screen panel.
    ///
    /// Runtime cost: two cached-int comparisons per frame; all real work happens only on the frame
    /// the resolution changes (window resize, monitor hop, fullscreen toggle — resizes are also
    /// caught event-style via OnRectTransformDimensionsChange on the driven canvas rect). Zero
    /// allocations, no LINQ, no string ops, no GetComponent outside initialization.
    /// [ExecuteAlways] keeps the Game view preview correct in edit mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public class AdaptiveCanvasScaler : MonoBehaviour
    {
        [Header("Aspect Matching")]
        [Tooltip("Aspect ratio (width / height) at and above which the scaler fully matches height (match = 1). Default 16:9.")]
        [SerializeField] private float referenceAspect = 16f / 9f;

        [Tooltip("Aspect span below the reference aspect over which matchWidthOrHeight blends 1 -> 0 " +
                 "instead of hard-switching. 0.15 reaches full width-match just above 16:10.")]
        [SerializeField, Range(0.01f, 0.5f)] private float blendRange = 0.15f;

        [Header("HUD Safe Zone (optional)")]
        [Tooltip("Optional: a direct child of the canvas holding HUD content. On displays wider than " +
                 "Safe Zone Max Aspect it is pinned to a centered region of that aspect, so HUD " +
                 "corners don't drift to the far edges of a 32:9 monitor. Leave null to disable.")]
        [SerializeField] private RectTransform safeZone;

        [Tooltip("Maximum aspect ratio the safe zone may span before being pinned to a centered region. Default 16:9.")]
        [SerializeField] private float safeZoneMaxAspect = 16f / 9f;

        [Header("Portrait (Docs/MENU_REDESIGN_B.md section 138)")]
        [Tooltip("This canvas's screens have an upright layout (Rev 63 B: the same PC UI re-flowed). In play mode, when the " +
                 "screen is taller than it is wide, the canvas switches to the Portrait Reference Resolution matched to the " +
                 "width and every PortraitRect under it takes its upright pose. Off: the PC layout is matched to the width.")]
        [SerializeField] private bool hasPortraitLayout;
        [Tooltip("The canvas reference when upright: 1080 x 1920, the PC pieces at their PC sizes on a 1080-wide canvas.")]
        [SerializeField] private Vector2 portraitReferenceResolution = new(1080f, 1920f);

        private CanvasScaler _scaler;
        private Vector2 _authoredReference;
        private bool _authoredReferenceRead;
        private PortraitRect[] _pcBlocks = Array.Empty<PortraitRect>();
        private float _pcBlocksFitWidth = -1f;

        /// <summary>Whether this canvas is laid out upright right now.</summary>
        public bool UsesPortraitLayout { get; private set; }

        /// <summary>Raised after the canvas switches to or from its upright layout (true = upright).</summary>
        public event Action<bool> PortraitLayoutChanged;
        private int _lastWidth;
        private int _lastHeight;

        private void Awake() => _scaler = GetComponent<CanvasScaler>();

        private void OnEnable() => Apply();

        // The root canvas RectTransform is driven to the screen size, so this fires on window
        // resizes and Game-view aspect changes — including in edit mode, where Update is sparse.
        private void OnRectTransformDimensionsChange()
        {
            if (Screen.width == _lastWidth && Screen.height == _lastHeight) return;
            Apply();
        }

        private void Update()
        {
            // The canvas rect takes its new size a frame after the reference changes: re-fit the PC-layout blocks once it
            // has, and on any later resize. A float compare per frame while upright, nothing otherwise.
            if (UsesPortraitLayout && _pcBlocks.Length > 0)
            {
                float w = ((RectTransform)transform).rect.width;
                if (!Mathf.Approximately(w, _pcBlocksFitWidth))
                {
                    _pcBlocksFitWidth = w;
                    for (int i = 0; i < _pcBlocks.Length; i++)
                        if (_pcBlocks[i]) _pcBlocks[i].FitPcBlock();
                }
            }

            // Fallback for transitions that don't resize the canvas rect on the same frame
            // (monitor hops, some fullscreen toggles). Cost: two int compares.
            if (Screen.width == _lastWidth && Screen.height == _lastHeight) return;
            Apply();
        }

        private void OnValidate()
        {
            // Invalidate the cache; the next Update / dimensions-change tick re-applies the
            // edited values. (Applying inside OnValidate would touch RectTransforms during a
            // serialization callback, which Unity warns about.)
            _lastWidth = 0;
            _lastHeight = 0;
        }

        /// <summary>
        /// Recomputes <see cref="CanvasScaler.matchWidthOrHeight"/> and the safe-zone anchors for
        /// the current screen resolution. Called automatically on enable and on resolution change;
        /// public so editor tooling can force a refresh after reconfiguring the canvas.
        /// </summary>
        public void Apply()
        {
            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0) return;

            _lastWidth = width;
            _lastHeight = height;

            if (!_scaler) _scaler = GetComponent<CanvasScaler>();
            if (!_scaler) return;

            float aspect = (float)width / height;

            if (!_authoredReferenceRead)
            {
                _authoredReference = _scaler.referenceResolution;
                _authoredReferenceRead = true;
            }
            bool upright = hasPortraitLayout && Application.isPlaying && height > width
                           && _scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (upright)
            {
                if (_scaler.referenceResolution != portraitReferenceResolution) _scaler.referenceResolution = portraitReferenceResolution;
                if (!Mathf.Approximately(_scaler.matchWidthOrHeight, 0f)) _scaler.matchWidthOrHeight = 0f;
                SetPortrait(true);
                ApplySafeZone(aspect);
                return;
            }
            if (_scaler.referenceResolution != _authoredReference) _scaler.referenceResolution = _authoredReference;
            SetPortrait(false);

            if (_scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                // 1 at/above the reference aspect, 0 at/below (reference - blendRange), linear between.
                float match = Mathf.Clamp01((aspect - (referenceAspect - blendRange)) / blendRange);
                if (!Mathf.Approximately(_scaler.matchWidthOrHeight, match))
                    _scaler.matchWidthOrHeight = match;
            }

            ApplySafeZone(aspect);
        }

        private void SetPortrait(bool upright)
        {
            if (upright == UsesPortraitLayout) return;
            UsesPortraitLayout = upright;
            var rects = GetComponentsInChildren<PortraitRect>(true);
            int blocks = 0;
            for (int i = 0; i < rects.Length; i++)
            {
                rects[i].Apply(upright);
                if (rects[i].DrawsAtPcScale) blocks++;
            }
            _pcBlocks = new PortraitRect[upright ? blocks : 0];
            if (upright)
                for (int i = 0, n = 0; i < rects.Length; i++)
                    if (rects[i].DrawsAtPcScale) _pcBlocks[n++] = rects[i];
            _pcBlocksFitWidth = -1f;
            PortraitLayoutChanged?.Invoke(upright);
        }

        private void ApplySafeZone(float aspect)
        {
            if (!safeZone) return;

            float span = aspect > safeZoneMaxAspect ? safeZoneMaxAspect / aspect : 1f;
            float inset = (1f - span) * 0.5f;
            var anchorMin = new Vector2(inset, 0f);
            var anchorMax = new Vector2(1f - inset, 1f);

            // Equality guards keep repeat applies from dirtying the scene / re-triggering
            // dimension-change messages in edit mode.
            if (safeZone.anchorMin != anchorMin) safeZone.anchorMin = anchorMin;
            if (safeZone.anchorMax != anchorMax) safeZone.anchorMax = anchorMax;
            if (safeZone.offsetMin != Vector2.zero) safeZone.offsetMin = Vector2.zero;
            if (safeZone.offsetMax != Vector2.zero) safeZone.offsetMax = Vector2.zero;
        }
    }
}
