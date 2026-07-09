namespace CosmicShore.Engine
{
    /// <summary>Original contract: how a Canvas positions itself in the world.</summary>
    public enum RenderMode
    {
        ScreenSpaceOverlay = 0,
        ScreenSpaceCamera = 1,
        WorldSpace = 2,
    }

    /// <summary>
    /// The UI root component (original contract: Canvas). A ROOT screen-space canvas
    /// drives its own <see cref="RectTransform"/>: rect size = screen / scaleFactor,
    /// pose = screen centre, scale = scaleFactor — so child anchor math resolves in
    /// canvas units and world corners land in pixels (the RectTransform pulls this
    /// through <c>DrivingCanvas</c>; see RectTransform.cs).
    ///
    /// Pull-based deviation (deliberate): the original engine PUSHES the scale factor
    /// from an attached CanvasScaler during its render pass; headless there is no render
    /// pass, so <see cref="scaleFactor"/> PULLS from the scaler on read. Steady-state
    /// values are identical; the port is simply never stale.
    /// </summary>
    public class Canvas : Behaviour
    {
        public RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
        public int sortingOrder;
        public bool overrideSorting;
        public Camera worldCamera;

        float _scaleFactor = 1f;

        /// <summary>The topmost Canvas in this canvas's parent chain (self when none above).</summary>
        public Canvas rootCanvas
        {
            get
            {
                Canvas top = this;
                for (var t = transform.parent; t is not null; t = t.parent)
                {
                    var canvas = t.gameObject.GetComponent<Canvas>();
                    if (canvas != null) top = canvas;
                }
                return top;
            }
        }

        public bool isRootCanvas => ReferenceEquals(rootCanvas, this);

        /// <summary>
        /// Canvas units → pixels. Nested canvases inherit the root's factor; a root canvas
        /// with an enabled <see cref="UI.CanvasScaler"/> reads the scaler's computed value
        /// (see the pull-based note in the class doc); otherwise the stored value.
        /// </summary>
        public float scaleFactor
        {
            get
            {
                if (!isRootCanvas) return rootCanvas.scaleFactor;
                var scaler = gameObject.GetComponent<UI.CanvasScaler>();
                if (scaler != null && scaler.isActiveAndEnabled)
                    return scaler.ComputeScaleFactor();
                return _scaleFactor;
            }
            set => _scaleFactor = value;
        }

        /// <summary>The canvas's pixel footprint — the screen, for screen-space canvases.</summary>
        public Rect pixelRect => new(0f, 0f, Screen.width, Screen.height);

        float _referencePixelsPerUnit = 100f;

        /// <summary>
        /// Pixel density a sprite's pixelsPerUnit is measured against when the UI sizes it
        /// (Image.pixelsPerUnit = sprite ppu / this). Same pull-based rule as
        /// <see cref="scaleFactor"/>: nested canvases inherit the root's; a root with an
        /// enabled scaler reads the scaler's reference value (the original pushes it during
        /// the render pass); otherwise the stored value (original default: 100).
        /// </summary>
        public float referencePixelsPerUnit
        {
            get
            {
                if (!isRootCanvas) return rootCanvas.referencePixelsPerUnit;
                var scaler = gameObject.GetComponent<UI.CanvasScaler>();
                if (scaler != null && scaler.isActiveAndEnabled)
                    return scaler.referencePixelsPerUnit;
                return _referencePixelsPerUnit;
            }
            set => _referencePixelsPerUnit = value;
        }
    }
}
