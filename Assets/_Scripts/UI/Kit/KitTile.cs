using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A selectable slivered tile: the style-B LEFT LIST entry, the §10.11 configure tile, the
    /// §10.13 class card, and - with hexagon sprites - the §10.5 / §10.12 hex nav tile.
    ///
    /// <para><b>Selected</b> is a logical state the owning screen sets (<see cref="IsSelected"/>),
    /// not the EventSystem selection: a selected tile takes the 2px Light border and the Neutral
    /// (Dark) fill (approved kit mockup; §10.11 "white border and light fill"). Gamepad focus is
    /// the separate CTA ring from <see cref="KitSelectableBase"/>, so a pad user can move focus
    /// across a list without moving the selection.</para>
    ///
    /// <para><b>Locked</b> dims the art and shows the lock glyph (§10.13); the secondary line takes
    /// <see cref="lockedSecondaryToken"/> (Purchase by default, for a tile unlocked by a spend). A
    /// locked tile stays pressable, so the owner can refuse with its own message - the shell-wide
    /// MenuAvailability contract (Docs/HomeHub/ARCHITECTURE.md §2).</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Tile")]
    public class KitTile : KitSelectableBase
    {
        private const float LockedArtAlpha = 0.35f;
        private const float DisabledContentAlpha = 0.5f;

        [Header("State")]
        [Tooltip("The logical selection the owning screen sets - not the gamepad focus.")]
        [SerializeField] private bool isSelected;
        [Tooltip("Dims the art, shows the lock glyph, recolours the secondary line.")]
        [SerializeField] private bool isLocked;

        [Header("Frame")]
        [Tooltip("The tile's fill (kit_sliver14_fill or kit_hex_fill).")]
        [SerializeField] private Image fillImage;
        [Tooltip("The tile's border; its sprite swaps between the two below.")]
        [SerializeField] private Image borderImage;
        [Tooltip("Border sprite while selected (the 2px band: kit_sliver14_line2 / kit_hex_line2).")]
        [SerializeField] private Sprite borderSelectedSprite;
        [Tooltip("Border sprite while not selected (list tiles: the 1px band; hex tiles: the 2px band).")]
        [SerializeField] private Sprite borderUnselectedSprite;

        [Header("Content")]
        [Tooltip("The name / icon / label - Light when selected.")]
        [SerializeField] private Graphic[] primaryGraphics = { };
        [Tooltip("What the primary graphics wear when NOT selected. List tiles: TextLight. Hex nav tiles: NeutralLightest.")]
        [SerializeField] private UIColorToken unselectedPrimaryToken = UIColorToken.TextLight;
        [Tooltip("The status line - Light when selected, Neutral (Lightest) otherwise.")]
        [SerializeField] private Graphic[] secondaryGraphics = { };
        [Tooltip("What the secondary line wears while LOCKED. Purchase for a tile a spend unlocks.")]
        [SerializeField] private UIColorToken lockedSecondaryToken = UIColorToken.Purchase;
        [Tooltip("Vessel or icon art. Drawn white (untinted) and dimmed while locked.")]
        [SerializeField] private Graphic[] artGraphics = { };
        [Tooltip("The lock glyph, shown only while locked.")]
        [SerializeField] private Graphic lockGraphic;

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                Refresh(false);
            }
        }

        public bool IsLocked
        {
            get => isLocked;
            set
            {
                if (isLocked == value) return;
                isLocked = value;
                Refresh(false);
            }
        }

        protected override void CollectColors(VisualState state, List<(Graphic graphic, Color color)> into)
        {
            bool hover = state == VisualState.Highlighted || state == VisualState.Pressed;
            float contentAlpha = state == VisualState.Disabled ? DisabledContentAlpha : 1f;

            if (fillImage)
            {
                var fill = isSelected || state == VisualState.Pressed ? UIColorToken.SurfaceDark : UIColorToken.SurfaceVeryDark;
                into.Add((fillImage, Token(fill)));
            }

            if (borderImage)
            {
                var sprite = isSelected ? borderSelectedSprite : borderUnselectedSprite;
                if (sprite && borderImage.sprite != sprite) borderImage.sprite = sprite;
                var border = isSelected ? UIColorToken.TextLight
                    : hover ? UIColorToken.SurfaceLight
                    : UIColorToken.SurfaceDark;
                into.Add((borderImage, Token(border)));
            }

            var primary = isLocked ? UIColorToken.NeutralLightest
                : isSelected ? UIColorToken.TextLight
                : unselectedPrimaryToken;
            foreach (var g in primaryGraphics)
                if (g) into.Add((g, Token(primary, contentAlpha)));

            var secondary = isLocked ? lockedSecondaryToken
                : isSelected ? UIColorToken.TextLight
                : UIColorToken.NeutralLightest;
            foreach (var g in secondaryGraphics)
                if (g) into.Add((g, Token(secondary, contentAlpha)));

            foreach (var g in artGraphics)
                if (g) into.Add((g, new Color(1f, 1f, 1f, isLocked ? LockedArtAlpha : contentAlpha)));

            if (lockGraphic) into.Add((lockGraphic, Token(UIColorToken.TextLight, isLocked ? 1f : 0f)));
        }
    }
}
