using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// How every picture-in-picture view is framed (<see cref="PipViewFrame"/>), in the style guide's terms
    /// (<c>Docs/STYLE_FOUNDATION.md</c> §2-§5): the corner sliver on two opposite corners, a 1px hairline in Light round
    /// the picture (bounding boxes), and the 2px chrome band in Neutral (Dark) outside it (panel borders, chrome). Every
    /// colour, width and the sliver come from the <see cref="UIThemeSO"/>; a null theme falls back to the authored §11
    /// values (<see cref="UITheme"/>). Loaded from <c>Resources/PipFrameStyle</c>; with the asset missing the frame
    /// draws these defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "PipFrameStyle", menuName = "ScriptableObjects/UI/PiP Frame Style")]
    public class PipFrameStyleSO : ScriptableObject
    {
        public const string ResourcePath = "PipFrameStyle";

        [Header("Theme")]
        [Tooltip("The style foundation's tokens (UITheme.asset). Null uses the authored §11 values.")]
        public UIThemeSO theme;

        [Header("Colours")]
        [Tooltip("The hairline round the picture. §3: Light (E6E9FF) draws bounding boxes.")]
        public UIColorToken edgeColor = UIColorToken.TextLight;
        [Tooltip("The hairline's opacity.")]
        [Range(0f, 1f)] public float edgeAlpha = 0.85f;
        [Tooltip("The band outside the hairline, and the filled sliver corners. §3: Neutral (Dark) (222645) is panel chrome.")]
        public UIColorToken chromeColor = UIColorToken.SurfaceDark;
        [Tooltip("The band's opacity.")]
        [Range(0f, 1f)] public float chromeAlpha = 1f;

        [Header("Shape")]
        [Tooltip("§5: the large sliver (cards, popups, panels) when on, the small one (buttons, chips) when off.")]
        public bool largeSliver = true;
        [Tooltip("§5: the sliver may mirror. Off cuts the top-left and bottom-right corners; on, the top-right and bottom-left.")]
        public bool flipSliver;
        [Tooltip("The style guide's widths are at 1080 lines; the frame scales them to the screen's height.")]
        public float referenceHeight = 1080f;

        [Header("Layering")]
        [Tooltip("The frame's screen-space overlay canvas order. Below the HUD's canvases, above every camera's picture.")]
        public int sortingOrder = -10;
    }
}
