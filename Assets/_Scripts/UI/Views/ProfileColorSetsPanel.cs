using System;
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
        [Tooltip("The Profile's veil and its sea lines: switched off while this page shows, so the cell is seen.")]
        [SerializeField] private Graphic[] veil = Array.Empty<Graphic>();

        [Header("Sets")]
        [Tooltip("Used when the theme manager is not up (a test scene). In the game, ThemeManagerDataContainer's catalog.")]
        [SerializeField] private ColorSetCatalogSO catalog;
        [SerializeField] private Thumb[] thumbs = Array.Empty<Thumb>();
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("The set shown")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text lineText;
        [Tooltip("\"2 of 3\".")]
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

        bool _showing;
        int _picked = -1;
        GameObject _lastSelected;

        ColorSetCatalogSO Catalog => ThemeManager.Data && ThemeManager.Data.Catalog ? ThemeManager.Data.Catalog : catalog;
        static ThemeManager Manager => ThemeManager.Instance;
        SO_ColorSet Equipped => Manager ? Manager.Equipped : ThemeManager.LiveOr(null);

        void Awake()
        {
            if (recordTab) recordTab.onClick.AddListener(ShowRecord);
            if (colourSetsTab) colourSetsTab.onClick.AddListener(ShowColourSets);
            if (applyButton) applyButton.onClick.AddListener(Apply);
            if (previousButton) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Step(+1));
            for (int i = 0; i < thumbs.Length; i++)
            {
                int index = i;
                if (thumbs[i].tile) thumbs[i].tile.onClick.AddListener(() => Pick(index));
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

        void OnRecordOpened() => SetPage(false);

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
            for (int i = 0; i < thumbs.Length; i++)
                if (thumbs[i].tile && thumbs[i].tile.gameObject == selected && i != _picked) { Pick(i); break; }
        }

        public void ShowRecord()
        {
            RevertPreview();
            SetPage(false);
        }

        public void ShowColourSets()
        {
            var cat = Catalog;
            _picked = cat ? Mathf.Max(0, cat.IndexOf(Equipped)) : -1;
            SetPage(true);
            Refresh();
            var events = EventSystem.current;
            if (events && _picked >= 0 && _picked < thumbs.Length && thumbs[_picked].tile)
                events.SetSelectedGameObject(thumbs[_picked].tile.gameObject);
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
            foreach (var g in veil)
                if (g) g.enabled = !colourSets;
            if (recordTab) recordTab.IsSelected = !colourSets;
            if (colourSetsTab) colourSetsTab.IsSelected = colourSets;
        }

        void Step(int by)
        {
            var cat = Catalog;
            int n = cat ? Mathf.Min(cat.Count, thumbs.Length) : 0;
            if (n == 0) return;
            int next = ((_picked < 0 ? 0 : _picked) + by % n + n) % n;
            Pick(next);
            var events = EventSystem.current;
            if (events && thumbs[next].tile) events.SetSelectedGameObject(thumbs[next].tile.gameObject);
        }

        // Browsing previews the set on the real cell; nothing is saved until APPLY.
        void Pick(int index)
        {
            _picked = index;
            var entry = Catalog ? Catalog.Get(index) : null;
            if (entry != null && entry.unlocked && entry.colorSet && Manager) Manager.PreviewColorSet(entry.colorSet);
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
            if (entry == null || !entry.unlocked || !entry.colorSet || !manager || entry.colorSet == Equipped) return;
            // The cell already wears the set (the preview); the veil is the moment it becomes yours. While another
            // load holds the screen no veil can rise, so the set is worn straight away.
            if (!EnvironmentLoadVeil.ShowColourSetChange(entry.displayName, entry.line,
                    () => manager.ApplyColorSet(entry.colorSet, entry.id)))
                manager.ApplyColorSet(entry.colorSet, entry.id);
        }

        void Refresh()
        {
            var cat = Catalog;
            var equipped = Equipped;
            int count = cat ? Mathf.Min(cat.Count, thumbs.Length) : 0;
            for (int i = 0; i < thumbs.Length; i++)
            {
                var t = thumbs[i];
                var entry = cat ? cat.Get(i) : null;
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
                    t.tile.IsSelected = i == _picked;
                    t.tile.IsLocked = !entry.unlocked;
                }
                for (int d = 0; d < DomainOrder.Length; d++)
                    if (entry.colorSet && entry.colorSet.TryGetPrismKindColors(DomainOrder[d], PrismKind.Plain, out var rim, out var face))
                    {
                        Paint(t.faces, d, face);
                        Paint(t.rims, d, rim);
                    }
            }

            var shown = cat ? cat.Get(_picked) : null;
            if (nameText) nameText.text = shown?.displayName ?? string.Empty;
            if (lineText) lineText.text = shown?.line ?? string.Empty;
            if (positionText)
                positionText.text = shown != null
                    ? TemplateText.Fill(positionTemplate, "index", (_picked + 1).ToString(), "count", count.ToString())
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
            if (applyButton) applyButton.gameObject.SetActive(shown != null && shown.unlocked && shown.colorSet && !isEquipped);
            if (equippedMark) equippedMark.SetActive(isEquipped);
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
