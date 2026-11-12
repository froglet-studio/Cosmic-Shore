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
    /// The Profile's COLOUR SETS tab, the Showroom (Rev 54 A, picked by Shombith 2026-11-05; Docs/MENU_REDESIGN_B.md
    /// §126). The page is a window onto the real menu cell: while it shows, the Profile's veil, the pilot card and the
    /// record step aside, and the cell wears the set being browsed (<see cref="ThemeManager.PreviewColorSet"/>). Over
    /// the cell sit the set's name, line and four rim-over-face pigments, a rail of set tiles (the pad moving onto a
    /// tile previews it) and APPLY, which raises the cell-change veil in its colour-set mode and wears the set
    /// (<see cref="ThemeManager.ApplyColorSet"/>). Leaving the tab or the Profile without applying puts the equipped set
    /// back. LB / RB step between the Record and Colour sets tabs.
    ///
    /// <para>Two rails (Shombith, 2026-11-09): the looks on the left, and apart on the right the sets made for a vision
    /// (colour blindness, low vision, Contrast), which are free because they are accessibility
    /// (<see cref="ColorSetCatalogSO.IsPickable"/>). Each rail is a window onto its own sets: its tiles show them from the
    /// rail's first on, and its previous and next buttons step the set shown and slide the window, so any number of sets
    /// is browsed through the same tiles. Settings > General > Colour sets is a breadcrumb that opens this page on the
    /// vision rail (<see cref="ShowColourSetsWhenOpen"/>).</para>
    ///
    /// <para>Every word is a field here, authored in the prefab, or the catalog's (<see cref="ColorSetCatalogSO"/>).</para>
    /// </summary>
    public class ProfileColorSetsPanel : MonoBehaviour
    {
        [Serializable]
        public struct Thumb
        {
            public CanvasGroup group;
            public KitTile tile;
            public TMP_Text nameText;
            [Tooltip("The lime tick beside the name while this set is equipped.")]
            public GameObject equippedMark;
            [Tooltip("Jade, Ruby, Gold, Blue: each prism swatch's face (fill) and rim (outline).")]
            public Image[] faces;
            public Image[] rims;
        }

        [Header("Tabs")]
        [SerializeField] private KitTextTab recordTab;
        [SerializeField] private KitTextTab colourSetsTab;
        [Tooltip("The pilot record (the Record tab's page). It stays open but hidden while this page shows.")]
        [SerializeField] private ProfileRecordWindow record;
        [Tooltip("This page.")]
        [SerializeField] private CanvasGroup group;

        [Header("Step aside while the world shows")]
        [Tooltip("The pilot card, hidden while this page shows (the board gives the world its space).")]
        [SerializeField] private CanvasGroup pilotCard;
        [Tooltip("The Profile's veil: dimmed to Veil While Showing while this page shows, so the cell is seen but sits " +
                 "back behind the words.")]
        [SerializeField] private Graphic veil;
        [Tooltip("The veil's alpha while this page shows (the cell through it). Its own alpha comes back on leaving.")]
        [Range(0f, 1f)] [SerializeField] private float veilWhileShowing = 0.55f;
        [Tooltip("Switched off while this page shows: the veil's sea lines, which would draw over the cell.")]
        [SerializeField] private Graphic[] hideWhileShowing = Array.Empty<Graphic>();

        [Header("Sets")]
        [Tooltip("Used when the theme manager is not up (a test scene). In the game, ThemeManagerDataContainer's catalog.")]
        [SerializeField] private ColorSetCatalogSO catalog;
        [Tooltip("The left rail: the sets picked for their looks (vision None).")]
        [SerializeField] private Thumb[] thumbs = Array.Empty<Thumb>();
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("Colour vision (free)")]
        [Tooltip("The right rail: only the sets made for a vision (colour blindness, low vision, Contrast). Always free.")]
        [SerializeField] private Thumb[] visionThumbs = Array.Empty<Thumb>();
        [SerializeField] private Button visionPreviousButton;
        [SerializeField] private Button visionNextButton;

        [Header("The set shown")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text lineText;
        [Tooltip("\"2 of 3\": the set's place in its own rail.")]
        [SerializeField] private TMP_Text positionText;
        [Tooltip("\"Wearing Nocturne\".")]
        [SerializeField] private TMP_Text wearingText;
        [Tooltip("Jade, Ruby, Gold, Blue: the rim swatch, the face swatch and the pigment words.")]
        [SerializeField] private Image[] pairRims = Array.Empty<Image>();
        [SerializeField] private Image[] pairFaces = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] pairTexts = Array.Empty<TMP_Text>();
        [SerializeField] private Button applyButton;
        [Tooltip("Shown instead of APPLY when the set shown is the one worn (a tick and the word).")]
        [SerializeField] private GameObject equippedMark;

        [Header("Words (empty shows nothing)")]
        [Tooltip("{index} the set's place in the rail, {count} how many sets.")]
        [SerializeField] private string positionTemplate = "{index} of {count}";
        [Tooltip("{name} the set worn now.")]
        [SerializeField] private string wearingTemplate = "Wearing {name}";

        static readonly Domains[] DomainOrder = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };

        /// <summary>One rail at run time: its tiles, buttons, the catalog indices it shows and its window.</summary>
        sealed class Rail
        {
            public Thumb[] Thumbs;
            public readonly List<int> Sets = new();   // catalog indices, in card order
            public int First;                          // the place in Sets on the first tile
            public bool Vision;
        }

        readonly Rail _looks = new() { Vision = false };
        readonly Rail _vision = new() { Vision = true };
        ColorSetCatalogSO _railsBuiltFor;

        bool _showing;
        int _picked = -1;      // the catalog index of the set shown
        bool _openOnVisionSets;
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
            _looks.Thumbs = thumbs ?? Array.Empty<Thumb>();
            _vision.Thumbs = visionThumbs ?? Array.Empty<Thumb>();
            if (previousButton) previousButton.onClick.AddListener(() => Step(_looks, -1));
            if (nextButton) nextButton.onClick.AddListener(() => Step(_looks, +1));
            if (visionPreviousButton) visionPreviousButton.onClick.AddListener(() => Step(_vision, -1));
            if (visionNextButton) visionNextButton.onClick.AddListener(() => Step(_vision, +1));
            foreach (var rail in new[] { _looks, _vision })
                for (int i = 0; i < rail.Thumbs.Length; i++)
                {
                    int tile = i;
                    var r = rail;
                    if (rail.Thumbs[i].tile) rail.Thumbs[i].tile.onClick.AddListener(() => PickTile(r, tile));
                }
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
        /// this page showing the first set made for a vision; if the Profile is already open, at once.
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

            // The pad moving onto a set's tile previews it: one reference compare a frame while the page shows.
            if (!_showing || !events) return;
            var selected = events.currentSelectedGameObject;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            foreach (var rail in new[] { _looks, _vision })
                for (int i = 0; i < rail.Thumbs.Length; i++)
                    if (rail.Thumbs[i].tile && rail.Thumbs[i].tile.gameObject == selected)
                    {
                        int index = SetAt(rail, rail.First + i);
                        if (index >= 0 && index != _picked) Pick(index);
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
            BuildRails(cat);
            int vision = _openOnVisionSets && cat ? cat.IndexOfFirstVisionSet() : -1;
            _openOnVisionSets = false;
            SetPage(true);
            if (vision >= 0)
            {
                // The breadcrumb opens the vision rail on its first set, shown (and previewed) on the cell.
                _vision.First = 0;
                Pick(vision);
            }
            else
            {
                _picked = cat ? Mathf.Max(0, cat.IndexOf(Equipped)) : -1;
                Reveal(_picked);
                Refresh();
            }
            SelectTile(_picked);
        }

        /// <summary>
        /// Splits the catalog into the two rails: the looks (vision None) and the sets made for a vision. The catalog's
        /// card order is kept inside each rail.
        /// </summary>
        public static void SplitRails(ColorSetCatalogSO cat, List<int> looks, List<int> vision)
        {
            looks.Clear();
            vision.Clear();
            int n = cat ? cat.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var entry = cat.Get(i);
                if (entry == null) continue;
                (entry.vision == ColourVision.None ? looks : vision).Add(i);
            }
        }

        void BuildRails(ColorSetCatalogSO cat)
        {
            if (_railsBuiltFor == cat && cat) return;
            _railsBuiltFor = cat;
            SplitRails(cat, _looks.Sets, _vision.Sets);
            _looks.First = _vision.First = 0;
        }

        Rail RailOf(int catalogIndex)
        {
            var entry = Catalog ? Catalog.Get(catalogIndex) : null;
            return entry != null && entry.vision != ColourVision.None ? _vision : _looks;
        }

        static int SetAt(Rail rail, int place) => place >= 0 && place < rail.Sets.Count ? rail.Sets[place] : -1;

        void PickTile(Rail rail, int tile)
        {
            int index = SetAt(rail, rail.First + tile);
            if (index >= 0) Pick(index);
        }

        /// <summary>
        /// The rail's first tile for a catalog of <paramref name="count"/> sets seen through <paramref name="tiles"/>
        /// tiles, moved as little as possible so <paramref name="index"/> is on a tile.
        /// </summary>
        public static int WindowStart(int first, int index, int tiles, int count)
        {
            if (tiles <= 0 || count <= tiles) return 0;
            if (index < first) first = index;
            else if (index >= first + tiles) first = index - tiles + 1;
            return Mathf.Clamp(first, 0, count - tiles);
        }

        void Reveal(int index)
        {
            if (index < 0) return;
            var rail = RailOf(index);
            int place = rail.Sets.IndexOf(index);
            if (place < 0) return;
            rail.First = WindowStart(rail.First, place, rail.Thumbs.Length, rail.Sets.Count);
        }

        void SelectTile(int index)
        {
            var events = EventSystem.current;
            if (!events || index < 0) return;
            var rail = RailOf(index);
            int tile = rail.Sets.IndexOf(index) - rail.First;
            if (tile >= 0 && tile < rail.Thumbs.Length && rail.Thumbs[tile].tile)
            {
                events.SetSelectedGameObject(rail.Thumbs[tile].tile.gameObject);
                _lastSelected = rail.Thumbs[tile].tile.gameObject;
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

        // A rail's < > step through that rail's own sets; from a set in the other rail they start at its first.
        void Step(Rail rail, int by)
        {
            BuildRails(Catalog);
            int n = rail.Sets.Count;
            if (n == 0) return;
            int place = rail.Sets.IndexOf(_picked);
            int next = place < 0 ? (by > 0 ? 0 : n - 1) : ((place + by) % n + n) % n;
            Pick(rail.Sets[next]);
            SelectTile(rail.Sets[next]);
        }

        // Browsing previews the set on the real cell; nothing is saved until APPLY.
        void Pick(int index)
        {
            _picked = index;
            Reveal(index);
            var entry = Catalog ? Catalog.Get(index) : null;
            if (ColorSetCatalogSO.IsPickable(entry) && Manager) Manager.PreviewColorSet(entry.colorSet);
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

        void Refresh()
        {
            var cat = Catalog;
            BuildRails(cat);
            var equipped = Equipped;
            RefreshRail(_looks, cat, equipped);
            RefreshRail(_vision, cat, equipped);

            var shown = cat ? cat.Get(_picked) : null;
            if (nameText) nameText.text = shown?.displayName ?? string.Empty;
            if (lineText) lineText.text = shown?.line ?? string.Empty;
            var shownRail = RailOf(_picked);
            int shownPlace = shownRail.Sets.IndexOf(_picked);
            if (positionText)
                positionText.text = shown != null && shownPlace >= 0
                    ? TemplateText.Fill(positionTemplate, "index", (shownPlace + 1).ToString(), "count", shownRail.Sets.Count.ToString())
                    : string.Empty;
            var worn = cat ? cat.Get(cat.IndexOf(equipped)) : null;
            if (wearingText) wearingText.text = worn != null ? TemplateText.Fill(wearingTemplate, "name", worn.displayName ?? string.Empty) : string.Empty;
            for (int d = 0; d < DomainOrder.Length; d++)
            {
                if (d < pairTexts.Length && pairTexts[d]) pairTexts[d].text = ColorSetCatalogSO.Pigment(shown, d);
                if (shown?.colorSet && shown.colorSet.TryGetPrismKindColors(DomainOrder[d], PrismKind.Plain, out var rim, out var face))
                {
                    Paint(pairRims, d, rim);
                    Paint(pairFaces, d, face);
                }
            }

            bool isEquipped = shown?.colorSet && shown.colorSet == equipped;
            if (applyButton) applyButton.gameObject.SetActive(ColorSetCatalogSO.IsPickable(shown) && !isEquipped);
            if (equippedMark) equippedMark.SetActive(isEquipped);
        }

        void RefreshRail(Rail rail, ColorSetCatalogSO cat, SO_ColorSet equipped)
        {
            for (int i = 0; i < rail.Thumbs.Length; i++)
            {
                var t = rail.Thumbs[i];
                int index = SetAt(rail, rail.First + i);
                var entry = cat && index >= 0 ? cat.Get(index) : null;
                if (t.group)
                {
                    t.group.alpha = entry != null ? 1f : 0f;
                    t.group.interactable = t.group.blocksRaycasts = entry != null;
                }
                if (entry == null) continue;
                if (t.nameText) t.nameText.text = entry.displayName ?? string.Empty;
                if (t.equippedMark) t.equippedMark.SetActive(entry.colorSet && entry.colorSet == equipped);
                if (t.tile)
                {
                    t.tile.IsSelected = index == _picked;
                    t.tile.IsLocked = !ColorSetCatalogSO.IsPickable(entry);
                }
                for (int d = 0; d < DomainOrder.Length; d++)
                    if (entry.colorSet && entry.colorSet.TryGetPrismKindColors(DomainOrder[d], PrismKind.Plain, out var rim, out var face))
                    {
                        Paint(t.faces, d, face);
                        Paint(t.rims, d, rim);
                    }
            }
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
