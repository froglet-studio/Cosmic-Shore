using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One vessel row in the Hangar list (style-B menu redesign, item 2): a <see cref="KitTile"/>
    /// showing the vessel's icon, name and whether it is owned or what it costs.
    ///
    /// Selecting the tile - a click, or gamepad / keyboard focus arriving on it - selects the vessel,
    /// so the selection follows focus and the pad never lands on a hidden panel.
    /// </summary>
    [RequireComponent(typeof(KitTile))]
    [RequireComponent(typeof(CanvasGroup))]
    public class HangarVesselTile : MonoBehaviour, ISelectHandler
    {
        [Header("Parts")]
        [Tooltip("The tile this row drives (selected / locked state and colours).")]
        [SerializeField] private KitTile tile;
        [Tooltip("The hull: its Hangar card art, else SO_Vessel.IconActive.")]
        [SerializeField] private Image icon;
        [Tooltip("The Hangar list's colourful card art (Tools/Build/author_hangar_card_art.py). A vessel it does not name " +
                 "shows its card icon.")]
        [SerializeField] private HangarCardArtSO cardArt;
        [Tooltip("The vessel's name (Body role; the tile owns its colour).")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("\"Owned\" or \"Locked / price\" (Label role; the tile owns its colour).")]
        [SerializeField] private KitText statusText;
        [Tooltip("Faded in by the screen's stagger animation.")]
        [SerializeField] private CanvasGroup canvasGroup;

        private SO_Vessel _vessel;
        private HangarScreen _screen;

        public SO_Vessel Vessel => _vessel;

        public bool IsSelected
        {
            get => tile && tile.IsSelected;
            set { if (tile) tile.IsSelected = value; }
        }

        public void Configure(SO_Vessel vessel, HangarScreen screen)
        {
            _vessel = vessel;
            _screen = screen;

            if (icon)
            {
                var art = cardArt ? cardArt.For(vessel) : null;
                icon.sprite = art ? art : vessel.IconActive;
            }
            if (nameText) nameText.text = vessel.Name;
            Refresh();

            if (tile)
            {
                tile.onClick.RemoveListener(OnPressed);
                tile.onClick.AddListener(OnPressed);
            }
        }

        /// <summary>Re-reads ownership and price.</summary>
        public void Refresh()
        {
            if (!_vessel) return;
            if (tile) tile.IsLocked = _vessel.IsLocked;
            if (statusText) statusText.SetText(StatusFor(_vessel));
        }

        /// <summary>The status line: "Owned", "Locked / 4000", or "Locked / Free" for a zero price.</summary>
        public static string StatusFor(SO_Vessel vessel) =>
            !vessel.IsLocked ? "Owned"
            : vessel.UnlockCost > 0 ? $"Locked / {vessel.UnlockCost}"
            : "Locked / Free";

        public void SetAlpha(float alpha)
        {
            if (canvasGroup) canvasGroup.alpha = alpha;
        }

        public void OnSelect(BaseEventData eventData) => OnPressed();

        private void OnPressed()
        {
            if (_screen && _vessel) _screen.OnTileFocused(this);
        }
    }
}
