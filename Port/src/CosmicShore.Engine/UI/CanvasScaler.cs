namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Computes the root canvas's scale factor from the screen size (original contract:
    /// CanvasScaler). The menu uses ScaleWithScreenSize + MatchWidthOrHeight against a
    /// reference resolution, which keeps UI proportions stable across aspect ratios.
    ///
    /// Pull-based deviation (deliberate, shared with <see cref="Canvas"/>): the original
    /// engine pushes canvas.scaleFactor from here every render pass; headless the Canvas
    /// pulls <see cref="ComputeScaleFactor"/> on read instead — identical steady-state
    /// values, never stale.
    /// </summary>
    public class CanvasScaler : MonoBehaviour
    {
        public enum ScaleMode
        {
            ConstantPixelSize = 0,
            ScaleWithScreenSize = 1,
            ConstantPhysicalSize = 2,
        }

        public enum ScreenMatchMode
        {
            MatchWidthOrHeight = 0,
            Expand = 1,
            Shrink = 2,
        }

        public ScaleMode uiScaleMode = ScaleMode.ConstantPixelSize;

        [Tooltip("Canvas units → pixels for ConstantPixelSize mode.")]
        public float scaleFactor = 1f;

        [Tooltip("The design resolution the UI was authored against (ScaleWithScreenSize).")]
        public Vector2 referenceResolution = new(800f, 600f);

        public ScreenMatchMode screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;

        [Range(0f, 1f)]
        [Tooltip("0 = scale with width, 1 = scale with height (MatchWidthOrHeight).")]
        public float matchWidthOrHeight;

        public float referencePixelsPerUnit = 100f;

        /// <summary>
        /// The scale factor for the current screen size. ConstantPhysicalSize needs real
        /// DPI hardware context — headless it falls back to the constant factor (the
        /// original falls back the same way when the DPI is unavailable).
        /// </summary>
        public float ComputeScaleFactor()
        {
            switch (uiScaleMode)
            {
                case ScaleMode.ScaleWithScreenSize:
                    return ComputeScaleWithScreenSize();
                default:
                    return scaleFactor;
            }
        }

        float ComputeScaleWithScreenSize()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            if (referenceResolution.x <= 0f || referenceResolution.y <= 0f) return 1f;

            switch (screenMatchMode)
            {
                case ScreenMatchMode.Expand:
                    return Mathf.Min(screen.x / referenceResolution.x, screen.y / referenceResolution.y);
                case ScreenMatchMode.Shrink:
                    return Mathf.Max(screen.x / referenceResolution.x, screen.y / referenceResolution.y);
                default:
                    // Original contract: interpolate in LOG space so match=0.5 is the
                    // geometric mean of the width and height ratios.
                    float logWidth = Mathf.Log(screen.x / referenceResolution.x, 2f);
                    float logHeight = Mathf.Log(screen.y / referenceResolution.y, 2f);
                    return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight));
            }
        }
    }
}
