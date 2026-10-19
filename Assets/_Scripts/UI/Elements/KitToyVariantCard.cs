using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The style-B variant card of the Toy configure window (Docs/MENU_REDESIGN_B.md section 17.4): a
    /// dark plate with the option's glyph, its name and its second line. The selected row is the tile's
    /// SELECTED state (the 2px TextLight border), gamepad focus its CTA ring; the second line reads in
    /// CTA green on the row you are already on ("flying"), Neutral otherwise.
    ///
    /// <para>The glyph is <c>ToyShellOption.Icon</c> when the option carries one (an element's petal),
    /// else - for a hull row, whose payload is its <see cref="VesselClassType"/> - that vessel's own icon
    /// from <see cref="vessels"/>. With neither the glyph hides and the name keeps its place.</para>
    ///
    /// <para>The lab card's accent tint, CTA glow and lift (<see cref="ToyVariantCard"/>) are not drawn:
    /// the window's colours are the theme's.</para>
    /// </summary>
    [RequireComponent(typeof(KitTile))]
    public class KitToyVariantCard : ToyVariantCard
    {
        [Header("Style-B")]
        [Tooltip("The card's tile (selected = the picked row).")]
        [SerializeField] private KitTile tile;
        [Tooltip("The option's glyph.")]
        [SerializeField] private Image icon;
        [Tooltip("The second line's KitText: CTA on the current row, Neutral otherwise.")]
        [SerializeField] private KitText detailStyle;
        [Tooltip("Where a hull row's icon comes from.")]
        [SerializeField] private SO_VesselList vessels;

        protected override void Draw(ToyShellOption option, bool selected)
        {
            if (tile) tile.IsSelected = option != null && selected;
            if (detailStyle)
                detailStyle.SetColorToken(option is { IsCurrent: true } ? UIColorToken.Cta : UIColorToken.NeutralLightest);
            if (!icon) return;
            var sprite = option == null ? null : option.Icon ? option.Icon : HullIcon(option);
            icon.sprite = sprite;
            icon.enabled = sprite;
        }

        private Sprite HullIcon(ToyShellOption option)
        {
            if (option.Payload is not VesselClassType hull || !vessels) return null;
            if (!vessels.TryGetVesselByClass(hull, out var vessel) || !vessel) return null;
            return vessel.IconActive ? vessel.IconActive : vessel.IconInactive;
        }
    }
}
