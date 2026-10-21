using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Everything the top bar's <see cref="PilotSwapIndicator"/> draws with: which hull icons, which
    /// control chips, its sizes, colour tokens and words. The indicator is built at runtime (it is
    /// ensured by <see cref="MiniGameHUD"/>, like the spectator badge), so it cannot carry serialized
    /// fields of its own - this asset, loaded from <c>Resources/PilotSwapIndicatorConfig</c>, is where
    /// a human edits it.
    /// </summary>
    [CreateAssetMenu(fileName = "PilotSwapIndicatorConfig",
                     menuName = "ScriptableObjects/UI/Pilot Swap Indicator Config")]
    public class PilotSwapIndicatorConfigSO : ScriptableObject
    {
        [Header("Sources")]
        [Tooltip("Where each hull's icon comes from (SO_Vessel.IconActive, else IconInactive). The full class " +
                 "list, so every hull an arena card can seat resolves.")]
        [SerializeField] private SO_VesselList vessels;

        [Tooltip("The fleet's control-glyph table. A pad player sees its D-pad LEFT / RIGHT artwork (or keycap " +
                 "name) under each icon; when the table authors no D-pad entry the pad chip is simply absent.")]
        [SerializeField] private ControlGlyphSetSO glyphSet;

        [Tooltip("The chrome colours (UITheme.asset). Empty = the Style Foundation's own values.")]
        [SerializeField] private UIThemeSO theme;

        [Tooltip("The keycap font. Empty = TextMesh Pro's default.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Words (empty shows nothing)")]
        [Tooltip("A small caption over the icons, e.g. SWAP. Empty draws no caption.")]
        [SerializeField] private string caption;

        [Tooltip("The keyboard key for the LEFT hull (PilotSwapGesture: 1). Empty = no keycap on a keyboard.")]
        [SerializeField] private string keyboardLeftLabel;

        [Tooltip("The keyboard key for the RIGHT hull (PilotSwapGesture: 2). Empty = no keycap on a keyboard.")]
        [SerializeField] private string keyboardRightLabel;

        [Header("Size")]
        [Tooltip("Icon tile edge in canvas units. Raised at runtime to Min Touch Millimetres when the screen " +
                 "would otherwise draw it smaller than a fingertip.")]
        [Min(16f)] [SerializeField] private float tileSize = 72f;

        [Tooltip("The smallest a tile may be on screen, in millimetres - a fingertip target (~9 mm).")]
        [Min(1f)] [SerializeField] private float minTouchMillimetres = 9f;

        [Tooltip("Gap between the two tiles, canvas units.")]
        [Min(0f)] [SerializeField] private float tileSpacing = 12f;

        [Tooltip("Inset of the hull icon inside its tile, as a fraction of the tile edge.")]
        [Range(0f, 0.4f)] [SerializeField] private float iconInset = 0.12f;

        [Tooltip("Keycap height under a tile, canvas units.")]
        [Min(8f)] [SerializeField] private float chipHeight = 26f;

        [Tooltip("Keycap label point size.")]
        [Min(6f)] [SerializeField] private float chipFontSize = 14f;

        [Tooltip("Caption point size.")]
        [Min(6f)] [SerializeField] private float captionFontSize = 14f;

        [Tooltip("Air kept between the indicator and the goal stack / score bar either side, canvas units.")]
        [Min(0f)] [SerializeField] private float neighbourMargin = 16f;

        [Header("Colour")]
        [Tooltip("Tile plate colour role.")]
        [SerializeField] private UIColorToken plateToken = UIColorToken.SurfaceVeryDark;

        [Tooltip("Plate opacity, so the world shows through the top bar as it does behind the goal stack.")]
        [Range(0f, 1f)] [SerializeField] private float plateAlpha = 0.7f;

        [Tooltip("Tile outline colour role.")]
        [SerializeField] private UIColorToken outlineToken = UIColorToken.SurfaceLight;

        [Tooltip("Hull icon colour role (a tint over the icon sprite).")]
        [SerializeField] private UIColorToken iconToken = UIColorToken.TextLight;

        [Tooltip("Keycap / caption text colour role.")]
        [SerializeField] private UIColorToken textToken = UIColorToken.TextLight;

        [Tooltip("Tile tint while pressed.")]
        [SerializeField] private UIColorToken pressedToken = UIColorToken.Selection;

        public SO_VesselList Vessels => vessels;
        public ControlGlyphSetSO GlyphSet => glyphSet;
        public UIThemeSO Theme => theme;
        public TMP_FontAsset Font => font;
        public string Caption => caption;
        public string KeyboardLeftLabel => keyboardLeftLabel;
        public string KeyboardRightLabel => keyboardRightLabel;
        public float TileSize => tileSize;
        public float MinTouchMillimetres => minTouchMillimetres;
        public float TileSpacing => tileSpacing;
        public float IconInset => iconInset;
        public float ChipHeight => chipHeight;
        public float ChipFontSize => chipFontSize;
        public float CaptionFontSize => captionFontSize;
        public float NeighbourMargin => neighbourMargin;
        public UIColorToken PlateToken => plateToken;
        public float PlateAlpha => plateAlpha;
        public UIColorToken OutlineToken => outlineToken;
        public UIColorToken IconToken => iconToken;
        public UIColorToken TextToken => textToken;
        public UIColorToken PressedToken => pressedToken;
    }
}
