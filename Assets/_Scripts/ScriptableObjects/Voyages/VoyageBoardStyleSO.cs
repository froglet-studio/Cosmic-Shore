using TMPro;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The look of the Voyages tab as Shombith picked it on Frogma (Docs/MENU_REDESIGN_B.md sections 161-164): Rev 81 B5's
    /// three columns (the voyage list, the course, the right panel with the squad's three seat cards) and Rev 83 S2's seat
    /// editor (the "Hangar bay": the choice large on a lit floor, the options a list beside it). <c>VoyageBoard</c> builds
    /// the tab at runtime and reads every sprite, colour and proportion from here; its words are the voyage content's
    /// (<see cref="VoyageLines"/>, the Board* lines).
    ///
    /// <para>The colours are the style guide's tokens (Docs/UI_STYLE_GUIDE.md, <c>Docs/UI/froglet-tokens.json</c>): Jade is
    /// the thing you picked, cyan the voyage you are looking at, a plate is a filled card, a popup is #00010A with a light
    /// 1-unit border. Loaded from <c>Resources/VoyageBoardStyle</c>; written by <c>Tools/Build/author_voyage_board.py</c>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageBoardStyle", menuName = "ScriptableObjects/Voyages/Voyage Board Style")]
    public class VoyageBoardStyleSO : ScriptableObject
    {
        public const string ResourcePath = "VoyageBoardStyle";

        [Header("Kit sprites (Assets/_Graphics/UI/Kit, 9-sliced)")]
        [Tooltip("The large surfaces' slivered fill: panels, the editor, seat cards (kit_sliver14_fill).")]
        public Sprite Sliver14Fill;
        [Tooltip("The large surfaces' slivered 1-unit border (kit_sliver14_line1).")]
        public Sprite Sliver14Line;
        [Tooltip("The seat being changed: the large surfaces' 2-unit border (kit_sliver14_line2), tinted Jade.")]
        public Sprite Sliver14Line2;
        [Tooltip("Buttons' and rows' slivered fill (kit_sliver10_fill).")]
        public Sprite Sliver10Fill;
        [Tooltip("Buttons' and rows' slivered 1-unit border (kit_sliver10_line1).")]
        public Sprite Sliver10Line;
        [Tooltip("A vertical fade, clear at the top (kit_fade_v): a seat card's ground and the hero's floor.")]
        public Sprite FadeV;
        [Tooltip("The lit floor under the editor's choice (kit_pool).")]
        public Sprite Pool;

        [Header("Type")]
        [Tooltip("Labels and tabs (Chakra Petch SemiBold, set in capitals). Empty: the window's own face.")]
        public TMP_FontAsset LabelFont;

        [Header("Colours (style guide tokens)")]
        [Tooltip("Text and the editor's border (#E6E9FF).")]
        public Color Light = new(0.902f, 0.914f, 1f, 1f);
        [Tooltip("Second lines and idle tabs (#747BAD).")]
        public Color Sub = new(0.455f, 0.482f, 0.678f, 1f);
        [Tooltip("The picked thing: the seat being changed, the picked option (#21C0BD).")]
        public Color Jade = new(0.129f, 0.753f, 0.741f, 1f);
        [Tooltip("The voyage being looked at, the active squad (#00D4FF).")]
        public Color Cyan = new(0f, 0.831f, 1f, 1f);
        [Tooltip("A filled plate: options and seat cards (#222645 at 72%).")]
        public Color Plate = new(0.133f, 0.149f, 0.271f, 0.72f);
        [Tooltip("A seat card's ground, darkening to its foot (#222645 at 92%).")]
        public Color CardFoot = new(0.133f, 0.149f, 0.271f, 0.92f);
        [Tooltip("An idle outline (#434C89).")]
        public Color Outline = new(0.263f, 0.298f, 0.537f, 1f);
        [Tooltip("The editor's ground (#00010A).")]
        public Color Popup = new(0f, 0.004f, 0.039f, 1f);
        [Tooltip("The open voyage's row (#00041F at 86%).")]
        public Color RowPicked = new(0f, 0.016f, 0.122f, 0.86f);
        [Tooltip("Set sail's fill: the call to action (#99FF80, the theme's cta).")]
        public Color CtaFill = new(0.6f, 1f, 0.502f, 1f);
        [Tooltip("Set sail's words and keycap (#00041F).")]
        public Color CtaText = new(0f, 0.016f, 0.122f, 1f);

        [Header("Hull renders")]
        [Tooltip("Each class's large render (SO_Vessel.SquadImage) cropped to the hull itself, so it reads large in a wide " +
                 "box: measured from the PNG by Tools/Build/author_voyage_board.py.")]
        public HullCrop[] HullCrops = System.Array.Empty<HullCrop>();

        [Header("Columns (fractions of the Voyages page's width, Rev 81 B5)")]
        [Tooltip("The voyage list's right edge.")]
        [Range(0f, 1f)] public float ListRight = 0.2935f;
        [Tooltip("The course's left edge.")]
        [Range(0f, 1f)] public float CourseLeft = 0.315f;
        [Tooltip("The course's right edge; the seat editor covers the page up to here.")]
        [Range(0f, 1f)] public float CourseRight = 0.674f;
        [Tooltip("The right panel's left edge.")]
        [Range(0f, 1f)] public float PanelLeft = 0.696f;
        [Tooltip("The course's edge view's height under the map, as a fraction of the column.")]
        [Range(0f, 1f)] public float EdgeShare = 0.38f;

        [Header("Sizes")]
        [Tooltip("A voyage row's height.")]
        public float RowHeight = 176f;
        [Tooltip("A seat card's share of the right panel's height.")]
        [Range(0f, 1f)] public float SeatCardShare = 0.41f;
        [Tooltip("The editor's option rows: vessel, pilot, and a word (element, artifact).")]
        public Vector3 OptionHeights = new(84f, 104f, 72f);

        [Header("Motion (style guide motion tokens)")]
        [Tooltip("The editor opening and closing, in seconds (the panel token).")]
        public float EditorFade = 0.2f;

        /// <summary>The part of <paramref name="sprite"/> its hull fills (0-1, from the bottom left); the whole sprite when
        /// it was not measured.</summary>
        public Rect CropOf(Sprite sprite)
        {
            if (sprite && HullCrops != null)
                foreach (var c in HullCrops)
                    if (c.Sprite == sprite) return c.Uv;
            return new Rect(0f, 0f, 1f, 1f);
        }

        public static VoyageBoardStyleSO Load()
        {
            var style = Resources.Load<VoyageBoardStyleSO>(ResourcePath);
            return style ? style : CreateInstance<VoyageBoardStyleSO>();
        }
    }

    /// <summary>A hull render and the part of it the hull fills.</summary>
    [System.Serializable]
    public struct HullCrop
    {
        public Sprite Sprite;
        public Rect Uv;
    }
}
