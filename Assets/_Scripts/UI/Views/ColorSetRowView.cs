using System;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One colour set in the Profile's list (Rev 112 D, Docs/MENU_REDESIGN_B.md §187): a card over the sea lines with the
    /// set's four prisms (its look at a glance), its name, the lime tick while it is worn, its four rim-over-face
    /// swatches, and on the right what kind it is - the eye for a Color Blind supported set, Owned or a crystal price
    /// for a Premium one. <see cref="ProfileColorSetsPanel"/> clones the template row once per catalog entry, so the
    /// list grows with the catalog and nothing here is per set. Every word is a field (empty shows nothing).
    /// </summary>
    public class ColorSetRowView : MonoBehaviour
    {
        [Tooltip("The card: its press picks the set, its focus previews it.")]
        [SerializeField] private KitTile tile;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("The lime tick beside the name while this set is worn.")]
        [SerializeField] private GameObject equippedMark;

        [Header("The set's look: Jade, Ruby, Gold, Blue")]
        [Tooltip("The four prisms' faces, rims and lit facets (the lit facet is the face lifted toward the rim).")]
        [SerializeField] private Image[] prismFaces = Array.Empty<Image>();
        [SerializeField] private Image[] prismRims = Array.Empty<Image>();
        [SerializeField] private Image[] prismLits = Array.Empty<Image>();
        [Tooltip("The small rim-over-face swatch pairs under the name.")]
        [SerializeField] private Image[] swatchRims = Array.Empty<Image>();
        [SerializeField] private Image[] swatchFaces = Array.Empty<Image>();

        [Header("Its kind")]
        [Tooltip("Shown on a Color Blind supported set.")]
        [SerializeField] private GameObject colorBlindMark;
        [Tooltip("Shown on a Premium set the player owns.")]
        [SerializeField] private GameObject ownedMark;
        [Tooltip("Shown on a Premium set still to unlock: the crystal and its price.")]
        [SerializeField] private GameObject priceMark;
        [SerializeField] private TMP_Text priceText;
        [Tooltip("{price} the crystals it costs.")]
        [SerializeField] private string priceTemplate = "{price}";

        static readonly Domains[] DomainOrder = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };

        public KitTile Tile => tile;

        /// <summary>The catalog index this row shows, or -1.</summary>
        public int Index { get; private set; } = -1;

        public ColorSetKind Kind { get; private set; }

        public void Show(ColorSetCatalogSO.Entry entry, int index, bool worn, bool picked, bool owned)
        {
            Index = entry != null ? index : -1;
            Kind = ColorSetCatalogSO.KindOf(entry);
            if (nameText) nameText.text = entry?.displayName ?? string.Empty;
            if (equippedMark) equippedMark.SetActive(worn);
            if (tile)
            {
                tile.IsSelected = picked;
                tile.IsLocked = false;   // a set still to unlock is shown and previewed; its price says the rest
            }
            if (colorBlindMark) colorBlindMark.SetActive(Kind == ColorSetKind.ColorBlind);
            if (ownedMark) ownedMark.SetActive(Kind == ColorSetKind.Premium && owned);
            if (priceMark) priceMark.SetActive(Kind == ColorSetKind.Premium && !owned);
            if (priceText) priceText.text = entry != null ? TemplateText.Fill(priceTemplate, "price", entry.price.ToString("N0")) : string.Empty;

            var set = entry?.colorSet;
            for (int d = 0; d < DomainOrder.Length; d++)
            {
                if (!set || !set.TryGetPrismKindColors(DomainOrder[d], PrismKind.Plain, out var rim, out var face)) continue;
                Paint(prismFaces, d, ProfileColorSetsPanel.Display(face));
                Paint(prismRims, d, ProfileColorSetsPanel.Display(rim));
                Paint(prismLits, d, ProfileColorSetsPanel.Lit(face, rim));
                Paint(swatchRims, d, ProfileColorSetsPanel.Display(rim));
                Paint(swatchFaces, d, ProfileColorSetsPanel.Display(face));
            }
        }

        static void Paint(Image[] images, int index, Color c)
        {
            if (images != null && index >= 0 && index < images.Length && images[index]) images[index].color = c;
        }
    }
}
