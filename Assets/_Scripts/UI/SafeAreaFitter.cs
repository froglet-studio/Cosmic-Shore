using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    /// <summary>
    /// Constrains a RectTransform to the display's safe area (the region not covered by a notch,
    /// punch-hole, rounded corner, or gesture bar) by driving its
    /// <see cref="RectTransform.anchorMin"/> / <see cref="RectTransform.anchorMax"/> from
    /// <see cref="Screen.safeArea"/>. Attach to the CONTENT layer of a canvas — never to the
    /// canvas root and never to background art.
    ///
    /// The contract this component exists to serve (<c>Docs/STYLE_FOUNDATION.md</c> §9):
    /// <c>androidRenderOutsideSafeArea</c> is ON in ProjectSettings and stays on, so background art
    /// deliberately bleeds under the notch and fills the whole panel; only the layer carrying
    /// readable/tappable content is pulled in. Two siblings solve the horizontal half of the same
    /// problem — <see cref="AdaptiveCanvasScaler"/> (aspect matching + an optional ultrawide HUD
    /// safe zone) and <see cref="WidescreenLayoutAdapter"/> (pillarboxing). This one handles device
    /// cutouts and composes with both: it writes anchors, so a parent constrained by either of
    /// those still bounds it.
    ///
    /// Only ANCHORS are written. Authored offsets (<c>offsetMin</c>/<c>offsetMax</c>) are left alone,
    /// so a rect authored full-stretch with zero offsets ends up exactly the safe rect, and one
    /// authored with padding keeps that padding relative to the safe rect. That is how §9's 24 px
    /// MINIMUM EDGE INSET is expressed — as authored padding on the content layer, which composes
    /// with the fit and, unlike a fitter-enforced inset, still holds on desktop where the fit itself
    /// is a no-op. Zeroing offsets here would make the two rules unable to coexist.
    ///
    /// The rect must therefore be STRETCH-anchored on the axes you want constrained
    /// (anchorMin != anchorMax); a fixed-size, point-anchored rect is warned about once at
    /// initialization, because moving its anchors alone would slide it without resizing it.
    ///
    /// Desktop and any other display whose safe area IS the full screen is a true no-op: the
    /// component detects it, writes nothing, and — if it had previously applied insets (a device
    /// rotating a cutout off-axis) — restores the anchors the rect was authored with.
    ///
    /// The project is LANDSCAPE-ONLY (auto-rotate between landscape left and right; portrait is
    /// disabled), so the rotation that matters here swaps which side the cutout is on while the
    /// resolution stays identical. That is why the change check reads the safe-area rect and
    /// <see cref="Screen.orientation"/> and not just width/height — a width/height cache alone
    /// would sleep through the one rotation this game can actually do.
    ///
    /// Runtime cost when nothing changed: one <see cref="Screen.safeArea"/> read plus a Rect and
    /// two int comparisons per frame, no allocations. All real work happens only on the frame the
    /// safe area, resolution, or orientation actually changes — resizes are also caught event-style
    /// via <see cref="OnRectTransformDimensionsChange"/>. [ExecuteAlways] keeps the Device Simulator
    /// preview honest in edit mode.
    ///
    /// Verify with Window > General > Device Simulator on a device profile that has a cutout
    /// (the <c>com.unity.device-simulator.devices</c> package ships them) — it is what overrides
    /// <see cref="Screen.safeArea"/> in the editor. Test scene:
    /// <c>Assets/_Scenes/Game_TestDesign/SafeAreaFitterTestScene.unity</c>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>
        /// Pixel slack when comparing the safe area against the full screen. Devices report the safe
        /// area in whole pixels, so anything under a pixel is rounding, not an inset.
        /// </summary>
        public const float FullScreenTolerancePixels = 0.5f;

        private RectTransform _rt;

        // The anchors the rect was authored with, restored whenever the safe area is (or becomes)
        // the full screen. Captured before this component ever writes to them.
        private Vector2 _authoredAnchorMin;
        private Vector2 _authoredAnchorMax;
        private bool _authoredCaptured;

        // Last state this component applied against. The cache is what makes the per-frame check
        // free; it is deliberately invalidated (not applied from) in OnValidate.
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;
        private ScreenOrientation _lastOrientation;
        private bool _cacheValid;

        // True while safe-area insets are on the rect, so a return to a full-screen safe area knows
        // it has something to undo.
        private bool _insetsApplied;

        /// <summary>The safe area this component last applied against. Diagnostics / tests.</summary>
        public Rect LastAppliedSafeArea => _lastSafeArea;

        /// <summary>True while the rect is carrying safe-area insets rather than its authored anchors.</summary>
        public bool InsetsApplied => _insetsApplied;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            CaptureAuthoredAnchors();
        }

        private void OnEnable()
        {
            _cacheValid = false;
            WarnIfNotStretched();
            Apply();
        }

        // The canvas drives its root rect to the screen size, so this fires on window resizes,
        // Game-view aspect changes, and orientation flips — including in edit mode, where Update
        // is sparse.
        private void OnRectTransformDimensionsChange()
        {
            if (!HasChanged()) return;
            Apply();
        }

        private void Update()
        {
            // Cost when nothing changed: one native safeArea read + a Rect/int compare.
            if (!HasChanged()) return;
            Apply();
        }

        private void OnValidate()
        {
            // Invalidate only — writing to a RectTransform inside a serialization callback is what
            // Unity warns about. The next Update / dimensions-change tick re-applies.
            _cacheValid = false;
        }

        private bool HasChanged()
        {
            if (!_cacheValid) return true;
            return Screen.width != _lastWidth
                   || Screen.height != _lastHeight
                   || Screen.orientation != _lastOrientation
                   || Screen.safeArea != _lastSafeArea;
        }

        /// <summary>
        /// Recomputes the anchors for the current safe area. Called automatically on enable and on
        /// safe-area / resolution / orientation change; public so editor tooling or a test harness
        /// can force a refresh.
        /// </summary>
        public void Apply()
        {
            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0) return;

            Rect safeArea = Screen.safeArea;
            // Some devices report a degenerate safe area for a frame mid-rotation. Applying it
            // would collapse the content layer; skip and leave the cache invalid so the next frame
            // re-reads rather than latching the bad value.
            if (safeArea.width <= 0f || safeArea.height <= 0f) return;

            if (!_rt) _rt = GetComponent<RectTransform>();
            if (!_rt) return;
            CaptureAuthoredAnchors();

            _lastSafeArea = safeArea;
            _lastWidth = width;
            _lastHeight = height;
            _lastOrientation = Screen.orientation;
            _cacheValid = true;

            if (IsFullScreenSafeArea(safeArea, width, height))
            {
                // Desktop, and any device orientation that puts every cutout off-axis. Nothing to
                // inset — but undo our own insets if we had applied some.
                if (_insetsApplied)
                {
                    SetAnchors(_authoredAnchorMin, _authoredAnchorMax);
                    _insetsApplied = false;
                }
                return;
            }

            ComputeAnchors(safeArea, width, height, out Vector2 anchorMin, out Vector2 anchorMax);
            SetAnchors(anchorMin, anchorMax);
            _insetsApplied = true;
        }

        private void SetAnchors(Vector2 anchorMin, Vector2 anchorMax)
        {
            // Equality guards keep repeat applies from dirtying the scene and from re-triggering
            // dimension-change messages in edit mode.
            if (_rt.anchorMin != anchorMin) _rt.anchorMin = anchorMin;
            if (_rt.anchorMax != anchorMax) _rt.anchorMax = anchorMax;
        }

        private void CaptureAuthoredAnchors()
        {
            if (_authoredCaptured || !_rt) return;
            _authoredAnchorMin = _rt.anchorMin;
            _authoredAnchorMax = _rt.anchorMax;
            _authoredCaptured = true;
        }

        private void WarnIfNotStretched()
        {
            if (!_rt) return;
            if (_rt.anchorMin.x < _rt.anchorMax.x || _rt.anchorMin.y < _rt.anchorMax.y) return;

            CSDebug.LogWarning(
                $"[SafeAreaFitter] '{name}' is point-anchored on both axes (anchorMin == anchorMax), " +
                "so driving its anchors moves it without resizing it. Anchor it stretched on the " +
                "axes that should follow the safe area.", this);
        }

        /// <summary>
        /// True when the safe area covers the whole screen — i.e. there is nothing to inset. Pure;
        /// unit-tested.
        /// </summary>
        public static bool IsFullScreenSafeArea(Rect safeArea, int screenWidth, int screenHeight,
                                                float pixelTolerance = FullScreenTolerancePixels)
        {
            return safeArea.xMin <= pixelTolerance
                   && safeArea.yMin <= pixelTolerance
                   && safeArea.xMax >= screenWidth - pixelTolerance
                   && safeArea.yMax >= screenHeight - pixelTolerance;
        }

        /// <summary>
        /// Converts a pixel-space safe area into normalized canvas anchors. Anchors are normalized
        /// against the canvas rect, which is driven to the screen, so this is exact without waiting
        /// for a layout pass. Pure; unit-tested.
        /// </summary>
        public static void ComputeAnchors(Rect safeArea, int screenWidth, int screenHeight,
                                          out Vector2 anchorMin, out Vector2 anchorMax)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            anchorMin = new Vector2(
                Mathf.Clamp01(safeArea.xMin / screenWidth),
                Mathf.Clamp01(safeArea.yMin / screenHeight));
            anchorMax = new Vector2(
                Mathf.Clamp01(safeArea.xMax / screenWidth),
                Mathf.Clamp01(safeArea.yMax / screenHeight));
        }
    }
}
