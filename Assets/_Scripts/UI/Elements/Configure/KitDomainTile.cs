using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One domain column of the style-B configure tree's pilots row: the domain tile (name, a
    /// "Your domain" / "Tap to join" line and the seat count), the seat chips the modal parents
    /// under its strip, and the domain's open seat with its "+ AI" button (decision Q2: Add AI is
    /// per domain, replacing the armed toggle).
    ///
    /// <para>State is the legacy <see cref="DomainInfoData"/>'s, driven by the modal as before;
    /// this class draws it. The tile's frame, name and line are its <see cref="KitTile"/> (also the
    /// Button the modal wires); the border and the accent stripe wear the DOMAIN's colour, which is
    /// palette data (<see cref="SO_ColorSet"/>), not a chrome token.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Domain Tile")]
    public class KitDomainTile : DomainInfoData
    {
        private const float UnselectedBorderAlpha = 0.55f;
        private const float ClosedAlpha = 0.4f;

        [Header("Style-B Tile")]
        [Tooltip("The chrome theme. Unwired falls back to the authored section 11 values.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("The palette the domain colour comes from (ThemeManagerDataContainer's colour set).")]
        [SerializeField] private SO_ColorSet colorSet;
        [Tooltip("The tile's KitTile: fill, focus ring, name and line colours.")]
        [SerializeField] private KitTile tile;
        [Tooltip("The tile border, in the domain colour: the 2 px band while selected, the 1 px band otherwise.")]
        [SerializeField] private Image borderImage;
        [Tooltip("Border sprite while this is the local player's domain.")]
        [SerializeField] private Sprite borderSelectedSprite;
        [Tooltip("Border sprite otherwise.")]
        [SerializeField] private Sprite borderUnselectedSprite;
        [Tooltip("The left accent stripe, in the domain colour.")]
        [SerializeField] private Graphic accent;
        [Tooltip("The column's own group: dims the whole column while the tile takes no taps (a weekly challenge).")]
        [SerializeField] private CanvasGroup columnGroup;

        [Header("Text")]
        [Tooltip("\"Your domain\", \"Tap to join\" or \"Set by the challenge\" (BodySmall).")]
        [SerializeField] private KitText lineText;
        [Tooltip("How many seats this domain holds right now: the chips under its strip (Data role).")]
        [SerializeField] private KitText countText;

        [Header("Open Seat")]
        [Tooltip("The open-seat row under the chips, shown while the match can still seat someone.")]
        [SerializeField] private CanvasGroup openSeatGroup;
        [Tooltip("The \"+ AI\" button's group, shown only to a player who may place an AI (the host).")]
        [SerializeField] private CanvasGroup addAiGroup;

        private int _shownCount = -1;

        private Color DomainColour => colorSet ? colorSet.GetDomainSignalColor(Domain) : UITheme.Resolve(theme, UIColorToken.TextLight);

        private void OnEnable()
        {
            _shownCount = -1;
            DrawSelected(IsSelected);
            DrawInteractable(IsInteractable);
        }

        protected override void DrawSelected(bool selected)
        {
            if (tile) tile.IsSelected = selected;

            var colour = DomainColour;
            if (borderImage)
            {
                var sprite = selected ? borderSelectedSprite : borderUnselectedSprite;
                if (sprite) borderImage.sprite = sprite;
                borderImage.color = new Color(colour.r, colour.g, colour.b, selected ? 1f : UnselectedBorderAlpha);
            }
            if (accent) accent.color = colour;
            if (countText) countText.GetComponent<Graphic>().color = colour;
            WriteLine();
        }

        protected override void DrawInteractable(bool interactable)
        {
            if (columnGroup) columnGroup.alpha = interactable ? 1f : ClosedAlpha;
            WriteLine();
        }

        public override void SetOpenSeat(bool open, bool canAddAI)
        {
            KitVisibility.SetShown(openSeatGroup, open);
            KitVisibility.SetShown(addAiGroup, open && canAddAI);
        }

        private void WriteLine()
        {
            if (!lineText) return;
            lineText.SetText(!IsInteractable ? "Set by the challenge" : IsSelected ? "Your domain" : "Tap to join");
        }

        // The count is read off the strip rather than pushed by the modal: chips are parented,
        // moved and destroyed from several paths (a pick, a late joiner, an AI placement, a kick),
        // and Destroy is deferred to the end of the frame, so the strip itself is the only count
        // that is right on every frame. Three tiles, a handful of children: nothing to measure.
        private void LateUpdate()
        {
            if (!countText) return;
            var strip = AvatarStripTransform;
            int count = 0;
            if (strip)
                for (int i = 0; i < strip.childCount; i++)
                {
                    var child = strip.GetChild(i);
                    if (child.gameObject.activeSelf && child.TryGetComponent<DomainAvatarChip>(out _)) count++;
                }
            if (count == _shownCount) return;
            _shownCount = count;
            countText.SetText(count.ToString());
        }
    }
}
