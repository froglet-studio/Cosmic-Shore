using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The style-B arcade card (Docs/MENU_REDESIGN_B.md, item 3): the same mode, favourite, party-pick
    /// and lock behaviour as <see cref="GameCard"/>, drawn through a <see cref="KitTile"/>.
    ///
    /// <para>The local player's own pick is the tile's SELECTED state (a 2px Control Active border);
    /// a lock is the tile's LOCKED state (art at 35%, the padlock and "Locked"); gamepad focus is the
    /// tile's CTA ring. The favourite star swaps between the outline and the filled star and takes
    /// Control Active when on. Party-pick chips show and hide through their CanvasGroups.</para>
    /// </summary>
    [RequireComponent(typeof(KitTile))]
    public class KitGameCard : GameCard
    {
        [Header("Style-B")]
        [Tooltip("The card's tile (selected = local pick, locked = progression lock).")]
        [SerializeField] private KitTile tile;
        [Tooltip("Paints the favourite star: Control Active when on, Neutral (Lightest) when off.")]
        [SerializeField] private KitThemedGraphic starPaint;

        protected override void DrawFavorite(bool favorited)
        {
            base.DrawFavorite(favorited);
            if (starPaint)
                starPaint.SetToken(favorited ? UIColorToken.ControlActive : UIColorToken.NeutralLightest);
        }

        protected override void DrawLocalPick(bool picked)
        {
            if (tile) tile.IsSelected = picked;
        }

        protected override void SetChipShown(Image chip, bool shown)
        {
            if (chip.TryGetComponent(out CanvasGroup group))
                KitVisibility.SetShown(group, shown);
        }

        protected override void DrawLocked(bool locked)
        {
            if (tile) tile.IsLocked = locked;
        }
    }
}
