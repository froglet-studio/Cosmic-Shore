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
    /// Phone scale (Docs/UI_STYLE_GUIDE.md §8, D6): a canvas whose screens have a phone layout ticks
    /// <see cref="hasPhoneLayout"/>. On a phone (<see cref="PhoneScale.IsPhone(int,int,float,bool)"/>) in play mode
    /// the scaler then lays the canvas out against 800 x 450 instead of its authored reference, and every
    /// <see cref="KitText"/> under it sets in its role's phone size. A canvas without a phone layout keeps the PC
    /// layout scaled down on a phone (off by default: a 1920-wide layout in an 800-wide canvas would spill), and
    /// nothing changes on PC, console or iPad either way. The reference is only switched in play mode, so a
    /// Device Simulator preview never saves 800 x 450 into the prefab or scene.
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

        [Header("Phone Scale (D6)")]
        [Tooltip("This canvas's screens have a phone layout (drawn on Frogma, built for 800 x 450). On a phone in " +
                 "play mode it is then laid out against the phone reference and its Kit Texts use the phone type " +
                 "column. Leave off until the phone layout exists: off keeps the PC layout scaled down.")]
        [SerializeField] private bool hasPhoneLayout;

        [Tooltip("The canvas reference on a phone. Docs/UI_STYLE_GUIDE.md §8: 800 x 450.")]
        [SerializeField] private Vector2 phoneReferenceResolution = new(800f, 450f);

        private CanvasScaler _scaler;
        private Vector2 _authoredReference;
        private bool _authoredReferenceRead;
        private int _lastWidth;
        private int _lastHeight;

        /// <summary>Whether this canvas is laid out at the phone scale right now.</summary>
        public bool UsesPhoneLayout { get; private set; }

        /// <summary>Whether this canvas has a phone layout to switch to (the inspector's Has Phone Layout).</summary>
        public bool HasPhoneLayout => hasPhoneLayout;

        private void Awake() => _scaler = GetComponent<CanvasScaler>();

        private void OnDisable()
        {
            // Hand the authored reference back so a disabled scaler never leaves the phone one behind.
            if (UsesPhoneLayout) SetPhoneLayout(false);
        }

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

            bool phone = hasPhoneLayout && Application.isPlaying &&
                         _scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                         PhoneScale.IsPhone(width, height, Screen.dpi, Application.isMobilePlatform);
            if (phone != UsesPhoneLayout) SetPhoneLayout(phone);

            if (_scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                // 1 at/above the reference aspect, 0 at/below (reference - blendRange), linear between.
                float match = Mathf.Clamp01((aspect - (referenceAspect - blendRange)) / blendRange);
                if (!Mathf.Approximately(_scaler.matchWidthOrHeight, match))
                    _scaler.matchWidthOrHeight = match;
            }

            ApplySafeZone(aspect);
        }

        private void SetPhoneLayout(bool phone)
        {
            if (!_scaler) _scaler = GetComponent<CanvasScaler>();
            if (!_scaler) return;
            if (!_authoredReferenceRead)
            {
                _authoredReference = _scaler.referenceResolution;
                _authoredReferenceRead = true;
            }

            UsesPhoneLayout = phone;
            _scaler.referenceResolution = phone ? phoneReferenceResolution : _authoredReference;

            // The profile changes on a resolution change only (rare), so one walk of the texts is fine here.
            var texts = GetComponentsInChildren<KitText>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].Apply();
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
