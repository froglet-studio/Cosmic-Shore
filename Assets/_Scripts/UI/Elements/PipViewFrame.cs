using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The frame round a picture-in-picture view (Garrett, 2026-10-22: "give the pip views a pleasant border across the
    /// game that matches our styleguide"). Every view that puts a camera in a small rect on the screen - the crew vessels'
    /// small square (<c>CrewPictureInPicture</c>, the Wrasse's <c>KiteNavigatorView</c>, the Orca's lifted pilot view) and
    /// the Jacks' (<c>JacksView</c>) - owns one and calls <see cref="Show"/> each frame with whichever camera is in the
    /// small rect, or null when none is.
    ///
    /// <para>The frame is a <see cref="PipFrameGraphic"/> on one shared screen-space overlay canvas, laid over the camera's
    /// pixel rect, so it sits on top of every camera's picture and under the HUD, never takes a tap, and costs nothing
    /// while the rect holds still (the canvas only rebuilds when the frame moves or resizes). Its look is
    /// <c>Resources/PipFrameStyle</c> (<see cref="PipFrameStyleSO"/>), in the style guide's tokens.</para>
    /// </summary>
    public sealed class PipViewFrame
    {
        static Canvas s_canvas;
        static PipFrameStyleSO s_style;
        static bool s_styleLoaded;

        PipFrameGraphic _graphic;
        Rect _shownPixels;
        float _shownScale;
        bool _visible;

        /// <summary>Frame <paramref name="small"/>'s rect on the screen; null (or a disabled camera) hides the frame.</summary>
        public void Show(Camera small)
        {
            if (!small || !small.isActiveAndEnabled)
            {
                Hide();
                return;
            }
            if (!_graphic && !Build()) return;

            var style = Style();
            float scale = Screen.height / Mathf.Max(1f, style ? style.referenceHeight : 1080f);
            var px = small.pixelRect;
            if (!_visible) { _graphic.gameObject.SetActive(true); _visible = true; }
            if (px == _shownPixels && Mathf.Approximately(scale, _shownScale)) return;
            _shownPixels = px;
            _shownScale = scale;
            ApplyStyle(style, scale);
            var rt = _graphic.rectTransform;
            rt.anchoredPosition = px.position;
            rt.sizeDelta = px.size;
        }

        /// <summary>Hide the frame (the view has no small rect this frame).</summary>
        public void Hide()
        {
            if (!_visible || !_graphic) { _visible = false; return; }
            _graphic.gameObject.SetActive(false);
            _visible = false;
        }

        /// <summary>Remove the frame for good.</summary>
        public void Dispose()
        {
            if (_graphic) Object.Destroy(_graphic.gameObject);
            _graphic = null;
            _visible = false;
        }

        bool Build()
        {
            if (!s_canvas)
            {
                var root = new GameObject("PipViewFrames");
                Object.DontDestroyOnLoad(root);
                s_canvas = root.AddComponent<Canvas>();
                s_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var style = Style();
                s_canvas.sortingOrder = style ? style.sortingOrder : -10;
            }
            var go = new GameObject("PipFrame", typeof(RectTransform));
            go.transform.SetParent(s_canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;   // bottom-left: canvas pixels are screen pixels
            rt.pivot = Vector2.zero;
            _graphic = go.AddComponent<PipFrameGraphic>();
            _graphic.raycastTarget = false;
            _shownPixels = default;
            _shownScale = 0f;
            _visible = true;
            return _graphic;
        }

        void ApplyStyle(PipFrameStyleSO style, float scale)
        {
            var theme = style ? style.theme : null;
            var edge = UITheme.Resolve(theme, style ? style.edgeColor : UIColorToken.TextLight);
            edge.a *= style ? style.edgeAlpha : 0.85f;
            var chrome = UITheme.Resolve(theme, style ? style.chromeColor : UIColorToken.SurfaceDark);
            chrome.a *= style ? style.chromeAlpha : 1f;
            float edgeWidth = Mathf.Max(1f, UITheme.BorderWidth(theme, emphasis: false) * scale);
            float chromeWidth = Mathf.Max(1f, UITheme.BorderWidth(theme, emphasis: true) * scale);
            float sliver = UITheme.Sliver(theme, style ? style.largeSliver : true) * scale;
            _graphic.SetStyle(edge, edgeWidth, chrome, chromeWidth, sliver, style && style.flipSliver);
        }

        static PipFrameStyleSO Style()
        {
            if (!s_styleLoaded)
            {
                s_style = Resources.Load<PipFrameStyleSO>(PipFrameStyleSO.ResourcePath);
                s_styleLoaded = true;
            }
            return s_style;
        }
    }
}
