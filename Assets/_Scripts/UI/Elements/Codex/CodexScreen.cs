using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The CODEX screen, the top bar's first tab (Docs/MENU_REDESIGN_B.md sections 88 and 97). The chapters along the
    /// top - ELEMENTS (the four elements and their Etherions, Omni, prisms, spindles), ECOLOGY (Flora / Fauna, every
    /// species the Toy Box offers) and LEXICON (one card each for the arcade, quests, voyages, missions, vessels, pilots,
    /// artifacts and toys) - step on LB / RB. Each chapter is drawn in one of two layouts (section 98, Shombith 2026-10-27:
    /// "keep this design only for elements section others can have design B"): ELEMENTS as the Orbit (Rev 30 board C,
    /// the entries on tilted rings round the picked one's picture, <see cref="CodexOrbit"/>), ECOLOGY and LEXICON as the
    /// Field Guide (board B, a two-page book - the picture's plate and its page - over a strip of every entry). Left /
    /// right on the pad or the keys steps through the entries; up leaves for the chapters. Every word of data comes from the game's own
    /// assets through <see cref="CodexCatalog"/>; every word around it is authored here or in the prefab.
    ///
    /// <para>Nothing is hidden behind discovery: the game has none yet (Docs/CODEX.md section 5), so every entry is
    /// listed.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Screen")]
    public class CodexScreen : MonoBehaviour, IScreen
    {
        const int StopCount = 4;
        static readonly CodexChapter[] TabStops = { CodexChapter.Elements, CodexChapter.Flora, CodexChapter.Lexicon };

        /// <summary>One way of drawing a chapter: its entries, how they are placed and the page they open.</summary>
        [Serializable]
        public sealed class CodexLayout
        {
            [Tooltip("The whole layout: shown while one of its chapters is open, hidden otherwise.")]
            public CanvasGroup group;
            [Tooltip("The entries' parent.")]
            public RectTransform content;
            public CodexTile tilePrefab;
            [Tooltip("Places the entries on rings and turns the picked one to the front (the Orbit). Empty: the content's " +
                     "layout group places them.")]
            public CodexOrbit orbit;
            [Tooltip("Scrolls so the entry the pad is on is in view (the Field Guide's strip). Optional.")]
            public ScrollRect scroll;
            [Tooltip("The open entry's page.")]
            public CodexPageView page;

            [NonSerialized] public readonly List<CodexTile> Tiles = new();
            [NonSerialized] public readonly List<RectTransform> Nodes = new();
        }

        [Header("Data")]
        [Tooltip("The vessels, in order: the Hangar's list. An element page lists each one's ability for it.")]
        [SerializeField] private SO_VesselList vessels;
        [Tooltip("The Toy Box's Spawn Matrix: Flora and Fauna list every species it offers, in its order.")]
        [SerializeField] private SpawnMatrixToyDefinitionSO roster;
        [Tooltip("The element icons, Charge to Time: the HUD's element shapes (Assets/_Graphics/ElementShapes).")]
        [SerializeField] private Sprite[] elementShapes = new Sprite[4];
        [Tooltip("Each element's Etherion crystal in motion, Charge to Time: a 4x4 sheet of frames.")]
        [SerializeField] private Texture2D[] elementFlipbooks = new Texture2D[4];
        [Tooltip("The domain palette the Codex's pictures are tinted from.")]
        [SerializeField] private SO_ColorSet colorSet;
        [Tooltip("The domains a picture is tinted with. Blue only (Shombith 2026-10-27: \"All codex should have blue domain " +
                 "not anything else\"): Blue is nobody's team, and an encyclopedia page belongs to no one.")]
        [SerializeField] private Domains[] pictureDomains = { Domains.Blue };

        [Header("Words")]
        [SerializeField] private CodexWords words = new();
        [Tooltip("The title per stop: Elements, Flora, Fauna, Lexicon.")]
        [SerializeField] private string[] stopTitles = { "Elements", "Flora", "Fauna", "Lexicon" };
        [Tooltip("The count. {count} is the number of entries.")]
        [SerializeField] private string countTemplate = "{count} entries";
        [Tooltip("What an empty stop says, per stop (same order as the titles). Empty: nothing.")]
        [SerializeField] private string[] emptyLines = { "", "", "", "" };

        [Header("Chapters")]
        [Tooltip("One tab per chapter: Elements, Ecology, Lexicon.")]
        [SerializeField] private KitTextTab[] chapterTabs = new KitTextTab[3];
        [Tooltip("Flora and Fauna, shown under the chapters while ECOLOGY is open.")]
        [SerializeField] private CanvasGroup ecologyTabsGroup;
        [SerializeField] private KitTextTab floraTab;
        [SerializeField] private KitTextTab faunaTab;
        [Tooltip("Step the chapters with the pad's shoulder buttons (LB / RB).")]
        [SerializeField] private bool shoulderInput = true;

        [Header("Head")]
        [SerializeField] private TMP_Text gridTitle;
        [SerializeField] private TMP_Text gridCount;
        [Tooltip("Shown instead of the entries on a stop with nothing in it.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [SerializeField] private TMP_Text emptyText;

        [Header("Layouts")]
        [Tooltip("The Orbit (board C): the chapters listed below.")]
        [SerializeField] private CodexLayout orbitLayout = new();
        [Tooltip("The chapters drawn as the Orbit; every other chapter is drawn as the Field Guide.")]
        [SerializeField] private CodexChapter[] orbitChapters = { CodexChapter.Elements };
        [Tooltip("The Field Guide (board B): a two-page book over a strip of every entry.")]
        [SerializeField] private CodexLayout guideLayout = new();

        readonly List<CodexItem>[] _items = new List<CodexItem>[StopCount];
        CodexChapter _stop = CodexChapter.Elements;
        CodexLayout _layout;
        int _selected = -1;
        int _shown;
        bool _active;
        GameObject _lastSelected;

        void Awake()
        {
            for (int i = 0; i < chapterTabs.Length && i < TabStops.Length; i++)
            {
                var stop = TabStops[i];
                if (chapterTabs[i]) chapterTabs[i].onClick.AddListener(() => ShowStop(stop));
            }
            if (floraTab) floraTab.onClick.AddListener(() => ShowStop(CodexChapter.Flora));
            if (faunaTab) faunaTab.onClick.AddListener(() => ShowStop(CodexChapter.Fauna));
        }

        public void OnScreenEnter()
        {
            _active = true;
            CodexPortrait.Animating = true;
            ShowStop(_stop);
        }

        public void OnScreenExit()
        {
            _active = false;
            CodexPortrait.Animating = false;
        }

        void OnDisable() => CodexPortrait.Animating = false;

        void Update()
        {
            if (!_active) return;
            var pad = Gamepad.current;
            if (shoulderInput && pad != null)
            {
                if (pad.rightShoulder.wasPressedThisFrame) Step(+1);
                else if (pad.leftShoulder.wasPressedThisFrame) Step(-1);
            }

            // The pad moving onto an entry opens it: one reference compare a frame while the Codex is shown.
            if (!EventSystem.current || _layout == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            var tiles = _layout.Tiles;
            for (int i = 0; i < _shown; i++)
                if (tiles[i].Button && tiles[i].Button.gameObject == selected)
                {
                    if (i != _selected) Select(i);
                    EnsureVisible(tiles[i]);
                    break;
                }
        }

        /// <summary>LB / RB: the next or previous stop, stopping at the ends.</summary>
        public void Step(int direction)
        {
            int next = Mathf.Clamp((int)_stop + direction, 0, StopCount - 1);
            if (next != (int)_stop) ShowStop((CodexChapter)next);
        }

        public void ShowStop(CodexChapter stop)
        {
            _stop = stop;
            var items = Items(stop);
            int s = (int)stop;

            for (int i = 0; i < chapterTabs.Length && i < TabStops.Length; i++)
                if (chapterTabs[i]) chapterTabs[i].IsSelected = TabStops[i] == stop || (TabStops[i] == CodexChapter.Flora && stop == CodexChapter.Fauna);
            bool ecology = stop is CodexChapter.Flora or CodexChapter.Fauna;
            KitVisibility.SetShown(ecologyTabsGroup, ecology);
            if (floraTab) floraTab.IsSelected = stop == CodexChapter.Flora;
            if (faunaTab) faunaTab.IsSelected = stop == CodexChapter.Fauna;

            if (gridTitle) gridTitle.text = s < stopTitles.Length ? stopTitles[s] : string.Empty;
            if (gridCount) gridCount.text = items.Count > 0 ? TemplateText.Fill(countTemplate, "count", items.Count.ToString()) : string.Empty;
            KitVisibility.SetShown(emptyGroup, items.Count == 0);
            if (emptyText) emptyText.text = s < emptyLines.Length ? emptyLines[s] : string.Empty;

            // One layout at a time: the other is hidden and let go of its pick, so nothing in it animates unseen.
            var next = Array.IndexOf(orbitChapters, stop) >= 0 ? orbitLayout : guideLayout;
            var other = next == orbitLayout ? guideLayout : orbitLayout;
            KitVisibility.SetShown(other.group, false);
            foreach (var tile in other.Tiles) tile.IsSelected = false;
            if (other.page) other.page.Show(null);
            KitVisibility.SetShown(next.group, true);
            _layout = next;

            FillTiles(items);
            _selected = -1;
            if (items.Count > 0) Select(0);
            else if (_layout.page) _layout.page.Show(null);
            if (_layout.scroll) _layout.scroll.horizontalNormalizedPosition = 0f;

            // The pad stays on the entries: one it was on may have just been reused, hidden or moved to the other layout.
            if (EventSystem.current && _shown > 0)
            {
                var current = EventSystem.current.currentSelectedGameObject;
                if (!current || OnEntries(current.transform))
                    EventSystem.current.SetSelectedGameObject(_layout.Tiles[0].Button ? _layout.Tiles[0].Button.gameObject : null);
            }
            _lastSelected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        }

        bool OnEntries(Transform t) =>
            (orbitLayout.content && t.IsChildOf(orbitLayout.content)) || (guideLayout.content && t.IsChildOf(guideLayout.content));

        List<CodexItem> Items(CodexChapter stop)
        {
            int s = (int)stop;
            return _items[s] ??= CodexCatalog.Build(stop, CodexSO.Load(), vessels, elementShapes, words, null, roster, elementFlipbooks,
                                                LifePalette());
        }

        Color[] _lifePalette;

        /// <summary>The picture's tint: <see cref="pictureDomains"/> as the HUD paints them (SO_ColorSet's signal colours).</summary>
        Color[] LifePalette()
        {
            if (_lifePalette != null || !colorSet || pictureDomains is not { Length: > 0 }) return _lifePalette;
            _lifePalette = new Color[pictureDomains.Length];
            for (int i = 0; i < pictureDomains.Length; i++) _lifePalette[i] = colorSet.GetDomainSignalColor(pictureDomains[i]);
            return _lifePalette;
        }

        void FillTiles(List<CodexItem> items)
        {
            var layout = _layout;
            if (!layout.tilePrefab || !layout.content) return;
            var tiles = layout.Tiles;
            while (tiles.Count < items.Count)
            {
                var tile = Instantiate(layout.tilePrefab, layout.content);
                tile.name = $"CodexTile{tiles.Count + 1}";
                tile.Picked += t => Select(t.Index);
                tiles.Add(tile);
                layout.Nodes.Add((RectTransform)tile.transform);
            }
            _shown = items.Count;
            for (int i = 0; i < tiles.Count; i++)
            {
                bool used = i < items.Count;
                if (tiles[i].TryGetComponent(out CanvasGroup group)) KitVisibility.SetShown(group, used);
                if (used) tiles[i].Show(i, items[i]);
            }
            if (layout.orbit) layout.orbit.Arrange(layout.Nodes, _shown, 0);
            WireNavigation();
        }

        /// <summary>
        /// The pad and the arrow keys through the entries: left and right step to the previous and next entry (wrapping),
        /// up leaves for the open chapter's tab. Explicit, because the Orbit's entries move: automatic navigation would
        /// follow where they happen to be drawn this frame.
        /// </summary>
        void WireNavigation()
        {
            Selectable up = KitVisibility.IsShown(ecologyTabsGroup) ? (_stop == CodexChapter.Fauna ? faunaTab : floraTab) : null;
            if (!up)
                for (int i = 0; i < chapterTabs.Length && i < TabStops.Length; i++)
                    if (chapterTabs[i] && chapterTabs[i].IsSelected) up = chapterTabs[i];
            var tiles = _layout.Tiles;
            for (int i = 0; i < _shown; i++)
            {
                var button = tiles[i].Button;
                if (!button) continue;
                button.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = tiles[(i - 1 + _shown) % _shown].Button,
                    selectOnRight = tiles[(i + 1) % _shown].Button,
                    selectOnUp = up,
                };
            }
        }

        void Select(int index)
        {
            var items = Items(_stop);
            if (index < 0 || index >= items.Count || _layout == null) return;
            _selected = index;
            for (int i = 0; i < _shown; i++) _layout.Tiles[i].IsSelected = i == index;
            if (_layout.orbit) _layout.orbit.Focus(index);
            if (_layout.page) _layout.page.Show(items[index].Page);
        }

        /// <summary>Scrolls the Field Guide's strip so an entry the pad moved onto is in view.</summary>
        void EnsureVisible(CodexTile tile)
        {
            var scroll = _layout.scroll;
            if (!scroll || !scroll.viewport || !_layout.content) return;
            float view = scroll.viewport.rect.width, content = _layout.content.rect.width;
            if (content <= view) return;
            var rt = (RectTransform)tile.transform;
            // The tile's left edge from the content's left edge (the layout group anchors it there).
            float left = rt.anchoredPosition.x - rt.rect.width * rt.pivot.x;
            float right = left + rt.rect.width;
            float scrolled = scroll.horizontalNormalizedPosition * (content - view);
            if (left < scrolled) scrolled = left;
            else if (right > scrolled + view) scrolled = right - view;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(scrolled / (content - view));
        }
    }
}
