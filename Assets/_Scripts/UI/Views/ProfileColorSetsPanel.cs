using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Profile's COLOUR SETS tab (Rev 51 A, picked by Shombith 2026-11-05; Docs/MENU_REDESIGN_B.md §123). Two word
    /// tabs in the header switch between the pilot record and this page. The page lists the catalog's sets as cards
    /// (each with its four domains' prism swatches and Equipped / Owned), previews the picked one at the three prism
    /// tiers plus the vessel colour, and APPLY raises the cell-change veil in its colour-set mode
    /// (<see cref="EnvironmentLoadVeil.ShowColourSetChange"/>), which repaints through
    /// <see cref="ThemeManager.ApplyColorSet"/> behind the opaque screen.
    ///
    /// <para>The page is a layer over the record: the record window keeps its own state, and this page returns to the
    /// Record tab whenever the record opens again (the Profile entering). Every word is a field here or authored in
    /// the prefab; the sets' names and lines are the catalog's (<see cref="ColorSetCatalogSO"/>).</para>
    /// </summary>
    public class ProfileColorSetsPanel : MonoBehaviour
    {
        [Serializable]
        public struct Card
        {
            public CanvasGroup group;
            public KitTile tile;
            public TMP_Text nameText;
            public TMP_Text lineText;
            public TMP_Text stateText;
            [Tooltip("The lime tick beside the state while this set is equipped.")]
            public GameObject equippedMark;
            [Tooltip("Jade, Ruby, Gold, Blue: each prism swatch's face (fill) and rim (outline).")]
            public Image[] faces;
            public Image[] rims;
        }

        [Header("Tabs")]
        [SerializeField] private KitTextTab recordTab;
        [SerializeField] private KitTextTab colourSetsTab;
        [Tooltip("The pilot record (the Record tab's page). Its CanvasGroup stops taking input while this page shows.")]
        [SerializeField] private ProfileRecordWindow record;
        [Tooltip("This page.")]
        [SerializeField] private CanvasGroup group;

        [Header("Sets")]
        [Tooltip("Used when the theme manager is not up (a test scene). In the game, ThemeManagerDataContainer's catalog.")]
        [SerializeField] private ColorSetCatalogSO catalog;
        [SerializeField] private Card[] cards = Array.Empty<Card>();

        [Header("Preview")]
        [SerializeField] private TMP_Text previewName;
        [SerializeField] private TMP_Text previewLine;
        [Tooltip("Row-major, Jade, Ruby, Gold, Blue x Prism, Shielded, Super-shielded: each swatch's face.")]
        [SerializeField] private Image[] tierFaces = Array.Empty<Image>();
        [Tooltip("The same swatches' rims.")]
        [SerializeField] private Image[] tierRims = Array.Empty<Image>();
        [Tooltip("Jade, Ruby, Gold, Blue: the vessel colour (DomainColorSet.ShipColor2).")]
        [SerializeField] private Image[] vessels = Array.Empty<Image>();
        [SerializeField] private Button applyButton;
        [Tooltip("Under the preview: why APPLY is not offered (equipped, locked).")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject statusTick;

        [Header("Words (empty shows nothing)")]
        [SerializeField] private string equippedWord = "Equipped";
        [SerializeField] private string ownedWord = "Owned";
        [SerializeField] private string lockedWord = "Unlocks with a quest";
        [Tooltip("Under the preview when the shown set is the one you wear.")]
        [SerializeField] private string equippedStatus = "Equipped - every prism, vessel and trail wears it";
        [Tooltip("Under the preview when the shown set is locked.")]
        [SerializeField] private string lockedStatus = "A quest will give this set";

        static readonly Domains[] DomainOrder = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
        static readonly PrismKind[] Tiers = { PrismKind.Plain, PrismKind.Shielded, PrismKind.SuperShielded };

        bool _showing;
        bool _recordWasOpen;
        int _picked = -1;

        ColorSetCatalogSO Catalog => ThemeManager.Data && ThemeManager.Data.Catalog ? ThemeManager.Data.Catalog : catalog;
        SO_ColorSet Equipped => ThemeManager.LiveOr(null);

        void Awake()
        {
            if (recordTab) recordTab.onClick.AddListener(ShowRecord);
            if (colourSetsTab) colourSetsTab.onClick.AddListener(ShowColourSets);
            if (applyButton) applyButton.onClick.AddListener(Apply);
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                if (cards[i].tile) cards[i].tile.onClick.AddListener(() => Pick(index));
            }
            SetPage(false);
        }

        void OnEnable() => ThemeManager.ColorSetApplied += OnApplied;
        void OnDisable() => ThemeManager.ColorSetApplied -= OnApplied;

        // The Profile opening again (the record window opening) brings back the Record tab.
        void Update()
        {
            bool open = record && record.IsOpen;
            if (open && !_recordWasOpen && _showing) SetPage(false);
            _recordWasOpen = open;
        }

        public void ShowRecord() => SetPage(false);

        public void ShowColourSets()
        {
            var cat = Catalog;
            _picked = cat ? Mathf.Max(0, cat.IndexOf(Equipped)) : -1;
            SetPage(true);
            Refresh();
        }

        void SetPage(bool colourSets)
        {
            _showing = colourSets;
            if (group)
            {
                group.alpha = colourSets ? 1f : 0f;
                group.interactable = colourSets;
                group.blocksRaycasts = colourSets;
            }
            if (record && record.IsOpen)
            {
                // The record stays open under this page; it only stops taking input while hidden.
                var recordGroup = record.GetComponent<CanvasGroup>();
                if (recordGroup) recordGroup.interactable = !colourSets;
            }
            if (recordTab) recordTab.IsSelected = !colourSets;
            if (colourSetsTab) colourSetsTab.IsSelected = colourSets;
        }

        void Pick(int index)
        {
            _picked = index;
            Refresh();
        }

        void OnApplied(SO_ColorSet _) { if (_showing) Refresh(); }

        void Apply()
        {
            var entry = Catalog ? Catalog.Get(_picked) : null;
            var manager = ThemeManager.Instance;
            if (entry == null || !entry.unlocked || !entry.colorSet || !manager || entry.colorSet == Equipped) return;
            // The repaint runs behind the opaque veil; with no veil possible (another load holds the screen) it waits.
            EnvironmentLoadVeil.ShowColourSetChange(entry.displayName, entry.line,
                () => manager.ApplyColorSet(entry.colorSet, entry.id));
        }

        void Refresh()
        {
            var cat = Catalog;
            var equipped = Equipped;
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                var entry = cat ? cat.Get(i) : null;
                if (card.group)
                {
                    card.group.alpha = entry != null ? 1f : 0f;
                    card.group.interactable = card.group.blocksRaycasts = entry != null;
                }
                if (entry == null) continue;
                bool isEquipped = entry.colorSet && entry.colorSet == equipped;
                if (card.nameText) card.nameText.text = entry.displayName ?? string.Empty;
                if (card.lineText) card.lineText.text = entry.line ?? string.Empty;
                if (card.stateText) card.stateText.text = isEquipped ? equippedWord : entry.unlocked ? ownedWord : lockedWord;
                if (card.equippedMark) card.equippedMark.SetActive(isEquipped);
                if (card.tile)
                {
                    card.tile.IsSelected = i == _picked;
                    card.tile.IsLocked = !entry.unlocked;
                }
                for (int d = 0; d < DomainOrder.Length; d++)
                {
                    if (!entry.colorSet || !entry.colorSet.TryGetPrismKindColors(DomainOrder[d], PrismKind.Plain, out var rim, out var face))
                        continue;
                    Paint(card.faces, d, face);
                    Paint(card.rims, d, rim);
                }
            }

            var shown = cat ? cat.Get(_picked) : null;
            if (previewName) previewName.text = shown?.displayName ?? string.Empty;
            if (previewLine) previewLine.text = shown?.line ?? string.Empty;
            var set = shown?.colorSet;
            if (set)
            {
                for (int d = 0; d < DomainOrder.Length; d++)
                {
                    for (int t = 0; t < Tiers.Length; t++)
                        if (set.TryGetPrismKindColors(DomainOrder[d], Tiers[t], out var rim, out var face))
                        {
                            Paint(tierFaces, d * Tiers.Length + t, face);
                            Paint(tierRims, d * Tiers.Length + t, rim);
                        }
                    if (set.TryGetColorSetByDomain(DomainOrder[d], out var domain)) Paint(vessels, d, domain.ShipColor2);
                }
            }

            bool isShownEquipped = set && set == equipped;
            bool canApply = shown != null && shown.unlocked && set && !isShownEquipped;
            if (applyButton) applyButton.gameObject.SetActive(canApply);
            if (statusTick) statusTick.SetActive(isShownEquipped);
            if (statusText)
                statusText.text = isShownEquipped ? equippedStatus : shown != null && !shown.unlocked ? lockedStatus : string.Empty;
        }

        static void Paint(Image[] images, int index, Color linear)
        {
            if (images == null || index < 0 || index >= images.Length || !images[index]) return;
            images[index].color = Display(linear);
        }

        /// <summary>
        /// A palette colour as a UI swatch. Palette fields are linear HDR (Docs/PALETTE.md §3) and a UI colour is
        /// gamma, so an over-1 colour is scaled back to its hue at full strength and converted with Unity's own
        /// transfer - the swatch shows the colour's look, not its bloom.
        /// </summary>
        public static Color Display(Color linear)
        {
            float peak = Mathf.Max(linear.r, Mathf.Max(linear.g, linear.b));
            var c = peak > 1f ? new Color(linear.r / peak, linear.g / peak, linear.b / peak) : new Color(linear.r, linear.g, linear.b);
            var g = c.gamma;
            return new Color(g.r, g.g, g.b, 1f);
        }
    }
}
