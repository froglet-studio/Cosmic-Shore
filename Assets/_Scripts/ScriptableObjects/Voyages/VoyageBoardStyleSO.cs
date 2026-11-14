using TMPro;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The look of the Voyage window as Shombith picked it on Frogma (the Voyages tab's two screens, Rev 98 1A and 2D,
    /// Docs/MENU_REDESIGN_B.md sections 180-184; the Saga, Quests and Episodes tables, Rev 90, sections 171-172): the chips
    /// on the left, the map or the table in the middle, the voyage or the squad on the right.
    /// <c>VoyageBoard</c> and <c>VoyageSlabPage</c> build the tabs at runtime and read every sprite, colour and
    /// proportion from here (through <c>VoyageKit</c>); their words are the voyage content's (<see cref="VoyageLines"/>,
    /// the Board* and Slab* lines). The tilt's own numbers are <c>VoyageSlab</c>'s.
    ///
    /// <para>The colours are the style guide's tokens (Docs/UI_STYLE_GUIDE.md, <c>Docs/UI/froglet-tokens.json</c>): Jade is
    /// the seat being changed, cyan the thing being looked at (the open voyage, the active squad, the picked quest), a
    /// plate is #00041F under an outline. Loaded from <c>Resources/VoyageBoardStyle</c>; written by
    /// <c>Tools/Build/author_voyage_board.py</c>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageBoardStyle", menuName = "ScriptableObjects/Voyages/Voyage Board Style")]
    public class VoyageBoardStyleSO : ScriptableObject
    {
        public const string ResourcePath = "VoyageBoardStyle";

        [Header("Kit sprites (Assets/_Graphics/UI/Kit, 9-sliced)")]
        [Tooltip("Buttons' and rows' slivered fill (kit_sliver10_fill).")]
        public Sprite Sliver10Fill;
        [Tooltip("Buttons' and rows' slivered 1-unit border (kit_sliver10_line1).")]
        public Sprite Sliver10Line;

        [Header("Type")]
        [Tooltip("Labels and tabs (Chakra Petch SemiBold, set in capitals). Empty: the window's own face.")]
        public TMP_FontAsset LabelFont;

        [Header("Colours (style guide tokens)")]
        [Tooltip("Text, a quiet button's border and the table's seam (#E6E9FF).")]
        public Color Light = new(0.902f, 0.914f, 1f, 1f);
        [Tooltip("Second lines and idle tabs (#747BAD).")]
        public Color Sub = new(0.455f, 0.482f, 0.678f, 1f);
        [Tooltip("The seat being changed (#21C0BD).")]
        public Color Jade = new(0.129f, 0.753f, 0.741f, 1f);
        [Tooltip("The voyage being looked at, the active squad (#00D4FF).")]
        public Color Cyan = new(0f, 0.831f, 1f, 1f);
        [Tooltip("An idle outline (#434C89).")]
        public Color Outline = new(0.263f, 0.298f, 0.537f, 1f);
        [Tooltip("The ground of a stepper, a quiet button and the table's front face (#00010A).")]
        public Color Popup = new(0f, 0.004f, 0.039f, 1f);
        [Tooltip("A plate: a chip, a seat (#00041F at 86%).")]
        public Color RowPicked = new(0f, 0.016f, 0.122f, 0.86f);
        [Tooltip("Set sail's fill: the call to action (#99FF80, the theme's cta).")]
        public Color CtaFill = new(0.6f, 1f, 0.502f, 1f);
        [Tooltip("Set sail's words and keycap (#00041F).")]
        public Color CtaText = new(0f, 0.016f, 0.122f, 1f);

        [Header("Hull renders")]
        [Tooltip("Each class's large render (SO_Vessel.SquadImage) cropped to the hull itself, so it reads large in a wide " +
                 "box: measured from the PNG by Tools/Build/author_voyage_board.py.")]
        public HullCrop[] HullCrops = System.Array.Empty<HullCrop>();

        [Header("Columns (fractions of the Voyages page's width: chips, map or table, the right column)")]
        [Tooltip("The voyage chips' right edge.")]
        [Range(0f, 1f)] public float ListRight = 0.1685f;
        [Tooltip("The table's left edge.")]
        [Range(0f, 1f)] public float CourseLeft = 0.1848f;
        [Tooltip("The table's right edge.")]
        [Range(0f, 1f)] public float CourseRight = 0.7391f;
        [Tooltip("The squad column's left edge.")]
        [Range(0f, 1f)] public float PanelLeft = 0.7554f;

        [Header("Sizes")]
        [Tooltip("A chip's height (a voyage, a saga part, a quest).")]
        public float RowHeight = 96f;

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
