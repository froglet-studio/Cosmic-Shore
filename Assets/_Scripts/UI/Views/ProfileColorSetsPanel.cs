using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Profile's COLOUR SETS tab: Rev 112 D, "the real cell" (picked by Shombith 2026-11-14; Docs/MENU_REDESIGN_B.md
    /// §187), which replaced the Showroom's two three-tile rails (Rev 54 A, §126) because the catalog keeps growing and
    /// stepping through it a tile at a time does not scale.
    ///
    /// <para>The world is the real menu cell: while the page shows, the Profile's veil dims, the pilot card and the record
    /// step aside, and the cell wears the set being browsed (<see cref="ThemeManager.PreviewColorSet"/>). Docked on the
    /// right is ONE scrolling list of every set, cloned from <see cref="rowTemplate"/> once per catalog entry, in three
    /// groups: Yours from the start, Color Blind supported (Free) and Premium, with All / Color Blind supported / Premium
    /// filters. Over the cell on the left: the set's kind, name, line and four rim-over-face pigments, and APPLY (or
    /// UNLOCK with its crystal price for a Premium set not owned yet, or Equipped). Along the foot four tiles show what
    /// the set repaints - prisms in three shields, vessels and their prism trails, crystals, menus - painted from the
    /// set itself.</para>
    ///
    /// <para>APPLY raises the cell-change veil and wears the set (<see cref="ThemeManager.ApplyColorSet"/>); UNLOCK
    /// spends the crystals (<see cref="PlayerDataService.TryUnlockColorSet"/>). Leaving the tab or the Profile without
    /// applying puts the equipped set back. LB / RB step between Record and Colour sets.</para>
    ///
    /// <para>Every word is a field here, authored in the prefab, or the catalog's (<see cref="ColorSetCatalogSO"/>).</para>
    /// </summary>
    public class ProfileColorSetsPanel : MonoBehaviour
    {
        public enum Filter
        {
            All = 0,
            ColorBlind = 1,
            Premium = 2,
        }

        [Header("Tabs")]
        [SerializeField] private KitTextTab recordTab;
        [SerializeField] private KitTextTab colourSetsTab;
        [Tooltip("The pilot record (the Record tab's page). It stays open but hidden while this page shows.")]
        [SerializeField] private ProfileRecordWindow record;
        [Tooltip("This page.")]
        [SerializeField] private CanvasGroup group;

        [Header("Step aside while the world shows")]
        [Tooltip("The pilot card, hidden while this page shows.")]
        [SerializeField] private CanvasGroup pilotCard;
        [Tooltip("The Profile's veil: dimmed to Veil While Showing while this page shows, so the cell is seen but sits " +
                 "back behind the words.")]
        [SerializeField] private Graphic veil;
        [Range(0f, 1f)] [SerializeField] private float veilWhileShowing = 0.55f;
        [Tooltip("Switched off while this page shows: the veil's sea lines, which would draw over the cell.")]
        [SerializeField] private Graphic[] hideWhileShowing = Array.Empty<Graphic>();

        [Header("Sets")]
        [Tooltip("Used when the theme manager is not up (a test scene). In the game, ThemeManagerDataContainer's catalog.")]
        [SerializeField] private ColorSetCatalogSO catalog;
        [Tooltip("The UI theme the Menus tile builds the set's theme on (ColorSetUITheme).")]
        [SerializeField] private UIThemeSO theme;

        [Header("The list")]
        [SerializeField] private ScrollRect scroll;
        [Tooltip("The list's content; the headings are its children and the rows are cloned into it.")]
        [SerializeField] private RectTransform content;
        [Tooltip("The row cloned once per set. Kept inactive.")]
        [SerializeField] private ColorSetRowView rowTemplate;
        [Tooltip("Yours from the start, Color Blind supported (Free), Premium: each group's heading.")]
        [SerializeField] private GameObject[] headings = new GameObject[3];
        [Tooltip("Each group's count beside its heading.")]
        [SerializeField] private TMP_Text[] headingCounts = new TMP_Text[3];

        [Header("Filters")]
        [SerializeField] private KitTextTab allFilter;
        [SerializeField] private KitTextTab colorBlindFilter;
        [SerializeField] private KitTextTab premiumFilter;
        [Tooltip("All, Color Blind supported, Premium: each filter's count.")]
        [SerializeField] private TMP_Text[] filterCounts = new TMP_Text[3];

        [Header("The set shown")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text lineText;
        [Tooltip("\"Wearing Nocturne\".")]
        [SerializeField] private TMP_Text wearingText;
        [Tooltip("Yours from the start, Color Blind supported (Free), Premium: the line above the name, one shown.")]
        [SerializeField] private GameObject[] kindLabels = new GameObject[3];
        [Tooltip("Jade, Ruby, Gold, Blue: the rim swatch, the face swatch and the pigment words.")]
        [SerializeField] private Image[] pairRims = Array.Empty<Image>();
        [SerializeField] private Image[] pairFaces = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] pairTexts = Array.Empty<TMP_Text>();
        [SerializeField] private Button applyButton;
        [Tooltip("UNLOCK and the price, for a Premium set the player does not own yet.")]
        [SerializeField] private Button unlockButton;
        [SerializeField] private TMP_Text unlockCaption;
        [Tooltip("Shown instead of APPLY when the set shown is the one worn (a tick and the word).")]
        [SerializeField] private GameObject equippedMark;
        [Tooltip("Shown after UNLOCK when the crystals are short, until another set is picked.")]
        [SerializeField] private GameObject shortMark;

        [Header("What the set repaints")]
        [Tooltip("Prisms: Jade, Ruby, Gold, Blue x plain, shielded, super-shielded (domain * 3 + tier).")]
        [SerializeField] private Image[] prismFaces = Array.Empty<Image>();
        [SerializeField] private Image[] prismRims = Array.Empty<Image>();
        [SerializeField] private Image[] prismLits = Array.Empty<Image>();
        [Tooltip("Vessels and trails: one vessel icon per domain, tinted with the domain's vessel colour.")]
        [SerializeField] private Image[] vesselIcons = Array.Empty<Image>();
        [Tooltip("Each vessel's trail of prisms (domain * Trail Length + k).")]
        [SerializeField] private Image[] trailFaces = Array.Empty<Image>();
        [SerializeField] private Image[] trailRims = Array.Empty<Image>();
        [Min(1)] [SerializeField] private int trailLength = 5;
        [Tooltip("Crystals: tinted with the set's call-to-action colour.")]
        [SerializeField] private Image[] crystals = Array.Empty<Image>();
        [Tooltip("Menus: a panel, its line, a picked card, a call to action and a purchase, in the set's UI theme.")]
        [SerializeField] private Image menuPanel;
        [SerializeField] private Image menuLine;
        [SerializeField] private Image menuSelection;
        [SerializeField] private Image menuCta;
        [SerializeField] private Image menuPurchase;

        [Header("Words (empty shows nothing)")]
        [Tooltip("{name} the set worn now.")]
        [SerializeField] private string wearingTemplate = "Wearing {name}";
        [Tooltip("{price} the crystals the set shown costs.")]
        [SerializeField] private string unlockTemplate = "UNLOCK {price}";
        [Tooltip("{count} how many sets.")]
        [SerializeField] private string countTemplate = "{count}";

        static readonly Domains[] DomainOrder = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
        static readonly PrismKind[] Tiers = { PrismKind.Plain, PrismKind.Shielded, PrismKind.SuperShielded };

        readonly List<ColorSetRowView> _rows = new();
        ColorSetCatalogSO _builtFor;
        Filter _filter = Filter.All;

        bool _showing;
        int _picked = -1;      // the catalog index of the set shown
        bool _openOnVisionSets;
        bool _short;
        float _veilAlpha = -1f;
        GameObject _lastSelected;

        ColorSetCatalogSO Catalog => ThemeManager.Data && ThemeManager.Data.Catalog ? ThemeManager.Data.Catalog : catalog;
        static ThemeManager Manager => ThemeManager.Instance;
        SO_ColorSet Equipped => Manager ? Manager.Equipped : ThemeManager.LiveOr(null);

        void Awake()
        {
            if (recordTab) recordTab.onClick.AddListener(ShowRecord);
            if (colourSetsTab) colourSetsTab.onClick.AddListener(ShowColourSets);
            if (applyButton) applyButton.onClick.AddListener(Apply);
            if (unlockButton) unlockButton.onClick.AddListener(Unlock);
            if (allFilter) allFilter.onClick.AddListener(() => SetFilter(Filter.All));
            if (colorBlindFilter) colorBlindFilter.onClick.AddListener(() => SetFilter(Filter.ColorBlind));
            if (premiumFilter) premiumFilter.onClick.AddListener(() => SetFilter(Filter.Premium));
            if (rowTemplate) rowTemplate.gameObject.SetActive(false);
            if (record)
            {
                record.Opened += OnRecordOpened;
                record.Closed += OnRecordClosed;
            }
            SetPage(false);
        }

        void OnDestroy()
        {
            if (!record) return;
            record.Opened -= OnRecordOpened;
            record.Closed -= OnRecordClosed;
        }

        void OnEnable() => ThemeManager.ColorSetApplied += OnApplied;
        void OnDisable() => ThemeManager.ColorSetApplied -= OnApplied;

        void OnRecordOpened()
        {
            if (_openOnVisionSets) ShowColourSets();
            else SetPage(false);
        }

        /// <summary>
        /// Settings > General > Colour sets (the accessibility breadcrumb): the next time the Profile opens, it opens on
        /// this page filtered to the Color Blind supported sets, showing the first; if the Profile is open, at once.
        /// </summary>
        public void ShowColourSetsWhenOpen()
        {
            _openOnVisionSets = true;
            if (record && record.IsOpen) ShowColourSets();
        }

        // Leaving the Profile without applying puts the equipped set back.
        void OnRecordClosed()
        {
            RevertPreview();
            SetPage(false);
        }

        void OnApplied(SO_ColorSet _) { if (_showing) Refresh(); }

        void Update()
        {
            if (!record || !record.IsOpen) return;

            // LB / RB step between the tabs, as the Codex and the Hangar step theirs; the favourite-pilot picker owns
            // the shoulders while it is open, and a window over the Profile (navigation off) owns the pad.
            var pad = Gamepad.current;
            var events = EventSystem.current;
            bool padFree = pad != null && !record.IsPickerOpen && (!events || events.sendNavigationEvents);
            if (padFree && pad.rightShoulder.wasPressedThisFrame && !_showing) ShowColourSets();
            else if (padFree && pad.leftShoulder.wasPressedThisFrame && _showing) ShowRecord();

            // The pad moving onto a row previews its set: one reference compare a frame while the page shows.
            if (!_showing || !events) return;
            var selected = events.currentSelectedGameObject;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            foreach (var row in _rows)
                if (row && row.Tile && row.Tile.gameObject == selected)
                {
                    if (row.Index >= 0 && row.Index != _picked) Pick(row.Index);
                    return;
                }
        }

        public void ShowRecord()
        {
            RevertPreview();
            SetPage(false);
        }

        public void ShowColourSets()
        {
            var cat = Catalog;
            BuildList(cat);
            int vision = _openOnVisionSets && cat ? cat.IndexOfFirstVisionSet() : -1;
            _openOnVisionSets = false;
            SetPage(true);
            if (vision >= 0)
            {
                // The breadcrumb opens the Color Blind supported sets, the first shown (and previewed) on the cell.
                _filter = Filter.ColorBlind;
                Pick(vision);
            }
            else
            {
                _filter = Filter.All;
                _picked = cat ? Mathf.Max(0, cat.IndexOf(Equipped)) : -1;
                Refresh();
            }
            SelectRow(_picked);
        }

        /// <summary>Whether a set of <paramref name="kind"/> shows under <paramref name="filter"/>.</summary>
        public static bool Shows(Filter filter, ColorSetKind kind) => filter switch
        {
            Filter.ColorBlind => kind == ColorSetKind.ColorBlind,
            Filter.Premium => kind == ColorSetKind.Premium,
            _ => true,
        };

        /// <summary>
        /// The catalog indices in list order: Yours from the start, then Color Blind supported, then Premium, each
        /// group in the catalog's card order.
        /// </summary>
        public static void ListOrder(ColorSetCatalogSO cat, List<int> order)
        {
            order.Clear();
            int n = cat ? cat.Count : 0;
            for (int kind = 0; kind < 3; kind++)
                for (int i = 0; i < n; i++)
                {
                    var entry = cat.Get(i);
                    if (entry != null && (int)ColorSetCatalogSO.KindOf(entry) == kind) order.Add(i);
                }
        }

        readonly List<int> _order = new();

        // One row per set, cloned from the template, under its group's heading. Built once per catalog.
        void BuildList(ColorSetCatalogSO cat)
        {
            if (_builtFor == cat && cat) return;
            _builtFor = cat;
            foreach (var row in _rows)
                if (row) Destroy(row.gameObject);
            _rows.Clear();
            if (!rowTemplate || !content) return;
            ListOrder(cat, _order);
            int sibling = 0;
            int at = 0;
            for (int kind = 0; kind < 3; kind++)
            {
                if (kind < headings.Length && headings[kind]) headings[kind].transform.SetSiblingIndex(sibling++);
                for (; at < _order.Count && (int)ColorSetCatalogSO.KindOf(cat.Get(_order[at])) == kind; at++)
                {
                    int index = _order[at];
                    var row = Instantiate(rowTemplate, content);
                    row.name = $"Set {cat.Get(index).id}";
                    row.gameObject.SetActive(true);
                    row.transform.SetSiblingIndex(sibling++);
                    if (row.Tile) row.Tile.onClick.AddListener(() => Pick(index));
                    _rows.Add(row);
                }
            }
            rowTemplate.transform.SetAsLastSibling();
        }

        void SelectRow(int index)
        {
            var events = EventSystem.current;
            if (!events || index < 0) return;
            foreach (var row in _rows)
                if (row && row.Index == index && row.gameObject.activeSelf && row.Tile)
                {
                    events.SetSelectedGameObject(row.Tile.gameObject);
                    _lastSelected = row.Tile.gameObject;
                    return;
                }
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
            if (record) record.SetHidden(colourSets);
            if (pilotCard)
            {
                pilotCard.alpha = colourSets ? 0f : 1f;
                pilotCard.interactable = pilotCard.blocksRaycasts = !colourSets;
            }
            if (veil)
            {
                if (_veilAlpha < 0f) _veilAlpha = veil.color.a;
                var c = veil.color;
                c.a = colourSets ? veilWhileShowing : _veilAlpha;
                veil.color = c;
            }
            foreach (var g in hideWhileShowing)
                if (g) g.enabled = !colourSets;
            if (recordTab) recordTab.IsSelected = !colourSets;
            if (colourSetsTab) colourSetsTab.IsSelected = colourSets;
        }

        void SetFilter(Filter filter)
        {
            _filter = filter;
            Refresh();
            if (scroll) scroll.verticalNormalizedPosition = 1f;
        }

        // Browsing previews the set on the real cell; nothing is saved until APPLY.
        void Pick(int index)
        {
            _picked = index;
            _short = false;
            var entry = Catalog ? Catalog.Get(index) : null;
            if (entry?.colorSet && Manager) Manager.PreviewColorSet(entry.colorSet);
            Refresh();
        }

        void RevertPreview()
        {
            var manager = Manager;
            if (manager && manager.Equipped && ThemeManager.LiveOr(null) != manager.Equipped)
                manager.PreviewColorSet(manager.Equipped);
        }

        void Apply()
        {
            var entry = Catalog ? Catalog.Get(_picked) : null;
            var manager = Manager;
            if (!ColorSetCatalogSO.IsPickable(entry) || !manager || entry.colorSet == Equipped) return;
            // The cell already wears the set (the preview); the veil is the moment it becomes yours. While another
            // load holds the screen no veil can rise, so the set is worn straight away.
            if (!EnvironmentLoadVeil.ShowColourSetChange(entry.displayName, entry.line,
                    () => manager.ApplyColorSet(entry.colorSet, entry.id)))
                manager.ApplyColorSet(entry.colorSet, entry.id);
        }

        void Unlock()
        {
            var entry = Catalog ? Catalog.Get(_picked) : null;
            var data = PlayerDataService.Instance;
            if (entry == null || ColorSetCatalogSO.IsPickable(entry) || !data) return;
            _short = !data.TryUnlockColorSet(entry.id, entry.price);
            Refresh();
            if (!_short && applyButton) EventSystem.current?.SetSelectedGameObject(applyButton.gameObject);
        }

        void Refresh()
        {
            var cat = Catalog;
            BuildList(cat);
            var equipped = Equipped;

            int[] counts = new int[3];
            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                if (!row) continue;
                int index = r < _order.Count ? _order[r] : -1;
                var entry = cat ? cat.Get(index) : null;
                row.Show(entry, index, entry?.colorSet && entry.colorSet == equipped, index == _picked, ColorSetCatalogSO.IsPickable(entry));
                if (entry != null) counts[(int)row.Kind]++;
                row.gameObject.SetActive(entry != null && Shows(_filter, row.Kind));
            }
            for (int k = 0; k < 3; k++)
            {
                // Yours from the start shows under All only; a group with no sets hides its heading.
                bool shown = counts[k] > 0 && Shows(_filter, (ColorSetKind)k);
                if (k < headings.Length && headings[k]) headings[k].SetActive(shown);
                if (k < headingCounts.Length && headingCounts[k]) headingCounts[k].text = Count(counts[k]);
            }
            SetCount(0, counts[0] + counts[1] + counts[2]);
            SetCount(1, counts[(int)ColorSetKind.ColorBlind]);
            SetCount(2, counts[(int)ColorSetKind.Premium]);
            if (allFilter) allFilter.IsSelected = _filter == Filter.All;
            if (colorBlindFilter) colorBlindFilter.IsSelected = _filter == Filter.ColorBlind;
            if (premiumFilter) premiumFilter.IsSelected = _filter == Filter.Premium;

            var shownEntry = cat ? cat.Get(_picked) : null;
            if (nameText) nameText.text = shownEntry?.displayName ?? string.Empty;
            if (lineText) lineText.text = shownEntry?.line ?? string.Empty;
            var worn = cat ? cat.Get(cat.IndexOf(equipped)) : null;
            if (wearingText) wearingText.text = worn != null ? TemplateText.Fill(wearingTemplate, "name", worn.displayName ?? string.Empty) : string.Empty;
            var kind = ColorSetCatalogSO.KindOf(shownEntry);
            for (int k = 0; k < kindLabels.Length; k++)
                if (kindLabels[k]) kindLabels[k].SetActive(shownEntry != null && (int)kind == k);
            for (int d = 0; d < DomainOrder.Length; d++)
            {
                if (d < pairTexts.Length && pairTexts[d]) pairTexts[d].text = ColorSetCatalogSO.Pigment(shownEntry, d);
                if (shownEntry?.colorSet && shownEntry.colorSet.TryGetPrismKindColors(DomainOrder[d], PrismKind.Plain, out var rim, out var face))
                {
                    Paint(pairRims, d, Display(rim));
                    Paint(pairFaces, d, Display(face));
                }
            }

            bool owned = ColorSetCatalogSO.IsPickable(shownEntry);
            bool isEquipped = shownEntry?.colorSet && shownEntry.colorSet == equipped;
            if (applyButton) applyButton.gameObject.SetActive(owned && !isEquipped);
            if (unlockButton) unlockButton.gameObject.SetActive(shownEntry != null && !owned);
            if (unlockCaption) unlockCaption.text = shownEntry != null ? TemplateText.Fill(unlockTemplate, "price", shownEntry.price.ToString("N0")) : string.Empty;
            if (equippedMark) equippedMark.SetActive(isEquipped);
            if (shortMark) shortMark.SetActive(_short);

            PaintRepaints(shownEntry?.colorSet);
        }

        string Count(int n) => TemplateText.Fill(countTemplate, "count", n.ToString());

        void SetCount(int filter, int n)
        {
            if (filter < filterCounts.Length && filterCounts[filter]) filterCounts[filter].text = Count(n);
        }

        // The four tiles: everything here is painted from the set shown, so they change as the list is browsed.
        void PaintRepaints(SO_ColorSet set)
        {
            if (!set) return;
            for (int d = 0; d < DomainOrder.Length; d++)
            {
                for (int t = 0; t < Tiers.Length; t++)
                    if (set.TryGetPrismKindColors(DomainOrder[d], Tiers[t], out var rim, out var face))
                    {
                        int i = d * Tiers.Length + t;
                        Paint(prismFaces, i, Display(face));
                        Paint(prismRims, i, Display(rim));
                        Paint(prismLits, i, Lit(face, rim));
                    }
                if (set.TryGetColorSetByDomain(DomainOrder[d], out var domain))
                    Paint(vesselIcons, d, Display(domain.ShipColor2));
                if (set.TryGetPrismKindColors(DomainOrder[d], PrismKind.Plain, out var trailRim, out var trailFace))
                    for (int k = 0; k < trailLength; k++)
                    {
                        // Oldest faintest, as a trail fades behind its vessel.
                        float a = Mathf.Lerp(0.35f, 1f, trailLength > 1 ? k / (float)(trailLength - 1) : 1f);
                        Paint(trailFaces, d * trailLength + k, WithAlpha(Display(trailFace), a));
                        Paint(trailRims, d * trailLength + k, WithAlpha(Display(trailRim), a));
                    }
            }
            var cta = Display(set.EnvironmentColors.BrightCTA);
            foreach (var c in crystals)
                if (c) c.color = cta;
            var ui = ColorSetUITheme.Build(theme, set);
            if (menuPanel) menuPanel.color = ui[UIColorToken.SurfaceVeryDark];
            if (menuLine) menuLine.color = ui[UIColorToken.SurfaceDark];
            if (menuSelection) menuSelection.color = ui[UIColorToken.ControlActive];
            if (menuCta) menuCta.color = ui[UIColorToken.Cta];
            if (menuPurchase) menuPurchase.color = ui[UIColorToken.Purchase];
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        static void Paint(Image[] images, int index, Color c)
        {
            if (images == null || index < 0 || index >= images.Length || !images[index]) return;
            images[index].color = c;
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

        /// <summary>A prism's lit facet: its face lifted halfway toward its rim, as the board draws it (Rev 54, Rev 112).</summary>
        public static Color Lit(Color face, Color rim) => Color.Lerp(Display(face), Display(rim), 0.5f);
    }
}
