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
    /// The CODEX screen, the top bar's first tab (Docs/MENU_REDESIGN_B.md section 88; the approved Rev 26 boards,
    /// Shombith 2026-10-26: "Go ahead implement the codex I like the design"). The chapters along the top - ETHIRIONS,
    /// ELEMENTS, ECOLOGY (Flora / Fauna), PILOTS, VESSELS, ARTIFACTS, TOOLS - step on LB / RB; the grid on the left, the
    /// open page on the right, the Hangar's split. Every word of data comes from the game's own assets through
    /// <see cref="CodexCatalog"/>; every word around it is authored here or in the prefab.
    ///
    /// <para>Nothing is hidden behind discovery: the game has none yet (Docs/CODEX.md section 5), so every entry is
    /// listed. ARTIFACTS has no data in the game and shows its authored empty line.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Screen")]
    public class CodexScreen : MonoBehaviour, IScreen
    {
        const int StopCount = 8;
        static readonly CodexChapter[] TabStops =
            { CodexChapter.Ethirions, CodexChapter.Elements, CodexChapter.Flora, CodexChapter.Pilots, CodexChapter.Vessels,
              CodexChapter.Artifacts, CodexChapter.Toys };

        [Header("Data")]
        [Tooltip("The pilots: the Hangar's PILOTS list (ElementalCaptains).")]
        [SerializeField] private SO_CaptainList captains;
        [Tooltip("The vessels, in order: the Hangar's list.")]
        [SerializeField] private SO_VesselList vessels;
        [Tooltip("The element icons, Charge to Time: the HUD's element shapes (Assets/_Graphics/ElementShapes).")]
        [SerializeField] private Sprite[] elementShapes = new Sprite[4];
        [Tooltip("The Hangar's colourful hull renders: a vessel's picture here.")]
        [SerializeField] private HangarCardArtSO cardArt;
        [Tooltip("The domain palette: a lifeform's picture drifts through Jade, Ruby and Gold, the colours it wears in the world.")]
        [SerializeField] private SO_ColorSet colorSet;

        [Header("Words")]
        [SerializeField] private CodexWords words = new();
        [Tooltip("The grid's title per stop: Ethirions, Elements, Flora, Fauna, Pilots, Vessels, Artifacts, Toys.")]
        [SerializeField] private string[] stopTitles = { "Ethirions", "Elements", "Flora", "Fauna", "Pilots", "Vessels", "Artifacts", "Toys" };
        [Tooltip("The grid's count. {count} is the number of entries.")]
        [SerializeField] private string countTemplate = "{count} entries";
        [Tooltip("What an empty stop says, per stop (same order as the titles). Empty: nothing.")]
        [SerializeField] private string[] emptyLines = { "", "", "", "", "", "", "Artifacts are found far out in the HyperSea. None found yet.", "" };

        [Header("Chapters")]
        [Tooltip("One tab per chapter: Ethirions, Elements, Ecology, Pilots, Vessels, Artifacts, Toys.")]
        [SerializeField] private KitTextTab[] chapterTabs = new KitTextTab[7];
        [Tooltip("Flora and Fauna, shown under the chapters while ECOLOGY is open.")]
        [SerializeField] private CanvasGroup ecologyTabsGroup;
        [SerializeField] private KitTextTab floraTab;
        [SerializeField] private KitTextTab faunaTab;
        [Tooltip("Step the chapters with the pad's shoulder buttons (LB / RB).")]
        [SerializeField] private bool shoulderInput = true;

        [Header("Grid")]
        [SerializeField] private TMP_Text gridTitle;
        [SerializeField] private TMP_Text gridCount;
        [SerializeField] private ScrollRect gridScroll;
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private CodexTile tilePrefab;
        [Tooltip("Shown instead of the grid on a stop with nothing in it.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [SerializeField] private TMP_Text emptyText;

        [Header("Page")]
        [SerializeField] private CodexPageView page;

        readonly List<CodexItem>[] _items = new List<CodexItem>[StopCount];
        readonly List<CodexTile> _tiles = new();
        CodexChapter _stop = CodexChapter.Ethirions;
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

            // The pad moving onto a tile opens it: one reference compare a frame while the Codex is shown.
            if (!EventSystem.current) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            for (int i = 0; i < _shown; i++)
                if (_tiles[i].Button && _tiles[i].Button.gameObject == selected)
                {
                    if (i != _selected) Select(i);
                    EnsureVisible(_tiles[i]);
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

            FillTiles(items);
            _selected = -1;
            if (items.Count > 0) Select(0);
            else if (page) page.Show(null);
            if (gridScroll) gridScroll.verticalNormalizedPosition = 1f;

            // The pad stays in the grid: a tile it was on may have just been reused or hidden.
            if (EventSystem.current && _shown > 0)
            {
                var current = EventSystem.current.currentSelectedGameObject;
                if (!current || current.transform.IsChildOf(gridContent))
                    EventSystem.current.SetSelectedGameObject(_tiles[0].Button ? _tiles[0].Button.gameObject : null);
            }
            _lastSelected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        }

        List<CodexItem> Items(CodexChapter stop)
        {
            int s = (int)stop;
            return _items[s] ??= CodexCatalog.Build(stop, CodexSO.Load(), captains, vessels, elementShapes, words, null, cardArt, LifePalette());
        }

        Color[] _lifePalette;

        /// <summary>The domains a lifeform is seen in, as the HUD paints them (SO_ColorSet's signal colours).</summary>
        Color[] LifePalette()
        {
            if (_lifePalette != null || !colorSet) return _lifePalette;
            return _lifePalette = new[] { colorSet.GetDomainSignalColor(Domains.Jade), colorSet.GetDomainSignalColor(Domains.Ruby),
                                          colorSet.GetDomainSignalColor(Domains.Gold) };
        }

        void FillTiles(List<CodexItem> items)
        {
            if (!tilePrefab || !gridContent) return;
            while (_tiles.Count < items.Count)
            {
                var tile = Instantiate(tilePrefab, gridContent);
                tile.name = $"CodexTile{_tiles.Count + 1}";
                tile.Picked += t => Select(t.Index);
                _tiles.Add(tile);
            }
            _shown = items.Count;
            for (int i = 0; i < _tiles.Count; i++)
            {
                bool used = i < items.Count;
                if (_tiles[i].TryGetComponent(out CanvasGroup group)) KitVisibility.SetShown(group, used);
                if (used) _tiles[i].Show(i, items[i]);
            }
        }

        void Select(int index)
        {
            var items = Items(_stop);
            if (index < 0 || index >= items.Count) return;
            _selected = index;
            for (int i = 0; i < _shown; i++) _tiles[i].IsSelected = i == index;
            if (page) page.Show(items[index].Page);
        }

        /// <summary>Scrolls the grid so a tile the pad moved onto is in view.</summary>
        void EnsureVisible(CodexTile tile)
        {
            if (!gridScroll || !gridScroll.viewport || !gridContent) return;
            float view = gridScroll.viewport.rect.height, content = gridContent.rect.height;
            if (content <= view) return;
            var rt = (RectTransform)tile.transform;
            float top = -rt.anchoredPosition.y - rt.rect.height * (1f - rt.pivot.y);
            float bottom = top + rt.rect.height;
            float scrolled = (1f - gridScroll.verticalNormalizedPosition) * (content - view);
            if (top < scrolled) scrolled = top;
            else if (bottom > scrolled + view) scrolled = bottom - view;
            gridScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(scrolled / (content - view));
        }
    }
}
